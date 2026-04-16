using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryDragDropSystem : MonoBehaviour
{
    public event System.Action<bool> OnDragEnded;
    private bool isNewItem = false;

    public static InventoryDragDropSystem Instance { get; private set; }

    [SerializeField] private List<InventoryTetris> inventories;

    /// <summary>
    /// The InventoryTetris that belongs to the local player's own backpack/hotbar.
    /// Only used during steal mode to know which inventory is the valid drop target.
    /// Assign in the Inspector.
    /// </summary>
    [SerializeField] private InventoryTetris playerOwnInventory;

    private InventoryTetris sourceInventory;
    private PlacedItem draggingItem;
    private Vector2Int originOnPickup;
    private ItemTetrisSO.Dir dirOnPickup;
    private ItemTetrisSO.Dir currentDir;
    private bool isDragging;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        if (!isDragging) return;
        if (Keyboard.current.rKey.wasPressedThisFrame)
            currentDir = ItemTetrisSO.GetNextDir(currentDir);
        SnapDraggedItem();

        if (isNewItem && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            EndDrag();
        }
    }

    public void BeginDrag(InventoryTetris inventory, PlacedItem item)
    {
        isNewItem = false;

        sourceInventory = inventory;
        draggingItem = item;
        originOnPickup = inventory.GetActualOrigin(item);
        dirOnPickup = item.dir;
        currentDir = item.dir;
        isDragging = true;

        inventory.PickUpItem(item);
        draggingItem.transform.SetAsLastSibling();
    }

    public void EndDrag()
    {
        isDragging = false;

        // Cache and clear references before any destroys
        PlacedItem item = draggingItem;
        InventoryTetris source = sourceInventory;
        Vector2Int origin = originOnPickup;
        ItemTetrisSO.Dir dir = dirOnPickup;
        bool newItem = isNewItem;

        // Cache steal context before clearing
        bool wasStealMode = InventoryTetris.IsStealMode;
        Panel stealPanel = InventoryTetris.StealSourcePanel;

        draggingItem = null;
        sourceInventory = null;
        isNewItem = false;

        // Restore visuals
        var cg = item.GetComponent<CanvasGroup>();
        if (cg != null) { cg.alpha = 1f; cg.blocksRaycasts = true; }

        InventoryTetris target = GetInventoryUnderMouse();
        bool placed = false;

        if (wasStealMode)
        {
            // ── Steal mode: only allow drop into the player's own inventory ──────
            placed = TryResolveStealDrop(item, source, origin, dir, target, stealPanel);
        }
        else
        {
            // ── Normal mode ───────────────────────────────────────────────────────
            if (target != null)
            {
                // In read-only or steal mode you shouldn't reach here, but guard anyway:
                // Don't allow dropping INTO a panel that's in steal mode from source.
                Vector2Int dropOrigin = GetCellUnderMouse(target);
                PlacedItem result = target.TryPlaceItem(item.itemSO, dropOrigin, currentDir);
                if (result != null)
                {
                    item.DestroySelf();
                    placed = true;
                }
            }

            if (!placed)
            {
                if (newItem)
                {
                    item.DestroySelf();
                }
                else
                {
                    PlacedItem result = source.TryPlaceItem(item.itemSO, origin, dir);
                    if (result != null)
                        item.DestroySelf();
                    else
                    {
                        Debug.LogWarning("[InventoryDragDropSystem] Could not return item to origin.");
                        item.DestroySelf();
                    }
                }
            }
        }

        OnDragEnded?.Invoke(placed);
    }

    // ── Private ───────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves a drag-end event when the player is in steal mode.
    ///
    /// Rules:
    ///   • Drop target must be the player's OWN inventory (playerOwnInventory).
    ///   • Drop target must NOT be the steal source panel.
    ///   • If drop fails or target is wrong → return item to steal-source panel.
    ///   • On success → commit the steal to the server.
    /// </summary>
    private bool TryResolveStealDrop(
        PlacedItem item,
        InventoryTetris source,
        Vector2Int origin,
        ItemTetrisSO.Dir dir,
        InventoryTetris target,
        Panel stealPanel)
    {
        bool placed = false;

        bool targetIsOwnInventory = target != null
            && playerOwnInventory != null
            && target == playerOwnInventory;

        if (targetIsOwnInventory)
        {
            Vector2Int dropOrigin = GetCellUnderMouse(target);
            PlacedItem result = target.TryPlaceItem(item.itemSO, dropOrigin, currentDir);
            if (result != null)
            {
                item.DestroySelf();
                placed = true;

                // Commit the steal: tell the server the panel's new state and
                // which client is the thief.
                if (stealPanel != null)
                {
                    string updatedJson = source.Save();
                    ulong localId = NetworkManager.Singleton.LocalClientId;
                    stealPanel.CommitStealRpc(localId, updatedJson);
                    Debug.Log($"[InventoryDragDropSystem] Steal committed. Panel state sent to server.");
                }
            }
        }

        if (!placed)
        {
            // Return item to where it came from in the steal-source panel
            PlacedItem result = source.TryPlaceItem(item.itemSO, origin, dir);
            if (result != null)
                item.DestroySelf();
            else
            {
                Debug.LogWarning("[InventoryDragDropSystem] Could not return stolen item to origin.");
                item.DestroySelf();
            }
        }

        return placed;
    }

    private void SnapDraggedItem()
    {
        if (draggingItem == null) return;

        InventoryTetris snapTarget = GetSnapTarget();
        float cs = snapTarget.GetCellSize();

        // Which cell is the mouse over?
        Vector2Int cell = GetCellUnderMouse(snapTarget);

        // anchoredPosition = cell * cs + rotationOffset * cs
        Vector2Int rotOffset = draggingItem.itemSO.GetRotationOffset(currentDir);
        Vector2 target = new Vector2(
            (cell.x + rotOffset.x) * cs,
            (cell.y + rotOffset.y) * cs);

        RectTransform rt = draggingItem.GetComponent<RectTransform>();
        rt.SetParent(snapTarget.GetItemContainer(), false);
        rt.anchoredPosition = Vector2.Lerp(
            rt.anchoredPosition, target, Time.unscaledDeltaTime * 25f);

        draggingItem.transform.rotation = Quaternion.Slerp(
            draggingItem.transform.rotation,
            Quaternion.Euler(0, 0, -draggingItem.itemSO.GetRotationAngle(currentDir)),
            Time.unscaledDeltaTime * 20f);
    }

    /// <summary>
    /// Returns the best snap target for the dragged item.
    /// In steal mode the snap target is restricted: the source panel is allowed
    /// (for visual feedback while hovering), and the player's own inventory.
    /// Dropping back into the source panel will be rejected at EndDrag time.
    /// </summary>
    private InventoryTetris GetSnapTarget()
    {
        InventoryTetris hovered = GetInventoryUnderMouse();

        if (InventoryTetris.IsStealMode)
        {
            // In steal mode: snap to hovered only if it's the player's own inventory
            // or the source panel (for visual feedback). Default to source otherwise.
            if (hovered != null && (hovered == playerOwnInventory || hovered == sourceInventory))
                return hovered;
            return sourceInventory;
        }

        return hovered ?? sourceInventory;
    }

    /// <summary>Returns the grid cell the mouse is currently over in the given inventory.</summary>
    private Vector2Int GetCellUnderMouse(InventoryTetris inv)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            inv.GetItemContainer(),
            Mouse.current.position.value,
            null,
            out Vector2 local);
        float cs = inv.GetCellSize();
        return new Vector2Int(
            Mathf.FloorToInt(local.x / cs),
            Mathf.FloorToInt(local.y / cs));
    }

    private InventoryTetris GetInventoryUnderMouse()
    {
        Vector2 mouse = Mouse.current.position.value;
        foreach (var inv in inventories)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                inv.GetItemContainer(), mouse, null, out Vector2 local);
            if (inv.IsValidGridPosition(inv.GetGridPosition(local))) return inv;
        }
        return null;
    }

    public void BeginDragNewItem(InventoryTetris inventory, PlacedItem item)
    {
        isNewItem = true;

        sourceInventory = inventory;
        draggingItem = item;
        originOnPickup = Vector2Int.zero;
        dirOnPickup = ItemTetrisSO.Dir.Down;
        currentDir = ItemTetrisSO.Dir.Down;
        isDragging = true;

        // Don't call PickUpItemAt — item isn't in the grid yet
        draggingItem.transform.SetAsLastSibling();
    }
}
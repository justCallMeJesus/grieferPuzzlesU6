using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryDragDropSystem : MonoBehaviour
{
    public event System.Action<bool> OnDragEnded;
    private bool isNewItem = false;

    public static InventoryDragDropSystem Instance { get; private set; }

    [SerializeField] private List<InventoryTetris> inventories;

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

        draggingItem = null;
        sourceInventory = null;
        isNewItem = false;

        // Restore visuals
        var cg = item.GetComponent<CanvasGroup>();
        if (cg != null) { cg.alpha = 1f; cg.blocksRaycasts = true; }

        InventoryTetris target = GetInventoryUnderMouse();
        bool placed = false;

        if (target != null)
        {
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

        OnDragEnded?.Invoke(placed);
    }

    // ── Private ───────────────────────────────────────────────────────────

    private void SnapDraggedItem()
    {
        if (draggingItem == null) return;

        InventoryTetris snapTarget = GetInventoryUnderMouse() ?? sourceInventory;
        float cs = snapTarget.GetCellSize();

        // Which cell is the mouse over?
        Vector2Int cell = GetCellUnderMouse(snapTarget);

        // anchoredPosition = cell * cs + rotationOffset * cs
        // This matches exactly what TryPlaceItem sets when placing at this cell.
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
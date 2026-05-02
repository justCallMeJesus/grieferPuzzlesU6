using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

public class InventoryDragDropSystem : MonoBehaviour
{
    public event System.Action<bool> OnDragEnded;
    private bool isNewItem = false;
    public static InventoryDragDropSystem Instance { get; private set; }

    [SerializeField] private List<InventoryTetris> inventories;
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
        {
            currentDir = ItemTetrisSO.GetNextDir(currentDir);
            SoundManager.Instance?.PlayOneShot("TetrisTurn");
        }

        SnapDraggedItem();

        if (isNewItem && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            EndDrag();
        }
    }

    public bool IsDragging => isDragging;

    public void CancelDrag()
    {
        if (!isDragging) return;
        isDragging = false;

        PlacedItem item = draggingItem;
        InventoryTetris source = sourceInventory;
        Vector2Int origin = originOnPickup;
        ItemTetrisSO.Dir dir = dirOnPickup;
        bool newItem = isNewItem;

        draggingItem = null;
        sourceInventory = null;
        isNewItem = false;

        var cg = item.GetComponent<CanvasGroup>();
        if (cg != null) { cg.alpha = 1f; cg.blocksRaycasts = true; }

        if (newItem)
        {
            item.DestroySelf();
        }
        else
        {
            PlacedItem result = source.TryPlaceItem(item.itemSO, origin, dir);
            if (result != null) item.DestroySelf();
            else item.DestroySelf();
        }

        OnDragEnded?.Invoke(false);
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
        SoundManager.Instance?.PlayOneShot("TetrisPickup");
    }

    public void EndDrag()
    {
        isDragging = false;

        PlacedItem item = draggingItem;
        InventoryTetris source = sourceInventory;
        Vector2Int origin = originOnPickup;
        ItemTetrisSO.Dir dir = dirOnPickup;
        bool newItem = isNewItem;

        bool wasStealMode = InventoryTetris.IsStealMode;
        Panel stealPanel = InventoryTetris.StealSourcePanel;

        draggingItem = null;
        sourceInventory = null;
        isNewItem = false;

        var cg = item.GetComponent<CanvasGroup>();
        if (cg != null) { cg.alpha = 1f; cg.blocksRaycasts = true; }

        InventoryTetris target = GetInventoryUnderMouse();
        bool placed = false;

        if (wasStealMode)
        {
            placed = TryResolveStealDrop(item, source, origin, dir, target, stealPanel);
        }
        else
        {
            if (target != null)
            {
                Vector2Int dropOrigin = GetCellUnderMouse(target);
                PlacedItem result = target.TryPlaceItem(item.itemSO, dropOrigin, currentDir);
                if (result != null)
                {
                    item.DestroySelf();
                    placed = true;
                    SoundManager.Instance?.PlayOneShot("TetrisPlace");
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
                    if (result != null) item.DestroySelf();
                    else item.DestroySelf();
                }
            }
        }

        OnDragEnded?.Invoke(placed);
    }

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
                if (stealPanel != null)
                {
                    string updatedJson = source.Save();
                    stealPanel.CmdCommitSteal(updatedJson);
                    Debug.Log($"[InventoryDragDropSystem] Steal committed via Mirror Command.");
                }
                SoundManager.Instance?.PlayOneShot("TetrisPlace");
                placed = true;
            }
        }

        if (!placed)
        {
            PlacedItem result = source.TryPlaceItem(item.itemSO, origin, dir);
            if (result != null) item.DestroySelf();
            else item.DestroySelf();
        }

        return placed;
    }

    private void SnapDraggedItem()
    {
        if (draggingItem == null) return;

        InventoryTetris snapTarget = GetSnapTarget();
        float cs = snapTarget.GetCellSize();

        Vector2Int cell = GetCellUnderMouse(snapTarget);
        Vector2Int rotOffset = draggingItem.itemSO.GetRotationOffset(currentDir);
        Vector2 target = new Vector2((cell.x + rotOffset.x) * cs, (cell.y + rotOffset.y) * cs);

        RectTransform rt = draggingItem.GetComponent<RectTransform>();
        rt.SetParent(snapTarget.GetItemContainer(), false);
        rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, target, Time.unscaledDeltaTime * 25f);

        draggingItem.transform.rotation = Quaternion.Slerp(
            draggingItem.transform.rotation,
            Quaternion.Euler(0, 0, -draggingItem.itemSO.GetRotationAngle(currentDir)),
            Time.unscaledDeltaTime * 20f);
    }

    private InventoryTetris GetSnapTarget()
    {
        InventoryTetris hovered = GetInventoryUnderMouse();
        if (InventoryTetris.IsStealMode)
        {
            if (hovered != null && (hovered == playerOwnInventory || hovered == sourceInventory))
                return hovered;
            return sourceInventory;
        }
        return hovered ?? sourceInventory;
    }

    private Vector2Int GetCellUnderMouse(InventoryTetris inv)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            inv.GetItemContainer(),
            Mouse.current.position.ReadValue(),
            null,
            out Vector2 local);
        float cs = inv.GetCellSize();
        return new Vector2Int(Mathf.FloorToInt(local.x / cs), Mathf.FloorToInt(local.y / cs));
    }

    private InventoryTetris GetInventoryUnderMouse()
    {
        Vector2 mouse = Mouse.current.position.ReadValue();

        foreach (var inv in inventories)
        {
            if (!inv.gameObject.activeInHierarchy) continue;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                inv.GetItemContainer(), mouse, null, out Vector2 local);

            if (inv.IsValidGridPosition(inv.GetGridPosition(local)))
                return inv;
        }
        return null;
    }

    public InventoryTetris GetStealSource() => sourceInventory;

    public void BeginDragNewItem(InventoryTetris inventory, PlacedItem item)
    {
        isNewItem = true;
        sourceInventory = inventory;
        draggingItem = item;
        originOnPickup = Vector2Int.zero;
        dirOnPickup = ItemTetrisSO.Dir.Down;
        currentDir = ItemTetrisSO.Dir.Down;
        isDragging = true;
        draggingItem.transform.SetAsLastSibling();
    }
}
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryDragDropSystem : MonoBehaviour
{

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
    }

    public void BeginDrag(InventoryTetris inventory, PlacedItem item)
    {
        sourceInventory = inventory;
        draggingItem = item;
        originOnPickup = item.origin;
        dirOnPickup = item.dir;
        currentDir = item.dir;
        isDragging = true;

        inventory.PickUpItemAt(item.origin);
        draggingItem.transform.SetAsLastSibling();
    }

    public void EndDrag()
    {
        isDragging = false;

        InventoryTetris target = GetInventoryUnderMouse();
        bool placed = false;

        if (target != null)
        {
            // Drop origin = the grid cell the mouse is over
            Vector2Int dropOrigin = GetCellUnderMouse(target);

            PlacedItem result = target.TryPlaceItem(draggingItem.itemSO, dropOrigin, currentDir);
            if (result != null)
            {
                draggingItem.DestroySelf();
                placed = true;
            }
        }

        if (!placed)
        {
            PlacedItem result = sourceInventory.TryPlaceItem(
                draggingItem.itemSO, originOnPickup, dirOnPickup);
            if (result != null)
            {
                draggingItem.DestroySelf();
            }
            else
            {
                Debug.LogWarning("[InventoryDragDropSystem] Could not return item to origin.");
                draggingItem.DestroySelf();
            }
        }

        draggingItem = null;
        sourceInventory = null;
    }

    // ── Private ───────────────────────────────────────────────────────────

    private void SnapDraggedItem()
    {
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
}
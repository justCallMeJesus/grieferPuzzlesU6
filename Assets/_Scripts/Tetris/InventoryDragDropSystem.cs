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

        // Read drop origin directly from where the visual is snapped to
        InventoryTetris targetInventory = GetInventoryUnderMouse();
        bool placed = false;

        if (targetInventory != null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                targetInventory.GetItemContainer(),
                Mouse.current.position.value,
                null,
                out Vector2 localMouse);

            float cs = targetInventory.GetCellSize();
            int mouseGridX = Mathf.FloorToInt(localMouse.x / cs);
            int mouseGridY = Mathf.FloorToInt(localMouse.y / cs);

            Vector2Int rotOffset = draggingItem.itemSO.GetRotationOffset(currentDir);
            Vector2Int dropOrigin = new Vector2Int(mouseGridX - rotOffset.x, mouseGridY - rotOffset.y);

            PlacedItem result = targetInventory.TryPlaceItem(draggingItem.itemSO, dropOrigin, currentDir);
            if (result != null)
            {
                draggingItem.DestroySelf();
                placed = true;
            }
        }

        if (!placed)
        {
            PlacedItem result = sourceInventory.TryPlaceItem(draggingItem.itemSO, originOnPickup, dirOnPickup);
            if (result != null) draggingItem.DestroySelf();
            else
            {
                Debug.LogWarning("[InventoryDragDropSystem] Could not return item to origin.");
                draggingItem.DestroySelf();
            }
        }

        draggingItem = null;
        sourceInventory = null;
    }

    private void SnapDraggedItem()
    {
        InventoryTetris snapTarget = GetInventoryUnderMouse() ?? sourceInventory;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            snapTarget.GetItemContainer(),
            Mouse.current.position.value,
            null,
            out Vector2 localMouse);

        float cs = snapTarget.GetCellSize();

        // Snap the mouse position itself to the nearest cell
        int mouseGridX = Mathf.FloorToInt(localMouse.x / cs);
        int mouseGridY = Mathf.FloorToInt(localMouse.y / cs);

        // Place origin at the snapped mouse cell
        Vector2Int rotOffset = draggingItem.itemSO.GetRotationOffset(currentDir);
        float originX = (mouseGridX - rotOffset.x) * cs;
        float originY = (mouseGridY - rotOffset.y) * cs;

        // Visual position includes the rotation offset
        Vector2 target = new Vector2(originX, originY)
                       + new Vector2(rotOffset.x, rotOffset.y) * cs;

        RectTransform rt = draggingItem.GetComponent<RectTransform>();
        rt.SetParent(snapTarget.GetItemContainer(), false);
        rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, target, Time.unscaledDeltaTime * 25f);

        draggingItem.transform.rotation = Quaternion.Slerp(
            draggingItem.transform.rotation,
            Quaternion.Euler(0, 0, -draggingItem.itemSO.GetRotationAngle(currentDir)),
            Time.unscaledDeltaTime * 20f);
    }

    private InventoryTetris GetInventoryUnderMouse()
    {
        Vector2 mouseScreen = Mouse.current.position.value;
        foreach (var inv in inventories)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                inv.GetItemContainer(), mouseScreen, null, out Vector2 local);
            if (inv.IsValidGridPosition(inv.GetGridPosition(local))) return inv;
        }
        return null;
    }
}
using UnityEngine;
using UnityEngine.EventSystems;

public class InventorySlot : MonoBehaviour, IDropHandler
{
    [SerializeField] private bool bigSlot = false;
    [SerializeField] private int slotIndex;

    private PlayerInventory playerInventory;
    private PlayerInventoryUI playerInventoryUI;

    private void Start()
    {
        // Walk up to the PlayerInventoryUI that owns this slot.
        // Because every player now has their own isolated Canvas, GetComponentInParent
        // is guaranteed to find the correct UI and therefore the correct PlayerManager.
        playerInventoryUI = GetComponentInParent<PlayerInventoryUI>();

        if (playerInventoryUI == null)
        {
            Debug.LogError("InventorySlot: no PlayerInventoryUI found in parents.", this);
            return;
        }

        playerInventory = playerInventoryUI.playerManager.inventory;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (transform.childCount > 0) return;

        GameObject dropped = eventData.pointerDrag;
        DraggableItem draggableItem = dropped.GetComponent<DraggableItem>();

        if (draggableItem != null)
        {
            if (draggableItem.itemData.largeItem != bigSlot) return;

            InventorySlot originSlot = draggableItem.parentAfterDrag?
                                                     .GetComponent<InventorySlot>();
            if (originSlot != null && originSlot != this)
                playerInventory?.SyncItemToSlot(null, originSlot.slotIndex);

            draggableItem.parentAfterDrag = transform;
            playerInventory?.SyncItemToSlot(draggableItem.itemData, slotIndex);
            return;
        }

        // World drop (IStorable) — no existing visual, so we create one.
        IStorable droppable = dropped.GetComponent<IStorable>();
        if (droppable != null && droppable.GetItemData().largeItem == bigSlot)
        {
            ItemData data = droppable.GetItemData();
            DraggableItem.Create(data, gameObject, playerInventory);
            Destroy(dropped);

            playerInventory?.SyncItemToSlot(data, slotIndex);
        }
    }
}
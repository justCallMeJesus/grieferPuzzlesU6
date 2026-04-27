using UnityEngine;
using UnityEngine.EventSystems;
using Mirror;

public class InventorySlot : MonoBehaviour, IDropHandler
{
    [SerializeField] private bool bigSlot = false;
    [SerializeField] private int slotIndex;

    private PlayerInventory playerInventory;
    private PlayerInventoryUI playerInventoryUI;

    // In InventorySlot.cs, add these two properties:
    public bool IsBigSlot => bigSlot;
    public int SlotIndex => slotIndex;
    private void Start()
    {
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

            if (originSlot == this)
            {
                draggableItem.parentAfterDrag = transform;
                return;
            }

            if (originSlot != null)
                playerInventory?.SyncItemToSlot(null, originSlot.slotIndex);

            draggableItem.parentAfterDrag = transform;
            playerInventory?.SyncItemToSlot(draggableItem.itemData, slotIndex);
            return;
        }

        // World drop (IStorable)
        IStorable droppable = dropped.GetComponent<IStorable>();
        if (droppable != null && droppable.GetItemData().largeItem == bigSlot)
        {
            ItemData data = droppable.GetItemData();
            DraggableItem.Create(data, gameObject, playerInventory);

            if (InventoryTetris.IsStealMode)
            {
                Panel stealPanel = InventoryTetris.StealSourcePanel;
                InventoryTetris stealSource = InventoryDragDropSystem.Instance.GetStealSource();
                if (stealPanel != null && stealSource != null)
                {
                    // FIX: Removed the old int localId argument — Panel.CmdCommitSteal now
                    // reads the sender id server-side automatically via Mirror
                    string updatedJson = stealSource.Save();
                    stealPanel.CmdCommitSteal(updatedJson);
                    stealPanel.CloseLocalPanel();
                }
            }

            Destroy(dropped);
            playerInventory?.SyncItemToSlot(data, slotIndex);
        }
    }
}
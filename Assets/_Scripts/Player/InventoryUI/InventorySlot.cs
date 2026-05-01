using UnityEngine.EventSystems;
using UnityEngine;

using UnityEngine;
using UnityEngine.EventSystems;
using Mirror;

public class InventorySlot : MonoBehaviour, IDropHandler
{
    [SerializeField] private bool bigSlot = false;

    [SerializeField] private GameObject selectIndicator;

    // slotIndex: 0 = big, 1 = small.  Kept for API compatibility with any
    // code that still references it, but small slots no longer differ by index.
    [SerializeField] private int slotIndex;

    private PlayerInventory playerInventory;
    private PlayerInventoryUI playerInventoryUI;

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
        GameObject dropped = eventData.pointerDrag;
        if (dropped == null) return;

        // ââ Big-slot path: accept DraggableItem drags (panel / hotbar rearranging) ââ
        if (bigSlot)
        {
            // Only accept drops onto an empty big slot.
            if (transform.childCount > 0) return;

            DraggableItem draggable = dropped.GetComponent<DraggableItem>();
            if (draggable != null && draggable.itemData.largeItem)
            {
                InventorySlot originSlot = draggable.parentAfterDrag?.GetComponent<InventorySlot>();

                if (originSlot == this)
                {
                    draggable.parentAfterDrag = transform;
                    return;
                }

                if (originSlot != null)
                    playerInventory?.SyncItemToSlot(null, originSlot.slotIndex);

                draggable.parentAfterDrag = transform;
                playerInventory?.SyncItemToSlot(draggable.itemData, slotIndex);
                return;
            }

            // World drop of a large IStorable into the big slot.
            IStorable droppable = dropped.GetComponent<IStorable>();
            if (droppable != null && droppable.GetItemData().largeItem)
            {
                ItemData data = droppable.GetItemData();
                DraggableItem.Create(data, gameObject, playerInventory);

                if (InventoryTetris.IsStealMode)
                {
                    Panel stealPanel = InventoryTetris.StealSourcePanel;
                    InventoryTetris stealSource = InventoryDragDropSystem.Instance.GetStealSource();
                    if (stealPanel != null && stealSource != null)
                    {
                        string updatedJson = stealSource.Save();
                        stealPanel.CmdCommitSteal(updatedJson);
                        stealPanel.CloseLocalPanel();
                    }
                }

                Destroy(dropped);
                playerInventory?.StoreBigItem(data);
            }

            return;
        }

        // ââ Small-slot path: count-based stacking, no drag rearranging needed ââ

        // Accept a world drop of a small IStorable (e.g. picking up from ground).
        IStorable smallDroppable = dropped.GetComponent<IStorable>();
        if (smallDroppable != null && !smallDroppable.GetItemData().largeItem)
        {
            if (!playerInventory.HasSmallSpace()) return;

            ItemData data = smallDroppable.GetItemData();
            Destroy(dropped);
            playerInventory?.StoreSmallItem(data);
        }
    }

    // Called by PlayerInventory when a drag-and-drop from a panel needs to
    // write directly to a slot index (big slot only â mirrors old SyncItemToSlot).
    public void SyncItemToSlot(ItemData item, int targetSlotIndex)
    {
        playerInventory?.SyncItemToSlot(item, targetSlotIndex);
    }

    public void SetSelected(bool selected)
    {
        if (selectIndicator != null)
            selectIndicator.SetActive(selected);
    }
}
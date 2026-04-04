using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.EventSystems;

public class InventorySlot : MonoBehaviour, IDropHandler
{
    [SerializeField] private bool bigSlot = false;
    public void OnDrop(PointerEventData eventData)
    {
        if (transform.childCount > 0) return;

        GameObject dropped = eventData.pointerDrag;
        DraggableItem draggableItem = dropped.GetComponent<DraggableItem>();
        if (draggableItem != null)
        {
            if (draggableItem.itemData.largeItem == bigSlot)
            {
                draggableItem.parentAfterDrag = transform;
                return;
            }
        }
        

        IStorable droppable = dropped.GetComponent<IStorable>();
        if (droppable != null && droppable.GetItemData().largeItem == bigSlot)
        {
            // re-create the DraggableItem in this slot
            DraggableItem.Create(droppable.GetItemData(), gameObject);
            Destroy(dropped); // destroy the PlacedItem visual
        }

    }

}

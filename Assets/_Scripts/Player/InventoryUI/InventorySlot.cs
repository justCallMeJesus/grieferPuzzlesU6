using UnityEngine;
using UnityEngine.EventSystems;

public class InventorySlot : MonoBehaviour, IDropHandler
{
    [SerializeField] private bool bigSlot = false;
    public void OnDrop(PointerEventData eventData)
    {
        if(transform.childCount == 0)
        {
            GameObject dropped = eventData.pointerDrag;
            DraggableItem draggableItem = dropped.GetComponent<DraggableItem>();
            if(draggableItem.itemData.largeItem == bigSlot)
            {
                draggableItem.parentAfterDrag = transform;

            }
        }
        
    }

}

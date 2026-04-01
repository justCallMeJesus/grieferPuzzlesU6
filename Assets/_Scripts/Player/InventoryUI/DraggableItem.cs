using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


[RequireComponent(typeof(Image))]
public class DraggableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private Image image;
    
    [HideInInspector] public Transform parentAfterDrag;

    [SerializeField] public ItemData itemData;

    private void OnEnable()
    {
        image = GetComponent<Image>();
        //image.sprite = itemData.sprite;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        parentAfterDrag = transform.parent;
        transform.SetParent(transform.root);
        transform.SetAsLastSibling();
        image.raycastTarget = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        transform.SetParent(parentAfterDrag);
        image.raycastTarget = true;
    }

    public static DraggableItem Create(ItemData itemData, GameObject parentSlot)
    {
        GameObject go = new GameObject(itemData.type.ToString(), typeof(RectTransform));

        Image image = go.AddComponent<Image>();
        image.sprite = itemData.sprite;

        DraggableItem draggable = image.AddComponent<DraggableItem>();
        draggable.itemData = itemData;

        draggable.transform.SetParent(parentSlot.transform);

        return draggable;
    }


}

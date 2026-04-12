using System.Collections.Generic;
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

    public PlayerInventory inventory;

    private static readonly Dictionary<ItemType, System.Type> draggableTypeMap = new()
    {
        { ItemType.TetrisBlock, typeof(TetrisDraggableItem) },
    };  

    private void OnEnable()
    {
        image = GetComponent<Image>();
        //image.sprite = itemData.sprite;
    }

    public virtual void OnBeginDrag(PointerEventData eventData)
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

    public static DraggableItem Create(ItemData itemData, GameObject parentSlot, PlayerInventory inventory)
    {
        GameObject go = new GameObject(itemData.type.ToString(), typeof(RectTransform));

        Image image = go.AddComponent<Image>();
        image.sprite = itemData.sprite;

        System.Type draggableType = draggableTypeMap.TryGetValue(itemData.type, out var t)
        ? t
        : typeof(DraggableItem);
        

        DraggableItem draggable = (DraggableItem)go.AddComponent(draggableType);
        draggable.itemData = itemData;

        draggable.transform.SetParent(parentSlot.transform);

        draggable.inventory = inventory;

        return draggable;
    }


}

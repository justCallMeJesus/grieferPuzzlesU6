using System.Collections.Generic;
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

    public virtual void OnEndDrag(PointerEventData eventData)
    {
        transform.SetParent(parentAfterDrag);
        image.raycastTarget = true;
    }

    public static DraggableItem Create(ItemData itemData, GameObject parentSlot, PlayerInventory inventory)
    {
        // Create the GO without parenting yet — parenting a prefab asset transform is what
        // triggers the "Setting the parent of a transform which resides in a Prefab Asset" error.
        GameObject go = new GameObject(itemData.type.ToString(), typeof(RectTransform));

        // Assign itemData BEFORE AddComponent so OnEnable has valid data if it fires early.
        System.Type draggableType = draggableTypeMap.TryGetValue(itemData.type, out var t)
            ? t
            : typeof(DraggableItem);

        DraggableItem draggable = (DraggableItem)go.AddComponent(draggableType);
        draggable.itemData = itemData;
        draggable.inventory = inventory;

        // Image is added by RequireComponent automatically alongside the draggable.
        // Set the sprite now that itemData is assigned.
        Image image = go.GetComponent<Image>();
        if (image != null)
            image.sprite = itemData.sprite;

        // Parent last — this is what was causing the prefab asset error when parentSlot
        // was accidentally a prefab reference instead of a scene instance.
        draggable.transform.SetParent(parentSlot.transform, false);

        return draggable;
    }
}
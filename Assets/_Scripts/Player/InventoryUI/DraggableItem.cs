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

    private void Awake()
    {
        // Use Awake instead of OnEnable so the image reference is fetched once
        // at instantiation time and never re-fetched when SetActive(true) is called
        // (e.g. when the tetris cancel path restores the item to its hotbar slot).
        image = GetComponent<Image>();
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
        GameObject go = new GameObject(itemData.type.ToString(), typeof(RectTransform));

        // Parent the GO before adding any components so that if OnEnable fires
        // during AddComponent, the transform already has a valid scene parent
        // rather than being a floating root object (which caused the
        // "Setting the parent of a transform which resides in a Prefab Asset"
        // error when transform.root resolved to a prefab asset root).
        go.transform.SetParent(parentSlot.transform, worldPositionStays: false);

        Image image = go.AddComponent<Image>();
        image.sprite = itemData.sprite;

        System.Type draggableType = draggableTypeMap.TryGetValue(itemData.type, out var t)
            ? t
            : typeof(DraggableItem);

        DraggableItem draggable = (DraggableItem)go.AddComponent(draggableType);
        draggable.itemData = itemData;
        draggable.inventory = inventory;

        return draggable;
    }
}
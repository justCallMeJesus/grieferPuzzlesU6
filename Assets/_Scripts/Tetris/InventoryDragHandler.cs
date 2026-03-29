using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────────────────────
// InventoryDragHandler  — attach to each PlacedItem visual
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Attach to the same GameObject as PlacedItem.
/// Notifies the singleton InventoryDragDropSystem on drag events.
/// </summary>
[RequireComponent(typeof(PlacedItem))]
public class InventoryDragHandler : MonoBehaviour,
    IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{

    private PlacedItem placedItem;
    private CanvasGroup canvasGroup;
    private InventoryTetris inventory; // which inventory this item belongs to

    private void Awake()
    {
        placedItem = GetComponent<PlacedItem>();
        canvasGroup = GetComponent<CanvasGroup>();
    }

    /// <summary>Called by InventoryTetris after creation.</summary>
    public void Init(InventoryTetris inv) => inventory = inv;

    public void OnPointerDown(PointerEventData e) { }

    public void OnBeginDrag(PointerEventData e)
    {
        canvasGroup.alpha = 0.6f;
        canvasGroup.blocksRaycasts = false;
        InventoryDragDropSystem.Instance.BeginDrag(inventory, placedItem);
    }

    public void OnDrag(PointerEventData e)
    {
        // Movement handled in DragDropSystem.Update() for snapping
    }

    public void OnEndDrag(PointerEventData e)
    {
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        InventoryDragDropSystem.Instance.EndDrag();
    }
}


// ─────────────────────────────────────────────────────────────────────────────
// InventoryDragDropSystem  — singleton, handles snap movement & drop resolution
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Singleton manager for the drag-drop system.
/// Place on any persistent GameObject in your scene (e.g. UIManager).
///
/// SETUP:
///   Assign all InventoryTetris panels you want to support in <see cref="inventories"/>.
/// </summary>

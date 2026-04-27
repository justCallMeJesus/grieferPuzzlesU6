using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// ─────────────────────────────────────────────────────────────────────────────
// InventoryDragHandler  — attach to each PlacedItem visual
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Attach to the same GameObject as PlacedItem.
/// Notifies the singleton InventoryDragDropSystem on drag events.
///
/// Hit-testing is done via the grid, not the RectTransform bounds.
/// This means non-rectangular items (L-shapes, T-shapes, etc.) and rotated
/// items are always picked up correctly: we look up which PlacedItem occupies
/// the grid cell under the mouse and drag THAT item, regardless of which
/// rectangular RectTransform Unity's raycaster happened to hit first.
/// </summary>
[RequireComponent(typeof(PlacedItem))]
public class InventoryDragHandler : MonoBehaviour,
    IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private PlacedItem placedItem;
    private CanvasGroup canvasGroup;
    private InventoryTetris inventory;

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
        // Ask the grid which item is actually under the mouse.
        // This correctly handles rotated and non-rectangular items because the
        // grid cells are the ground truth — no RectTransform rotation math needed.
        PlacedItem actualItem = GetItemUnderMouse();

        if (actualItem == null)
        {
            // Nothing in the grid here (clicked empty corner of a shape's bounding
            // rect). Cancel by blocking further drag events.
            e.pointerDrag = null;
            return;
        }

        // If a different item is under the mouse (e.g. an L-shape neighbour whose
        // rect overlaps this one), redirect the drag to that item's handler instead.
        if (actualItem != placedItem)
        {
            InventoryDragHandler correctHandler = actualItem.GetComponent<InventoryDragHandler>();
            if (correctHandler != null)
            {
                correctHandler.StartDrag();
                // Redirect Unity's drag tracking to the correct handler's GameObject
                e.pointerDrag = correctHandler.gameObject;
                return;
            }
        }

        StartDrag();
    }

    /// <summary>Initiates the drag for this handler's own PlacedItem.</summary>
    public void StartDrag()
    {
        canvasGroup.alpha = 0.6f;
        canvasGroup.blocksRaycasts = false;
        InventoryDragDropSystem.Instance.BeginDrag(inventory, placedItem);
    }

    public void OnDrag(PointerEventData e)
    {
        // Movement handled in InventoryDragDropSystem.Update() for grid snapping.
    }

    public void OnEndDrag(PointerEventData e)
    {
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        InventoryDragDropSystem.Instance.EndDrag();
    }

    public void OnBeginDragNewBlock()
    {
        canvasGroup.alpha = 0.6f;
        canvasGroup.blocksRaycasts = false;
    }

    // ── Private ───────────────────────────────────────────────────────────

    /// <summary>
    /// Converts the current mouse position to a grid cell and returns
    /// whichever PlacedItem occupies that cell, or null if empty.
    /// </summary>
    private PlacedItem GetItemUnderMouse()
    {
        if (inventory == null) return null;

        RectTransform container = inventory.GetItemContainer();
        Vector2 mousePos = Mouse.current.position.value;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                container, mousePos, null, out Vector2 local))
            return null;

        Vector2Int gridPos = inventory.GetGridPosition(local);

        if (!inventory.IsValidGridPosition(gridPos)) return null;

        return inventory.GetGrid().GetGridObject(gridPos)?.GetOccupant();
    }
}
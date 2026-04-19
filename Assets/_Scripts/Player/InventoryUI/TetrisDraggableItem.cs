using UnityEngine;
using UnityEngine.EventSystems;

public class TetrisDraggableItem : DraggableItem
{
    // True while the tetris drag system owns this item's lifecycle.
    // Suppresses the base OnEndDrag so Unity's pointer-up event doesn't
    // reparent or destroy the item independently of the tetris system.
    private bool isTetrisDragging = false;

    public override void OnBeginDrag(PointerEventData eventData)
    {
        // No tetris panel open -- behave like a normal inventory draggable
        if (!InventoryTetris.IsPanelOpen)
        {
            base.OnBeginDrag(eventData);
            return;
        }

        // Panel is open but this player is a viewer only -- treat as a normal drag
        // so the item stays in their inventory and never touches the tetris grid
        if (!InventoryTetris.IsLocalPlayerEditor)
        {
            base.OnBeginDrag(eventData);
            return;
        }

        TetrisData tetrisData = (TetrisData)itemData;

        PlacedItem result = InventoryTetris.Instance.SpawnItemAtMouse(tetrisData.tetrisSO);
        if (result == null)
        {
            Debug.Log("No space!");
            return;
        }

        // Save the slot we came from so we can re-parent back on cancel.
        // base.OnBeginDrag is intentionally NOT called here because we don't
        // want this item reparented to the canvas root -- the tetris PlacedItem
        // is the visible proxy while this hotbar item stays hidden in its slot.
        parentAfterDrag = transform.parent;

        isTetrisDragging = true;
        gameObject.SetActive(false);
        InventoryDragDropSystem.Instance.OnDragEnded += Instance_OnDragEnded;
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        // While the tetris system owns the drag, suppress the base behaviour.
        // Instance_OnDragEnded handles everything (re-show or destroy).
        if (isTetrisDragging) return;

        base.OnEndDrag(eventData);
    }

    private void Instance_OnDragEnded(bool placed)
    {
        InventoryDragDropSystem.Instance.OnDragEnded -= Instance_OnDragEnded;
        isTetrisDragging = false;

        if (placed)
        {
            Destroy(gameObject);        // placed successfully -- remove from player inventory
            inventory.RemoveItem(0);
        }
        else
        {
            // Placement failed or panel was closed mid-drag -- restore item to its slot
            transform.SetParent(parentAfterDrag);
            gameObject.SetActive(true);
        }
    }
}
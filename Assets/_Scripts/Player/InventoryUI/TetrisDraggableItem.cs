using UnityEngine;
using UnityEngine.EventSystems;

public class TetrisDraggableItem : DraggableItem
{
    public override void OnBeginDrag(PointerEventData eventData)
    {
        // No tetris panel open — behave like a normal inventory draggable
        if (!InventoryTetris.IsPanelOpen)
        {
            base.OnBeginDrag(eventData);
            return;
        }

        // Panel is open but this player is a viewer only — treat as a normal drag
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

        gameObject.SetActive(false);
        InventoryDragDropSystem.Instance.OnDragEnded += Instance_OnDragEnded;
    }

    private void Instance_OnDragEnded(bool placed)
    {
        InventoryDragDropSystem.Instance.OnDragEnded -= Instance_OnDragEnded;

        if (placed)
        {
            Destroy(gameObject);        // placed successfully — remove from player inventory
            inventory.RemoveItem(0);
        }
        else
        {
            gameObject.SetActive(true); // placement failed — return item to player inventory
        }
    }
}
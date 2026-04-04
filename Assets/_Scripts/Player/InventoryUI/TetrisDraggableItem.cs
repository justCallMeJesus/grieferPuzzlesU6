using NUnit.Framework.Internal;
using NUnit;
using UnityEngine;
using UnityEngine.EventSystems;

public class TetrisDraggableItem : DraggableItem
{
    public override void OnBeginDrag(PointerEventData eventData)
    {
        if (!InventoryTetris.IsPanelOpen)
        {
            base.OnBeginDrag(eventData); // behave like a normal draggable item
            return;
        }

        TetrisData tetrisData = (TetrisData)itemData;

        PlacedItem result = InventoryTetris.Instance.SpawnItemAtMouse(tetrisData.tetrisSO);
        if (result == null)
            Debug.Log("No space!");

        this.gameObject.SetActive(false);

        InventoryDragDropSystem.Instance.OnDragEnded += Instance_OnDragEnded;
    }

    private void Instance_OnDragEnded(bool placed)
    {
        InventoryDragDropSystem.Instance.OnDragEnded -= Instance_OnDragEnded;

        if (placed)
            Destroy(gameObject); // placed successfully, remove from player inventory
        else
            gameObject.SetActive(true); // failed, return item to player inventory
    }
}

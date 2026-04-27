using UnityEngine;

public class PlayerInventoryUI : MonoBehaviour
{
    public GameObject bigSlot;
    public GameObject[] smallSlots;

    public PlayerManager playerManager;

    private PlayerInventory playerInventory;

    private void RefreshSlot(GameObject slot, ItemData item)
    {
        for (int i = slot.transform.childCount - 1; i >= 0; i--)
            Destroy(slot.transform.GetChild(i).gameObject);

        if (item != null)
            DraggableItem.Create(item, slot, playerManager.inventory);
    }

    public void RefreshAll(PlayerInventory playerInventory)
    {
        RefreshSlot(bigSlot, playerInventory.GetSelectedItem(0));
        for (int i = 0; i < smallSlots.Length; i++)
            RefreshSlot(smallSlots[i], playerInventory.GetSelectedItem(i + 1));
    }

    public void Init(PlayerInventory inventory)
    {
        playerInventory = inventory;
        RefreshAll(inventory);
    }
}
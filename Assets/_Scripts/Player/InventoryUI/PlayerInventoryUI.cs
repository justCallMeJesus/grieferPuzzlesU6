using UnityEngine;

public class PlayerInventoryUI : MonoBehaviour
{
    public GameObject bigSlot;
    public GameObject[] smallSlots;

    public PlayerManager playerManager;

    private PlayerInventory playerInventory;

    private void RefreshSlot(GameObject slot, ItemData item)
    {
        foreach (Transform child in slot.transform)
            DestroyImmediate(child.gameObject);

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
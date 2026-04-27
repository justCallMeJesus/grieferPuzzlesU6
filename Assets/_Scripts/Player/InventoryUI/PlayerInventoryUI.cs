using System.Linq;
using UnityEngine;

public class PlayerInventoryUI : MonoBehaviour
{
    public PlayerManager playerManager;

    private GameObject bigSlotGO;
    private GameObject[] smallSlotGOs;

    private void Awake()
    {
        // Find all InventorySlot children at runtime — these will be actual
        // scene instances, not prefab asset references
        InventorySlot[] allSlots = GetComponentsInChildren<InventorySlot>(true);

        InventorySlot big = allSlots.FirstOrDefault(s => s.IsBigSlot);
        bigSlotGO = big != null ? big.gameObject : null;

        smallSlotGOs = allSlots
            .Where(s => !s.IsBigSlot)
            .OrderBy(s => s.SlotIndex)
            .Select(s => s.gameObject)
            .ToArray();
    }

    private void RefreshSlot(GameObject slot, ItemData item)
    {
        if (slot == null) return;

        for (int i = slot.transform.childCount - 1; i >= 0; i--)
            Destroy(slot.transform.GetChild(i).gameObject);

        if (item != null)
            DraggableItem.Create(item, slot, playerManager.inventory);
    }

    public void RefreshAll(PlayerInventory playerInventory)
    {
        RefreshSlot(bigSlotGO, playerInventory.GetSelectedItem(0));
        for (int i = 0; i < smallSlotGOs.Length; i++)
            RefreshSlot(smallSlotGOs[i], playerInventory.GetSelectedItem(i + 1));
    }

    public void Init(PlayerInventory inventory)
    {
        RefreshAll(inventory);
    }
}
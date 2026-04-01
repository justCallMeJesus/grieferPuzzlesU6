using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class PlayerInventory : NetworkBehaviour
{
    [SerializeField] private ItemData[] smallItemInventory = new ItemData[3];

    [SerializeField]
    private ItemData bigInventorySlot;

    [SerializeField] private GameObject bigInventorySlotUI;

    private PlayerManager manager;

    private void Start()
    {
        manager = GetComponent<PlayerManager>();
    }

    public bool HasSmallSpace()
    {
        // check if any slot is free
        return smallItemInventory.Any(slot => slot == null);
    }

    public bool HasBigSpace()
    {
        // check if any slot is free
        return bigInventorySlot == null;
    }

    public void StoreBigItem(ItemData item)
    {
        bigInventorySlot = item;
        DraggableItem.Create(item, manager.playerInventoryUI.bigSlot);
    }

    public void StoreSmallItem(ItemData item)
    {
        for (int i = 0; i < smallItemInventory.Length; i++)
        {
            if (smallItemInventory[i] == null)
            {
                smallItemInventory[i] = item;
                DraggableItem.Create(item, manager.playerInventoryUI.smallSlots[i]);
                return;
            }
        }
    }
}

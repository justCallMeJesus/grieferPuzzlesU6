using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class PlayerInventory : NetworkBehaviour
{
    [SerializeField] public ItemData[] smallItemInventory = new ItemData[3];

    public bool HasSpace()
    {
        // check if any slot is free
        return smallItemInventory.Any(slot => slot == null);
    }
}

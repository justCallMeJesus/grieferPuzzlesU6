using Mirror;
using UnityEngine;

public class Item : NetworkBehaviour, IPickupable
{
    public ItemData ItemData;
    public GameObject GameObject => this.gameObject;

    // [SyncVar] replaces NetworkVariable. 
    // The "hook" is the function that runs on all clients when this value changes.
    [SyncVar(hook = nameof(OnCollectedChanged))]
    private bool isCollected = false;

    // Mirror equivalent of OnNetworkSpawn
    public override void OnStartClient()
    {
        base.OnStartClient();
        // Apply current state immediately (handles late joiners)
        gameObject.SetActive(!isCollected);
    }

    // Mirror hook signature: (oldValue, newValue)
    private void OnCollectedChanged(bool oldVal, bool newVal)
    {
        gameObject.SetActive(!newVal);
    }

    public void OnPickup(PlayerInventory player)
    {
        if (!ItemData.largeItem)
        {
            if (!player.HasSmallSpace()) return;
            CmdPickup(player.gameObject);
        }
        else
        {
            if (!player.HasBigSpace()) return;
            CmdPickupLarge(player.gameObject);
        }
    }

    // Mirror uses [Command] to send from Client to Server.
    // We can pass the GameObject directly; Mirror handles the network reference for us.
    [Command(requiresAuthority = false)]
    private void CmdPickup(GameObject playerObj)
    {
        if (isCollected) return;

        if (playerObj.TryGetComponent(out PlayerInventory inventory))
        {
            inventory.StoreSmallItem(ItemData);
            isCollected = true; // SyncVar updates all clients automatically
        }
    }

    [Command(requiresAuthority = false)]
    private void CmdPickupLarge(GameObject playerObj)
    {
        if (isCollected) return;

        if (playerObj.TryGetComponent(out PlayerInventory inventory))
        {
            inventory.StoreBigItem(ItemData);
            isCollected = true;
        }
    }

    // Logic for RequestDestroy/DestroyRpc is redundant now because 
    // setting isCollected = true in the Commands handles the hiding.
}

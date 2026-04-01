using Unity.Netcode;
using UnityEngine;

public class Item : NetworkBehaviour, IPickupable
{
    public ItemData ItemData;
    public GameObject GameObject => this.gameObject;

    // [AI] Syncs collected state across all clients, including late joiners
    private NetworkVariable<bool> isCollected = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // [AI] Called when this object is spawned on the network (including late joiners)
    public override void OnNetworkSpawn()
    {
        // [AI] Subscribe to future changes
        isCollected.OnValueChanged += OnCollectedChanged;
        // [AI] Apply current state immediately (handles late joiners)
        gameObject.SetActive(!isCollected.Value);
    }

    // [AI] Called on every client whenever isCollected changes
    private void OnCollectedChanged(bool previous, bool current)
    {
        gameObject.SetActive(!current);
    }

    // Called when a player interacts with this item
    public void OnPickup(PlayerInventory player)
    {
        if (!ItemData.largeItem)
        {
            if (!player.HasSmallSpace()) { return; }

            RequestPickup(player);
            RequestDestroy();
        }
        else
        {
            if (!player.HasBigSpace()) { return; }

            RequestPickupLarge(player);
            RequestDestroy();
        }
        
    }
     
    // Initiates the networked removal of this item
    public void RequestDestroy()
    {
        DestroyRpc();
    }

    // [AI] Runs on the server — marks item as collected and hides it
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void DestroyRpc()
    {
        // [AI] Setting this to true syncs to all current and future clients,
        // triggering OnCollectedChanged which hides the object on each client
        isCollected.Value = true;
        gameObject.SetActive(false);
    }

    public void RequestPickup(PlayerInventory player)
    {
        PickupRpc(player.NetworkObject);
    }

    public void RequestPickupLarge(PlayerInventory player)
    {
        PickupLargeRpc(player.NetworkObject);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PickupRpc(NetworkObjectReference playerRef)
    {
        if (isCollected.Value) return; // prevent double pickup

        if (playerRef.TryGet(out NetworkObject playerNetObj))
        {
            PlayerInventory inventory = playerNetObj.GetComponent<PlayerInventory>();
            inventory.StoreSmallItem(ItemData);
        }

        isCollected.Value = true;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PickupLargeRpc(NetworkObjectReference playerRef)
    {
        if (isCollected.Value) return;

        if (playerRef.TryGet(out NetworkObject playerNetObj))
        {
            PlayerInventory inventory = playerNetObj.GetComponent<PlayerInventory>();
            inventory.StoreBigItem(ItemData);
        }

        isCollected.Value = true;
    }
}
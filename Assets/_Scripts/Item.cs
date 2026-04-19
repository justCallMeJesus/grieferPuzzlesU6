using Unity.Netcode;
using UnityEngine;

public class Item : NetworkBehaviour, IPickupable
{
    public ItemData ItemData;
    public GameObject GameObject => this.gameObject;

    private NetworkVariable<bool> isCollected = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> isGrounded = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        isCollected.OnValueChanged += OnCollectedChanged;
        gameObject.SetActive(!isCollected.Value);
    }

    private void OnCollectedChanged(bool previous, bool current)
    {
        gameObject.SetActive(!current);
    }

    // ── Grounded state ────────────────────────────────────────────────────────

    public void SetGrounded(bool grounded)
    {
        if (!IsSpawned)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                isGrounded.Value = grounded;
            return;
        }

        SetGroundedRpc(grounded);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SetGroundedRpc(bool grounded)
    {
        isGrounded.Value = grounded;
    }

    // ── Pickup ────────────────────────────────────────────────────────────────

    public void OnPickup(PlayerInventory player)
    {
        // Both checks run client-side to avoid sending RPCs that arrive at the
        // server after isCollected is already true — the root cause of duplicate
        // inventory entries when the same item was thrown multiple times.
        if (isCollected.Value) return;
        if (!isGrounded.Value) return;

        if (!ItemData.largeItem)
        {
            if (!player.HasSmallSpace()) return;
            PickupRpc(player.NetworkObject);
        }
        else
        {
            if (!player.HasBigSpace()) return;
            PickupLargeRpc(player.NetworkObject);
        }
    }

    // Kept for any external callers
    public void RequestPickup(PlayerInventory player) => PickupRpc(player.NetworkObject);
    public void RequestPickupLarge(PlayerInventory player) => PickupLargeRpc(player.NetworkObject);

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PickupRpc(NetworkObjectReference playerRef)
    {
        if (isCollected.Value) return;
        if (!isGrounded.Value) return;

        if (playerRef.TryGet(out NetworkObject playerNetObj))
        {
            PlayerInventory inventory = playerNetObj.GetComponent<PlayerInventory>();
            inventory.StoreSmallItem(ItemData);
        }

        isCollected.Value = true;
        NetworkObject.Despawn(destroy: true);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PickupLargeRpc(NetworkObjectReference playerRef)
    {
        if (isCollected.Value) return;
        if (!isGrounded.Value) return;

        if (playerRef.TryGet(out NetworkObject playerNetObj))
        {
            PlayerInventory inventory = playerNetObj.GetComponent<PlayerInventory>();
            inventory.StoreBigItem(ItemData);
        }

        isCollected.Value = true;
        NetworkObject.Despawn(destroy: true);
    }
}
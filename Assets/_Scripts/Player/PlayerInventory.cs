using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

public class PlayerInventory : NetworkBehaviour
{
    [SerializeField] private InputActionReference throwAction;
    [SerializeField] private InputActionReference slot1Action; // big slot
    [SerializeField] private InputActionReference slot2Action; // small slot

    [Header("Inventory Data")]
    // Small items are now a single type stored with a quantity counter.
    // Max stack size can be tuned here.
    [SerializeField] private ItemData smallItemType;
    [SerializeField] private int smallItemCount = 0;
    [SerializeField] private int smallItemMaxCount = 10;
    [SerializeField] private ItemData bigInventorySlot;

    private PlayerManager manager;
    public Transform playerThrowPoint;

    // selectedSlot: -1 = none, 0 = big slot, 1 = small slot
    private int selectedSlot = -1;

    private bool throwPending = false;

    // FIX: Track whether this player is fully ready to send Commands.
    // FizzySteam does not guarantee message ordering, so Commands sent
    // immediately after spawn can arrive before the server has registered
    // the NetworkIdentity, producing "Spawned object not found" warnings.
    private bool isReadyToSendCommands = false;

    private System.Action<InputAction.CallbackContext> _onSlot1;
    private System.Action<InputAction.CallbackContext> _onSlot2;
    private System.Action<InputAction.CallbackContext> _onThrow;

    public override void OnStartLocalPlayer()
    {
        manager = GetComponent<PlayerManager>();

        _onSlot1 = _ => SelectSlot(0);
        _onSlot2 = _ => SelectSlot(1);
        _onThrow = _ => OnThrow();

        slot1Action.action.Enable();
        slot2Action.action.Enable();
        throwAction.action.Enable();

        slot1Action.action.performed += _onSlot1;
        slot2Action.action.performed += _onSlot2;
        throwAction.action.performed += _onThrow;
    }

    // OnStartAuthority fires on the client after the server has fully spawned
    // and acknowledged this object. Safe to send Commands from this point.
    public override void OnStartAuthority()
    {
        isReadyToSendCommands = true;
    }

    private void OnDisable()
    {
        if (!isLocalPlayer) return;

        if (_onSlot1 != null) slot1Action.action.performed -= _onSlot1;
        if (_onSlot2 != null) slot2Action.action.performed -= _onSlot2;
        if (_onThrow != null) throwAction.action.performed -= _onThrow;

        isReadyToSendCommands = false;
    }

    // -------------------------------------------------------------------------
    // Sync Logic
    // -------------------------------------------------------------------------

    private void RefreshUILocal()
    {
        if (!isLocalPlayer) return;
        manager?.playerInventoryUI?.RefreshAll(this);
    }

    [Server]
    private void PushStateToClient()
    {
        string big = bigInventorySlot != null ? bigInventorySlot.name : "";
        string small = smallItemType != null ? smallItemType.name : "";
        TargetSyncInventory(connectionToClient, big, small, smallItemCount);
    }

    [TargetRpc]
    private void TargetSyncInventory(NetworkConnection target, string bigName, string smallName, int count)
    {
        bigInventorySlot = !string.IsNullOrEmpty(bigName) ? ItemRegistry.Get(bigName) : null;
        smallItemType = !string.IsNullOrEmpty(smallName) ? ItemRegistry.Get(smallName) : null;
        smallItemCount = count;

        throwPending = false;
        RefreshUILocal();
    }

    // -------------------------------------------------------------------------
    // Inventory Queries
    // -------------------------------------------------------------------------

    /// <summary>Returns true when the small slot can accept at least one more item.</summary>
    public bool HasSmallSpace() => smallItemCount < smallItemMaxCount;

    public bool HasBigSpace() => bigInventorySlot == null;

    /// <summary>slot 0 = big, slot 1 = small (returns the ItemData regardless of count).</summary>
    public ItemData GetSelectedItem(int slot)
    {
        if (slot == 0) return bigInventorySlot;
        if (slot == 1) return smallItemCount > 0 ? smallItemType : null;
        return null;
    }

    public int SelectedSlot => selectedSlot;
    public ItemData SmallItemType => smallItemType;
    public int SmallItemCount => smallItemCount;
    public ItemData BigItem => bigInventorySlot;

    // -------------------------------------------------------------------------
    // Inventory Mutations
    // -------------------------------------------------------------------------

    public void StoreBigItem(ItemData item)
    {
        bigInventorySlot = item;
        if (isServer) PushStateToClient();
        else
        {
            CmdSyncBigSlot(item != null ? item.name : "");
            RefreshUILocal();
        }
    }

    /// <summary>
    /// Add one of the small item to the stack. The caller is responsible for
    /// checking HasSmallSpace() first. All small items are assumed to be the
    /// same type; smallItemType is set on the first pickup and cleared when
    /// count reaches zero.
    /// </summary>
    public void StoreSmallItem(ItemData item)
    {
        if (item == null || !HasSmallSpace()) return;

        smallItemType = item;
        smallItemCount++;

        if (isServer) PushStateToClient();
        else
        {
            CmdSyncSmallSlot(item.name, smallItemCount);
            RefreshUILocal();
        }
    }

    /// <summary>
    /// Remove one item from the appropriate slot.
    /// For slot 1 (small), decrements the count and clears the type when it
    /// reaches zero.
    /// </summary>
    public void RemoveItem(int slot)
    {
        if (isServer)
        {
            InternalRemoveItem(slot);
            PushStateToClient();
        }
        else
        {
            CmdRemoveItem(slot);
            InternalRemoveItem(slot); // Optimistic prediction
            RefreshUILocal();
        }
    }

    [Command]
    private void CmdRemoveItem(int slot)
    {
        InternalRemoveItem(slot);
        PushStateToClient();
    }

    private void InternalRemoveItem(int slot)
    {
        if (slot == 0)
        {
            bigInventorySlot = null;
        }
        else if (slot == 1)
        {
            smallItemCount = Mathf.Max(0, smallItemCount - 1);
            if (smallItemCount == 0) smallItemType = null;
        }
    }

    // -------------------------------------------------------------------------
    // Big-slot drag-drop compatibility (used by InventorySlot / panel system)
    // Only slot 0 (big) is meaningful here; small items use StoreSmallItem.
    // -------------------------------------------------------------------------

    public void SyncItemToSlot(ItemData item, int slotIndex)
    {
        if (slotIndex != 0) return; // small slot is count-based, not drag-slotted
        bigInventorySlot = item;
        if (!isServer)
        {
            CmdSyncBigSlot(item != null ? item.name : "");
            RefreshUILocal();
        }
    }

    // Sync big slot independently (used when a client picks up a big item).
    [Command]
    private void CmdSyncBigSlot(string itemName)
    {
        bigInventorySlot = !string.IsNullOrEmpty(itemName) ? ItemRegistry.Get(itemName) : null;
    }

    // Sync small slot count independently (used when a client picks up a small item).
    [Command]
    private void CmdSyncSmallSlot(string itemName, int count)
    {
        smallItemType = !string.IsNullOrEmpty(itemName) ? ItemRegistry.Get(itemName) : null;
        smallItemCount = count;
        if (smallItemCount <= 0) smallItemType = null;
    }

    // -------------------------------------------------------------------------
    // Slot Selection & Throwing
    // -------------------------------------------------------------------------

    private void SelectSlot(int slot)
    {
        if (!isLocalPlayer) return;
        selectedSlot = slot;
        RefreshUILocal();
    }

    private void OnThrow()
    {
        if (!isLocalPlayer) return;

        // FIX: Don't send Commands until the server has fully acknowledged
        // this object. Prevents "Spawned object not found" on FizzySteam
        // when a joined client throws very quickly after spawning.
        if (!isReadyToSendCommands) return;

        // Block throwing while a panel UI is open (edit, read-only, or steal mode).
        if (manager != null && manager.IsPanelOpen) return;

        if (throwPending) return;
        if (selectedSlot == -1) return;

        ItemData item = GetSelectedItem(selectedSlot);
        if (item == null) return;

        throwPending = true;

        int slotToThrow = selectedSlot;

        // Only do optimistic local removal on a pure client.
        // On host, the Command runs on the same component instance so removing
        // here would wipe the item before CmdThrow can read it.
        if (!isServer)
        {
            InternalRemoveItem(slotToThrow);
            // Clear throwPending immediately on the client so the next stack
            // item can be thrown without waiting for the server RPC round-trip.
            throwPending = false;
            RefreshUILocal();
        }

        // FIX: Pass the item name through the Command so the server can resolve
        // it even if its own inventory state is still desynced.
        CmdThrow(slotToThrow, transform.forward, item.name);
    }

    [Command]
    private void CmdThrow(int slot, Vector3 direction, string itemName)
    {
        if (connectionToClient == null) return;

        // FIX: Try the server's own inventory first; fall back to item name from client.
        ItemData item = GetSelectedItem(slot);
        if (item == null)
            item = ItemRegistry.Get(itemName);

        if (item == null || item.prefab == null)
        {
            PushStateToClient();
            return;
        }

        InternalRemoveItem(slot);
        PushStateToClient();

        Vector3 spawnPos = playerThrowPoint != null
            ? playerThrowPoint.position
            : transform.position + transform.forward * 0.5f + Vector3.up * 0.5f;

        GameObject thrown = Instantiate(item.prefab, spawnPos, Quaternion.LookRotation(direction));
        NetworkServer.Spawn(thrown, connectionToClient);

        if (thrown.TryGetComponent(out ThrowableItem throwable))
        {
            int connId = connectionToClient.connectionId;
            uint throwerId = netId;

            throwable.ServerLaunch(direction, connId, throwerId);
            StartCoroutine(LaunchNextFrame(throwable, direction, connId, throwerId));
        }
    }

    // Waits one server frame so clients have registered the spawned netId before RpcLaunch.
    private IEnumerator LaunchNextFrame(ThrowableItem throwable, Vector3 direction, int connId, uint throwerId)
    {
        yield return null;
        if (throwable != null)
            throwable.RpcLaunch(direction, connId, throwerId);
    }
}
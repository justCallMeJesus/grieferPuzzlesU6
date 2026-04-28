using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

public class PlayerInventory : NetworkBehaviour
{
    [SerializeField] private InputActionReference throwAction;
    [SerializeField] private InputActionReference slot1Action;
    [SerializeField] private InputActionReference slot2Action;
    [SerializeField] private InputActionReference slot3Action;
    [SerializeField] private InputActionReference slot4Action;

    [Header("Inventory Data")]
    [SerializeField] private ItemData[] smallItemInventory = new ItemData[3];
    [SerializeField] private ItemData bigInventorySlot;

    private PlayerManager manager;
    public Transform playerThrowPoint;

    // selectedSlot: -1 = none, 0 = big slot, 1-3 = small slots
    private int selectedSlot = -1;

    // Track whether we are mid-throw to prevent double-firing.
    private bool throwPending = false;

    // FIX: Track whether this player is fully ready to send Commands.
    // FizzySteam does not guarantee message ordering, so Commands sent
    // immediately after spawn can arrive before the server has registered
    // the NetworkIdentity, producing "Spawned object not found" warnings.
    // We set this true in OnStartAuthority (fires after server is ready for us)
    // instead of relying solely on OnStartLocalPlayer.
    private bool isReadyToSendCommands = false;

    private System.Action<InputAction.CallbackContext> _onSlot1;
    private System.Action<InputAction.CallbackContext> _onSlot2;
    private System.Action<InputAction.CallbackContext> _onSlot3;
    private System.Action<InputAction.CallbackContext> _onSlot4;
    private System.Action<InputAction.CallbackContext> _onThrow;

    public override void OnStartLocalPlayer()
    {
        manager = GetComponent<PlayerManager>();

        _onSlot1 = _ => SelectSlot(0);
        _onSlot2 = _ => SelectSlot(1);
        _onSlot3 = _ => SelectSlot(2);
        _onSlot4 = _ => SelectSlot(3);
        _onThrow = _ => OnThrow();

        slot1Action.action.Enable();
        slot2Action.action.Enable();
        slot3Action.action.Enable();
        slot4Action.action.Enable();
        throwAction.action.Enable();

        slot1Action.action.performed += _onSlot1;
        slot2Action.action.performed += _onSlot2;
        slot3Action.action.performed += _onSlot3;
        slot4Action.action.performed += _onSlot4;
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
        if (_onSlot3 != null) slot3Action.action.performed -= _onSlot3;
        if (_onSlot4 != null) slot4Action.action.performed -= _onSlot4;
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
        string sm0 = smallItemInventory[0] != null ? smallItemInventory[0].name : "";
        string sm1 = smallItemInventory[1] != null ? smallItemInventory[1].name : "";
        string sm2 = smallItemInventory[2] != null ? smallItemInventory[2].name : "";

        TargetSyncInventory(connectionToClient, big, sm0, sm1, sm2);
    }

    [TargetRpc]
    private void TargetSyncInventory(NetworkConnection target, string bigName, string small0, string small1, string small2)
    {
        bigInventorySlot = !string.IsNullOrEmpty(bigName) ? ItemRegistry.Get(bigName) : null;
        smallItemInventory[0] = !string.IsNullOrEmpty(small0) ? ItemRegistry.Get(small0) : null;
        smallItemInventory[1] = !string.IsNullOrEmpty(small1) ? ItemRegistry.Get(small1) : null;
        smallItemInventory[2] = !string.IsNullOrEmpty(small2) ? ItemRegistry.Get(small2) : null;

        if (selectedSlot != -1 && GetSelectedItem(selectedSlot) == null)
            selectedSlot = -1;

        throwPending = false;

        RefreshUILocal();
    }

    // -------------------------------------------------------------------------
    // Inventory Queries
    // -------------------------------------------------------------------------

    public bool HasSmallSpace() => smallItemInventory.Any(slot => slot == null);
    public bool HasBigSpace() => bigInventorySlot == null;

    public ItemData GetSelectedItem(int slot)
    {
        if (slot == 0) return bigInventorySlot;
        int i = slot - 1;
        return (i >= 0 && i < smallItemInventory.Length) ? smallItemInventory[i] : null;
    }

    public int SelectedSlot => selectedSlot;

    // -------------------------------------------------------------------------
    // Inventory Mutations
    // -------------------------------------------------------------------------

    public void StoreBigItem(ItemData item)
    {
        bigInventorySlot = item;
        if (isServer) PushStateToClient();
        else RefreshUILocal();
    }

    public void StoreSmallItem(ItemData item)
    {
        for (int i = 0; i < smallItemInventory.Length; i++)
        {
            if (smallItemInventory[i] == null)
            {
                smallItemInventory[i] = item;
                if (isServer) PushStateToClient();
                else RefreshUILocal();
                return;
            }
        }
    }

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
            InternalRemoveItem(slot); // Prediction
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
        if (slot == 0) bigInventorySlot = null;
        else
        {
            int i = slot - 1;
            if (i >= 0 && i < smallItemInventory.Length) smallItemInventory[i] = null;
        }
    }

    public void SyncItemToSlot(ItemData item, int slotIndex)
    {
        InternalSyncToSlot(item, slotIndex);
        if (!isServer)
        {
            string itemName = item != null ? item.name : "";
            CmdSyncItemToSlot(itemName, slotIndex);
        }
    }

    [Command]
    private void CmdSyncItemToSlot(string itemName, int slotIndex)
    {
        ItemData item = !string.IsNullOrEmpty(itemName) ? ItemRegistry.Get(itemName) : null;
        InternalSyncToSlot(item, slotIndex);
    }

    private void InternalSyncToSlot(ItemData item, int slotIndex)
    {
        if (slotIndex == 0) bigInventorySlot = item;
        else
        {
            int i = slotIndex - 1;
            if (i >= 0 && i < smallItemInventory.Length) smallItemInventory[i] = item;
        }
    }

    // -------------------------------------------------------------------------
    // Slot Selection & Throwing
    // -------------------------------------------------------------------------

    private void SelectSlot(int slot)
    {
        if (!isLocalPlayer) return;
        selectedSlot = (selectedSlot == slot) ? -1 : slot;
        RefreshUILocal();
    }

    private void OnThrow()
    {
        if (!isLocalPlayer) return;

        // FIX: Don't send Commands until the server has fully acknowledged
        // this object. Prevents "Spawned object not found" on FizzySteam
        // when a joined client throws very quickly after spawning.
        if (!isReadyToSendCommands) return;

        if (throwPending) return;
        if (selectedSlot == -1) return;

        ItemData item = GetSelectedItem(selectedSlot);
        if (item == null) return;

        throwPending = true;

        int slotToThrow = selectedSlot;
        selectedSlot = -1;

        // Only do optimistic local removal on a pure client.
        // On host, the Command runs on the same component instance so removing
        // here would wipe the item before CmdThrow can read it.
        if (!isServer)
        {
            InternalRemoveItem(slotToThrow);
            RefreshUILocal();
        }

        CmdThrow(slotToThrow, transform.forward);
    }

    [Command]
    private void CmdThrow(int slot, Vector3 direction)
    {
        if (connectionToClient == null) return;

        ItemData item = GetSelectedItem(slot);
        if (item == null || item.prefab == null)
        {
            // Server rejected throw — re-sync so client can recover.
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
            throwable.RpcLaunch(direction, connectionToClient.connectionId, netId);
        }
    }
}
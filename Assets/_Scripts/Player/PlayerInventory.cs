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
    private int selectedSlot = -1;

    public override void OnStartLocalPlayer()
    {
        manager = GetComponent<PlayerManager>();

        // Enable and Subscribe to Actions
        slot1Action.action.Enable();
        slot2Action.action.Enable();
        slot3Action.action.Enable();
        slot4Action.action.Enable();
        throwAction.action.Enable();

        slot1Action.action.performed += _ => SelectSlot(0);
        slot2Action.action.performed += _ => SelectSlot(1);
        slot3Action.action.performed += _ => SelectSlot(2);
        slot4Action.action.performed += _ => SelectSlot(3);
        throwAction.action.performed += _ => OnThrow();
    }

    private void OnDisable()
    {
        if (!isLocalPlayer) return;

        slot1Action.action.performed -= _ => SelectSlot(0);
        slot2Action.action.performed -= _ => SelectSlot(1);
        slot3Action.action.performed -= _ => SelectSlot(2);
        slot4Action.action.performed -= _ => SelectSlot(3);
        throwAction.action.performed -= _ => OnThrow();
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

        // TargetRpc ensures only the owner gets the update
        TargetSyncInventory(connectionToClient, big, sm0, sm1, sm2);
    }

    [TargetRpc]
    private void TargetSyncInventory(NetworkConnection target, string bigName, string small0, string small1, string small2)
    {
        bigInventorySlot = !string.IsNullOrEmpty(bigName) ? ItemRegistry.Get(bigName) : null;
        smallItemInventory[0] = !string.IsNullOrEmpty(small0) ? ItemRegistry.Get(small0) : null;
        smallItemInventory[1] = !string.IsNullOrEmpty(small1) ? ItemRegistry.Get(small1) : null;
        smallItemInventory[2] = !string.IsNullOrEmpty(small2) ? ItemRegistry.Get(small2) : null;

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
        if (!isLocalPlayer || selectedSlot == -1) return;
        ItemData item = GetSelectedItem(selectedSlot);
        if (item == null) return;

        CmdThrow(selectedSlot, playerThrowPoint.position, transform.forward);
        selectedSlot = -1;
        RefreshUILocal();
    }

    [Command]
    private void CmdThrow(int slot, Vector3 spawnPos, Vector3 direction)
    {
        ItemData item = GetSelectedItem(slot);
        if (item == null) return;

        InternalRemoveItem(slot);

        GameObject thrown = Instantiate(item.prefab, spawnPos, Quaternion.LookRotation(direction));

        // Mirror standard spawn
        NetworkServer.Spawn(thrown);

        // Tell all clients to launch the physics
        if (thrown.TryGetComponent(out ThrowableItem throwable))
        {
            throwable.RpcLaunch(direction, connectionToClient.connectionId);
        }
    }
}

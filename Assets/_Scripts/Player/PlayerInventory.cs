using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInventory : NetworkBehaviour
{
    [SerializeField] private InputActionReference throwAction;
    [SerializeField] private InputActionReference slot1Action;
    [SerializeField] private InputActionReference slot2Action;
    [SerializeField] private InputActionReference slot3Action;
    [SerializeField] private InputActionReference slot4Action;

    [SerializeField] private ItemData[] smallItemInventory = new ItemData[3];
    [SerializeField] private ItemData bigInventorySlot;

    private PlayerManager manager;
    public Transform playerThrowPoint;

    private int selectedSlot = -1;

    public override void OnNetworkSpawn()
    {
        manager = GetComponent<PlayerManager>();
    }

    private void OnEnable()
    {
        slot1Action.action.performed += _ => SelectSlot(0);
        slot2Action.action.performed += _ => SelectSlot(1);
        slot3Action.action.performed += _ => SelectSlot(2);
        slot4Action.action.performed += _ => SelectSlot(3);
        throwAction.action.performed += _ => OnThrow();
    }

    private void OnDisable()
    {
        slot1Action.action.performed -= _ => SelectSlot(0);
        slot2Action.action.performed -= _ => SelectSlot(1);
        slot3Action.action.performed -= _ => SelectSlot(2);
        slot4Action.action.performed -= _ => SelectSlot(3);
        throwAction.action.performed -= _ => OnThrow();
    }

    // -------------------------------------------------------------------------
    // UI refresh
    // -------------------------------------------------------------------------

    private void RefreshUILocal()
    {
        if (!IsOwner) return;
        manager?.playerInventoryUI?.RefreshAll(this);
    }

    // Sends full inventory state from server to the owning client.
    // string[] can't be sent over NGO RPCs, so we send 3 slots as separate strings.
    private void PushStateToClient()
    {
        string big = bigInventorySlot != null ? bigInventorySlot.name : "";
        string sm0 = smallItemInventory[0] != null ? smallItemInventory[0].name : "";
        string sm1 = smallItemInventory[1] != null ? smallItemInventory[1].name : "";
        string sm2 = smallItemInventory[2] != null ? smallItemInventory[2].name : "";
        SyncInventoryClientRpc(big, sm0, sm1, sm2);
    }

    [ClientRpc]
    private void SyncInventoryClientRpc(string bigName, string small0, string small1, string small2)
    {
        if (!IsOwner) return;

        bigInventorySlot = bigName != "" ? ItemRegistry.Get(bigName) : null;
        smallItemInventory[0] = small0 != "" ? ItemRegistry.Get(small0) : null;
        smallItemInventory[1] = small1 != "" ? ItemRegistry.Get(small1) : null;
        smallItemInventory[2] = small2 != "" ? ItemRegistry.Get(small2) : null;

        manager?.playerInventoryUI?.RefreshAll(this);
    }

    // -------------------------------------------------------------------------
    // Inventory queries
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
    // Inventory mutations
    // -------------------------------------------------------------------------

    public void StoreBigItem(ItemData item)
    {
        bigInventorySlot = item;
        if (IsServer) PushStateToClient();
        else RefreshUILocal();
    }

    public void StoreSmallItem(ItemData item)
    {
        for (int i = 0; i < smallItemInventory.Length; i++)
        {
            if (smallItemInventory[i] == null)
            {
                smallItemInventory[i] = item;
                if (IsServer) PushStateToClient();
                else RefreshUILocal();
                return;
            }
        }
    }

    public void RemoveItem(int slot)
    {
        if (slot == 0)
            bigInventorySlot = null;
        else
        {
            int i = slot - 1;
            if (i >= 0 && i < smallItemInventory.Length)
                smallItemInventory[i] = null;
        }
        if (IsServer) PushStateToClient();
        else RefreshUILocal();
    }

    public void SyncItemToSlot(ItemData item, int slotIndex)
    {
        if (slotIndex == 0)
            bigInventorySlot = item;
        else
        {
            int i = slotIndex - 1;
            if (i >= 0 && i < smallItemInventory.Length)
                smallItemInventory[i] = item;
        }
    }

    // -------------------------------------------------------------------------
    // Slot selection
    // -------------------------------------------------------------------------

    public void SetSelectedSlot(int slot)
    {
        selectedSlot = slot;
    }

    private void SelectSlot(int slot)
    {
        if (!IsOwner) return;
        if (selectedSlot == slot) { selectedSlot = -1; SetSelectedSlot(-1); return; }
        selectedSlot = slot;
        SetSelectedSlot(slot);
    }

    // -------------------------------------------------------------------------
    // Throwing
    // -------------------------------------------------------------------------

    private void OnThrow()
    {
        if (!IsOwner) return;
        if (selectedSlot == -1) return;
        ItemData item = GetSelectedItem(selectedSlot);
        if (item == null) return;

        ThrowServerRpc(selectedSlot, playerThrowPoint.position, transform.forward);
        RemoveItem(selectedSlot);
        selectedSlot = -1;
        SetSelectedSlot(-1);
    }

    [ServerRpc]
    private void ThrowServerRpc(int slot, Vector3 spawnPos, Vector3 direction)
    {
        GameObject thrown = Instantiate(GetSelectedItem(slot).prefab, spawnPos,
                                        Quaternion.LookRotation(direction));
        thrown.GetComponent<NetworkObject>().Spawn();
        thrown.GetComponent<ThrowableItem>().Launch(direction);
    }
}
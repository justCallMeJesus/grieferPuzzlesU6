using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Networked panel (chest, puzzle board, etc.) backed by InventoryTetris.
///
/// NETWORK CONTRACT
/// ────────────────
/// • savedState    – authoritative JSON of the grid, server-only write.
///                   Other clients never load this into the visual inventory
///                   until they actually open the panel themselves.
/// • currentUserId – who has the panel open right now. NOBODY = ulong.MaxValue.
///                   (Cannot use 0 — the host's LocalClientId is 0.)
/// • ownerId       – set permanently the first time anyone interacts.
///                   Only that client may open the panel afterwards.
///
/// SETUP
/// ─────
///   1. Add this component + NetworkObject to the panel GameObject.
///   2. Assign inventoryTetris and inventoryPanel in the Inspector.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class Panel : NetworkBehaviour, IInteractable
{
    public GameObject GameObject => gameObject;

    [SerializeField] private GameObject UIPanel;
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private InventoryTetris inventoryTetris;

    // ── Sentinel ──────────────────────────────────────────────────────────

    /// <summary>
    /// "No client" sentinel. Never use 0 — the host's LocalClientId is 0.
    /// NGO never assigns ulong.MaxValue as a real client ID.
    /// </summary>
    private const ulong NOBODY = ulong.MaxValue;

    // ── Networked state ───────────────────────────────────────────────────

    private readonly NetworkVariable<FixedString4096Bytes> savedState =
        new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<ulong> currentUserId =
        new(NOBODY, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<ulong> ownerId =
        new(NOBODY, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ── Local state ───────────────────────────────────────────────────────

    private bool isLocallyOpen = false;

    // ── NetworkBehaviour lifecycle ────────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        savedState.OnValueChanged += OnSavedStateChanged;
        currentUserId.OnValueChanged += OnCurrentUserChanged;
        ownerId.OnValueChanged += OnOwnerChanged;

        // NOTE: we deliberately do NOT call ReloadLocalInventory here for
        // late-joining clients. The inventory is only ever loaded into the
        // visual grid when this client actually opens the panel (GrantAccessRpc).

        if (inventoryTetris != null)
            inventoryTetris.OnGridFull += HandleGridFull;
    }

    public override void OnNetworkDespawn()
    {
        savedState.OnValueChanged -= OnSavedStateChanged;
        currentUserId.OnValueChanged -= OnCurrentUserChanged;
        ownerId.OnValueChanged -= OnOwnerChanged;

        if (inventoryTetris != null)
            inventoryTetris.OnGridFull -= HandleGridFull;
    }

    // ── IInteractable ─────────────────────────────────────────────────────

    public bool CanInteract() => true;

    public void OnInteract(PlayerManager player)
    {
        if (!player.IsOwner) return;
        RequestOpenRpc(NetworkManager.Singleton.LocalClientId);
    }

    public void OnStopInteraction(PlayerManager player)
    {
        if (!player.IsOwner) return;
        if (!isLocallyOpen) return;

        string json = inventoryTetris.Save();
        RequestCloseRpc(NetworkManager.Singleton.LocalClientId, json);
    }

    // ── Server Rpcs ───────────────────────────────────────────────────────

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestOpenRpc(ulong requesterId)
    {
        // First-touch: unclaimed panel is permanently assigned to whoever opens it first
        if (ownerId.Value == NOBODY)
        {
            ownerId.Value = requesterId;
            Debug.Log($"[Panel] Client {requesterId} claimed ownership.");
        }

        // Owner check
        if (ownerId.Value != requesterId)
        {
            Debug.Log($"[Panel] Client {requesterId} denied: owned by {ownerId.Value}.");
            GrantAccessRpc("This panel belongs to someone else.", false,
                RpcTarget.Single(requesterId, RpcTargetUse.Temp));
            return;
        }

        // Lock check
        if (currentUserId.Value != NOBODY)
        {
            Debug.Log($"[Panel] Client {requesterId} denied: in use by {currentUserId.Value}.");
            GrantAccessRpc("Someone else is already using this panel.", false,
                RpcTarget.Single(requesterId, RpcTargetUse.Temp));
            return;
        }

        // Grant
        currentUserId.Value = requesterId;
        GrantAccessRpc(savedState.Value.ToString(), true,
            RpcTarget.Single(requesterId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestCloseRpc(ulong requesterId, string json)
    {
        if (currentUserId.Value != requesterId)
        {
            Debug.LogWarning($"[Panel] Close from {requesterId} ignored " +
                             $"(current user: {currentUserId.Value}).");
            return;
        }

        // Save state server-side — no clients reload from this change (see OnSavedStateChanged)
        savedState.Value = new FixedString4096Bytes(json);

        // Releasing the lock triggers OnCurrentUserChanged on all clients,
        // which closes the UI only on the client that had it open
        currentUserId.Value = NOBODY;
    }

    // ── Targeted Client Rpc ───────────────────────────────────────────────

    [Rpc(SendTo.SpecifiedInParams)]
    private void GrantAccessRpc(string jsonOrReason, bool granted, RpcParams rpcParams = default)
    {
        if (!granted)
        {
            Debug.Log($"[Panel] Access denied — {jsonOrReason}");
            // HUDManager.Instance.ShowToast(jsonOrReason);
            return;
        }

        isLocallyOpen = true;

        // Load the saved grid into the visual inventory NOW (only for this client)
        inventoryTetris.ClearAll();
        if (!string.IsNullOrEmpty(jsonOrReason))
            inventoryTetris.Load(jsonOrReason);

        inventoryPanel.SetActive(true);
        inventoryTetris.SetPanelIsOpen(true);

        PlayerManager player = GetLocalPlayerManager();
        if (player != null)
        {
            player.interaction.currentlyInteractingObject = this;
            player.FreezePlayer();
        }
    }

    // ── NetworkVariable callbacks ─────────────────────────────────────────

    private void OnSavedStateChanged(FixedString4096Bytes previous, FixedString4096Bytes current)
    {
        // The JSON changed because someone closed the panel and saved their work.
        // We deliberately do NOTHING here — other clients must not load items into
        // the visual grid while the panel is closed, or items appear floating on screen.
        // The fresh JSON will be passed directly in GrantAccessRpc when this client
        // opens the panel next time.
    }

    private void OnCurrentUserChanged(ulong previous, ulong current)
    {
        // Lock released — close the UI on whichever client had it open
        if (current == NOBODY
            && previous == NetworkManager.Singleton.LocalClientId
            && isLocallyOpen)
        {
            CloseLocalPanel();
        }
    }

    private void OnOwnerChanged(ulong previous, ulong current)
    {
        string label = current == NOBODY ? "nobody" : current.ToString();
        Debug.Log($"[Panel] Ownership claimed by client {label}.");
        // Drive a world-space lock icon here if needed.
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private void CloseLocalPanel()
    {
        isLocallyOpen = false;
        inventoryTetris.ClearAll();
        inventoryPanel.SetActive(false);
        inventoryTetris.SetPanelIsOpen(false);

        PlayerManager player = GetLocalPlayerManager();
        player?.UnfreezePlayer();
    }

    private void HandleGridFull()
    {
        Debug.Log("[Panel] Grid fully filled!");
        // Add your completion logic here.
    }

    private PlayerManager GetLocalPlayerManager()
    {
        foreach (var pm in FindObjectsByType<PlayerManager>(FindObjectsSortMode.None))
            if (pm.IsOwner) return pm;
        return null;
    }

    // ── Public read accessors ─────────────────────────────────────────────

    public ulong GetOwnerId() => ownerId.Value;
    public bool IsClaimed() => ownerId.Value != NOBODY;
    public bool IsInUse() => currentUserId.Value != NOBODY;
}
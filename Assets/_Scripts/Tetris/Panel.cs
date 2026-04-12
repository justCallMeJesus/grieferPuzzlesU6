using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Networked panel (chest, puzzle board, etc.) backed by InventoryTetris.
///
/// NETWORK CONTRACT
/// ────────────────
/// • savedState    – authoritative JSON of the grid, server-only write.
///                   Only loaded into the visual grid when a client actually opens the panel.
/// • currentUserId – who has write-access right now. NOBODY = ulong.MaxValue.
///                   (Cannot use 0 — the host's LocalClientId is 0.)
///                   Only one client may have write-access at a time.
/// • ownerId       – set permanently the first time anyone interacts.
///                   The owner gets full read/write access.
///                   All other clients get read-only (can view, cannot drag).
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

    /// <summary>ClientId of the client that currently has write-access. NOBODY = available.</summary>
    private readonly NetworkVariable<ulong> currentUserId =
        new(NOBODY, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>First client to interact claims permanent ownership.</summary>
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

        // Only the owner persists changes; viewers just close locally
        bool isOwner = NetworkManager.Singleton.LocalClientId == ownerId.Value;
        if (isOwner)
        {
            string json = inventoryTetris.Save();
            RequestCloseRpc(NetworkManager.Singleton.LocalClientId, json);
        }
        else
        {
            CloseLocalPanel();
        }
    }

    // ── Server Rpcs ───────────────────────────────────────────────────────

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestOpenRpc(ulong requesterId)
    {
        // First-touch: unclaimed panel is permanently assigned to the first opener
        if (ownerId.Value == NOBODY)
        {
            ownerId.Value = requesterId;
            Debug.Log($"[Panel] Client {requesterId} claimed ownership.");
        }

        bool requesterIsOwner = ownerId.Value == requesterId;

        if (requesterIsOwner)
        {
            // Owner: enforce the single-writer lock
            if (currentUserId.Value != NOBODY)
            {
                Debug.Log($"[Panel] Owner {requesterId} denied: panel in use by {currentUserId.Value}.");
                GrantAccessRpc("Panel is currently in use.", false, false,
                    RpcTarget.Single(requesterId, RpcTargetUse.Temp));
                return;
            }

            // Acquire write lock
            currentUserId.Value = requesterId;
        }
        // Non-owner: no lock — read-only view, nothing to lock

        GrantAccessRpc(savedState.Value.ToString(), true, requesterIsOwner,
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

        // Persist new state — no clients reload visuals from this (see OnSavedStateChanged)
        savedState.Value = new FixedString4096Bytes(json);

        // Release write lock — triggers OnCurrentUserChanged on all clients
        currentUserId.Value = NOBODY;
    }

    // ── Targeted Client Rpc ───────────────────────────────────────────────

    /// <summary>
    /// Delivered only to the requesting client.
    /// granted=false  → show denial message, do not open UI.
    /// granted=true   → open UI; canEdit controls whether drag handlers are active.
    /// RpcParams MUST be the last parameter for SendTo.SpecifiedInParams.
    /// </summary>
    [Rpc(SendTo.SpecifiedInParams)]
    private void GrantAccessRpc(string jsonOrReason, bool granted, bool canEdit,
        RpcParams rpcParams = default)
    {
        if (!granted)
        {
            Debug.Log($"[Panel] Access denied — {jsonOrReason}");
            // HUDManager.Instance.ShowToast(jsonOrReason);
            return;
        }

        isLocallyOpen = true;

        // Load the saved grid visually
        inventoryTetris.ClearAll();
        if (!string.IsNullOrEmpty(jsonOrReason))
            inventoryTetris.Load(jsonOrReason);

        // Tell InventoryTetris (and TetrisDraggableItem) whether this client may edit
        inventoryTetris.SetLocalPlayerEditor(canEdit);

        // Enable or disable drag handlers on already-placed items
        SetDragHandlersEnabled(canEdit);

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
        // Intentionally empty — visuals are only loaded in GrantAccessRpc,
        // never from a background state change, to prevent items floating on screen.
    }

    private void OnCurrentUserChanged(ulong previous, ulong current)
    {
        // Write lock released — close the UI on whichever client held it
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

    /// <summary>
    /// Enables or disables every InventoryDragHandler under the item container,
    /// allowing read-only view when enabled=false.
    /// </summary>
    private void SetDragHandlersEnabled(bool enabled)
    {
        if (inventoryTetris == null) return;
        var container = inventoryTetris.GetItemContainer();
        if (container == null) return;

        foreach (var handler in container.GetComponentsInChildren<InventoryDragHandler>(true))
            handler.enabled = enabled;
    }

    private void CloseLocalPanel()
    {
        isLocallyOpen = false;

        // Re-enable drag handlers and reset editor flag for next open
        SetDragHandlersEnabled(true);
        inventoryTetris.SetLocalPlayerEditor(false);

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
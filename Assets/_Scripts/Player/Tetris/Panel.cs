using Mirror;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Panel : NetworkBehaviour, IInteractable
{
    public GameObject GameObject => gameObject;

    [SerializeField] private GameObject UIPanel;
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private InventoryTetris inventoryTetris;

    [Header("Team Settings")]
    public Material teamColor;

    private const int NOBODY = -1;

    [SyncVar(hook = nameof(OnSavedStateChanged))]
    private string savedState = "";

    [SyncVar(hook = nameof(OnCurrentUserChanged))]
    private int currentUserId = NOBODY;

    // Tracks a non-owner who has the panel open in read-only mode.
    // Kept separate from currentUserId so that read-only viewers still
    // occupy the panel and block everyone else (including the owner).
    [SyncVar(hook = nameof(OnReadOnlyUserChanged))]
    private int readOnlyUserId = NOBODY;

    [SyncVar(hook = nameof(OnOwnerChanged))]
    private int ownerId = NOBODY;

    // The owner's netId — stored separately because KilledPlayers is keyed
    // by netId (from ThrowableItem), while ownerId is a connectionId.
    // These are different ID spaces and must never be compared against each other.
    [SyncVar]
    private uint ownerNetId = 0;

    private bool isLocallyOpen = false;
    private bool isLocallyInStealMode = false;
    private bool isFull = false;

    public bool IsFull => isFull;

    public event System.Action OnPanelFull;
    public event System.Action OnPanelNoLongerFull;

    // -- Mirror Lifecycle --

    public override void OnStartClient() => base.OnStartClient();
    public override void OnStopClient() => base.OnStopClient();

    // -- IInteractable --

    public bool CanInteract() => true;

    public void OnInteract(PlayerManager player)
    {
        if (!player.isLocalPlayer) return;
        CmdRequestOpen();
    }

    public void OnStopInteraction(PlayerManager player)
    {
        if (!player.isLocalPlayer) return;

        string currentJson = inventoryTetris != null ? inventoryTetris.Save() : "";

        CmdRequestClose(currentJson);

        CloseLocalPanel(); // handles unfreeze, UI teardown, drag handlers — everything
    }

    // -- Server Commands --

    [Command(requiresAuthority = false)]
    private void CmdRequestOpen(NetworkConnectionToClient sender = null)
    {
        int requesterId = sender.connectionId;

        Debug.Log($"[Panel] CmdRequestOpen received from client {requesterId}.");

        // If panel is in use (edit or read-only) by someone else, deny immediately
        bool panelOccupied = (currentUserId != NOBODY && currentUserId != requesterId)
                          || (readOnlyUserId != NOBODY && readOnlyUserId != requesterId);
        if (panelOccupied)
        {
            TargetGrantAccess(sender, "Panel is currently in use.", false, false, false);
            return;
        }

        if (ownerId == NOBODY)
        {
            TargetGrantAccess(sender, "Panel has no owner.", false, false, false);
            return;
        }

        bool requesterIsOwner = (ownerId == requesterId);

        if (requesterIsOwner)
        {
            currentUserId = requesterId;
            TargetGrantAccess(sender, savedState, true, true, false);
            return;
        }

        // -- Non-owner path --
        bool canSteal = false;
        PlayerManager requesterPM = GetPlayerManagerByConnectionId(requesterId);
        if (requesterPM != null && ownerNetId != 0 && requesterPM.KilledPlayers.Contains((ulong)ownerNetId))
            canSteal = true;

        // Non-owners who cannot steal only get read access
        if (!canSteal)
        {
            readOnlyUserId = requesterId;
            TargetGrantAccess(sender, savedState, true, false, false);
            return;
        }

        currentUserId = requesterId;
        TargetGrantAccess(sender, savedState, true, false, true);
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestClose(string json, NetworkConnectionToClient sender = null)
    {
        int requesterId = sender.connectionId;

        if (currentUserId != requesterId && readOnlyUserId != requesterId)
        {
            Debug.LogWarning($"[Panel] Close from {requesterId} ignored (current user is {currentUserId}, read-only user is {readOnlyUserId}).");
            return;
        }

        if (!string.IsNullOrEmpty(json))
            savedState = json;

        if (currentUserId == requesterId)
            currentUserId = NOBODY;

        if (readOnlyUserId == requesterId)
            readOnlyUserId = NOBODY;

        Debug.Log($"[Panel] Closed by {requesterId}. Panel is now free.");
    }

    [Command(requiresAuthority = false)]
    public void CmdCommitSteal(string updatedPanelJson, NetworkConnectionToClient sender = null)
    {
        int requesterId = sender.connectionId;

        if (ownerId == NOBODY || requesterId == ownerId)
        {
            Debug.LogWarning($"[Panel] CommitSteal from {requesterId} rejected.");
            return;
        }

        // Verify the requester actually has steal rights still
        PlayerManager requesterPM = GetPlayerManagerByConnectionId(requesterId);
        if (requesterPM == null || ownerNetId == 0 || !requesterPM.KilledPlayers.Contains((ulong)ownerNetId))
        {
            Debug.LogWarning($"[Panel] CommitSteal from {requesterId} rejected — no steal rights.");
            TargetRevokeStealAccess(sender);
            return;
        }

        savedState = updatedPanelJson;
        Debug.Log($"[Panel] Client {requesterId} stole from panel owned by {ownerId} (netId {ownerNetId}).");

        // Remove steal rights so the thief can only view the panel on future opens.
        // Do NOT transfer ownership — ownerId stays with the original owner so the
        // thief falls into the non-owner (read-only) path on any subsequent interaction.
        PlayerManager thiefPM = GetPlayerManagerByConnectionId(requesterId);
        if (thiefPM != null)
            thiefPM.RemoveKilledPlayer((ulong)ownerNetId);

        // Clear the thief's active session slots so the panel is free again.
        currentUserId = NOBODY;
        readOnlyUserId = NOBODY;

        TargetRevokeStealAccess(sender);
    }

    // -- TargetRpcs --

    [TargetRpc]
    private void TargetGrantAccess(NetworkConnection target, string jsonOrReason, bool granted, bool canEdit, bool canSteal)
    {
        if (!granted)
        {
            Debug.Log($"[Panel] Access denied — {jsonOrReason}");
            return;
        }

        isLocallyOpen = true;
        isLocallyInStealMode = canSteal;

        inventoryTetris.ClearAll();
        if (!string.IsNullOrEmpty(jsonOrReason))
            inventoryTetris.Load(jsonOrReason);

        inventoryTetris.SetLocalPlayerEditor(canEdit);
        inventoryTetris.SetStealMode(canSteal, this);

        SetDragHandlersEnabled(canEdit || canSteal);

        if (UIPanel != null) UIPanel.SetActive(true);
        inventoryPanel.SetActive(true);
        inventoryTetris.SetPanelIsOpen(true);

        // Subscribe now — this is the only panel using InventoryTetris right now.
        inventoryTetris.OnGridFull += HandleGridFull;
        inventoryTetris.OnGridNoLongerFull += HandleGridNoLongerFull;

        PlayerManager localPlayer = NetworkClient.localPlayer?.GetComponent<PlayerManager>();
        if (localPlayer != null)
        {
            localPlayer.interaction.currentlyInteractingObject = (IInteractable)this;
            localPlayer.FreezePlayer();
            localPlayer.IsPanelOpen = true; // Block throwing while panel is open
        }
    }

    [TargetRpc]
    private void TargetRevokeStealAccess(NetworkConnection target)
    {
        Debug.Log("[Panel] Steal committed — closing panel and revoking access.");
        CloseLocalPanel();

        PlayerManager player = GetLocalPlayerManager();
        if (player != null && player.interaction != null)
            player.interaction.currentlyInteractingObject = null;
    }

    // -- SyncVar Hooks --

    private void OnSavedStateChanged(string oldState, string newState) { }

    private void OnCurrentUserChanged(int oldUser, int newUser)
    {
        if (NetworkClient.connection == null) return;

        // Only force-close if the server removed us without us initiating it
        if (newUser == NOBODY
            && oldUser == NetworkClient.connection.connectionId
            && isLocallyOpen)
        {
            CloseLocalPanel();
        }
    }

    private void OnReadOnlyUserChanged(int oldUser, int newUser)
    {
        if (NetworkClient.connection == null) return;

        // Force-close if the server cleared our read-only slot
        if (newUser == NOBODY
            && oldUser == NetworkClient.connection.connectionId
            && isLocallyOpen)
        {
            CloseLocalPanel();
        }
    }

    private void OnOwnerChanged(int oldOwner, int newOwner)
    {
        string label = newOwner == NOBODY ? "nobody" : newOwner.ToString();
        Debug.Log($"[Panel] Ownership claimed by client {label}.");
    }

    // -- Private Helpers --

    private void SetDragHandlersEnabled(bool enabled)
    {
        if (inventoryTetris == null) return;
        var container = inventoryTetris.GetItemContainer();
        if (container == null) return;

        foreach (var handler in container.GetComponentsInChildren<InventoryDragHandler>(true))
            handler.enabled = enabled;
    }

    public void CloseLocalPanel()
    {
        isLocallyOpen = false;
        isLocallyInStealMode = false;

        // Unsubscribe before clearing state so no stale events fire during teardown.
        inventoryTetris.OnGridFull -= HandleGridFull;
        inventoryTetris.OnGridNoLongerFull -= HandleGridNoLongerFull;

        SetDragHandlersEnabled(false);
        inventoryTetris.SetLocalPlayerEditor(false);
        inventoryTetris.SetStealMode(false, null);
        inventoryTetris.ClearAll();
        inventoryPanel.SetActive(false);
        if (UIPanel != null) UIPanel.SetActive(false);
        inventoryTetris.SetPanelIsOpen(false);

        PlayerManager player = GetLocalPlayerManager();
        if (player != null)
        {
            player.IsPanelOpen = false; // Re-enable throwing now that panel is closed
            player.UnfreezePlayer();
        }
    }

    private void HandleGridFull()
    {
        if (isFull) return;
        isFull = true;
        Debug.Log("[Panel] Grid fully filled!");
        OnPanelFull?.Invoke();
    }

    private void HandleGridNoLongerFull()
    {
        if (!isFull) return;
        isFull = false;
        Debug.Log("[Panel] Grid no longer full.");
        OnPanelNoLongerFull?.Invoke();
    }

    private PlayerManager GetLocalPlayerManager()
    {
        if (NetworkClient.localPlayer != null)
            return NetworkClient.localPlayer.GetComponent<PlayerManager>();
        return null;
    }

    private PlayerManager GetPlayerManagerByConnectionId(int connId)
    {
        if (NetworkServer.connections.TryGetValue(connId, out NetworkConnectionToClient conn))
        {
            if (conn.identity != null)
                return conn.identity.GetComponent<PlayerManager>();
        }
        return null;
    }

    // -- Public Accessors --

    public int GetOwnerId() => ownerId;
    public bool IsClaimed() => ownerId != NOBODY;
    public bool IsInUse() => currentUserId != NOBODY || readOnlyUserId != NOBODY;

    [Server]
    public void SetOwner(PlayerManager player, NetworkConnectionToClient conn)
    {
        if (player == null || conn == null)
        {
            ownerId = NOBODY;
            ownerNetId = 0;
            Debug.Log("[Panel] Owner cleared.");
            return;
        }

        NetworkIdentity identity = player.GetComponent<NetworkIdentity>();
        if (identity == null)
        {
            Debug.LogWarning("[Panel] SetOwner failed — player has no NetworkIdentity.");
            return;
        }

        ownerId = conn.connectionId;
        ownerNetId = identity.netId;
        Debug.Log($"[Panel] Owner set to client {ownerId} (netId {ownerNetId}).");
    }
}
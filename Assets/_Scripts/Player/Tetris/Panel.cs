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

    private const int NOBODY = -1;

    [SyncVar(hook = nameof(OnSavedStateChanged))]
    private string savedState = "";

    [SyncVar(hook = nameof(OnCurrentUserChanged))]
    private int currentUserId = NOBODY;

    [SyncVar(hook = nameof(OnOwnerChanged))]
    private int ownerId = NOBODY;

    private bool isLocallyOpen = false;
    private bool isLocallyInStealMode = false;

    // -- Mirror Lifecycle --

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (inventoryTetris != null)
            inventoryTetris.OnGridFull += HandleGridFull;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        if (inventoryTetris != null)
            inventoryTetris.OnGridFull -= HandleGridFull;
    }

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

        // If panel is in use by someone else, deny immediately
        if (currentUserId != NOBODY && currentUserId != requesterId)
        {
            TargetGrantAccess(sender, "Panel is currently in use.", false, false, false);
            return;
        }

        if (ownerId == NOBODY)
        {
            // First ever opener becomes owner
            ownerId = requesterId;
            Debug.Log($"[Panel] Client {requesterId} claimed ownership.");
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
        if (requesterPM != null && requesterPM.KilledPlayers.Contains((ulong)ownerId))
            canSteal = true;

        // Non-owners who cannot steal only get read access (don't lock currentUserId)
        if (!canSteal)
        {
            // Grant read-only without occupying the slot
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

        if (currentUserId != requesterId)
        {
            Debug.LogWarning($"[Panel] Close from {requesterId} ignored (current user is {currentUserId}).");
            return;
        }

        if (!string.IsNullOrEmpty(json))
            savedState = json;

        currentUserId = NOBODY;
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
        if (requesterPM == null || !requesterPM.KilledPlayers.Contains((ulong)ownerId))
        {
            Debug.LogWarning($"[Panel] CommitSteal from {requesterId} rejected — no steal rights.");
            TargetRevokeStealAccess(sender);
            return;
        }

        savedState = updatedPanelJson;
        Debug.Log($"[Panel] Client {requesterId} stole from panel owned by {(ulong)ownerId}.");

        PlayerManager thiefPM = GetPlayerManagerByConnectionId(requesterId);
        if (thiefPM != null)
            thiefPM.RemoveKilledPlayer((ulong)ownerId);

        ownerId = requesterId;
        currentUserId = NOBODY;

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

        PlayerManager localPlayer = NetworkClient.localPlayer?.GetComponent<PlayerManager>();
        if (localPlayer != null)
        {
            localPlayer.interaction.currentlyInteractingObject = (IInteractable)this;
            localPlayer.FreezePlayer();
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

        SetDragHandlersEnabled(false);
        inventoryTetris.SetLocalPlayerEditor(false);
        inventoryTetris.SetStealMode(false, null);
        inventoryTetris.ClearAll();
        inventoryPanel.SetActive(false);
        if (UIPanel != null) UIPanel.SetActive(false);
        inventoryTetris.SetPanelIsOpen(false);

        PlayerManager player = GetLocalPlayerManager();
        player?.UnfreezePlayer();
    }

    private void HandleGridFull()
    {
        Debug.Log("[Panel] Grid fully filled!");
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
    public bool IsInUse() => currentUserId != NOBODY;
}
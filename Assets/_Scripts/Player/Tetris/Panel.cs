using Mirror;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Wir nutzen NetworkBehaviour für Mirror
public class Panel : NetworkBehaviour, IInteractable
{
    public GameObject GameObject => gameObject;

    [SerializeField] private GameObject UIPanel;
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private InventoryTetris inventoryTetris;

    // -- Sentinel --
    // In Mirror ist der Standard-Wert für "Kein Client" meist -1 (da connectionId ein int ist)
    private const int NOBODY = -1;

    // -- Networked State (Mirror [SyncVar]) --

    [SyncVar(hook = nameof(OnSavedStateChanged))]
    private string savedState = "";

    [SyncVar(hook = nameof(OnCurrentUserChanged))]
    private int currentUserId = NOBODY;

    [SyncVar(hook = nameof(OnOwnerChanged))]
    private int ownerId = NOBODY;

    // -- Local state --
    private bool isLocallyOpen = false;
    private bool isLocallyInStealMode = false;

    // -- Mirror Lifecycle --

    public override void OnStartClient()
    {
        base.OnStartClient();
        // Initialisierung des Inventory-Events
        if (inventoryTetris != null)
            inventoryTetris.OnGridFull += HandleGridFull;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        if (inventoryTetris != null)
            inventoryTetris.OnGridFull -= HandleGridFull;
    }


    // -- IInteractable Implementation --

    public bool CanInteract() => true;

    public void OnInteract(PlayerManager player)
    {
        // Check local player
        if (!player.isLocalPlayer) return;

        // FIX: We do NOT pass any ID here. 
        // Mirror fills it in automatically on the server side.
        CmdRequestOpen();
    }

    public void OnStopInteraction(PlayerManager player)
    {
        if (!player.isLocalPlayer) return;

        // Get your current state (however you've implemented your JSON saving)
        string currentJson = ""; // Replace this with your actual save data logic

        // CALL THE COMMAND HERE
        CmdRequestClose(currentJson);

        // Locally close your UI
        isLocallyOpen = false;
        if (UIPanel != null) UIPanel.SetActive(false);
    }

    // -- Server Commands --

    [Command(requiresAuthority = false)]
    private void CmdRequestOpen(NetworkConnectionToClient sender = null)
    {
        // Mirror automatically populates 'sender' with the person who clicked 'F'
        int requesterId = sender.connectionId;

        Debug.Log($"[Panel] CmdRequestOpen received from client {requesterId}.");

        if (ownerId == NOBODY)
        {
            ownerId = requesterId;
            Debug.Log($"[Panel] Client {requesterId} claimed ownership.");
        }

        bool requesterIsOwner = (ownerId == requesterId);

        if (requesterIsOwner)
        {
            if (currentUserId != NOBODY && currentUserId != requesterId)
            {
                // FIX: Use 'sender' instead of connectionToClient
                TargetGrantAccess(sender, "Panel is currently in use.", false, false, false);
                return;
            }

            currentUserId = requesterId;
            TargetGrantAccess(sender, savedState, true, true, false);
            return;
        }

        // -- Non-owner Path --
        if (currentUserId != NOBODY && currentUserId != requesterId)
        {
            TargetGrantAccess(sender, "Panel is currently in use.", false, false, false);
            return;
        }

        // Steal-Mode Logic
        bool canSteal = false;
        if (ownerId != NOBODY)
        {
            PlayerManager requesterPM = GetPlayerManagerByConnectionId(requesterId);
            if (requesterPM != null && requesterPM.KilledPlayers.Contains((ulong)ownerId))
                canSteal = true;
        }

        currentUserId = requesterId;
        TargetGrantAccess(sender, savedState, true, false, canSteal);
    }



    [Command(requiresAuthority = false)]
    private void CmdRequestClose(string json, NetworkConnectionToClient sender = null)
    {
        // Mirror identifies the sender automatically
        int requesterId = sender.connectionId;

        if (currentUserId != requesterId)
        {
            Debug.LogWarning($"[Panel] Close from {requesterId} ignored (current user is {currentUserId}).");
            return;
        }

        // Save the data sent from the client
        if (!string.IsNullOrEmpty(json))
        {
            savedState = json;
        }

        // Free the panel for the next person
        currentUserId = NOBODY;
        Debug.Log($"[Panel] Closed by {requesterId}. Panel is now free.");
    }


    [Command(requiresAuthority = false)]
    public void CmdCommitSteal(int thiefId, string updatedPanelJson, NetworkConnectionToClient sender = null)
    {
        if (ownerId == NOBODY || thiefId == ownerId)
        {
            Debug.LogWarning($"[Panel] CommitSteal from {thiefId} rejected.");
            return;
        }

        savedState = updatedPanelJson;
        Debug.Log($"[Panel] Client {thiefId} stole from panel owned by {(ulong)ownerId}.");

        PlayerManager thiefPM = GetPlayerManagerByConnectionId(thiefId);
        if (thiefPM != null)
        {
            thiefPM.RemoveKilledPlayer((ulong)ownerId);
        }

        currentUserId = NOBODY;

        // Force the thief to close UI
        TargetRevokeStealAccess(sender);
    }

    // --- Targeted Client Rpcs (Mirror equivalent of Targeted RPCs) ---

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

        inventoryPanel.SetActive(true);
        inventoryTetris.SetPanelIsOpen(true);

        // Replace with your local player acquisition logic
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

        // Clear the interaction reference locally
        PlayerManager player = GetLocalPlayerManager();
        if (player != null && player.interaction != null)
            player.interaction.currentlyInteractingObject = null;
    }

    // --- SyncVar Hooks (Callbacks) ---

    // Mirror Hooks require (oldValue, newValue) parameters
    private void OnSavedStateChanged(string oldState, string newState)
    {
        // Intentionally empty per your logic
    }

    private void OnCurrentUserChanged(int oldUser, int newUser)
    {
        // If the lock was just released (set to NOBODY) and I was the one holding it
        if (newUser == NOBODY 
            && oldUser == (int)NetworkClient.connection.connectionId 
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

    // --- Private Helpers ---

    private void SetDragHandlersEnabled(bool enabled)
    {
        if (inventoryTetris == null) return;
        var container = inventoryTetris.GetItemContainer();
        if (container == null) return;

        // Note: In Mirror, you might need to ensure these handlers are 
        // also not fighting with NetworkIdentity authority if they move objects
        foreach (var handler in container.GetComponentsInChildren<InventoryDragHandler>(true))
            handler.enabled = enabled;
    }

    public void CloseLocalPanel()
    {
        isLocallyOpen = false;
        isLocallyInStealMode = false;

        SetDragHandlersEnabled(true);
        inventoryTetris.SetLocalPlayerEditor(false);
        inventoryTetris.SetStealMode(false, null);

        inventoryTetris.ClearAll();
        inventoryPanel.SetActive(false);
        inventoryTetris.SetPanelIsOpen(false);

        PlayerManager player = GetLocalPlayerManager();
        player?.UnfreezePlayer();
    }

    private void HandleGridFull()
    {
        Debug.Log("[Panel] Grid fully filled!");
    }

    /// <summary>
    /// Mirror Helper: Gets the local player sitting at this computer.
    /// </summary>
    private PlayerManager GetLocalPlayerManager()
    {
        if (NetworkClient.localPlayer != null)
            return NetworkClient.localPlayer.GetComponent<PlayerManager>();
        return null;
    }

    /// <summary>
    /// Mirror Server-side helper: finds a PlayerManager by connectionId.
    /// </summary>
    private PlayerManager GetPlayerManagerByConnectionId(int connId)
    {
        // Change NetworkConnection to NetworkConnectionToClient
        if (NetworkServer.connections.TryGetValue(connId, out NetworkConnectionToClient conn))
        {
            // conn.identity is the player object associated with this connection
            if (conn.identity != null)
            {
                return conn.identity.GetComponent<PlayerManager>();
            }
        }
        return null;
    }


    // --- Public Accessors ---

    public int GetOwnerId() => ownerId;
    public bool IsClaimed() => ownerId != NOBODY;
    public bool IsInUse() => currentUserId != NOBODY;
}

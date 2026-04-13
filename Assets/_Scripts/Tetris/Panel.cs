using Mirror;
using UnityEngine;

[RequireComponent(typeof(NetworkIdentity))]
public class Panel : NetworkBehaviour, IInteractable
{
    public GameObject GameObject => gameObject;

    [SerializeField] private GameObject UIPanel;
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private InventoryTetris inventoryTetris;

    private const int NOBODY = -1; // Mirror uses int for connection IDs usually

    // ── Networked state ───────────────────────────────────────────────────

    [SyncVar] private string savedState = "";

    [SyncVar(hook = nameof(OnCurrentUserChanged))]
    private int currentUserId = NOBODY;

    [SyncVar] private int ownerId = NOBODY;

    // ── Local state ───────────────────────────────────────────────────────

    private bool isLocallyOpen = false;

    // ── NetworkBehaviour lifecycle ────────────────────────────────────────

    public override void OnStartClient()
    {
        if (inventoryTetris != null)
            inventoryTetris.OnGridFull += HandleGridFull;

        // Apply initial state if already open
        if (currentUserId == (int)NetworkClient.connection.connectionId)
        {
            // This handles cases where a client might reconnect
        }
    }

    public override void OnStopClient()
    {
        if (inventoryTetris != null)
            inventoryTetris.OnGridFull -= HandleGridFull;
    }

    // ── IInteractable ─────────────────────────────────────────────────────

    public bool CanInteract() => true;

    public void OnInteract(PlayerManager player)
    {
        // In Mirror, 'isLocalPlayer' is the check
        if (!player.isLocalPlayer) return;
        CmdRequestOpen();
    }

    public void OnStopInteraction(PlayerManager player)
    {
        if (!player.isLocalPlayer) return;
        if (!isLocallyOpen) return;

        bool isOwner = (int)NetworkClient.connection.connectionId == ownerId;
        if (isOwner)
        {
            string json = inventoryTetris.Save();
            CmdRequestClose(json);
        }
        else
        {
            CloseLocalPanel();
        }
    }

    // ── Commands (Server Side) ────────────────────────────────────────────

    [Command(requiresAuthority = false)]
    private void CmdRequestOpen(NetworkConnectionToClient sender = null)
    {
        int requesterId = sender.connectionId;

        if (ownerId == NOBODY)
        {
            ownerId = requesterId;
            Debug.Log($"[Panel] Client {requesterId} claimed ownership.");
        }

        bool requesterIsOwner = ownerId == requesterId;

        if (requesterIsOwner)
        {
            if (currentUserId != NOBODY)
            {
                TargetGrantAccess(sender, "Panel is currently in use.", false, false);
                return;
            }
            currentUserId = requesterId;
        }

        TargetGrantAccess(sender, savedState, true, requesterIsOwner);
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestClose(string json, NetworkConnectionToClient sender = null)
    {
        if (currentUserId != sender.connectionId) return;

        savedState = json;
        currentUserId = NOBODY;
    }

    // ── TargetRpc (Delivered only to the specific client) ─────────────────

    [TargetRpc]
    private void TargetGrantAccess(NetworkConnection target, string jsonOrReason, bool granted, bool canEdit)
    {
        if (!granted)
        {
            Debug.Log($"[Panel] Access denied — {jsonOrReason}");
            return;
        }

        isLocallyOpen = true;
        inventoryTetris.ClearAll();

        if (!string.IsNullOrEmpty(jsonOrReason))
            inventoryTetris.Load(jsonOrReason);

        inventoryTetris.SetLocalPlayerEditor(canEdit);
        SetDragHandlersEnabled(canEdit);

        inventoryPanel.SetActive(true);
        inventoryTetris.SetPanelIsOpen(true);

        PlayerManager localPlayer = NetworkClient.localPlayer.GetComponent<PlayerManager>();
        if (localPlayer != null)
        {
            localPlayer.interaction.currentlyInteractingObject = this;
            localPlayer.FreezePlayer();
        }
    }

    // ── Hooks ─────────────────────────────────────────────────────────────

    private void OnCurrentUserChanged(int oldUser, int newUser)
    {
        // If the lock was released and I was the one holding it, close my UI
        if (newUser == NOBODY && oldUser == (int)NetworkClient.connection.connectionId && isLocallyOpen)
        {
            CloseLocalPanel();
        }
    }

    private void CloseLocalPanel()
    {
        isLocallyOpen = false;
        inventoryPanel.SetActive(false);
        inventoryTetris.SetPanelIsOpen(false);

        PlayerManager localPlayer = NetworkClient.localPlayer?.GetComponent<PlayerManager>();
        if (localPlayer != null) localPlayer.UnfreezePlayer();
    }

    private void SetDragHandlersEnabled(bool enabled) { /* Your existing logic */ }
    private void HandleGridFull() { /* Your existing logic */ }
}

using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class PlayerManager : NetworkBehaviour
{
    [HideInInspector] public PlayerInventory inventory;
    [HideInInspector] public PlayerInteraction interaction;
    [HideInInspector] public PlayerMovement movement;

    [SerializeField] private PlayerInventoryUI playerInventoryUIPrefab;

    [HideInInspector] public PlayerInventoryUI playerInventoryUI;

    // Server-authoritative set of unique players this player has killed.
    // HashSet ensures each victim's clientId only appears once.
    public HashSet<ulong> KilledPlayers { get; private set; } = new HashSet<ulong>();

    // Total kill count -- increments on every kill, including repeat victims.
    public int TotalKillCount { get; private set; } = 0;

    // True while the local player has a Panel UI open (edit, read-only, or steal mode).
    // Set by Panel.TargetGrantAccess / Panel.CloseLocalPanel.
    // Used by PlayerInventory to block throwing while a panel is open.
    public bool IsPanelOpen { get; set; } = false;

    public override void OnStartClient()
    {
        base.OnStartClient();
        inventory = GetComponent<PlayerInventory>();
        interaction = GetComponent<PlayerInteraction>();
        movement = GetComponent<PlayerMovement>();

        // Disable the AudioListener on every player that isn't ours.
        // OnStartClient runs for ALL player objects on every client.
        if (!isLocalPlayer)
        {
            AudioListener al = GetComponent<AudioListener>();
            if (al != null) al.enabled = false;
        }
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        if (playerInventoryUIPrefab == null)
        {
            Debug.LogError("[PlayerManager] playerInventoryUIPrefab is NULL!");
            return;
        }

        // Find the container � it should already be on the canvas in the scene
        GameObject uiParent = GameObject.Find("TetrisStuffContainer");
        if (uiParent == null)
        {
            Debug.LogError("[PlayerManager] TetrisStuffContainer not found in scene!");
            return;
        }

        // Don't reparent it � it should already be under the Canvas in your scene hierarchy

        playerInventoryUI = Instantiate(playerInventoryUIPrefab, uiParent.transform);
        playerInventoryUI.playerManager = this;
        playerInventoryUI.Init(inventory);
        if (GetComponent<AudioListener>() == null)
            gameObject.AddComponent<AudioListener>();

    }

    /// <summary>
    /// Called server-side by ThrowableItem when this player kills someone.
    /// Adds the victim to the kill list and notifies the owning client.
    /// </summary>
    public void RegisterKill(ulong killedClientId)
    {
        if (!isServer)
        {
            Debug.LogWarning("[PlayerManager] RegisterKill must only be called on the server.");
            return;
        }
        KilledPlayers.Add(killedClientId); // HashSet: no-op if already present
        TotalKillCount++;
        Debug.Log($"[PlayerManager] Client {netId} killed client {killedClientId}. Unique victims: {KilledPlayers.Count}, Total kills: {TotalKillCount}");

        // Notify the owning client so they can react (e.g. update kill feed UI)
        NotifyKillClientRpc(killedClientId);
    }

    /// <summary>
    /// Sent to the owner whenever they register a new kill.
    /// Use this to update UI or trigger local effects.
    /// </summary>
    [ClientRpc]
    private void NotifyKillClientRpc(ulong killedClientId)
    {
        if (!isLocalPlayer) return;

        Debug.Log($"[PlayerManager] You killed player {killedClientId}! Total kills: {TotalKillCount}");

        // TODO: hook into your kill-feed UI here
    }

    /// <summary>
    /// Removes a specific player from the killed-players set.
    /// Only call server-side.
    /// </summary>
    public void RemoveKilledPlayer(ulong clientId) => KilledPlayers.Remove(clientId);

    /// <summary>
    /// Clears the entire killed-players set. Does not reset TotalKillCount.
    /// Only call server-side.
    /// </summary>
    public void ClearKilledPlayers() => KilledPlayers.Clear();

    public void FreezePlayer() => movement.DisableMovement();
    public void UnfreezePlayer() => movement.EnableMovement();
}
using System.Collections.Generic;
using Unity.Netcode;
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

    public override void OnNetworkSpawn()
    {
        inventory = GetComponent<PlayerInventory>();
        interaction = GetComponent<PlayerInteraction>();
        movement = GetComponent<PlayerMovement>();

        if (!IsOwner) return;

        if (playerInventoryUIPrefab == null)
        {
            Debug.LogError("[PlayerManager] playerInventoryUIPrefab is NULL!");
            return;
        }

        // Find the existing scene Canvas instead of creating a new one.
        // This ensures the UI uses the correct EventSystem and CanvasScaler
        // that's already set up in your scene.
        Canvas canvas = FindAnyObjectByType<Canvas>();

        if (canvas == null)
        {
            // Fallback: create one if somehow none exists
            GameObject canvasGO = new GameObject($"PlayerCanvas_{OwnerClientId}");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        playerInventoryUI = Instantiate(playerInventoryUIPrefab, canvas.transform);
        playerInventoryUI.playerManager = this;
        playerInventoryUI.Init(inventory);
    }

    /// <summary>
    /// Called server-side by ThrowableItem when this player kills someone.
    /// Adds the victim to the kill list and notifies the owning client.
    /// </summary>
    public void RegisterKill(ulong killedClientId)
    {
        if (!IsServer)
        {
            Debug.LogWarning("[PlayerManager] RegisterKill must only be called on the server.");
            return;
        }

        KilledPlayers.Add(killedClientId); // HashSet: no-op if already present
        TotalKillCount++;
        Debug.Log($"[PlayerManager] Client {OwnerClientId} killed client {killedClientId}. Unique victims: {KilledPlayers.Count}, Total kills: {TotalKillCount}");

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
        if (!IsOwner) return;

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
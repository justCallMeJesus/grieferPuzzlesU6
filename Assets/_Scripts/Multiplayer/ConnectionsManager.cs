using Mirror;
using Steamworks;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ConnectionsManager : NetworkManager
{
    [Header("Game Start Settings")]
    public float readyWaitTimeout = 10f;
    public GameObject PlayerPrefab;

    // Maps connectionId -> spawn Transform so ThrowableItem can respawn players
    public static readonly Dictionary<int, Transform> PlayerSpawns = new Dictionary<int, Transform>();

    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);
        Debug.Log($"Server: Scene changed to {sceneName}");
        if (sceneName == "GameScene")
        {
            StartCoroutine(WaitForPlayersAndStart());
            Debug.Log("Server: Started waiting for players to be ready...");
        }
    }


    [Server]
    private IEnumerator WaitForPlayersAndStart()
    {
        Debug.Log("Server: Waiting for all clients to be ready...");
        float elapsed = 0f;

        while (elapsed < readyWaitTimeout)
        {
            // Count connections that don't yet have a player spawned
            int pendingCount = NetworkServer.connections.Values.Count(conn => conn.identity == null);

            if (NetworkServer.connections.Count > 0 && pendingCount == NetworkServer.connections.Count)
            {
                // No one has been spawned yet — check if all are ready
                bool allReady = NetworkServer.connections.Values.All(conn => conn.isReady);
                if (allReady)
                {
                    SpawnPlayers();
                    yield break;
                }
            }
            else if (pendingCount == 0)
            {
                // Everyone already has a player — nothing to do
                yield break;
            }

            elapsed += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }

        Debug.LogWarning("Server: Timed out waiting for clients. Spawning whoever is ready.");
        SpawnPlayers();
    }

    private void SpawnPlayers()
    {
        PlayerSpawns.Clear();

        var sortedSpawns = startPositions.OrderBy(s => s.name).ToList();
        int playerIndex = 0;

        // Sort by connectionId for consistent spawn order across editor and build
        foreach (var conn in NetworkServer.connections.Values.OrderBy(c => c.connectionId))
        {
            if (conn.identity != null) continue; // already has a player

            Transform chosenSpawn = sortedSpawns[playerIndex % sortedSpawns.Count];
            PlayerSpawnpoint spawnPoint = chosenSpawn.GetComponent<PlayerSpawnpoint>();
            GameObject playerTank = Instantiate(PlayerPrefab, chosenSpawn.position, chosenSpawn.rotation);
            PlayerManager playerManager = playerTank.GetComponent<PlayerManager>();

            NetworkServer.AddPlayerForConnection(conn, playerTank);

            PlayerSpawns[conn.connectionId] = chosenSpawn;

            spawnPoint.panel.SetOwner(playerManager, conn);

            // Steam name
            string pName;
            try
            {
                pName = new Friend((ulong)conn.connectionId).Name;
                if (string.IsNullOrEmpty(pName) || pName == "[unknown]")
                    pName = "Player " + (playerIndex + 1);
            }
            catch
            {
                Debug.LogWarning($"Failed to get Steam name for connection {conn.connectionId}. Using default name.");
                pName = "Player " + (playerIndex + 1);
            }

            // Color & identity
            PlayerColor identity = playerTank.GetComponent<PlayerColor>();
            if (identity != null)
            {
                identity.SetPlayerIdentity(playerIndex, pName);
            }

            // Hide UI
            PlayerMovement playerScript = playerTank.GetComponent<PlayerMovement>();
            if (playerScript != null)
            {
                playerScript.RpcHideUI();
            }

            playerIndex++;
        }
    }
}
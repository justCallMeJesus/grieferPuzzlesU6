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

    public override void OnClientSceneChanged()
    {
        base.OnClientSceneChanged();

        if (LoadingScreenManager.Instance != null)
        {
            LoadingScreenManager.Instance.HideLoadingScreen();
        }
    }

    [Server]
    private IEnumerator WaitForPlayersAndStart()
    {
        Debug.Log("Server: Waiting for all clients to be ready...");
        float elapsed = 0f;

        while (elapsed < readyWaitTimeout)
        {
            bool allReady = NetworkServer.connections.Values.All(conn => conn.isReady);

            if (allReady && NetworkServer.connections.Count > 0)
            {
                SpawnPlayers();
                yield break;
            }

            elapsed += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }

        SpawnPlayers();
    }

    private void SpawnPlayers()
    {
        PlayerSpawns.Clear();

        var sortedSpawns = startPositions.OrderBy(s => s.name).ToList();
        int playerIndex = 0;

        foreach (var conn in NetworkServer.connections.Values)
        {
            if (!conn.isReady || conn.identity != null) continue;

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
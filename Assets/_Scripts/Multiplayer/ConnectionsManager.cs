using Mirror;
using UnityEngine;
using System.Collections;
using System.Linq;

public class ConnectionsManager : NetworkManager
{
    [Header("Game Start Settings")]
    public float readyWaitTimeout = 10f;
    public GameObject PlayerPrefab;

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

    // Called on every client (including host) after the scene finishes loading locally
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
        var sortedSpawns = startPositions.OrderBy(s => s.name).ToList();
        int playerIndex = 0;

        foreach (var conn in NetworkServer.connections.Values)
        {
            if (!conn.isReady || conn.identity != null) continue;

            Transform chosenSpawn = sortedSpawns[playerIndex % sortedSpawns.Count];
            GameObject playerTank = Instantiate(PlayerPrefab, chosenSpawn.position, chosenSpawn.rotation);
            NetworkServer.AddPlayerForConnection(conn, playerTank);
            playerIndex++;
        }
    }
}
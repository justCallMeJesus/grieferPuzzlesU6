using Mirror;
using UnityEngine;

public class GameStartHandler : NetworkBehaviour
{
    [Header("Prefabs & UI")]
    public GameObject PlayerPrefab;
    public UiManager uiManager;

    // Called by your UI Button (the "Start Game" button)
    // IMPORTANT: Only the Host should be able to click this!
    public void RequestStartGame()
    {
        if (isServer)
        {
            StartGame();
        }
    }

    [Server]
    public void StartGame()
    {
        Debug.Log("Server: Starting Game...");
        Debug.Log($"Total connections on server: {NetworkServer.connections.Count}");

        // Log all connection states before spawning
        foreach (var conn in NetworkServer.connections.Values)
        {
            Debug.Log($"Conn {conn.connectionId} | isReady: {conn.isReady} | hasPlayer: {conn.identity != null}");
        }

        var availableSpawns = new System.Collections.Generic.List<Transform>(NetworkManager.startPositions);

        foreach (var conn in NetworkServer.connections.Values)
        {
            // Skip connections that aren't ready yet (clients not fully joined)
            if (!conn.isReady)
            {
                Debug.LogWarning($"Connection {conn.connectionId} is not ready, skipping.");
                continue;
            }

            // Skip connections that already have a player object
            if (conn.identity != null)
            {
                Debug.LogWarning($"Connection {conn.connectionId} already has a player, skipping.");
                continue;
            }

            // Pick a spawn point
            Vector3 spawnPos = Vector3.zero;
            Quaternion spawnRot = Quaternion.identity;

            if (availableSpawns.Count > 0)
            {
                int index = Random.Range(0, availableSpawns.Count);
                Transform chosenSpawn = availableSpawns[index];
                spawnPos = chosenSpawn.position;
                spawnRot = chosenSpawn.rotation;

                // Remove so the next player gets a different spot
                availableSpawns.RemoveAt(index);
            }

            // Instantiate at the chosen position
            GameObject playerTank = Instantiate(PlayerPrefab, spawnPos, spawnRot);

            // Spawn and assign authority
            NetworkServer.AddPlayerForConnection(conn, playerTank);

            PlayerMovement playerScript = playerTank.GetComponent<PlayerMovement>();
            if (playerScript != null)
            {
                playerScript.RpcHideUI();
            }

            Debug.Log($"Spawned Player at {spawnPos} for Connection ID: {conn.connectionId}");
        }
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        uiManager.OnMirrorStop();
    }
}
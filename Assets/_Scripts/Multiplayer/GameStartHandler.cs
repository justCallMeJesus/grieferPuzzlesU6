using Mirror;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GameStartHandler : NetworkBehaviour
{
    [Header("Prefabs & UI")]
    public GameObject PlayerPrefab;
    public UiManager uiManager;

    [Header("Settings")]
    [Tooltip("How long (seconds) to wait for slow clients to become ready before giving up on them.")]
    public float readyWaitTimeout = 10f;

    // Called by your UI Button (the "Start Game" button).
    // IMPORTANT: Only the Host should be able to click this!
    public void RequestStartGame()
    {
        if (!NetworkServer.active)
        {
            Debug.LogWarning("RequestStartGame called but server is not active.");
            return;
        }

        if (isServer)
        {
            // FIX #4: Use a coroutine so we wait for slow/high-latency clients
            // to become ready instead of skipping them permanently.
            StartCoroutine(WaitForPlayersAndStart());
        }
    }

    // FIX #4: Polls until all connections are ready (or times out) before spawning players.
    [Server]
    private IEnumerator WaitForPlayersAndStart()
    {
        Debug.Log("Server: Waiting for all clients to be ready...");
        float elapsed = 0f;

        while (elapsed < readyWaitTimeout)
        {
            bool allReady = true;
            foreach (var conn in NetworkServer.connections.Values)
            {
                if (!conn.isReady)
                {
                    allReady = false;
                    break;
                }
            }

            if (allReady)
            {
                Debug.Log("All clients ready. Starting game.");
                StartGame();
                yield break;
            }

            elapsed += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }

        // Timeout reached — start anyway and skip genuinely unready connections.
        Debug.LogWarning($"Timed out waiting for all clients after {readyWaitTimeout}s. Starting with ready players only.");
        StartGame();
    }

    [Server]
    public void StartGame()
    {
        Debug.Log("Server: Starting Game...");
        Debug.Log($"Total connections: {NetworkServer.connections.Count}");

        foreach (var conn in NetworkServer.connections.Values)
        {
            Debug.Log($"Conn {conn.connectionId} | isReady: {conn.isReady} | hasPlayer: {conn.identity != null}");
        }

        var availableSpawns = new List<Transform>(NetworkManager.startPositions);

        foreach (var conn in NetworkServer.connections.Values)
        {
            if (!conn.isReady)
            {
                Debug.LogWarning($"Connection {conn.connectionId} still not ready at game start, skipping.");
                continue;
            }

            if (conn.identity != null)
            {
                Debug.LogWarning($"Connection {conn.connectionId} already has a player, skipping.");
                continue;
            }

            Vector3 spawnPos = Vector3.zero;
            Quaternion spawnRot = Quaternion.identity;

            if (availableSpawns.Count > 0)
            {
                int index = Random.Range(0, availableSpawns.Count);
                Transform chosenSpawn = availableSpawns[index];
                spawnPos = chosenSpawn.position;
                spawnRot = chosenSpawn.rotation;
                availableSpawns.RemoveAt(index);
            }

            GameObject playerTank = Instantiate(PlayerPrefab, spawnPos, spawnRot);
            NetworkServer.AddPlayerForConnection(conn, playerTank);

            PlayerMovement playerScript = playerTank.GetComponent<PlayerMovement>();
            if (playerScript != null)
            {
                playerScript.RpcHideUI();
            }

            Debug.Log($"Spawned player at {spawnPos} for Connection {conn.connectionId}");
        }
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        uiManager.OnMirrorStop();
    }
}
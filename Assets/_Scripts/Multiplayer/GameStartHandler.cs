using Mirror;
using Steamworks;
using System.Collections;
using System.Linq; // Required for OrderBy
using UnityEngine;

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

        // Timeout reached � start anyway and skip genuinely unready connections.
        Debug.LogWarning($"Timed out waiting for all clients after {readyWaitTimeout}s. Starting with ready players only.");
        StartGame();
    }


    [Server]
    public void StartGame()
    {
        var sortedSpawns = NetworkManager.startPositions.OrderBy(s => s.name).ToList();
        int playerIndex = 0;

        foreach (var conn in NetworkServer.connections.Values)
        {
            if (!conn.isReady || conn.identity != null) continue;

            Transform chosenSpawn = sortedSpawns[playerIndex % sortedSpawns.Count];
            PlayerSpawnpoint playerSpawnpoint = chosenSpawn.GetComponent<PlayerSpawnpoint>();
            GameObject playerTank = Instantiate(PlayerPrefab, chosenSpawn.position, chosenSpawn.rotation);
            PlayerManager player = playerTank.GetComponent<PlayerManager>();
            NetworkServer.AddPlayerForConnection(conn, playerTank);
            playerSpawnpoint.panel.SetOwner(player, conn);

            // 1. Set Steam Identity and Color
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

            PlayerColor identity = playerTank.GetComponent<PlayerColor>();
            if (identity != null)
            {
                identity.SetPlayerIdentity(playerIndex, pName);
            }

            // 2. HIDE THE UI (This is the missing part)
            PlayerMovement playerScript = playerTank.GetComponent<PlayerMovement>();
            if (playerScript != null)
            {
                playerScript.RpcHideUI();
            }

            playerIndex++;
        }
    }






    public override void OnStopClient()
    {
        base.OnStopClient();
        uiManager.OnMirrorStop();
    }
}
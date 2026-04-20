using Mirror;
using UnityEngine;

public class GameStartHandler : NetworkBehaviour
{
    [Header("Prefabs & UI")]
    public GameObject PlayerPrefab;
    public UiManager uiManager;
    // This is called by your UI Button (the "Start Game" button)
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

        // Create a copy of the spawn points list so we can track which ones are used
        // (Optional: removes the chance of two players spawning on the same spot)
        var availableSpawns = new System.Collections.Generic.List<Transform>(NetworkManager.startPositions);

        foreach (var conn in NetworkServer.connections.Values)
        {
            // 1. Pick a random spawn point
            Vector3 spawnPos = Vector3.zero;
            Quaternion spawnRot = Quaternion.identity;

            if (availableSpawns.Count > 0)
            {
                int index = Random.Range(0, availableSpawns.Count);
                Transform chosenSpawn = availableSpawns[index];

                spawnPos = chosenSpawn.position;
                spawnRot = chosenSpawn.rotation;

                // Remove from list so the next player gets a different spot
                availableSpawns.RemoveAt(index);
            }

            // 2. Instantiate at the chosen position
            GameObject playerTank = Instantiate(PlayerPrefab, spawnPos, spawnRot);

            // 3. Spawn and assign authority
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

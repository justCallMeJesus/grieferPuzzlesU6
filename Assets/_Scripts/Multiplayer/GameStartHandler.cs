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
        // Use numPlayers for a more accurate count of active participants
        Debug.Log($"Starting game with {NetworkServer.numPlayers} players.");

        var availableSpawns = new List<Transform>(NetworkManager.startPositions);

        // Iterating through values is safer in Mirror to avoid index errors
        foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
        {
            if (conn == null) continue;

            // 1. Pick Spawn Point
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

            // 2. Spawn and replace the "Lobby" player with the "Game" player
            GameObject playerTank = Instantiate(PlayerPrefab, spawnPos, spawnRot);

            // This is the CRITICAL Mirror function for transitioning from Lobby to Game
            NetworkServer.ReplacePlayerForConnection(conn, playerTank);

            playerTank.GetComponent<PlayerMovement>()?.RpcHideUI();
        }
    }



    public override void OnStopClient()
    {
        base.OnStopClient();
        uiManager.OnMirrorStop();
    }
}

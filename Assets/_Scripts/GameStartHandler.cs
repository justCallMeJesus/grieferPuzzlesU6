using Mirror;
using UnityEngine;

public class GameStartHandler : NetworkBehaviour
{
    [Header("Prefabs & UI")]
    public GameObject tank;
    public GameObject BackGroundImage;
    public GameObject InLobbyUI;
    public GameObject StartButton;
    public GameObject LeaveButton;
    public GameObject HomeScreenUI;
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


        // 2. Loop through all active connections and spawn a tank for each
        foreach (var conn in NetworkServer.connections.Values)
        {
            // Instantiate the tank on the server
            GameObject playerTank = Instantiate(tank);

            // Spawn it and give authority to the specific connection
            // This makes it THEIR "Player Object"
            NetworkServer.AddPlayerForConnection(conn, playerTank);
            PlayerMovement playerScript = playerTank.GetComponent<PlayerMovement>();
            if (playerScript != null)
            {
                playerScript.RpcHideUI();
            }
            Debug.Log($"Spawned tank for Connection ID: {conn.connectionId}");
        }
    }

   

    public override void OnStartServer()
    {
        base.OnStartServer();
        //uiManager.OnMirrorStartServer();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        //uiManager.OnMirrorStartClient();
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        uiManager.OnMirrorStop();
    }
}

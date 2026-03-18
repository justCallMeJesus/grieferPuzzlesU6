using UnityEngine;
using Mirror;
using Steamworks;
using Steamworks.Data;

public class SteamNetworkManager : NetworkManager 
{

    public override void Awake()
    {
        base.Awake();

        
        Debug.Log("Network Manager is awake and ready!");
        DontDestroyOnLoad(this);
    }
    // --- MIRROR OVERRIDES ---

    // Called on the Server/Host when a client finishes connecting via Steam
    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        // 1. Instantiate the prefab (assigned in Inspector)
        GameObject player = Instantiate(playerPrefab);

        // 2. Link the prefab to the connection (This sets isLocalPlayer correctly)
        NetworkServer.AddPlayerForConnection(conn, player);

        Debug.Log($"Player spawned for Steam Connection: {conn.connectionId}");
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log($"Server Active: {NetworkServer.active} | Client Active: {NetworkClient.active}");
            Debug.Log($"Connections: {NetworkServer.connections.Count}");
        }
    }


}

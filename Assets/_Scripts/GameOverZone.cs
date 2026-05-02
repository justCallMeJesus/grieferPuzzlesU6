using UnityEngine;
using Mirror;

public class GameOverZone : NetworkBehaviour
{
    public GameObject BlueWon;
    public GameObject GreenWon;
    public GameObject RedWon;
    public GameObject YellowWon;

    private void OnTriggerEnter(Collider other)
    {
        if (!isServer) return;

        PlayerManager player = other.GetComponent<PlayerManager>();
        if (player == null) return;

        int connId = player.connectionToClient != null ? player.connectionToClient.connectionId : -1;
        Debug.Log($"Game Over triggered by: Connection ID {connId}");

        RpcShowWinScreen(connId);
    }

    [ClientRpc]
    private void RpcShowWinScreen(int connId)
    {
        if (connId == 0)
            BlueWon.SetActive(true);
        else if (connId == 1)
            GreenWon.SetActive(true);
        else if (connId == 2)
            RedWon.SetActive(true);
        else if (connId == 3)
            YellowWon.SetActive(true);
        else
            Debug.Log($"Unknown player with Connection ID {connId} hit the Game Over zone.");
    }
}
using Mirror;
using UnityEngine;

public class GameStartHandler : MonoBehaviour // Changed to MonoBehaviour
{
    public void ChangeSceneOnGameStart()
    {
        // Check if we are the server/host before allowing the scene change
        if (NetworkServer.active)
        {
            Debug.Log("Host clicked Start: Changing scene...");
            NetworkManager.singleton.ServerChangeScene("GameScene");
        }
        else
        {
            Debug.LogWarning("Non-host tried to start the game!");
        }
    }
}

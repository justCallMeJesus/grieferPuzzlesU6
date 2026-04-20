using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;

public class PlayerToPlayerInteraction : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private string playerTag = "Player";

    private Camera myLocalCamera;
    private Camera spectatorTarget;
    private bool isDead = false;

    // This runs only on the client that owns this player prefab
    public override void OnStartLocalPlayer()
    {
        // 1. Find the camera attached to THIS spawned prefab
        myLocalCamera = GetComponentInChildren<Camera>();

        if (myLocalCamera != null)
        {
            myLocalCamera.enabled = true;
            // Ensure we have an AudioListener active for ourselves
            AudioListener listener = myLocalCamera.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = true;
        }
        else
        {
            Debug.LogError("DeathCameraScript: No Camera found on the Player Prefab!");
        }
    }

    void Update()
    {
        // Only run input logic for the player sitting at this computer
        if (!isLocalPlayer) return;

        // Toggle death state with "O" for testing
        if (Keyboard.current.oKey.wasPressedThisFrame)
        {
            if (!isDead)
                OnDeath();
            else
                OnRespawn();
        }
    }

    public void OnDeath()
    {
        isDead = true;
        Debug.Log("Local Player Died. Searching for spectator target...");

        // 1. Shut off our own view
        if (myLocalCamera != null) myLocalCamera.enabled = false;

        // 2. Find all players in the scene
        GameObject[] players = GameObject.FindGameObjectsWithTag(playerTag);

        foreach (GameObject p in players)
        {
            NetworkIdentity ni = p.GetComponent<NetworkIdentity>();

            // Check: Is this a valid player AND is it NOT me?
            if (ni != null && ni.netId != netId)
            {
                Camera otherCam = p.GetComponentInChildren<Camera>();
                if (otherCam != null)
                {
                    otherCam.enabled = true;
                    spectatorTarget = otherCam;

                    // Optional: Enable their audio so you can hear what they hear
                    AudioListener otherListener = otherCam.GetComponent<AudioListener>();
                    if (otherListener != null) otherListener.enabled = true;

                    Debug.Log($"Spectating Player with NetID: {ni.netId}");
                    return; // Stop searching once we find someone
                }
            }
        }

        Debug.LogWarning("No other players found to spectate.");
    }

    public void OnRespawn()
    {
        isDead = false;

        // 1. Stop spectating the other player
        if (spectatorTarget != null)
        {
            spectatorTarget.enabled = false;

            AudioListener otherListener = spectatorTarget.GetComponent<AudioListener>();
            if (otherListener != null) otherListener.enabled = false;

            spectatorTarget = null;
        }

        // 2. Turn our own camera back on
        if (myLocalCamera != null)
        {
            myLocalCamera.enabled = true;
        }
    }
}

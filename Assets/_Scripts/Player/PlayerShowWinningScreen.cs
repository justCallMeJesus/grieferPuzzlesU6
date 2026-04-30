using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class PlayerShowWinningScreen : NetworkBehaviour
{
    [System.Serializable]
    public struct PlayerWinScreen
    {
        public string playerName;
        public GameObject winScreenObject;
    }

    [Header("Win Screens")]
    [Tooltip("Map each player identifier to its corresponding win screen GameObject.")]
    public List<PlayerWinScreen> winScreens = new List<PlayerWinScreen>();

    // ── Server: call this when a winner is determined ──────────────────────────


    //To call use this: winningScreenObject.GetComponent<PlayerShowWinningScreen>().OnGameWon(connectionId); 0 = blue, 1 = red, etc. (match these to your player identifiers)
    public void OnGameWon(string winnerName)
    {
        if (!isServer)
        {
            Debug.LogWarning("[PlayerShowWinningScreen] OnGameWon() must be called on the server.", this);
            return;
        }

        RpcShowWinningScreen(winnerName);
    }

    // ── ClientRpc: runs on every client (and host) ─────────────────────────────

    [ClientRpc]
    private void RpcShowWinningScreen(string winnerName)
    {
        // Hide all screens first.
        foreach (PlayerWinScreen entry in winScreens)
        {
            if (entry.winScreenObject != null)
                entry.winScreenObject.SetActive(false);
        }

        // Activate the matching winner screen.
        bool found = false;
        foreach (PlayerWinScreen entry in winScreens)
        {
            if (entry.playerName == winnerName)
            {
                if (entry.winScreenObject != null)
                    entry.winScreenObject.SetActive(true);

                found = true;
                break;
            }
        }

        if (!found)
            Debug.LogWarning($"[PlayerShowWinningScreen] No win screen found for winner '{winnerName}'.", this);
    }
}
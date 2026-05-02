using UnityEngine;
using UnityEngine.UI;
using Steamworks;
using Steamworks.Data;
using UnityEngine.InputSystem;
using Mirror;

public class UiManagerGameScene : MonoBehaviour
{
    public static UiManager Instance { get; private set; }

    [Header("UI Elements")]

    public GameObject RulesContainerCopy;
    public GameObject RulesContainerCopyCopy;
    public GameObject PauseMenuUI;
    public GameObject SettingsContainer;

    private bool isPaused = false;

    public Lobby currentLobby;

    private void Update()
    {
        SteamClient.RunCallbacks();
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isPaused)
                ClosePauseMenu();
            else
                OpenPauseMenu();
        }
    }

    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
        Debug.Log("Game is exiting...");
    }

    public void LeaveLobby()
    {
        currentLobby.Leave();

        if (NetworkServer.active && NetworkClient.isConnected)
        {
            // Host: stop host and return to lobby scene
            NetworkManager.singleton.StopHost();
        }
        else if (NetworkClient.isConnected)
        {
            // Client: disconnect and return to lobby scene
            NetworkManager.singleton.StopClient();
        }

        Debug.Log("Left lobby and returning to lobby scene.");
    }

    public void ShowRules()
    {
        if (RulesContainerCopy != null) RulesContainerCopy.SetActive(true);
    }

    public void HideRules()
    {
        if (RulesContainerCopy != null) RulesContainerCopy.SetActive(false);
    }

    public void HideRulesCopy()
    {
        if (RulesContainerCopyCopy != null) RulesContainerCopyCopy.SetActive(false);
    }

    private void OpenPauseMenu()
    {
        if (PauseMenuUI != null) PauseMenuUI.SetActive(true);
        isPaused = true;
        Debug.Log("Pause Menu Opened");
    }

    public void ClosePauseMenu()
    {
        if (PauseMenuUI != null) PauseMenuUI.SetActive(false);
        isPaused = false;
        Debug.Log("Pause Menu Closed");
    }

    public void OpenSettings()
    {
        if (SettingsContainer != null) SettingsContainer.SetActive(true);
    }

    public void CloseSettings()
    {
        if (SettingsContainer != null) SettingsContainer.SetActive(false);
    }
}
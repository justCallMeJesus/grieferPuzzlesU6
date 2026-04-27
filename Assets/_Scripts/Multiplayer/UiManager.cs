using UnityEngine;
using UnityEngine.UI;
using Steamworks;
using Steamworks.Data;
using UnityEngine.InputSystem;

public class UiManager : MonoBehaviour
{
    public static UiManager Instance { get; private set; }

    [Header("UI Elements")]
    public GameObject startGameButton;
    public GameObject LeaveButton;
    public GameObject mainMenuUI;
    public GameObject HostedLobbyUI;
    public GameObject RulesContainer;
    public GameObject RulesContainerCopy;
    public GameObject PauseMenuUI;
    public GameObject MainContainer;
    public GameObject TetrisContainer;
    public GameObject SettingsContainer;

    private bool isPaused = false;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    // FIX #5: Removed Steam event subscriptions from UiManager entirely.
    // UiManager now reacts only to the UnityEvents exposed by SteamLobbyManager,
    // which you wire up in the Inspector. This prevents duplicate lobby state and
    // conflicting SetPublic() / SetJoinable() calls.
    //
    // In the Inspector, wire up:
    //   SteamLobbyManager.OnLobbyCreated  → UiManager.OnLobbyCreatedUI
    //   SteamLobbyManager.OnLobbyJoined   → UiManager.OnLobbyEnteredUI
    //   SteamLobbyManager.OnLobbyLeave    → UiManager.OnMirrorStop

    private void Update()
    {
        // FIX #6: Removed duplicate SteamClient.RunCallbacks() call.
        // SteamLobbyManager.Update() is the single place this runs.

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isPaused)
                ClosePauseMenu();
            else
                OpenPauseMenu();
        }
    }

    // --- LOBBY UI CALLBACKS (called via UnityEvents from SteamLobbyManager) ---

    public void OnLobbyCreatedUI()
    {
        if (mainMenuUI != null) mainMenuUI.SetActive(false);
        if (HostedLobbyUI != null) HostedLobbyUI.SetActive(true);
        RefreshStartButton();
        Debug.Log("UiManager: Lobby created.");
    }

    public void OnLobbyEnteredUI()
    {
        if (mainMenuUI != null) mainMenuUI.SetActive(false);
        if (LeaveButton != null) LeaveButton.SetActive(true);
        if (HostedLobbyUI != null) HostedLobbyUI.SetActive(true);
        RefreshStartButton();
        Debug.Log("UiManager: Entered lobby.");
    }

    // Reads lobby state from the single source of truth: SteamLobbyManager.
    public void RefreshStartButton()
    {
        if (SteamLobbyManager.Instance == null) return;
        var lobby = SteamLobbyManager.Instance.currentLobby;
        if (lobby.Id.Value == 0) return;

        if (startGameButton != null)
        {
            startGameButton.SetActive(lobby.Owner.Id == SteamClient.SteamId);
        }
    }

    // Keep this overload so existing callers that pass a string reason still compile.
    public void RefreshUI(string reason)
    {
        Debug.Log($"Refreshing UI: {reason}");
        RefreshStartButton();
    }

    // --- MIRROR BRIDGES ---

    public void OnMirrorStop()
    {
        if (SteamLobbyManager.Instance != null)
            SteamLobbyManager.Instance.LeaveLobby();

        if (HostedLobbyUI != null) HostedLobbyUI.SetActive(false);
        if (mainMenuUI != null) mainMenuUI.SetActive(true);
        if (LeaveButton != null) LeaveButton.SetActive(false);
        if (startGameButton != null) startGameButton.SetActive(false);
        if (MainContainer != null) MainContainer.SetActive(true);

        if (TetrisContainer != null)
        {
            Transform inventoryTransform = TetrisContainer.transform.Find("PlayerInventory (Clone)");
            if (inventoryTransform != null)
                Destroy(inventoryTransform.gameObject);
        }
    }

    // --- GENERAL UI ---

    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
        Debug.Log("Game is exiting...");
    }

    public void ShowRules()
    {
        if (RulesContainer != null) RulesContainer.SetActive(true);
        if (RulesContainerCopy != null) RulesContainerCopy.SetActive(true);
    }

    public void HideRules()
    {
        if (RulesContainer != null) RulesContainer.SetActive(false);
        if (RulesContainerCopy != null) RulesContainerCopy.SetActive(false);
    }

    public void JoinLobbyUi()
    {
        if (mainMenuUI != null) mainMenuUI.SetActive(false);
        if (LeaveButton != null) LeaveButton.SetActive(true);
        if (HostedLobbyUI != null) HostedLobbyUI.SetActive(true);
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
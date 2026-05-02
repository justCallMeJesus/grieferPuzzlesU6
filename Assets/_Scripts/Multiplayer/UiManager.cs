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
    public GameObject RulesContainerCopy; //This is an excact copy in the settings container, im just lazy lol
    public GameObject PauseMenuUI;
    public GameObject MainContainer;
    public GameObject TetrisContainer;
    public GameObject SettingsContainer;

    private bool isPaused = false;

    // This is the variable that was missing
    public Lobby currentLobby;

    private void Awake()
    {
        // Singleton pattern to keep this manager accessible
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); }
    }

    private void OnEnable()
    {
        // Steam Events
        SteamMatchmaking.OnLobbyCreated += OnLobbyCreated;
        SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
        SteamMatchmaking.OnLobbyMemberJoined += (lobby, friend) => RefreshUI("Player Joined");
        SteamMatchmaking.OnLobbyMemberLeave += (lobby, friend) => RefreshUI("Player Left");
        SteamMatchmaking.OnLobbyMemberDisconnected += (lobby, friend) => RefreshUI("Player Disconnected");
    }

    private void OnDisable()
    {
        SteamMatchmaking.OnLobbyCreated -= OnLobbyCreated;
        SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
    }

    private void Update()
    {
        // Vital for Facepunch events to fire
        SteamClient.RunCallbacks();
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isPaused)
                ClosePauseMenu();
            else
                OpenPauseMenu();
        }
    }

    // --- STEAM CALLBACKS ---

    private void OnLobbyCreated(Result result, Lobby lobby)
    {
        if (result != Result.OK)
        {
            Debug.LogError("Lobby creation failed!");
            return;
        }
        // Save the lobby reference
        currentLobby = lobby;
        lobby.SetPublic();
        lobby.SetJoinable(true);
        if (mainMenuUI != null) mainMenuUI.SetActive(false);
        if (HostedLobbyUI != null) HostedLobbyUI.SetActive(true);
        Debug.Log("Lobby created successfully.");
    }

    private void OnLobbyEntered(Lobby lobby)
    {
        currentLobby = lobby;
        RefreshUI("Entered Lobby");

        // Transition UI
        if (mainMenuUI != null) mainMenuUI.SetActive(false);
        if (LeaveButton != null) LeaveButton.SetActive(true);
    }

    // --- UI LOGIC ---

    public void RefreshUI(string reason)
    {
        Debug.Log($"Refreshing UI: {reason}");

        if (currentLobby.Id.Value == 0) return;

        // 2. Toggle Start Button (Only for Host)
        if (startGameButton != null)
        {
            // currentLobby.IsOwnedByMe is built into Facepunch
            
            startGameButton.SetActive(currentLobby.Owner.Id == SteamClient.SteamId);
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            Debug.Log($"Start Game Button active: {startGameButton.activeSelf}");
        }
    }

    // --- MIRROR BRIDGES ---

    public void OnMirrorStop()
    {
        // If the game ends or disconnects, leave the Steam lobby
        currentLobby.Leave();
        currentLobby = default; // Reset the variable

        if (HostedLobbyUI != null) HostedLobbyUI.SetActive(false);
        if (mainMenuUI != null) mainMenuUI.SetActive(true);
        if (LeaveButton != null) LeaveButton.SetActive(false);
        if (startGameButton != null) startGameButton.SetActive(false);
        if (MainContainer != null) MainContainer.SetActive(true);

        Transform inventoryTransform = TetrisContainer.transform.Find("PlayerInventory (Clone)");

        if (inventoryTransform != null)
        {
            Destroy(inventoryTransform.gameObject);
        }
    }

    // Some Ui stuff (hopefully every Change in Ui is in this script lol)

    public void QuitGame()
    {
        // This line closes the actual built game (.exe / .app)
        Application.Quit();

        // This line stops the "Play" mode inside the Unity Editor so you can see it works
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

    // Pause Menu Logic

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
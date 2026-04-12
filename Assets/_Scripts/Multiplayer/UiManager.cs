using UnityEngine;
using UnityEngine.UI;
using Steamworks;
using Steamworks.Data;
using Mirror;
using TMPro;

public class UiManager : MonoBehaviour
{
    public static UiManager Instance { get; private set; }

    [Header("UI Elements")]
    public GameObject startGameButton;

    public GameObject LeaveButton;
    public GameObject mainMenuUI;

    // This is the variable that was missing
    public Lobby currentLobby;

    private void Awake()
    {
        // Singleton pattern to keep this manager accessible
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
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
        }
    }

    // --- MIRROR BRIDGES ---

    public void OnMirrorStop()
    {
        // If the game ends or disconnects, leave the Steam lobby
        currentLobby.Leave();
        currentLobby = default; // Reset the variable

        if (mainMenuUI != null) mainMenuUI.SetActive(true);
        if (LeaveButton != null) LeaveButton.SetActive(false);
        if (startGameButton != null) startGameButton.SetActive(false);
    }
}

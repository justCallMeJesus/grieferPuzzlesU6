using Mirror;
using Steamworks;
using Steamworks.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class SteamLobbyManager : MonoBehaviour
{
    public Lobby currentLobby;

    public UnityEvent OnLobbyCreated;
    public UnityEvent OnLobbyJoined;
    public static Action OnClientJoinedLobby;
    public UnityEvent OnLobbyLeave;

    public GameObject PlayerPrefab;
    public Dictionary<SteamId, GameObject> inLobby = new Dictionary<SteamId, GameObject>();

    public static SteamLobbyManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); }
    }

    public void TriggerLobbyEvent()
    {
        Debug.Log("Triggering lobby event for clients...");
        OnLobbyJoined?.Invoke();
    }

    private void Start()
    {
     

        SteamMatchmaking.OnLobbyCreated += OnLobbyCreatedCallBack;
        SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
        SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
        SteamMatchmaking.OnChatMessage += OnChatMessage;
        SteamMatchmaking.OnLobbyMemberDisconnected += OnLobbyMemberDisconnected;
        SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberDisconnected;
        SteamMatchmaking.OnLobbyGameCreated += OnLobbyGameCreated;
        SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequest;
        SteamMatchmaking.OnLobbyInvite += OnLobbyInvite;
    }

    // FIX #7: Moved SteamClient.Shutdown() from OnDisable to OnApplicationQuit
    // so scene transitions / object deactivation don't kill the Steam client mid-session.
    private void OnApplicationQuit()
    {
        SteamClient.Shutdown();
    }

    void Update()
    {
        SteamClient.RunCallbacks();
    }

    void OnLobbyInvite(Friend friend, Lobby lobby)
    {
        Debug.Log($"{friend.Name} invited you to their lobby.");
    }

    private void OnLobbyGameCreated(Lobby lobby, uint ip, ushort port, SteamId id)
    {
        // Intentionally empty – extend as needed.
    }

    public static Texture2D GetTextureFromImage(Steamworks.Data.Image img)
    {
        Texture2D texture = new Texture2D((int)img.Width, (int)img.Height, TextureFormat.RGBA32, false);
        for (int y = 0; y < img.Height; y++)
        {
            for (int x = 0; x < img.Width; x++)
            {
                var p = img.GetPixel(x, y);
                texture.SetPixel(x, (int)img.Height - y, new UnityEngine.Color(
                    p.r / 255.0f, p.g / 255.0f, p.b / 255.0f, p.a / 255.0f));
            }
        }
        texture.Apply();
        return texture;
    }

    void OnLobbyMemberJoined(Lobby lobby, Friend friend)
    {
        Debug.Log($"{friend.Name} joined the lobby.");
    }

    void OnLobbyMemberDisconnected(Lobby lobby, Friend friend)
    {
        Debug.Log($"{friend.Name} left the lobby. New owner: {currentLobby.Owner}");
        if (inLobby.ContainsKey(friend.Id))
        {
            Destroy(inLobby[friend.Id]);
            inLobby.Remove(friend.Id);
        }
    }

    void OnChatMessage(Lobby lobby, Friend friend, string message)
    {
        Debug.Log($"Chat from {friend.Name}: {message}");
    }

    async void OnGameLobbyJoinRequest(Lobby joinedLobby, SteamId id)
    {
        Debug.Log($"Received join request from {id}. Joining...");
        RoomEnter joinResult = await joinedLobby.Join();
        if (joinResult != RoomEnter.Success)
        {
            Debug.LogError("Failed to join lobby: " + joinResult);
        }
        else
        {
            currentLobby = joinedLobby;
        }
    }

    // FIX #3: Mirror client startup moved here (authoritative Steam callback)
    // instead of inline in JoinLobby(), eliminating the race condition.
    void OnLobbyEntered(Lobby lobby)
    {
        Debug.Log("Entered Steam lobby.");
        currentLobby = lobby;
        OnLobbyJoined.Invoke();

        // Check if I am the owner. If I am, I should be hosting, not joining.
        if (lobby.Owner.Id == SteamClient.SteamId)
        {
            Debug.Log("I am the Lobby Owner. Mirror is already starting the host.");
            return;
        }

        // Only join as a client if I am NOT the owner
        string hostSteamId = lobby.Owner.Id.ToString();
        NetworkManager.singleton.networkAddress = hostSteamId;
        NetworkManager.singleton.StartClient();
        Debug.Log($"Mirror client connecting to host: {hostSteamId}");
    }


    public async void JoinLobby(SteamId lobbyId)
    {
        Debug.Log($"Joining Steam Lobby: {lobbyId}");

        var lobbyResult = await SteamMatchmaking.JoinLobbyAsync(lobbyId);

        if (lobbyResult.HasValue)
        {
            // FIX #3: Do NOT call StartClient here. OnLobbyEntered fires after this
            // and is the correct, authoritative place to start the Mirror client.
            Debug.Log("JoinLobbyAsync succeeded. Waiting for OnLobbyEntered callback.");
        }
        else
        {
            Debug.LogError("Failed to join Steam Lobby. It may be full, private, or gone.");
        }
    }

    // FIX #1 + #2: OnLobbyCreatedCallBack is ONLY called by the Steam event (registered in Start).
    // CreateLobbyAsync no longer calls it manually with a fake default lobby.
    // This also fixes the double-StartHost() bug.
    void OnLobbyCreatedCallBack(Result result, Lobby lobby)
    {
        Debug.Log("Lobby creation callback: " + result);
        if (result != Result.OK)
        {
            Debug.LogError("Lobby creation failed.");
            return;
        }

        NetworkManager.singleton.StartHost();
        Debug.Log("Host started successfully.");

        lobby.SetData("HostAddress", SteamClient.SteamId.ToString());
        Debug.Log($"HostAddress set to {SteamClient.SteamId}");

        OnLobbyCreated?.Invoke();
    }

    public async void HostLobby()
    {
        await CreateLobbyAsync();
    }

    // FIX #1 + #2: Removed the manual OnLobbyCreatedCallBack(lobby: default, ...) call.
    // Steam's own OnLobbyCreated event fires with the real lobby object and handles everything.
    public async Task<bool> CreateLobbyAsync()
    {
        Debug.Log("Creating lobby...");
        bool result = await CreateLobby();
        if (!result)
        {
            Debug.LogError("Failed to create lobby.");
            return false;
        }
        // Steam fires OnLobbyCreated → OnLobbyCreatedCallBack automatically with the real lobby.
        return true;
    }

    public static async Task<bool> CreateLobby()
    {
        try
        {
            var createLobbyOutput = await SteamMatchmaking.CreateLobbyAsync();
            if (!createLobbyOutput.HasValue)
            {
                Debug.LogError("Lobby created but not correctly instantiated.");
                return false;
            }

            Instance.currentLobby = createLobbyOutput.Value;
            Instance.currentLobby.SetPublic();
            Instance.currentLobby.SetJoinable(true);

            Debug.Log("Lobby created successfully.");
            return true;
        }
        catch (System.Exception exception)
        {
            Debug.LogError("Failed to create lobby: " + exception);
            return false;
        }
    }

    public void LeaveLobby()
    {
        try
        {
            currentLobby.Leave();
            OnLobbyLeave.Invoke();
            currentLobby = default;
            Debug.Log("Left lobby successfully.");
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to leave lobby: " + e);
        }
    }
}
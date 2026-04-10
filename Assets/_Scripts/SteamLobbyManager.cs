using Mirror;

using Steamworks;
using Steamworks.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

using static UnityEngine.LowLevelPhysics2D.PhysicsLayers;


public class SteamLobbyManager : MonoBehaviour
{
    public Lobby currentLobby;
    

    public UnityEvent OnLobbyCreated;

    public UnityEvent OnLobbyJoined;

    public static Action OnClientJoinedLobby;


    public UnityEvent OnLobbyLeave;

    public GameObject tank;
    public GameObject prefabToSpawn;



    public GameObject HomeScreenUI;
    public GameObject InLobbyUI;
    public GameObject StartButton;
    public GameObject LeaveButton;
    public TMP_InputField InputFieldHostId;


    public GameObject BackGroundImage;
    public Dictionary<SteamId, GameObject> inLobby = new Dictionary<SteamId, GameObject>();


    public static SteamLobbyManager Instance { get; private set; }
    private void Awake()
    {
        // 2. Initialize the Instance
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
        DontDestroyOnLoad(this);


        SteamMatchmaking.OnLobbyCreated += OnLobbyCreatedCallBack;
        SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
        SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
        SteamMatchmaking.OnChatMessage += OnChatMessage;
        SteamMatchmaking.OnLobbyMemberDisconnected += OnLobbyMemberDisconnected;
        SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberDisconnected;
        SteamMatchmaking.OnLobbyGameCreated += OnLobbyGameCreated;
        SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequest;
        SteamMatchmaking.OnLobbyInvite += OnLobbyInvite;
        //SteamMatchmaking.OnLobbyDataChanged += OnLobbyDataUpdated;

        

    }

    void Update()
    {
        SteamClient.RunCallbacks();
    }

    //public void StartGame()
    //{
      //  Debug.Log("Starting Game...");
       // Debug.Log(NetworkServer.active);
       // BackGroundImage.SetActive(false);
        //InLobbyUI.SetActive(false);

       // GameObject newPlayerPrefab = Instantiate(tank);
       // Debug.Log("------------Instantiated player prefab for host. ------------");
       // NetworkServer.Spawn(newPlayerPrefab);
       // NetworkServer.AddPlayerForConnection(NetworkServer.localConnection, newPlayerPrefab);
   // }

    void OnLobbyInvite(Friend friend, Lobby lobby)
    {
        Debug.Log($"{friend.Name} invited you to his lobby.");
    }

    
    private void OnLobbyGameCreated(Lobby lobby, uint ip, ushort port, SteamId id)
    {
        

        //GameObject newPlayerPrefab = Instantiate(tank);

        //NetworkServer.Spawn(newPlayerPrefab);
        //NetworkServer.AddPlayerForConnection(NetworkServer.localConnection, newPlayerPrefab);
        //FollowPrefab.target = newPlayerPrefab.transform;
    }

    private Texture2D ConvertSteamImage(Steamworks.Data.Image image)
    {
        var texture = new Texture2D((int)image.Width, (int)image.Height, TextureFormat.RGBA32, false);

        // Facepunch gives us raw RGBA bytes; Unity can load this directly
        texture.LoadRawTextureData(image.Data);
        texture.Apply();
        return texture;
    }

    public static Texture2D GetTextureFromImage(Steamworks.Data.Image img)
    {
        Texture2D texture = new Texture2D((int)img.Width, (int)img.Height, TextureFormat.RGBA32, false);
        for (int y = 0; y < img.Height; y++)
        {
            for (int x = 0; x < img.Width; x++)
            {
                var p = img.GetPixel(x, y);
                texture.SetPixel(x, (int)img.Height - y, new UnityEngine.Color(p.r / 255.0f, p.g / 255.0f, p.b / 255.0f, p.a / 255.0f));
            }
        }
        texture.Apply();
        return texture;
    }

    void OnLobbyMemberJoined(Lobby lobby, Friend friend)
    {

        Debug.Log("------------Someone joinged------------------");
        //currentLobby.SetData("Member", friend.Name);

        Debug.Log($"{friend.Name} joined the lobby");

        

    }

    void OnLobbyMemberDisconnected(Lobby lobby, Friend friend)
    {
        Debug.Log($"{friend.Name} left the lobby");
        Debug.Log($"New lobby owner is {currentLobby.Owner}");
        if (inLobby.ContainsKey(friend.Id))
        {
            Destroy(inLobby[friend.Id]);
            inLobby.Remove(friend.Id);
        }
    }

    void OnChatMessage(Lobby lobby, Friend friend, string message)
    {
        Debug.Log($"incoming chat message from {friend.Name} : {message}");
    }

    async void OnGameLobbyJoinRequest(Lobby joinedLobby, SteamId id)
    {
        
        Debug.Log($"Received game lobby join request from {id}. Attempting to join...");
        RoomEnter joinedLobbySuccess = await joinedLobby.Join();
        if (joinedLobbySuccess != RoomEnter.Success)
        {
            Debug.Log("failed to join lobby : " + joinedLobbySuccess);
        }
        else
        {
            currentLobby = joinedLobby;
        }
    }

    public async void OnLobbyJoinRequest()
    {
        Debug.Log("OnLobbyJoinRequest triggered");
        string hostIdString = InputFieldHostId.text;

        SteamId hostId = ulong.Parse(hostIdString);
        Debug.Log($"Attempting to join lobby with Host ID: {hostId}");
        var joinedLobby = await SteamMatchmaking.JoinLobbyAsync(hostId); //Joining SteamLobby
        if (!joinedLobby.HasValue)
        {
            Debug.LogError("Join failed! Lobby is null. This usually means the ID was wrong or the lobby no longer exists.");
            return;
        }
        Lobby lobbyToJoin = joinedLobby.Value;
        Debug.Log($"Successfully joined lobby: {lobbyToJoin.Id}");
        Debug.Log($"Member count is now: {lobbyToJoin.MemberCount}");
        //lobbyToJoin.SetData("HostAddress", hostIdString);
        //await lobbyToJoin.Join();
        NetworkManager.singleton.networkAddress = lobbyToJoin.Owner.Id.ToString();
        NetworkManager.singleton.StartClient();
        // OnLobbyDataUpdated(hostIdString);
    }

    public async void JoinLobby(SteamId lobbyId)
    {
        Debug.Log($"Attempting to join Steam Lobby: {lobbyId}");

        // 1. Join the Steam Lobby via API
        // This adds the player to the 'lobby.Members' list
        var lobbyResult = await SteamMatchmaking.JoinLobbyAsync(lobbyId);

        if (lobbyResult.HasValue)
        {
            Lobby lobby = lobbyResult.Value;
            currentLobby = lobby; // Store it in your manager

            Debug.Log("Joined Steam Lobby successfully.");

            // 2. Get the Host's SteamID from the Lobby
            // We use the Owner of the lobby as the 'Network Address'
            string hostSteamId = lobby.Owner.Id.ToString();

            // 3. Connect Mirror
            // Ensure FizzyFacepunch is the active transport on your NetworkManager
            NetworkManager.singleton.networkAddress = hostSteamId;
            NetworkManager.singleton.StartClient();

            Debug.Log($"Mirror Client connecting to Host: {hostSteamId}");
        }
        else
        {
            Debug.LogError("Failed to join Steam Lobby. It might be full, private, or no longer exists.");
        }
    }
    void OnLobbyDataUpdated(string hostIdString)
    {
        // 1. If we are the Host, we don't need to connect to ourselves
        Debug.Log("Lobby data updated callback received.");
        if (NetworkServer.active) return;
        string hostAddress = hostIdString;
        // 2. Try to get the Host's ID
        //string hostAddress = lobby.GetData("HostAddress");
        Debug.Log("Lobby data updated. Host Address: " + hostAddress);

        // 3. If it's not empty and we aren't already connecting...
        if (!string.IsNullOrEmpty(hostAddress) && !NetworkClient.active)
        {
            Debug.Log("Found Host Address! Connecting now...");

            NetworkManager.singleton.networkAddress = hostAddress;
            NetworkManager.singleton.StartClient();
            Debug.Log("Client started successfully, connecting to host at: " + hostAddress);
        }
    }


    void OnLobbyCreatedCallBack(Result result, Lobby lobby)
    {
        Debug.Log("Lobby creation callback received with result: " + result);
        if (result != Result.OK)
        {
            Debug.LogError("Lobby creation failed");
            return;
        }


        // Start the Network Manager 
       
        NetworkManager.singleton.StartHost();
        Debug.Log("Host started successfully.");
        //  Set the Steam Data so others can join
        lobby.SetData("HostAddress", SteamClient.SteamId.ToString());

      
        
    }
   
 
    async void OnLobbyEntered(Lobby lobby)
    {

        Debug.Log("Client joined the lobby");
        

        OnLobbyJoined.Invoke();  //needed to display the hosts Name and profile picture in the InLobbyList
    }

    public void HostLobby()
    {
        CreateLobbyAsync();  //Function for the button because a function called by a button has to be a void but createLobbyAsync has to return a bool for something but i dont remember what in what script
    }

    public async Task<bool> CreateLobbyAsync()
    {

        //HomeScreenUI.SetActive(false);
        //StartButton.SetActive(true);
        //LeaveButton.SetActive(true);
        Debug.Log("Creating lobby...");
        
        bool result = await CreateLobby();
        OnLobbyCreatedCallBack(lobby: default, result: Result.OK);
        if (!result)
        {
            Debug.Log("Failed to create lobby.");
            return false;
        }
       return true;
    }

    public static async Task<bool> CreateLobby()
    {

        try
        {
            var createLobbyOutput = await SteamMatchmaking.CreateLobbyAsync();
            if (!createLobbyOutput.HasValue)
            {
                Debug.Log("Lobby created but not correctly instantiated.");
                return false;
            }
            Instance.currentLobby = createLobbyOutput.Value;  //Instance being used because currentlobby cant be static becuase its needs to be passed into friendsmanager

            Instance.currentLobby.SetPublic();
            //currentLobby.SetPrivate();
            Instance.currentLobby.SetJoinable(true);


            Debug.Log("Lobby created successfully.");

            return true;
        }
        catch(System.Exception exception)
        {
            Debug.Log("Failed to create multiplayer lobby : " + exception);
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


            //StartButton.SetActive(false);
            //LeaveButton.SetActive(false);
            //HomeScreenUI.SetActive(true);
            

            Debug.Log("Left lobby succesfully");
        }
        catch
        {
            Debug.Log("failed to leave lobby");
        }
    
    }


    void OnDisable()
    {
        SteamClient.Shutdown();
        


    }

}



//To do:
//Refresh friends list every x seconds to check if they have a joinable lobby 
//When game is on, friends in lobby shouldnt refresh

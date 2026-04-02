using Mirror;

using Steamworks;
using Steamworks.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
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
    

    public UnityEvent OnLobbyLeave;

    public GameObject tank;
    public GameObject prefabToSpawn;



    public GameObject HomeScreenUI;
    public GameObject InLobbyUI;
    public GameObject StartButton;
    public GameObject LeaveButton;


    public GameObject BackGroundImage;
    public Dictionary<SteamId, GameObject> inLobby = new Dictionary<SteamId, GameObject>();


    public static SteamLobbyManager Instance { get; private set; }
    private void Awake()
    {
        // 2. Initialize the Instance
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); }
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
        SteamMatchmaking.OnLobbyDataChanged += OnLobbyDataUpdated;

        

    }



    public void StartGame()
    {
        Debug.Log("Starting Game...");
        Debug.Log(NetworkServer.active);
        BackGroundImage.SetActive(false);
        InLobbyUI.SetActive(false);

        GameObject newPlayerPrefab = Instantiate(tank);
        Debug.Log("------------Instantiated player prefab for host. ------------");
        NetworkServer.Spawn(newPlayerPrefab);
        NetworkServer.AddPlayerForConnection(NetworkServer.localConnection, newPlayerPrefab);
    }

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

    private async void OnLobbyMemberJoined(Lobby lobby, Friend friend)
    {

        Debug.Log("Someone joinged");

        


        //GameObject obj = Instantiate(InLobbyFriend, content);
        //obj.GetComponentInChildren<Text>().text = friend.Name;
        //var img = await SteamFriends.GetLargeAvatarAsync(friend.Id);
        //obj.GetComponentInChildren<RawImage>().texture = ConvertSteamImage(img.Value); ;//SteamFriendsManager.GetTextureFromSteamIdAsync(friend.Id);
        //inLobby.Add(friend.Id, obj);

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


    void OnLobbyDataUpdated(Lobby lobby)
    {
        // 1. If we are the Host, we don't need to connect to ourselves
        if (NetworkServer.active) return;

        // 2. Try to get the Host's ID
        string hostAddress = lobby.GetData("HostAddress");

        // 3. If it's not empty and we aren't already connecting...
        if (!string.IsNullOrEmpty(hostAddress) && !NetworkClient.active)
        {
            Debug.Log("Found Host Address! Connecting now...");

            NetworkManager.singleton.networkAddress = hostAddress;
            NetworkManager.singleton.StartClient();
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
        
        foreach (var user in inLobby.Values)
        {
            Destroy(user);
        }
        inLobby.Clear();

        
        OnLobbyJoined.Invoke();
    }


    public async Task<bool> CreateLobbyAsync()
    {
        
        
        HomeScreenUI.SetActive(false);
        StartButton.SetActive(true);
        LeaveButton.SetActive(true);
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

        //try
        //{
        //    SteamClient.Init(480);
        //    Debug.Log("Steam client initialized successfully.");
        //}
        //catch (System.Exception e)
        //{
        //    Debug.Log("Could not connect to Steam: " + e.Message);
        //}

        
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


            StartButton.SetActive(false);
            LeaveButton.SetActive(false);
            HomeScreenUI.SetActive(true);
            

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

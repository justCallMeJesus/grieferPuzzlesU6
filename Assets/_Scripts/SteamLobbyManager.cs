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


public class SteamLobbyManager : MonoBehaviour
{
    public static Lobby currentLobby;
    public static bool UserInLobby;
    public UnityEvent OnLobbyCreated;
    public UnityEvent OnLobbyJoined;
    public UnityEvent OnLobbyLeave;

    public GameObject tank;
    public GameObject prefabToSpawn;
    public CameraFollowScript FollowPrefab; // Reference your camera's follow script

    public GameObject UI2;

    public GameObject InLobbyFriend;
    public Transform content;

    public Dictionary<SteamId, GameObject> inLobby = new Dictionary<SteamId, GameObject>();

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



    void OnLobbyInvite(Friend friend, Lobby lobby)
    {
        Debug.Log($"{friend.Name} invited you to his lobby.");
    }


    private void OnLobbyGameCreated(Lobby lobby, uint ip, ushort port, SteamId id)
    {
        

        //GameObject newPlayerPrefab = Instantiate(tank);

        //NetworkServer.Spawn(newPlayerPrefab);
       // NetworkServer.AddPlayerForConnection(NetworkServer.localConnection, newPlayerPrefab);
        //FollowPrefab.target = newPlayerPrefab.transform;
    }

    private async void OnLobbyMemberJoined(Lobby lobby, Friend friend)
    {
        


        Debug.Log($"{friend.Name} joined the lobby");
        GameObject obj = Instantiate(InLobbyFriend, content);
        obj.GetComponentInChildren<Text>().text = friend.Name;
        obj.GetComponentInChildren<RawImage>().texture = await SteamFriendsManager.GetTextureFromSteamIdAsync(friend.Id);
        inLobby.Add(friend.Id, obj);

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
        if (result != Result.OK)
        {
            Debug.LogError("Lobby creation failed");
            return;
        }

       
        


        // A. Start the Network Manager IMMEDIATELY
        // This makes 'Server Active' = True and spawns the host
        NetworkManager.singleton.StartHost();

        // B. Set the Steam Data so others can join
        lobby.SetData("HostAddress", SteamClient.SteamId.ToString());

      
        
    }
   
 
    async void OnLobbyEntered(Lobby lobby)
    {
       

        //string hostAddress = lobby.GetData("HostAddress");

       

   

        //Debug.Log("Host Address : " + hostAddress);
        //if (!string.IsNullOrEmpty(hostAddress))
        //{
            // If we found the address and aren't already connected, start the client
           // if (!NetworkClient.isConnected && !NetworkServer.active)
           // {
             //   NetworkManager.singleton.networkAddress = hostAddress;
             //   NetworkManager.singleton.StartClient();
              //  Debug.Log("Connecting to Host: " + hostAddress);
           // }
        //}

        UI2.SetActive(false);

        Debug.Log("Client joined the lobby");
        UserInLobby = true;
        foreach (var user in inLobby.Values)
        {
            Destroy(user);
        }
        inLobby.Clear();

        GameObject obj = Instantiate(InLobbyFriend, content);
        obj.GetComponentInChildren<Text>().text = SteamClient.Name;
        obj.GetComponentInChildren<RawImage>().texture = await SteamFriendsManager.GetTextureFromSteamIdAsync(SteamClient.SteamId);

        inLobby.Add(SteamClient.SteamId, obj);

        foreach (var friend in currentLobby.Members)
        {
            if (friend.Id != SteamClient.SteamId)
            {
                GameObject obj2 = Instantiate(InLobbyFriend, content);
                obj2.GetComponentInChildren<Text>().text = friend.Name;
                obj2.GetComponentInChildren<RawImage>().texture = await SteamFriendsManager.GetTextureFromSteamIdAsync(friend.Id);

                inLobby.Add(friend.Id, obj2);

                

            }
        }
        OnLobbyJoined.Invoke();
    }


    public async void CreateLobbyAsync()
    {
        bool result = await CreateLobby();
        if (!result)
        {
            //Invoke a error message.
        }
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
            currentLobby = createLobbyOutput.Value;

            currentLobby.SetPublic();
            //currentLobby.SetPrivate();
            currentLobby.SetJoinable(true);

          

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
            UserInLobby = false;
            currentLobby.Leave();
            OnLobbyLeave.Invoke();
            foreach (var user in inLobby.Values)
            {
                Destroy(user);
            }
            inLobby.Clear();
        }
        catch
        {

        }
    }
    

}

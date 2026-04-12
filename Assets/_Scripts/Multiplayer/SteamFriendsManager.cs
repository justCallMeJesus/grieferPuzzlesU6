using Mirror;
using Steamworks;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class SteamFriendsManager : MonoBehaviour
{
    public RawImage clientProfilePic;
    public TextMeshProUGUI clientName;
    public GameObject friendRowPrefab;
    public Transform contentParent;         //scroll view for friends list
    public SteamLobbyManager lobbyManager;
    public Transform lobbyContentParent;   //scroll view for lobby members
    public GameObject SteamNotOpen;
    private Dictionary<SteamId, GameObject> friendRows = new Dictionary<SteamId, GameObject>();

    void Update()
    {
        SteamClient.RunCallbacks();
    }
    private void OnEnable()
    {
        // Listen for the event you already created
        lobbyManager.OnLobbyJoined.AddListener(UpdateLobbyUI);
    }

    private void OnDisable()
    {
        lobbyManager.OnLobbyJoined.RemoveListener(UpdateLobbyUI);
    }

    async void Start()
    {
        await InitializeSteam();
        SteamNotOpen.SetActive(false);
    }
    private async Task InitializeSteam()
    {
        bool connected = false;

        while (!connected)
        {
            Debug.Log("Checking if Steam client is valid...");

            if (SteamClient.IsValid)
            {
                connected = true;
                Debug.Log("Steam is already valid and running!");
            }
            else
            {
                try
                {
                    SteamClient.Init(480); // AppID
                    connected = true;
                    Debug.Log("Successfully connected to Steam!");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Could not connect to Steam: {e.Message}. Retrying in 5 seconds...");
                    SteamNotOpen.SetActive(true);
                    // Wait 5 seconds before trying again so we don't freeze the CPU
                    await Task.Delay(5000);
                }
            }
        }

        Debug.Log(SteamClient.Name);
        clientName.text = SteamClient.Name;
        Debug.Log(SteamClient.Name);
        var img = await SteamFriends.GetLargeAvatarAsync(SteamClient.SteamId);
        Debug.Log(img);
        clientProfilePic.texture = GetTextureFromImage(img.Value);
        Debug.Log("Steam client initialized successfully.");
        InitFriends();

        SteamMatchmaking.OnLobbyMemberJoined += (lobby, friend) => {
            Debug.Log($"{friend.Name} joined the lobby! Refreshing UI...");
            UpdateLobbyUI(); // Call your function that clears and redraws the UI
        };

        // Also handle when people leave so the list stays accurate
        SteamMatchmaking.OnLobbyMemberLeave += (lobby, friend) => {
            UpdateLobbyUI();
        };

        SteamMatchmaking.OnLobbyMemberDisconnected += (lobby, friend) => {
            UpdateLobbyUI();
        };
    }
    public static Texture2D GetTextureFromImage(Steamworks.Data.Image img)
    {
        Texture2D texture = new Texture2D((int)img.Width, (int)img.Height, TextureFormat.RGBA32, false);
        for (int y = 0; y < img.Height; y++)
        {
            for (int x = 0; x < img.Width; x++)
            {
                var p = img.GetPixel(x, y);
                texture.SetPixel(x, (int)img.Height - y, new Color(p.r / 255.0f, p.g / 255.0f, p.b / 255.0f, p.a / 255.0f));
            }
        }
        texture.Apply();
        return texture;
    }
    public void InitFriends()
    {
        // Clear existing list first if necessary
        foreach (Transform child in contentParent) Destroy(child.gameObject);

        foreach (var friend in SteamFriends.GetFriends())
        {
            // 1. Instantiate the Prefab
            GameObject row = Instantiate(friendRowPrefab, contentParent);
            friendRows.Add(friend.Id, row);
            // 2. Get references to components
            var nameText = row.transform.Find("FriendName").GetComponent<TextMeshProUGUI>();
            var idText = row.transform.Find("FriendSteamID").GetComponent<TextMeshProUGUI>();
            var profilePic = row.transform.Find("FriendProfilePic").GetComponent<RawImage>();
            var inviteBtn = row.transform.Find("InviteButton").GetComponent<Button>();

            var joinBtn = row.transform.Find("JoinButton").GetComponent<Button>();

            // 3. Set basic data
            nameText.text = friend.Name;
            idText.text = friend.Id.ToString();

            // 4. Handle Button Click (Passing the ID)
            string friendId = friend.Id.ToString();
            inviteBtn.onClick.AddListener(() => OnInviteClick(friendId));

            if (friend.IsPlayingThisGame && friend.GameInfo?.Lobby != null)
            {
                joinBtn.gameObject.SetActive(true);

                // Get the Lobby ID for the join function
                var lobbyId = friend.GameInfo.Value.Lobby.Value.Id;
                joinBtn.onClick.AddListener(() => lobbyManager.JoinLobby(lobbyId));
            }
            else
            {
                joinBtn.gameObject.SetActive(false);
            }
            UpdateSingleFriendUI(friend, row);
            // 5. Load Avatar Asynchronously (So the UI doesn't freeze)
            LoadFriendAvatar(friend.Id, profilePic);

            Debug.Log($"Added friend {friend.Name} to the Canvas list.");
        }
        StopAllCoroutines();
        StartCoroutine(RefreshLobbyStatusLoop());
    }

    private IEnumerator RefreshLobbyStatusLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(7f);

            foreach (var friend in SteamFriends.GetFriends())
            {
                // 1. Check if they have a lobby via GameInfo
                if (friend.GameInfo?.Lobby != null)
                {
                    // 2. This is the Facepunch equivalent to RequestLobbyData
                    // It refreshes the metadata for that specific lobby
                    friend.GameInfo.Value.Lobby.Value.Refresh();
                }

                // 3. Update the UI row visibility
                if (friendRows.TryGetValue(friend.Id, out GameObject row))
                {
                    UpdateSingleFriendUI(friend, row);
                }
            }
        }
    }

    private void UpdateSingleFriendUI(Friend friend, GameObject row)
    {
        var joinBtn = row.transform.Find("JoinButton").GetComponent<Button>();

        // Check Steam for lobby info
        if (friend.IsPlayingThisGame && friend.GameInfo?.Lobby != null)
        {
            joinBtn.gameObject.SetActive(true);
            joinBtn.onClick.RemoveAllListeners();
            joinBtn.onClick.AddListener(() => lobbyManager.JoinLobby(friend.GameInfo.Value.Lobby.Value.Id));
        }
        else
        {
            joinBtn.gameObject.SetActive(false);
        }
    }

    private async void LoadFriendAvatar(SteamId id, RawImage targetImage)
    {
        var imgTask = await SteamFriends.GetLargeAvatarAsync(id);
        if (imgTask.HasValue && targetImage != null)
        {
            targetImage.texture = GetTextureFromImage(imgTask.Value);
        }
    }


    private async void OnInviteClick(string friendId)
    {
        bool currentlobby = true;
        if (lobbyManager.currentLobby.Id == 0)
        {
            currentlobby = await lobbyManager.CreateLobbyAsync();
            
        }

        if (!string.IsNullOrEmpty(friendId) && currentlobby)
        {
            Debug.Log($"Invite sent to SteamID: {friendId}");

            // 3. Call your Steam/FizzyFacepunch logic
            ulong id = ulong.Parse(friendId);
            lobbyManager.currentLobby.InviteFriend(id);
        }
        else
        {
            Debug.LogError("Invalid friend ID: " + friendId + "or Lobby wasn't created");
        }
        // 1. Get the button that triggered the click



    }

    public void UpdateLobbyUI()
    {
        // 1. Clear existing list
        Debug.Log("Updating Lobby UI...");
        foreach (Transform child in lobbyContentParent) Destroy(child.gameObject);
        Debug.Log("Cleared existing lobby member list.");
   
        // 2. Loop through current lobby members (using Facepunch syntax)
        foreach (var member in lobbyManager.currentLobby.Members)
        {
            GameObject row = Instantiate(friendRowPrefab, lobbyContentParent);
            Debug.Log($"Added lobby member {member.Name} to the Canvas list.");
            // 3. Set the data (Same paths as before)
            row.transform.Find("FriendName").GetComponent<TextMeshProUGUI>().text = member.Name;
            var profilePic = row.transform.Find("FriendProfilePic").GetComponent<RawImage>();
            LoadFriendAvatar(member.Id, profilePic);
            Debug.Log(member.Id);

            // 4. Transform the "Invite" button into a "Kick" button
            var btn = row.transform.Find("InviteButton").GetComponent<Button>();
            var btnText = btn.GetComponentInChildren<TextMeshProUGUI>();

            btnText.text = "KICK";
            btn.onClick.RemoveAllListeners();

            // 5. Logic: Only Host can see Kick button, and not on themselves
            bool isHost = lobbyManager.currentLobby.IsOwnedBy(SteamClient.SteamId);
            bool isMe = member.Id == SteamClient.SteamId;

            if (isHost && !isMe)
            {
                btn.onClick.AddListener(() => KickPlayer(member.Id));
            }
            else
            {
                btn.gameObject.SetActive(false);
            }
        }
    }

    void KickPlayer(SteamId id)
    {
        Debug.Log($"Kicking player: {id}");
        // Add your Mirror/Fizzy kick logic here
    }

    public void OnLobbyLeave()
    {
        foreach (Transform child in lobbyContentParent)
        {
            // Destroy the GameObject associated with the child transform
            Destroy(child.gameObject);
        }
        Debug.Log("Removed all Player Prefabs from lobby list");
    }

}

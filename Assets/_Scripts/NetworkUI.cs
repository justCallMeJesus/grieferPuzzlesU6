
using UnityEngine;
using UnityEngine.UIElements;
using Mirror;
using Steamworks;
using Steamworks.Data;


public class NetworkUI : MonoBehaviour
{
    private UIDocument document;
    private Button clientButton;
    private Button hostButton;
    private Button leaveButton;
    public SteamLobbyManager lobbyManager;
    public SteamId steamid;


    private void Awake()
    {
        document = GetComponent<UIDocument>();

        clientButton = document.rootVisualElement.Q("ClientButton") as Button;
        clientButton.RegisterCallback<ClickEvent>(OnPlayGameClick);

        hostButton = document.rootVisualElement.Q("HostButton") as Button;
        hostButton.RegisterCallback<ClickEvent>(OnHostClick);

        leaveButton = document.rootVisualElement.Q("LeaveButton") as Button;
        //leaveButton.RegisterCallback<ClickEvent>(OnLeaveClick);

    }

    private void OnDisable()
    {
        //clientButton.UnregisterCallback<ClickEvent>(OnPlayGameClick);
        //hostButton.UnregisterCallback<ClickEvent>(OnHostClick);
    }

    private void OnPlayGameClick(ClickEvent evt)
    {
        Debug.Log("Client pressed");
       
    }

    private void OnHostClick(ClickEvent evt)
    {
        Debug.Log("Host pressed2");
        
    }

    private void OnLeaveClick(ClickEvent evt)
    {
        Debug.Log("Leave pressed");
        lobbyManager.LeaveLobby();
    }

}

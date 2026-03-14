using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class NetworkUI : MonoBehaviour
{
    private UIDocument document;
    private Button clientButton;
    private Button hostButton;


    private void Awake()
    {
        document = GetComponent<UIDocument>();

        clientButton = document.rootVisualElement.Q("ClientButton") as Button;
        clientButton.RegisterCallback<ClickEvent>(OnPlayGameClick);

        hostButton = document.rootVisualElement.Q("HostButton") as Button;
        hostButton.RegisterCallback<ClickEvent>(OnHostClick);
    }

    private void OnDisable()
    {
        clientButton.UnregisterCallback<ClickEvent>(OnPlayGameClick);
        hostButton.UnregisterCallback<ClickEvent>(OnHostClick);
    }

    private void OnPlayGameClick(ClickEvent evt)
    {
        Debug.Log("Client pressed");
        Client();
    }

    private void OnHostClick(ClickEvent evt)
    {
        Debug.Log("Host pressed");
        Host();
    }

    public void Host()
    {
        NetworkManager.Singleton.StartHost();
    }

    public void Client()
    {
        NetworkManager.Singleton.StartClient();
    }
}

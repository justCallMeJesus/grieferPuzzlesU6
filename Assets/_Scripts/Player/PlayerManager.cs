using Mirror;
using UnityEngine;

public class PlayerManager : NetworkBehaviour
{
    [HideInInspector] public PlayerInventory inventory;
    [HideInInspector] public PlayerInteraction interaction;
    [HideInInspector] public PlayerMovement movement;

    [SerializeField] private PlayerInventoryUI playerInventoryUIPrefab;

    [HideInInspector] public PlayerInventoryUI playerInventoryUI;


// 1. General setup for everyone (Server and all Clients)
public override void OnStartClient()
{
    base.OnStartClient();
    inventory = GetComponent<PlayerInventory>();
    interaction = GetComponent<PlayerInteraction>();
    movement = GetComponent<PlayerMovement>();
}

// 2. UI setup ONLY for the local player
public override void OnStartLocalPlayer()
{
    base.OnStartLocalPlayer();

    if (playerInventoryUIPrefab == null)
    {
        Debug.LogError("[PlayerManager] playerInventoryUIPrefab is NULL!");
        return;
    }

    // Mirror uses 'netId' or 'connectionToClient.connectionId' instead of 'OwnerClientId'
    Canvas canvas = FindAnyObjectByType<Canvas>();

    if (canvas == null)
    {
        GameObject canvasGO = new GameObject($"PlayerCanvas_{netId}");
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
    }

    playerInventoryUI = Instantiate(playerInventoryUIPrefab, canvas.transform);
    playerInventoryUI.playerManager = this;
    playerInventoryUI.Init(inventory);
}


public void FreezePlayer() => movement.DisableMovement();
    public void UnfreezePlayer() => movement.EnableMovement();
}
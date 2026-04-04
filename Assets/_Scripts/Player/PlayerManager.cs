using Unity.Netcode;
using UnityEngine;

public class PlayerManager : NetworkBehaviour
{
    [HideInInspector] public PlayerInventory inventory;
    [HideInInspector] public PlayerInteraction interaction;
    [HideInInspector] public PlayerMovement movement;

    [SerializeField] private PlayerInventoryUI playerInventoryUIPrefab;

    [HideInInspector] public PlayerInventoryUI playerInventoryUI;

    public override void OnNetworkSpawn()
    {
        inventory = GetComponent<PlayerInventory>();
        interaction = GetComponent<PlayerInteraction>();
        movement = GetComponent<PlayerMovement>();

        if (!IsOwner) return;

        if (playerInventoryUIPrefab == null)
        {
            Debug.LogError("[PlayerManager] playerInventoryUIPrefab is NULL!");
            return;
        }

        // Find the existing scene Canvas instead of creating a new one.
        // This ensures the UI uses the correct EventSystem and CanvasScaler
        // that's already set up in your scene.
        Canvas canvas = FindAnyObjectByType<Canvas>();

        if (canvas == null)
        {
            // Fallback: create one if somehow none exists
            GameObject canvasGO = new GameObject($"PlayerCanvas_{OwnerClientId}");
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
using UnityEngine;

public class Panel : MonoBehaviour, IInteractable
{
    public GameObject GameObject => this.gameObject;
    private string savedState = "";

    [SerializeField] private GameObject UIPanel;

    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private InventoryTetris inventoryTetris;
    public void OnInteract(PlayerManager player)
    {
        inventoryTetris.ClearAll();

        if (!string.IsNullOrEmpty(savedState))
            inventoryTetris.Load(savedState);

        inventoryPanel.SetActive(true);

        Debug.Log("interacted with panel");
        //UIPanel.SetActive(true);


        player.interaction.currentlyInteractingObject = this;
        player.FreezePlayer();
    }

    public bool CanInteract()
    {
        return true;
    }

    public void OnStopInteraction(PlayerManager player)
    {

        savedState = inventoryTetris.Save();

        inventoryTetris.ClearAll();
        inventoryPanel.SetActive(false);
        //UIPanel.SetActive(false);
        player.UnfreezePlayer();
    }

    

    public void Open(InventoryTetris inventoryUI, GameObject inventoryPanel)
    {
        inventoryUI.ClearAll();

        if (!string.IsNullOrEmpty(savedState))
            inventoryUI.Load(savedState);

        inventoryPanel.SetActive(true);
    }

    public void Close(InventoryTetris inventoryUI, GameObject inventoryPanel)
    {
        savedState = inventoryUI.Save();
        inventoryPanel.SetActive(false);
        inventoryUI.ClearAll();
    }


}

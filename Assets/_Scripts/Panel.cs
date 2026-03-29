using UnityEngine;

public class Panel : MonoBehaviour, IInteractable
{
    public GameObject GameObject => this.gameObject;

    [SerializeField] private GameObject UIPanel;

    public void OnInteract(PlayerManager player)
    {
        Debug.Log("interacted with panel");
        UIPanel.SetActive(true);

        player.interaction.currentlyInteractingObject = this;
        player.FreezePlayer();
    }

    public bool CanInteract()
    {
        return true;
    }

    public void OnStopInteraction(PlayerManager player)
    {
        UIPanel.SetActive(false);
        player.UnfreezePlayer();
    }
}

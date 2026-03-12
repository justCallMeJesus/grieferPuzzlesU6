using UnityEngine;

public class Item : MonoBehaviour, IInteractable
{
    public ItemData ItemData;

    public GameObject GameObject => this.gameObject;

    public void OnInteract(PlayerInventory player)
    {
        // try pickup
        player.smallItemInventory[0] = ItemData;
        Destroy(gameObject);

    }

}

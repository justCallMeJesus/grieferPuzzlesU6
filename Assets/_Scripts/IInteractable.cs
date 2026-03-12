using UnityEngine;

public interface IInteractable
{
    GameObject GameObject { get; }
    public void OnInteract(PlayerInventory player);
}

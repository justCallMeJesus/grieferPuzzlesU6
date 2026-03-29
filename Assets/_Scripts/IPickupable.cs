using UnityEngine;

public interface IPickupable
{
    GameObject GameObject { get; }
    public void OnPickup(PlayerInventory player);
}

public interface IInteractable
{
    GameObject GameObject { get; }
    public void OnInteract(PlayerManager player);

    public void OnStopInteraction(PlayerManager player);

    public bool CanInteract();
}

using UnityEngine;

public interface IPickupable
{
    GameObject GameObject { get; }
    public void OnPickup(PlayerInventory player);
}

public interface IInteractable
{
    GameObject GameObject { get; }
    public void OnInteract(PlayerInventory player);
}

using Unity.Netcode;
using UnityEngine;

public class PlayerManager : NetworkBehaviour
{
    [HideInInspector]
    public PlayerInventory inventory;
    [HideInInspector]   
    public PlayerInteraction interaction;
    [HideInInspector]
    public PlayerMovement movement;

    private void Start()
    {
        inventory = GetComponent<PlayerInventory>();
        interaction = GetComponent<PlayerInteraction>();
        movement = GetComponent<PlayerMovement>();
    }

    public void FreezePlayer()
    {
        movement.DisableMovement();
    }

    public void UnfreezePlayer()
    {
        movement.EnableMovement();
    }
}

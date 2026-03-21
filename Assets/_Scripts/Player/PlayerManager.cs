using Unity.Netcode;
using UnityEngine;

public class PlayerManager : NetworkBehaviour
{
    [HideInInspector]
    public PlayerInventory inventory;
    [HideInInspector]   
    public PlayerInteraction interaction;

    private void Start()
    {
        inventory = GetComponent<PlayerInventory>();
        interaction = GetComponent<PlayerInteraction>();
    }

    
}

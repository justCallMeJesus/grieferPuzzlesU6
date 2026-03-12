using UnityEngine;

public class PlayerManager : MonoBehaviour
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

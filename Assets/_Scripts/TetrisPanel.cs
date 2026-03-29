using UnityEngine;

public class TetrisPanel : MonoBehaviour, IInteractable
{
    public GameObject GameObject => this.gameObject;

    public void OnInteract(PlayerInventory player)
    {
        Debug.Log("interacted with panel");
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

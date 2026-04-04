using NUnit;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class Testing : MonoBehaviour
{
    [SerializeField] private ItemTetrisSO testSO;

    private void Start()
    {
        //InventoryTetris inv = GetComponent<InventoryTetris>();

        //// Place a pistol at grid position (0, 0), no rotation
        //PlacedItem result = inv.TryPlaceItem(testSO, new Vector2Int(0, 0), ItemTetrisSO.Dir.Down);
        //PlacedItem result2 = inv.TryPlaceItem(testSO, new Vector2Int(3, 0), ItemTetrisSO.Dir.Down);

        //if (result == null)
        //    Debug.Log("No space!");
    }

    private void Update()
    {
        if (Keyboard.current.lKey.wasPressedThisFrame)
        {
            InventoryTetris inv = GetComponent<InventoryTetris>();

            PlacedItem result = inv.TryPlaceItem(testSO, new Vector2Int(0, 0), ItemTetrisSO.Dir.Down);
            if (result == null)
                Debug.Log("No space!");
        }

        if (Keyboard.current.oKey.wasPressedThisFrame)
        {
            InventoryTetris inv = GetComponent<InventoryTetris>();

            PlacedItem result = inv.SpawnItemAtMouse(testSO);
            if (result == null)
                Debug.Log("No space!");
        }
    }
}

using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class ItemSpawnManager : NetworkBehaviour
{
    [Header("Item Settings")]
    public List<GameObject> itemPrefabs = new List<GameObject>();



    [Header("Spawn Settings")]
    public bool spawnOnStart = true;


    // We only want the Server to run the spawning logic
    public void OnGameStart()
    {
        if (spawnOnStart)
        {
            SpawnAllItems();
            Debug.Log("Spawned all items on server start.");
        }
    }

    [Server]
    public void SpawnAllItems()
    {
        ItemSpawnPoint[] spawnPoints = FindObjectsByType<ItemSpawnPoint>(FindObjectsSortMode.None);


        foreach (ItemSpawnPoint point in spawnPoints)
        {
            if (point.allowedItemIndices.Count == 0) continue;
            Debug.Log($"Spawning item at {point.transform.position} with {point.allowedItemIndices.Count} options.");
            // 1. Pick a random index from the point's allowed list
            int randomIndex = point.allowedItemIndices[Random.Range(0, point.allowedItemIndices.Count)];

            // 2. Safety check: does this index exist in our main prefab list?
            if (randomIndex >= 0 && randomIndex < itemPrefabs.Count)
            {
                SpawnItem(itemPrefabs[randomIndex], point.transform.position, point.transform.rotation);
                Debug.Log($"Spawned {itemPrefabs[randomIndex].name} at {point.transform.position}");
            }
        }
    }

    [Server]
    private void SpawnItem(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        // 1. Instantiate on the Server
        GameObject item = Instantiate(prefab, position, rotation);

        // 2. Spawn on the Network
        // This tells Mirror/Fizzy to tell all clients to spawn this object
        NetworkServer.Spawn(item);
    }
}

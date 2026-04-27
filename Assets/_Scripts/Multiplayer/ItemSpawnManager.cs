using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class ItemSpawnManager : NetworkBehaviour
{
    [Header("Item Settings")]
    public List<GameObject> itemPrefabs = new List<GameObject>();

    [Header("Spawn Settings")]
    public bool spawnOnStart = true;

    public void OnGameStart()
    {
        if (!NetworkServer.active)
        {
            Debug.LogWarning("OnGameStart called but server is not active yet.");
            return;
        }

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

            int randomIndex = point.allowedItemIndices[Random.Range(0, point.allowedItemIndices.Count)];

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
        GameObject item = Instantiate(prefab, position, rotation);
        NetworkServer.Spawn(item);
    }
}
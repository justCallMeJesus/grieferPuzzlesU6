using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Server-authoritative random spawner for Unity Netcode for GameObjects (NGO).
///
/// KEY CONCEPTS:
///  - Only the SERVER runs the spawn loop and calls NetworkObject.Spawn().
///  - NGO automatically replicates spawned NetworkObjects to all connected clients,
///    including clients who join late (they receive all currently-spawned objects).
///  - Prefabs MUST be registered in the NetworkManager's "Network Prefabs" list.
///  - Despawning is also server-authoritative via DespawnAll().
///
/// SETUP:
///  1. Attach this script to a GameObject that also has a NetworkObject component.
///  2. In the Inspector, fill "Prefabs To Spawn" with your prefab references.
///     ⚠ Each prefab MUST have a NetworkObject component AND be added to
///       NetworkManager → NetworkConfig → Network Prefabs.
///  3. Fill "Spawn Points" with scene Transforms (empty GameObjects work great).
///  4. Set "Spawn Cooldown", limits, and options as desired.
///  5. Place the GameObject in the scene before the host/server starts.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class RandomSpawner : NetworkBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────────

    [Header("Spawn Configuration")]
    [Tooltip("Prefabs to randomly pick from. Each MUST be registered in NetworkManager's Network Prefabs list and have a NetworkObject component.")]
    public List<NetworkObject> prefabsToSpawn = new List<NetworkObject>();

    [Tooltip("Scene Transforms used as possible spawn locations.")]
    public List<Transform> spawnPoints = new List<Transform>();

    [Header("Timing")]
    [Tooltip("Seconds between each automatic spawn.")]
    [Min(0.01f)]
    public float spawnCooldown = 2f;

    [Tooltip("Delay before the very first spawn after the server starts.")]
    [Min(0f)]
    public float initialDelay = 0f;

    [Header("Limits")]
    [Tooltip("Maximum simultaneously live spawned objects. 0 = unlimited.")]
    [Min(0)]
    public int maxSpawnedObjects = 0;

    [Header("Options")]
    [Tooltip("Prevents the same spawn point being chosen twice in a row.")]
    public bool avoidRepeatSpawnPoint = true;

    [Tooltip("Begin spawning automatically when the server/host starts.")]
    public bool autoStart = true;

    // ── Runtime state (server-only) ────────────────────────────────────────────

    private readonly List<NetworkObject> _spawnedObjects = new();
    private Coroutine _spawnCoroutine;
    private int _lastSpawnPointIndex = -1;

    // ── NGO lifecycle ──────────────────────────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        // Only the server/host runs the spawn loop.
        if (!IsServer) return;

        if (autoStart)
            StartSpawning();
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;
        StopSpawning();
    }

    // ── Public API (call from server-side code only) ───────────────────────────

    /// <summary>
    /// Starts the automatic spawn loop. Server only.
    /// </summary>
    public void StartSpawning()
    {
        if (!IsServer)
        {
            Debug.LogWarning("[NetworkedRandomSpawner] StartSpawning() must be called on the server.", this);
            return;
        }

        if (_spawnCoroutine != null)
            StopCoroutine(_spawnCoroutine);

        _spawnCoroutine = StartCoroutine(SpawnLoop());
    }

    /// <summary>
    /// Stops the automatic spawn loop. Already-spawned objects remain. Server only.
    /// </summary>
    public void StopSpawning()
    {
        if (_spawnCoroutine == null) return;
        StopCoroutine(_spawnCoroutine);
        _spawnCoroutine = null;
    }

    /// <summary>
    /// Despawns and destroys all objects spawned by this spawner. Server only.
    /// </summary>
    public void DespawnAll()
    {
        if (!IsServer) return;

        foreach (NetworkObject obj in _spawnedObjects)
        {
            if (obj != null && obj.IsSpawned)
                obj.Despawn(destroy: true);
        }
        _spawnedObjects.Clear();
    }

    /// <summary>
    /// Manually triggers a single spawn outside the loop. Server only.
    /// Returns the spawned NetworkObject, or null if spawn failed.
    /// </summary>
    public NetworkObject SpawnOnce()
    {
        if (!IsServer)
        {
            Debug.LogWarning("[NetworkedRandomSpawner] SpawnOnce() must be called on the server.", this);
            return null;
        }

        return TrySpawn();
    }

    // ── Core logic (server only) ───────────────────────────────────────────────

    private IEnumerator SpawnLoop()
    {
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            TrySpawn();
            yield return new WaitForSeconds(spawnCooldown);
        }
    }

    private NetworkObject TrySpawn()
    {
        // ── Validate prefab list ──
        if (prefabsToSpawn == null || prefabsToSpawn.Count == 0)
        {
            Debug.LogWarning("[NetworkedRandomSpawner] No prefabs assigned.", this);
            return null;
        }

        // ── Validate spawn point list ──
        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogWarning("[NetworkedRandomSpawner] No spawn points assigned.", this);
            return null;
        }

        // ── Respect max objects cap ──
        if (maxSpawnedObjects > 0)
        {
            _spawnedObjects.RemoveAll(o => o == null || !o.IsSpawned);

            if (_spawnedObjects.Count >= maxSpawnedObjects)
            {
                Debug.Log($"[NetworkedRandomSpawner] Cap of {maxSpawnedObjects} reached – skipping spawn.");
                return null;
            }
        }

        // ── Pick a random prefab ──
        int prefabIndex = Random.Range(0, prefabsToSpawn.Count);
        NetworkObject chosenPrefab = prefabsToSpawn[prefabIndex];

        if (chosenPrefab == null)
        {
            Debug.LogWarning($"[NetworkedRandomSpawner] Prefab at index {prefabIndex} is null.", this);
            return null;
        }

        // ── Pick a random spawn point ──
        int spawnIndex = PickSpawnIndex();
        Transform spawnPoint = spawnPoints[spawnIndex];

        if (spawnPoint == null)
        {
            Debug.LogWarning($"[NetworkedRandomSpawner] Spawn point at index {spawnIndex} is null.", this);
            return null;
        }

        // ── Instantiate locally, then hand off to NGO ──
        // Instantiate → NetworkObject.Spawn() is the correct NGO pattern.
        // NGO will replicate the object (position, rotation, NetworkVariables) to all clients.
        NetworkObject instance = Instantiate(
            chosenPrefab,
            spawnPoint.position,
            spawnPoint.rotation
        );

        // Spawn on the network. destroyWithScene: true means NGO cleans it up
        // when the scene unloads, which is the safe default.
        instance.Spawn(destroyWithScene: true);

        _spawnedObjects.Add(instance);
        _lastSpawnPointIndex = spawnIndex;

        Debug.Log($"[NetworkedRandomSpawner] Server spawned '{chosenPrefab.name}' " +
                  $"at '{spawnPoint.name}' (NetworkObjectId: {instance.NetworkObjectId}).");

        return instance;
    }

    private int PickSpawnIndex()
    {
        if (!avoidRepeatSpawnPoint || spawnPoints.Count == 1)
            return Random.Range(0, spawnPoints.Count);

        int index;
        int attempts = 0;
        const int maxAttempts = 20;

        do
        {
            index = Random.Range(0, spawnPoints.Count);
            attempts++;
        }
        while (index == _lastSpawnPointIndex && attempts < maxAttempts);

        return index;
    }

    // ── Scene-view gizmos ──────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (spawnPoints == null) return;

        Gizmos.color = new Color(0f, 1f, 0.4f, 0.85f);
        foreach (Transform sp in spawnPoints)
        {
            if (sp == null) continue;
            Gizmos.DrawWireSphere(sp.position, 0.4f);
            Gizmos.DrawLine(transform.position, sp.position);
        }
    }
}
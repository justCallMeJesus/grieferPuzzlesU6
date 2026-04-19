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
///  - The live count is determined each tick by scanning the scene for active
///    instances of any prefab in the list — this includes pre-placed scene objects
///    and correctly reacts to objects being destroyed or disabled at runtime.
///
/// SETUP:
///  1. Attach this script to a GameObject that also has a NetworkObject component.
///  2. In the Inspector, fill "Prefabs To Spawn" with your prefab references.
///     ⚠ Each prefab MUST have a NetworkObject component AND be added to
///       NetworkManager → NetworkConfig → Network Prefabs.
///  3. Fill "Spawn Points" with scene Transforms (empty GameObjects work great).
///  4. Set "Desired Pool Size" — the spawner tops up whenever the live scene count
///     of all listed prefab types combined falls below this number.
///  5. Set "Spawn Cooldown" and options as desired.
///  6. Place the GameObject in the scene before the host/server starts.
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

    [Header("Pool")]
    [Tooltip("Total active scene instances to maintain across all prefab types combined. The spawner scans the scene each tick and tops up with a random prefab whenever the count drops below this value.")]
    [Min(0)]
    public int desiredPoolSize = 5;

    [Header("Timing")]
    [Tooltip("Seconds between each spawn check.")]
    [Min(0.01f)]
    public float spawnCooldown = 2f;

    [Tooltip("Delay before the very first spawn check after the server starts.")]
    [Min(0f)]
    public float initialDelay = 0f;

    [Header("Options")]
    [Tooltip("Prevents the same spawn point being chosen twice in a row.")]
    public bool avoidRepeatSpawnPoint = true;

    [Tooltip("Begin spawning automatically when the server/host starts.")]
    public bool autoStart = true;

    // ── Runtime state (server-only) ────────────────────────────────────────────

    // Cached set of prefab names for fast scene-scan matching.
    private readonly HashSet<string> _prefabNames = new();

    // Tracks only objects spawned by this spawner, so DespawnAll() works correctly.
    private readonly List<NetworkObject> _spawnedObjects = new();

    private Coroutine _spawnCoroutine;
    private int _lastSpawnPointIndex = -1;

    // ── NGO lifecycle ──────────────────────────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        RebuildPrefabNameCache();

        if (autoStart)
            StartSpawning();
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;
        StopSpawning();
    }

    // ── Public API (call from server-side code only) ───────────────────────────

    /// <summary>Starts the automatic spawn loop. Server only.</summary>
    public void StartSpawning()
    {
        if (!IsServer)
        {
            Debug.LogWarning("[RandomSpawner] StartSpawning() must be called on the server.", this);
            return;
        }

        RebuildPrefabNameCache();

        if (_spawnCoroutine != null)
            StopCoroutine(_spawnCoroutine);

        _spawnCoroutine = StartCoroutine(SpawnLoop());
    }

    /// <summary>Stops the automatic spawn loop. Already-spawned objects remain. Server only.</summary>
    public void StopSpawning()
    {
        if (_spawnCoroutine == null) return;
        StopCoroutine(_spawnCoroutine);
        _spawnCoroutine = null;
    }

    /// <summary>Despawns and destroys all objects spawned by this spawner. Server only.</summary>
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
    /// Manually triggers a single spawn check outside the loop. Server only.
    /// Returns the spawned NetworkObject, or null if the pool is already at target.
    /// </summary>
    public NetworkObject SpawnOnce()
    {
        if (!IsServer)
        {
            Debug.LogWarning("[RandomSpawner] SpawnOnce() must be called on the server.", this);
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
        // ── Validate lists ──
        if (prefabsToSpawn == null || prefabsToSpawn.Count == 0)
        {
            Debug.LogWarning("[RandomSpawner] No prefabs assigned.", this);
            return null;
        }

        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogWarning("[RandomSpawner] No spawn points assigned.", this);
            return null;
        }

        // ── Count all active scene instances of any listed prefab type ──
        // FindObjectsByType only returns active objects, so disabled ones are
        // automatically excluded — no extra filtering needed.
        int activeCount = CountLiveSceneInstances();

        if (activeCount >= desiredPoolSize)
            return null;

        Debug.Log($"[RandomSpawner] Pool at {activeCount}/{desiredPoolSize} — spawning.");

        // ── Pick a completely random prefab, no filtering ──
        int prefabIndex = Random.Range(0, prefabsToSpawn.Count);
        NetworkObject chosenPrefab = prefabsToSpawn[prefabIndex];

        if (chosenPrefab == null)
        {
            Debug.LogWarning($"[RandomSpawner] Prefab at index {prefabIndex} is null.", this);
            return null;
        }

        // ── Pick a spawn point ──
        int spawnIndex = PickSpawnIndex();
        Transform spawnPoint = spawnPoints[spawnIndex];

        if (spawnPoint == null)
        {
            Debug.LogWarning($"[RandomSpawner] Spawn point at index {spawnIndex} is null.", this);
            return null;
        }

        // ── Instantiate and network-spawn ──
        NetworkObject instance = Instantiate(
            chosenPrefab,
            spawnPoint.position,
            spawnPoint.rotation
        );

        instance.Spawn(destroyWithScene: true);

        _spawnedObjects.Add(instance);
        _lastSpawnPointIndex = spawnIndex;

        Debug.Log($"[RandomSpawner] Spawned '{chosenPrefab.name}' at '{spawnPoint.name}' " +
                  $"(NetworkObjectId: {instance.NetworkObjectId}). " +
                  $"Pool: {CountLiveSceneInstances()}/{desiredPoolSize}");

        return instance;
    }

    /// <summary>
    /// Scans the scene for active NetworkObjects whose name matches any prefab in
    /// the list. Unity appends "(Clone)" to instantiated objects, so we strip it
    /// before comparing against the cached prefab names.
    /// FindObjectsByType with FindObjectsInactive.Exclude only returns active objects,
    /// so destroyed or disabled instances are never counted.
    /// </summary>
    private int CountLiveSceneInstances()
    {
        int count = 0;
        NetworkObject[] allNetworkObjects = FindObjectsByType<NetworkObject>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        foreach (NetworkObject obj in allNetworkObjects)
        {
            string cleanName = obj.name.Replace("(Clone)", "").Trim();
            if (_prefabNames.Contains(cleanName))
                count++;
        }

        return count;
    }

    /// <summary>
    /// Rebuilds the HashSet of prefab names used for scene-scan matching.
    /// Called on spawn and before starting the loop so Inspector changes are picked up.
    /// </summary>
    private void RebuildPrefabNameCache()
    {
        _prefabNames.Clear();
        if (prefabsToSpawn == null) return;

        foreach (NetworkObject prefab in prefabsToSpawn)
        {
            if (prefab != null)
                _prefabNames.Add(prefab.name);
        }
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
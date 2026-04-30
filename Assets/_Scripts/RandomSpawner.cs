using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

/// <summary>
/// Server-authoritative random spawner for Mirror + FizzySteamworks (Facepunch) transport.
///
/// KEY CONCEPTS:
///  - Only the SERVER runs the spawn loop and calls NetworkServer.Spawn().
///  - Mirror automatically replicates spawned NetworkIdentity objects to all
///    connected clients, including late joiners (via the spawn handler system).
///  - Prefabs MUST be registered in NetworkManager's "Registered Spawnable Prefabs"
///    list (or via NetworkClient.RegisterPrefab at runtime).
///  - Despawning is server-authoritative via NetworkServer.Destroy() / Unspawn().
///  - Live count is determined each tick by scanning the scene for active instances
///    of any prefab in the list.
///  - Each spawn point tracks the item it spawned; a point is free once its object
///    is destroyed or despawned.
///
/// SETUP:
///  1. Attach this script to a GameObject that also has a NetworkIdentity component.
///  2. In the Inspector, fill "Prefabs To Spawn" with your prefab references.
///     ⚠ Each prefab MUST have a NetworkIdentity component AND be registered in
///       NetworkManager → Registered Spawnable Prefabs.
///  3. Fill "Spawn Points" with scene Transforms (empty GameObjects work great).
///  4. Set "Desired Pool Size", "Spawn Cooldown", and options as desired.
///  5. Place the GameObject in the scene before the host/server starts.
///
/// MIRROR vs NGO DIFFERENCES:
///  - NetworkBehaviour  →  NetworkBehaviour (Mirror namespace, not NGO)
///  - NetworkObject     →  NetworkIdentity
///  - IsServer          →  isServer
///  - OnNetworkSpawn()  →  OnStartServer()
///  - OnNetworkDespawn()→  OnStopServer()
///  - obj.Spawn()       →  NetworkServer.Spawn(obj)
///  - obj.Despawn()     →  NetworkServer.Destroy(obj)
///  - obj.IsSpawned     →  obj.netId != 0  (non-zero netId means spawned)
///  - FindObjectsByType →  FindObjectsOfType (Unity standard)
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public class RandomSpawner : NetworkBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────────

    [Header("Spawn Configuration")]
    [Tooltip("Prefabs to randomly pick from. Each MUST be registered in NetworkManager's " +
             "Registered Spawnable Prefabs list and have a NetworkIdentity component.")]
    public List<NetworkIdentity> prefabsToSpawn = new List<NetworkIdentity>();

    [Tooltip("Scene Transforms used as possible spawn locations.")]
    public List<Transform> spawnPoints = new List<Transform>();

    [Header("Pool")]
    [Tooltip("Total active scene instances to maintain across all prefab types combined. " +
             "The spawner scans the scene each tick and tops up whenever the count is below this value.")]
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
    private readonly HashSet<string> _prefabNames = new HashSet<string>();

    // Tracks only objects spawned by this spawner, so DespawnAll() works correctly.
    private readonly List<NetworkIdentity> _spawnedObjects = new List<NetworkIdentity>();

    // Maps each spawn point to the item currently on it (null = free).
    private readonly Dictionary<Transform, NetworkIdentity> _pointOccupancy =
        new Dictionary<Transform, NetworkIdentity>();

    // Reusable list of free points rebuilt each tick.
    private readonly List<Transform> _freePoints = new List<Transform>();

    private Coroutine _spawnCoroutine;
    private int _lastSpawnPointIndex = -1;

    // ── Mirror lifecycle ───────────────────────────────────────────────────────

    // Mirror equivalent of NGO's OnNetworkSpawn() for the server.
    public override void OnStartServer()
    {
        RebuildPrefabNameCache();
        InitOccupancyMap();

        if (autoStart)
            StartSpawning();
    }

    // Mirror equivalent of NGO's OnNetworkDespawn() for the server.
    public override void OnStopServer()
    {
        StopSpawning();
    }

    // ── Public API (call from server-side code only) ───────────────────────────

    /// <summary>Starts the automatic spawn loop. Server only.</summary>
    public void StartSpawning()
    {
        // Mirror uses isServer (lowercase) instead of NGO's IsServer.
        if (!isServer)
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
        if (!isServer) return;

        foreach (NetworkIdentity obj in _spawnedObjects)
        {
            // In Mirror, check netId != 0 to confirm the object is currently spawned.
            // NetworkServer.Destroy() despawns AND destroys the GameObject on all clients.
            if (obj != null && obj.netId != 0)
                NetworkServer.Destroy(obj.gameObject);
        }

        _spawnedObjects.Clear();
        InitOccupancyMap(); // reset all points to free
    }

    /// <summary>
    /// Manually triggers a single spawn check outside the loop. Server only.
    /// Returns the spawned NetworkIdentity, or null if the pool is already at target.
    /// </summary>
    public NetworkIdentity SpawnOnce()
    {
        if (!isServer)
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

    private NetworkIdentity TrySpawn()
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

        // ── Pool check ──
        int activeCount = CountLiveSceneInstances();

        if (activeCount >= desiredPoolSize)
            return null;

        Debug.Log($"[RandomSpawner] Pool at {activeCount}/{desiredPoolSize} — checking free points.");

        // ── Collect free spawn points ──
        // A point is free when its tracked NetworkIdentity is null, destroyed,
        // or no longer spawned (netId == 0).
        _freePoints.Clear();
        foreach (var kvp in _pointOccupancy)
        {
            bool occupied = kvp.Value != null && kvp.Value.netId != 0;
            if (!occupied)
                _freePoints.Add(kvp.Key);
        }

        if (_freePoints.Count == 0)
        {
            Debug.Log("[RandomSpawner] All spawn points occupied — skipping.");
            return null;
        }

        // ── Pick a random prefab ──
        int prefabIndex = Random.Range(0, prefabsToSpawn.Count);
        NetworkIdentity chosenPrefab = prefabsToSpawn[prefabIndex];

        if (chosenPrefab == null)
        {
            Debug.LogWarning($"[RandomSpawner] Prefab at index {prefabIndex} is null.", this);
            return null;
        }

        // ── Pick a free spawn point ──
        Transform spawnPoint = PickFreeSpawnPoint();

        // ── Instantiate ──
        NetworkIdentity instance = Instantiate(
            chosenPrefab,
            spawnPoint.position,
            spawnPoint.rotation
        );

        // ── Network-spawn via Mirror ──
        // NetworkServer.Spawn() replicates the object to all clients (including
        // late joiners via Mirror's spawn handler). Equivalent to NGO's obj.Spawn().
        NetworkServer.Spawn(instance.gameObject);

        _spawnedObjects.Add(instance);
        _pointOccupancy[spawnPoint] = instance;
        _lastSpawnPointIndex = spawnPoints.IndexOf(spawnPoint);

        Debug.Log($"[RandomSpawner] Spawned '{chosenPrefab.name}' at '{spawnPoint.name}' " +
                  $"(netId: {instance.netId}). " +
                  $"Pool: {CountLiveSceneInstances()}/{desiredPoolSize}");

        return instance;
    }

    /// <summary>
    /// Scans the scene for active NetworkIdentity objects whose name matches any
    /// prefab in the list. Unity appends "(Clone)" to instantiated objects, so we
    /// strip it before comparing against the cached prefab names.
    /// Only active (non-destroyed) objects are returned by FindObjectsOfType.
    /// </summary>
    private int CountLiveSceneInstances()
    {
        int count = 0;

        // Mirror doesn't have FindObjectsByType; use Unity's FindObjectsOfType.
        // includeInactive: false mirrors the behaviour of NGO's FindObjectsInactive.Exclude.
        NetworkIdentity[] allNetworkIdentities =
            FindObjectsOfType<NetworkIdentity>(includeInactive: false);

        foreach (NetworkIdentity obj in allNetworkIdentities)
        {
            string cleanName = obj.name.Replace("(Clone)", "").Trim();
            if (_prefabNames.Contains(cleanName))
                count++;
        }

        return count;
    }

    /// <summary>
    /// Rebuilds the HashSet of prefab names used for scene-scan matching.
    /// </summary>
    private void RebuildPrefabNameCache()
    {
        _prefabNames.Clear();
        if (prefabsToSpawn == null) return;

        foreach (NetworkIdentity prefab in prefabsToSpawn)
        {
            if (prefab != null)
                _prefabNames.Add(prefab.name);
        }
    }

    /// <summary>
    /// Initialises the occupancy map from the spawnPoints list.
    /// Existing entries with live items are preserved; new points start as free.
    /// </summary>
    private void InitOccupancyMap()
    {
        if (spawnPoints == null) return;

        // Remove stale entries for points no longer in the list.
        var toRemove = new List<Transform>();
        foreach (Transform key in _pointOccupancy.Keys)
            if (!spawnPoints.Contains(key)) toRemove.Add(key);
        foreach (Transform key in toRemove)
            _pointOccupancy.Remove(key);

        // Add new points as free (don't overwrite live entries).
        foreach (Transform sp in spawnPoints)
            if (sp != null && !_pointOccupancy.ContainsKey(sp))
                _pointOccupancy[sp] = null;
    }

    /// <summary>
    /// Picks a random point from _freePoints, optionally avoiding the last used one.
    /// _freePoints must be populated before calling this.
    /// </summary>
    private Transform PickFreeSpawnPoint()
    {
        if (!avoidRepeatSpawnPoint || _freePoints.Count == 1)
            return _freePoints[Random.Range(0, _freePoints.Count)];

        Transform last = _lastSpawnPointIndex >= 0 && _lastSpawnPointIndex < spawnPoints.Count
            ? spawnPoints[_lastSpawnPointIndex]
            : null;

        Transform picked;
        int attempts = 0;
        const int maxAttempts = 20;

        do
        {
            picked = _freePoints[Random.Range(0, _freePoints.Count)];
            attempts++;
        }
        while (picked == last && attempts < maxAttempts);

        return picked;
    }

    // ── Scene-view gizmos ──────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (spawnPoints == null) return;

        foreach (Transform sp in spawnPoints)
        {
            if (sp == null) continue;

            // Green = free, red = occupied (colour only meaningful in Play mode).
            bool occupied = _pointOccupancy.TryGetValue(sp, out NetworkIdentity obj)
                            && obj != null && obj.netId != 0;

            Gizmos.color = occupied
                ? new Color(1f, 0.2f, 0.2f, 0.85f)
                : new Color(0f, 1f, 0.4f, 0.85f);

            Gizmos.DrawWireSphere(sp.position, 0.4f);
            Gizmos.DrawLine(transform.position, sp.position);
        }
    }
}
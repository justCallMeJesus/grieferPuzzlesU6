using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ThrowableItem : NetworkBehaviour
{
    [Header("Throw Settings")]
    public float throwForce = 20f;
    public float upwardAngle = 10f;

    [Header("Hit Effects")]
    public float stunDuration = 2f;
    public float knockbackForce = 8f;
    public bool destroyOnPlayerHit = true;

    [Header("Despawn Settings")]
    [Tooltip("Seconds the item may sit on the ground (after being dropped) before auto-despawning. 0 = never.")]
    public float groundedDespawnDelay = 15f;
    [Tooltip("Hard timeout from the moment the item is thrown — despawns even if it never lands (e.g. fell off the map). Should be longer than any realistic flight time.")]
    public float flightTimeoutDuration = 10f;
    [Tooltip("How many seconds before despawn the item starts blinking.")]
    public float blinkWarningDuration = 3f;
    [Tooltip("Seconds between each visibility toggle while blinking.")]
    public float blinkInterval = 0.15f;

    // Set this before calling Launch() so kills are attributed correctly
    [HideInInspector] public ulong throwerClientId = ulong.MaxValue;

    private Rigidbody rb;
    private Vector3 lastVelocity;
    private bool hasHit = false;

    // True once a player has actually thrown this item, so freshly-spawned
    // items that were never held never start the despawn clock.
    private bool wasDroppedByPlayer = false;

    private Coroutine despawnCoroutine;
    private Coroutine blinkCoroutine;
    private Coroutine flightTimeoutCoroutine;

    private Renderer[] renderers;
    private Item itemComponent;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        itemComponent = GetComponent<Item>();
        renderers = GetComponentsInChildren<Renderer>();
    }

    void Start()
    {
        var nt = GetComponent<NetworkTransform>();
        Debug.Log($"[ThrowableItem] '{gameObject.name}' | " +
                  $"NetworkTransform: {nt != null} | " +
                  $"Kinematic: {rb.isKinematic} | " +
                  $"Mass: {rb.mass} | " +
                  $"Layer: {gameObject.layer} ({LayerMask.LayerToName(gameObject.layer)}) | " +
                  $"IsServer: {NetworkManager.Singleton.IsServer}");
    }

    void FixedUpdate()
    {
        lastVelocity = rb.linearVelocity;
    }

    [ClientRpc]
    public void LaunchClientRpc(Vector3 direction, ulong thrower)
    {
        throwerClientId = thrower;
        hasHit = false;
        wasDroppedByPlayer = true;  // A real player launched this

        // Cancel any running timers — the item is airborne again
        if (despawnCoroutine != null) { StopCoroutine(despawnCoroutine); despawnCoroutine = null; }
        if (blinkCoroutine != null) { StopCoroutine(blinkCoroutine); blinkCoroutine = null; }
        if (flightTimeoutCoroutine != null) { StopCoroutine(flightTimeoutCoroutine); flightTimeoutCoroutine = null; }
        SetRenderersVisible(true);

        // Server starts a hard timeout in case the item never lands
        if (IsServer && flightTimeoutDuration > 0f)
            flightTimeoutCoroutine = StartCoroutine(FlightTimeoutCoroutine());

        itemComponent?.SetGrounded(false);

        Physics.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Player"), false);

        Vector3 throwDir = direction + Vector3.up * Mathf.Tan(upwardAngle * Mathf.Deg2Rad);
        rb.AddForce(throwDir.normalized * throwForce, ForceMode.Impulse);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasHit) return;

        if (collision.gameObject.TryGetComponent(out PlayerManager hitPlayer))
        {
            if (NetworkManager.Singleton.IsServer)
            {
                Debug.Log($"[ThrowableItem] Player {hitPlayer.OwnerClientId} was hit by thrower {throwerClientId}");

                bool isSelfHit = throwerClientId == hitPlayer.OwnerClientId;

                if (!isSelfHit && throwerClientId != ulong.MaxValue)
                {
                    if (NetworkManager.Singleton.ConnectedClients.TryGetValue(throwerClientId, out var throwerClient))
                    {
                        if (throwerClient.PlayerObject != null &&
                            throwerClient.PlayerObject.TryGetComponent(out PlayerManager throwerManager))
                        {
                            throwerManager.RegisterKill(hitPlayer.OwnerClientId);
                        }
                        else
                        {
                            Debug.LogWarning($"[ThrowableItem] Could not find PlayerManager on thrower {throwerClientId}");
                        }
                    }
                }

                if (destroyOnPlayerHit)
                    GetComponent<NetworkObject>().Despawn(true);
            }

            return;
        }

        hasHit = true;

        Physics.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Player"), true);

        rb.isKinematic = true;

        itemComponent?.SetGrounded(true);

        // Item landed normally — cancel the off-map safety net
        if (flightTimeoutCoroutine != null)
        {
            StopCoroutine(flightTimeoutCoroutine);
            flightTimeoutCoroutine = null;
        }

        // Only the server owns the authoritative despawn timer
        if (NetworkManager.Singleton.IsServer && wasDroppedByPlayer && groundedDespawnDelay > 0f)
        {
            if (despawnCoroutine != null)
                StopCoroutine(despawnCoroutine);

            despawnCoroutine = StartCoroutine(DespawnAfterDelay(groundedDespawnDelay));
        }
    }

    private IEnumerator DespawnAfterDelay(float delay)
    {
        // Wait until it's time to start blinking, then tell all clients
        float waitBeforeBlink = delay - blinkWarningDuration;
        if (waitBeforeBlink > 0f)
            yield return new WaitForSeconds(waitBeforeBlink);

        if (blinkWarningDuration > 0f)
            StartBlinkClientRpc();

        yield return new WaitForSeconds(Mathf.Min(blinkWarningDuration, delay));

        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            Debug.Log($"[ThrowableItem] '{gameObject.name}' auto-despawning after {delay}s on ground.");
            netObj.Despawn(true);
        }
    }

    // Fallback: despawn immediately (no blink) if the item never hits the ground
    private IEnumerator FlightTimeoutCoroutine()
    {
        yield return new WaitForSeconds(flightTimeoutDuration);

        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            Debug.Log($"[ThrowableItem] '{gameObject.name}' flight timeout — despawning without landing.");
            netObj.Despawn(true);
        }
    }

    // ── Blink (visual only, runs on every client) ─────────────────────────────

    [ClientRpc]
    private void StartBlinkClientRpc()
    {
        if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
        blinkCoroutine = StartCoroutine(BlinkCoroutine());
    }

    private IEnumerator BlinkCoroutine()
    {
        var wait = new WaitForSeconds(blinkInterval);
        while (true)
        {
            SetRenderersVisible(false);
            yield return wait;
            SetRenderersVisible(true);
            yield return wait;
        }
    }

    private void SetRenderersVisible(bool visible)
    {
        foreach (var r in renderers)
            if (r != null) r.enabled = visible;
    }
}
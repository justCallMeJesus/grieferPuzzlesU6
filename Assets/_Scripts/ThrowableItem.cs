using System.Collections;
using UnityEngine;
using Mirror;

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
    public float groundedDespawnDelay = 15f;
    public float flightTimeoutDuration = 10f;
    public float blinkWarningDuration = 3f;
    public float blinkInterval = 0.15f;

    [HideInInspector] public int throwerConnectionId = -1;

    private Rigidbody rb;
    private Vector3 lastVelocity;
    private bool hasHit = false;
    private bool wasDroppedByPlayer = false;

    private Coroutine despawnCoroutine;
    private Coroutine blinkCoroutine;
    private Coroutine flightTimeoutCoroutine;

    private Renderer[] renderers;
    private Item itemComponent;
    private Collider thisCollider;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        itemComponent = GetComponent<Item>();
        renderers = GetComponentsInChildren<Renderer>();
        thisCollider = GetComponent<Collider>();
    }

    void FixedUpdate()
    {
        lastVelocity = rb.linearVelocity;
    }

    /// <summary>
    /// Called directly on the server instance after spawn to apply physics and
    /// set up ignore-collision. RpcLaunch handles the same for all clients.
    /// </summary>
    public void ServerLaunch(Vector3 direction, int connId, uint throwerNetId)
    {
        throwerConnectionId = connId;
        hasHit = false;
        wasDroppedByPlayer = true;

        if (flightTimeoutDuration > 0f)
            flightTimeoutCoroutine = StartCoroutine(FlightTimeoutCoroutine());

        itemComponent?.SetGrounded(false);

        if (NetworkServer.spawned.TryGetValue(throwerNetId, out NetworkIdentity throwerIdentity))
        {
            Collider throwerCol = throwerIdentity.GetComponent<Collider>();
            if (throwerCol != null && thisCollider != null)
                Physics.IgnoreCollision(thisCollider, throwerCol, true);
        }

        Vector3 throwDir = direction + Vector3.up * Mathf.Tan(upwardAngle * Mathf.Deg2Rad);
        rb.AddForce(throwDir.normalized * throwForce, ForceMode.Impulse);
    }

    /// <summary>
    /// Call this on the server before calling RpcLaunch.
    /// throwerNetId = the netId of the player who threw this item.
    /// </summary>
    [ClientRpc]
    public void RpcLaunch(Vector3 direction, int throwerId, uint throwerNetId)
    {
        throwerConnectionId = throwerId;
        hasHit = false;
        wasDroppedByPlayer = true;

        StopActiveCoroutines();
        SetRenderersVisible(true);

        if (isServer && flightTimeoutDuration > 0f)
            flightTimeoutCoroutine = StartCoroutine(FlightTimeoutCoroutine());

        itemComponent?.SetGrounded(false);

        // Physically ignore the thrower's collider so the item can't
        // deflect off them or register a self-hit on any client.
        // NetworkClient.spawned is empty on a dedicated server, so we must
        // also check NetworkServer.spawned (populated on host and server).
        NetworkIdentity throwerIdentity = null;
        if (NetworkServer.spawned.TryGetValue(throwerNetId, out NetworkIdentity serverIdentity))
            throwerIdentity = serverIdentity;
        else if (NetworkClient.spawned.TryGetValue(throwerNetId, out NetworkIdentity clientIdentity))
            throwerIdentity = clientIdentity;

        if (throwerIdentity != null)
        {
            Collider throwerCol = throwerIdentity.GetComponent<Collider>();
            if (throwerCol != null && thisCollider != null)
                Physics.IgnoreCollision(thisCollider, throwerCol, true);
        }

    }

    void OnCollisionEnter(Collision collision)
    {
        // Only the server resolves hit logic.
        if (!isServer || hasHit) return;

        if (collision.gameObject.TryGetComponent(out PlayerManager hitPlayer))
        {
            // Self-hit guard (server-side, belt-and-suspenders).
            bool isSelfHit = hitPlayer.connectionToClient != null
                && hitPlayer.connectionToClient.connectionId == throwerConnectionId;

            if (isSelfHit) return;

            if (throwerConnectionId != -1)
            {
                PlayerManager throwerManager = GetPlayerByConnId(throwerConnectionId);
                throwerManager?.RegisterKill(hitPlayer.netId);
            }

            if (destroyOnPlayerHit)
                NetworkServer.Destroy(gameObject);

            return;
        }

        // Hit ground / wall — item is now at rest.
        hasHit = true;
        RpcOnLanded();

        if (flightTimeoutCoroutine != null) StopCoroutine(flightTimeoutCoroutine);

        if (wasDroppedByPlayer && groundedDespawnDelay > 0f)
        {
            if (despawnCoroutine != null) StopCoroutine(despawnCoroutine);
            despawnCoroutine = StartCoroutine(DespawnAfterDelay(groundedDespawnDelay));
        }
    }

    [ClientRpc]
    private void RpcOnLanded()
    {
        // Per-object: ignore collision with every player that currently exists.
        // This does NOT affect any other throwable in the scene.
        if (thisCollider != null)
        {
            foreach (var player in FindObjectsByType<PlayerManager>(FindObjectsSortMode.None))
            {
                Collider playerCol = player.GetComponent<Collider>();
                if (playerCol != null)
                    Physics.IgnoreCollision(thisCollider, playerCol, true);
            }
        }

        rb.isKinematic = true;
        itemComponent?.SetGrounded(true);
    }

    private IEnumerator DespawnAfterDelay(float delay)
    {
        float waitBeforeBlink = delay - blinkWarningDuration;
        if (waitBeforeBlink > 0f)
            yield return new WaitForSeconds(waitBeforeBlink);

        if (blinkWarningDuration > 0f)
            RpcStartBlink();

        yield return new WaitForSeconds(Mathf.Min(blinkWarningDuration, delay));

        if (gameObject != null)
            NetworkServer.Destroy(gameObject);
    }

    private IEnumerator FlightTimeoutCoroutine()
    {
        yield return new WaitForSeconds(flightTimeoutDuration);
        if (gameObject != null)
            NetworkServer.Destroy(gameObject);
    }

    [ClientRpc]
    private void RpcStartBlink()
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

    private void StopActiveCoroutines()
    {
        if (despawnCoroutine != null) StopCoroutine(despawnCoroutine);
        if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
        if (flightTimeoutCoroutine != null) StopCoroutine(flightTimeoutCoroutine);
    }

    private PlayerManager GetPlayerByConnId(int connId)
    {
        if (NetworkServer.connections.TryGetValue(connId, out NetworkConnectionToClient conn))
            return conn.identity.GetComponent<PlayerManager>();
        return null;
    }
}
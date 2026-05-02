using System.Collections;
using UnityEngine;
using Mirror;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(AudioSource))]
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
    [Tooltip("Total time (seconds) before the item despawns after being thrown. Timer starts the moment it is thrown.")]
    public float lifetimeDuration = 15f;
    public float blinkWarningDuration = 3f;
    public float blinkInterval = 0.15f;

    [HideInInspector] public int throwerConnectionId = -1;

    private Rigidbody rb;
    private Vector3 lastVelocity;
    private bool hasHit = false;
    private bool wasDroppedByPlayer = false;

    private Coroutine despawnCoroutine;
    private Coroutine blinkCoroutine;

    private Renderer[] renderers;
    private Item itemComponent;
    private Collider thisCollider;
    private AudioSource audioSource;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        itemComponent = GetComponent<Item>();
        renderers = GetComponentsInChildren<Renderer>();
        thisCollider = GetComponent<Collider>();
        audioSource = GetComponent<AudioSource>();
        audioSource.spatialBlend = 1f;
    }

    void FixedUpdate()
    {
        lastVelocity = rb.linearVelocity;
    }

    public void ServerLaunch(Vector3 direction, int connId, uint throwerNetId)
    {
        RpcHideForThrow();
        throwerConnectionId = connId;
        hasHit = false;
        wasDroppedByPlayer = true;

        if (lifetimeDuration > 0f)
        {
            if (despawnCoroutine != null) StopCoroutine(despawnCoroutine);
            despawnCoroutine = StartCoroutine(DespawnAfterDelay(lifetimeDuration));
        }

        itemComponent?.SetGrounded(false);

        if (NetworkServer.spawned.TryGetValue(throwerNetId, out NetworkIdentity throwerIdentity))
        {
            Collider throwerCol = throwerIdentity.GetComponent<Collider>();
            if (throwerCol != null && thisCollider != null)
                Physics.IgnoreCollision(thisCollider, throwerCol, true);
        }

        Vector3 throwDir = direction + Vector3.up * Mathf.Tan(upwardAngle * Mathf.Deg2Rad);
        rb.AddForce(throwDir.normalized * throwForce, ForceMode.Impulse);

        RpcPlayThrowSound();
    }

    [ClientRpc]
    private void RpcHideForThrow()
    {
        SetRenderersVisible(false);
    }

    [ClientRpc]
    private void RpcPlayThrowSound()
    {
        if (itemComponent == null || itemComponent.ItemData == null) return;
        AudioClip clip = itemComponent.ItemData.throwSound;
        if (clip == null) return;
        audioSource.PlayOneShot(clip);
    }

    [ClientRpc]
    public void RpcLaunch(Vector3 direction, int throwerId, uint throwerNetId)
    {
        throwerConnectionId = throwerId;
        hasHit = false;
        wasDroppedByPlayer = true;

        if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
        SetRenderersVisible(true);

        itemComponent?.SetGrounded(false);

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
        if (!isServer || hasHit) return;

        if (collision.gameObject.TryGetComponent(out PlayerManager hitPlayer))
        {
            bool isSelfHit = hitPlayer.connectionToClient != null
                && hitPlayer.connectionToClient.connectionId == throwerConnectionId;

            if (isSelfHit) return;

            if (throwerConnectionId != -1)
            {
                PlayerManager throwerManager = GetPlayerByConnId(throwerConnectionId);
                throwerManager?.RegisterKill(hitPlayer.netId);
            }

            // Respawn the hit player at their assigned spawn
            if (hitPlayer.connectionToClient != null)
            {
                int hitConnId = hitPlayer.connectionToClient.connectionId;
                if (ConnectionsManager.PlayerSpawns.TryGetValue(hitConnId, out Transform spawn))
                {
                    hitPlayer.transform.position = spawn.position;
                    hitPlayer.transform.rotation = spawn.rotation;

                    Rigidbody hitRb = hitPlayer.GetComponent<Rigidbody>();
                    if (hitRb != null)
                    {
                        hitRb.linearVelocity = Vector3.zero;
                        hitRb.angularVelocity = Vector3.zero;
                    }

                    // Clear the hit player's inventory on death
                    PlayerInventory hitInventory = hitPlayer.GetComponent<PlayerInventory>();
                    hitInventory?.ClearInventory();

                    RpcRespawnPlayer(hitPlayer.netId, spawn.position, spawn.rotation);
                }
            }

            if (destroyOnPlayerHit)
                NetworkServer.Destroy(gameObject);

            return;
        }

        hasHit = true;
        RpcOnLanded();
    }

    [ClientRpc]
    private void RpcRespawnPlayer(uint playerNetId, Vector3 position, Quaternion rotation)
    {
        if (NetworkClient.spawned.TryGetValue(playerNetId, out NetworkIdentity identity))
        {
            identity.transform.position = position;
            identity.transform.rotation = rotation;

            Rigidbody hitRb = identity.GetComponent<Rigidbody>();
            if (hitRb != null)
            {
                hitRb.linearVelocity = Vector3.zero;
                hitRb.angularVelocity = Vector3.zero;
            }
        }
    }

    [ClientRpc]
    private void RpcOnLanded()
    {
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
    }

    private PlayerManager GetPlayerByConnId(int connId)
    {
        if (NetworkServer.connections.TryGetValue(connId, out NetworkConnectionToClient conn))
            return conn.identity.GetComponent<PlayerManager>();
        return null;
    }
}
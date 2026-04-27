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

    // Mirror nutzt int für connectionIds (Standard -1 für "keiner")
    [HideInInspector] public int throwerConnectionId = -1;

    private Rigidbody rb;
    private Vector3 lastVelocity;
    private bool hasHit = false;
    private bool wasDroppedByPlayer = false;

    private Coroutine despawnCoroutine;
    private Coroutine blinkCoroutine;
    private Coroutine flightTimeoutCoroutine;

    private Renderer[] renderers;
    private Item itemComponent; // Falls du deine Item-Klasse auch in Mirror hast

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        itemComponent = GetComponent<Item>();
        renderers = GetComponentsInChildren<Renderer>();
    }

    void FixedUpdate()
    {
        // rb.linearVelocity ist Unity 6 Standard
        lastVelocity = rb.linearVelocity;
    }

    // Launch wird vom Server aufgerufen und an alle Clients gesendet
    [ClientRpc]
    public void RpcLaunch(Vector3 direction, int throwerId)
    {
        throwerConnectionId = throwerId;
        hasHit = false;
        wasDroppedByPlayer = true;

        StopActiveCoroutines();
        SetRenderersVisible(true);

        // Server startet den Sicherheits-Timeout (off-map)
        if (isServer && flightTimeoutDuration > 0f)
            flightTimeoutCoroutine = StartCoroutine(FlightTimeoutCoroutine());

        itemComponent?.SetGrounded(false);

        // Kollision mit Spielern erlauben (kurzzeitig)
        Physics.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Player"), false);

        Vector3 throwDir = direction + Vector3.up * Mathf.Tan(upwardAngle * Mathf.Deg2Rad);
        rb.AddForce(throwDir.normalized * throwForce, ForceMode.Impulse);
    }

    void OnCollisionEnter(Collision collision)
    {
        // Nur der Server berechnet Treffer-Logik in Mirror
        if (!isServer || hasHit) return;

        if (collision.gameObject.TryGetComponent(out PlayerManager hitPlayer))
        {
            Debug.Log($"[ThrowableItem] Player {hitPlayer.netId} was hit by thrower {throwerConnectionId}");

            // Prüfen ob man sich selbst getroffen hat (über connectionId)
            bool isSelfHit = (hitPlayer.connectionToClient != null && hitPlayer.connectionToClient.connectionId == throwerConnectionId);

            if (!isSelfHit && throwerConnectionId != -1)
            {
                // Helper-Funktion um PlayerManager des Werfers zu finden
                PlayerManager throwerManager = GetPlayerByConnId(throwerConnectionId);
                if (throwerManager != null)
                {
                    // Wir nutzen netId oder connectionId zur Registrierung
                    throwerManager.RegisterKill(hitPlayer.netId);
                }
            }

            if (destroyOnPlayerHit)
                NetworkServer.Destroy(gameObject);

            return;
        }

        // Wenn es kein Spieler war, ist es der Boden/Wand
        hasHit = true;
        RpcOnLanded(); // Clients informieren

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
        // Physische Anpassung auf allen Clients
        Physics.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Player"), true);
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

    // Hilfsmethode für Mirror
    private PlayerManager GetPlayerByConnId(int connId)
    {
        if (NetworkServer.connections.TryGetValue(connId, out NetworkConnectionToClient conn))
        {
            return conn.identity.GetComponent<PlayerManager>();
        }
        return null;
    }
}

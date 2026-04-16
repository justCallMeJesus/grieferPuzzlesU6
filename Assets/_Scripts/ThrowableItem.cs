using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ThrowableItem : MonoBehaviour
{
    [Header("Throw Settings")]
    public float throwForce = 20f;
    public float upwardAngle = 10f;

    [Header("Hit Effects")]
    public float stunDuration = 2f;
    public float knockbackForce = 8f;
    public bool destroyOnPlayerHit = true;

    // Set this before calling Launch() so kills are attributed correctly
    [HideInInspector] public ulong throwerClientId = ulong.MaxValue;

    private Rigidbody rb;
    private Vector3 lastVelocity;
    private bool hasHit = false;

    private Item itemComponent;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        itemComponent = GetComponent<Item>();
    }

    void FixedUpdate()
    {
        lastVelocity = rb.linearVelocity;
    }

    public void Launch(Vector3 direction, ulong thrower)
    {
        throwerClientId = thrower;
        hasHit = false;

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

                // Prevent friendly self-hit from counting as a kill
                bool isSelfHit = throwerClientId == hitPlayer.OwnerClientId;

                if (!isSelfHit && throwerClientId != ulong.MaxValue)
                {
                    // Look up the throwing player's PlayerManager and register the kill
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

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        itemComponent?.SetGrounded(true);
    }
}
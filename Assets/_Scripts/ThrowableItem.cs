using UnityEngine;

public class ThrowableItem : MonoBehaviour
{
    [Header("Throw Settings")]
    public float throwForce = 20f;
    public float upwardAngle = 10f; // slight upward arc

    [Header("Hit Effects")]
    public float stunDuration = 2f;
    public float knockbackForce = 8f;

    private Rigidbody rb;
    private Vector3 lastVelocity;
    private bool hasHit = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        // Track velocity before collision for knockback direction
        lastVelocity = rb.linearVelocity;
    }

    public void Launch(Vector3 direction)
    {
        Vector3 throwDir = direction + Vector3.up * Mathf.Tan(upwardAngle * Mathf.Deg2Rad);
        rb.AddForce(throwDir.normalized * throwForce, ForceMode.Impulse);
    }
}

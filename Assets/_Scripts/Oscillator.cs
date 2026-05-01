using UnityEngine;

/// <summary>
/// Makes a GameObject oscillate (bob) up and down using a sine wave.
/// Attach this script to any GameObject you want to animate.
/// </summary>
public class Oscillator : MonoBehaviour
{
    [Header("Oscillation Settings")]
    [Tooltip("How far the object moves from its origin (in world units).")]
    public float amplitude = 1f;

    [Tooltip("How many full cycles per second.")]
    public float speed = 1f;

    [Header("Options")]
    [Tooltip("Offset the phase so multiple objects don't move in sync (0 – 2π).")]
    public float phaseOffset = 0f;

    [Tooltip("Axis to oscillate along. Defaults to world Y (up/down).")]
    public Vector3 axis = Vector3.up;

    [Tooltip("When true the position is set relative to the object's starting position. " +
             "When false it oscillates around world origin on the chosen axis.")]
    public bool useLocalOrigin = true;

    // ── private state ──────────────────────────────────────────────────────────
    private Vector3 _origin;

    // ──────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _origin = transform.position;
    }

    private void Update()
    {
        float displacement = amplitude * Mathf.Sin(2f * Mathf.PI * speed * Time.time + phaseOffset);
        Vector3 basePosition = useLocalOrigin ? _origin : Vector3.zero;
        transform.position = basePosition + axis.normalized * displacement;
    }

    // ── Editor helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Call this if you move the object at runtime and want the new position
    /// to become the new oscillation centre.
    /// </summary>
    public void ResetOrigin()
    {
        _origin = transform.position;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Visualise the oscillation range in the Scene view.
        Vector3 centre = Application.isPlaying ? _origin : transform.position;
        Vector3 dir = axis.normalized * amplitude;

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
        Gizmos.DrawLine(centre - dir, centre + dir);

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.4f);
        Gizmos.DrawWireSphere(centre + dir, 0.05f);
        Gizmos.DrawWireSphere(centre - dir, 0.05f);
        Gizmos.DrawWireSphere(centre, 0.03f);
    }
#endif
}
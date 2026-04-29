using UnityEngine;

public class NameplateBillboard : MonoBehaviour
{
    private Transform camTransform;

    void LateUpdate()
    {
        // 1. Try to find the camera if we don't have it yet
        if (camTransform == null)
        {
            if (Camera.main != null)
            {
                camTransform = Camera.main.transform;
            }
            return; // Skip this frame if no camera found
        }

        // 2. Face the camera (Standard Billboard logic)
        // This ensures the text stays upright even when the FreeLook cam orbits
        transform.LookAt(transform.position + camTransform.rotation * Vector3.forward,
                         camTransform.rotation * Vector3.up);
    }
}

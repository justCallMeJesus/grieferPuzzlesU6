using UnityEngine;
using UnityEngine.InputSystem;

public class UIPlayerHeadLook : MonoBehaviour
{
    public Transform headBone; // Drag the head bone from the prefab here
    public float lookSpeed = 5f;
    public Vector2 lookLimits = new Vector2(40f, 40f); // Max rotation angles

    void LateUpdate()
    {
        // 1. Get mouse position in 0 to 1 range relative to screen
        Vector3 mousePos = Mouse.current.position.ReadValue();

        float mouseY = (mousePos.x / Screen.width) - 0.72f;
        float mouseX = (mousePos.y / Screen.height)-0.5f;
        
        // 2. Calculate target rotation
        // Adjust these axes based on your model's bone orientation
        Quaternion targetRotation = Quaternion.Euler(mouseY * lookLimits.y,0, mouseX * lookLimits.x);

        // 3. Smoothly rotate the bone
        headBone.localRotation = targetRotation;
    }
}

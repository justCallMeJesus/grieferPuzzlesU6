using Unity.Burst.Intrinsics;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class PlayerMouseRotation : MonoBehaviour
{
    private PlayerMovement playerMovement;
    private new Camera camera;

    [Header("Rotation Attributes")]
    [SerializeField] private InputActionReference mousePosition;
    [SerializeField] private InputActionReference aim;
    [SerializeField] private LayerMask trackerLayer;

    private void Start()
    {
        playerMovement = GetComponent<PlayerMovement>();
        camera = Camera.main;
    }

    private void Update()
    {
        if (aim.action.ReadValue<float>() != 0 && !playerMovement.IsMoving())
        {
            RotateToMouse();
        }
    }

    private void RotateToMouse()
    {
        if (camera == null) return;

        Ray ray = camera.ScreenPointToRay(mousePosition.action.ReadValue<Vector2>());

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, trackerLayer))
        {
            Vector3 direction = hit.point - transform.position;
            direction.y = 0f;

            if (direction != Vector3.zero)
            {
                Quaternion lookRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Euler(0f, lookRotation.eulerAngles.y, 0f);
            }
        }
    }
}
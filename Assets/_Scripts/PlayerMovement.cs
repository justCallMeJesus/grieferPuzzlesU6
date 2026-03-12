using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

public class PlayerMovement : MonoBehaviour
{
    public enum PlayerMovementState
    {
        FreeMovement,
        NoMovement
    }

    [HideInInspector] public PlayerMovementState state;
    // input access fields
    [SerializeField] private InputActionReference move;
    public InputActionReference Move => move;
    [SerializeField] public CinemachineOrbitalFollow freeLook;

    [Header("Free Movement Parameters")]
    public float speed = 4f;
    [SerializeField] public LayerMask collisionIgnoreMask;

    IMovementMode currentMode;

    IMovementMode freeMovement = new FreeMovement();


    private void Awake()
    {
        currentMode = freeMovement;
    }

    private void Update()
    {
        currentMode.Tick(this);
    }


    public interface IMovementMode
    {
        void Tick(PlayerMovement player);
    }

    public class FreeMovement : IMovementMode
    {
        public void Tick(PlayerMovement player)
        {
            // move player normally
            // get GameInput info for movement direction
            Vector2 inputVector = player.Move.action.ReadValue<Vector2>().normalized;
            Vector3 moveDir = new Vector3(inputVector.x, 0f, inputVector.y);

            moveDir = Quaternion.AngleAxis(player.freeLook.HorizontalAxis.Value, Vector3.up) * moveDir;

            // player speed
            float moveDistance = player.speed * Time.deltaTime;

            // determine player movebility
            float playerRadius = 0.3f;
            float playerHeight = 1f;
            bool canMove = !Physics.CapsuleCast(player.transform.position + Vector3.up * 0.5f, player.transform.position + Vector3.up * playerHeight, playerRadius, moveDir, out RaycastHit hit, moveDistance, ~player.collisionIgnoreMask);

            // Player rotation
            float rotateSpeed = 25f;
            if (moveDir != Vector3.zero)
            {
                player.transform.forward = Vector3.Slerp(player.transform.forward, moveDir, Time.deltaTime * rotateSpeed);

            }


            if (!canMove)
            {
                // cannot move towards moveDir
                Vector3 moveDirX = new Vector3(moveDir.x, 0, 0);
                canMove = !Physics.CapsuleCast(player.transform.position, player.transform.position + Vector3.up * playerHeight, playerRadius, moveDirX, moveDistance);
                if (canMove)
                {
                    // can move only on the X
                    moveDir = moveDirX;
                }
                else
                {
                    // cannot move on X, attempt only Z movement
                    Vector3 moveDirZ = new Vector3(0, 0, moveDir.z);
                    canMove = !Physics.CapsuleCast(player.transform.position, player.transform.position + Vector3.up * playerHeight, playerRadius, moveDirZ, moveDistance);
                    if (canMove)
                    {
                        moveDir = moveDirZ;
                    }

                }
            }
            if (canMove)
            {
                player.transform.position += moveDir * moveDistance;
            }
        }
    }
}

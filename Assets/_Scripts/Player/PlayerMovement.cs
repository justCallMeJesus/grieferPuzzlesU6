using Mirror;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{
    public enum PlayerMovementState
    {
        FreeMovement,
        NoMovement
    }

    [HideInInspector] public PlayerMovementState state;
    [SerializeField] private InputActionReference move;
    public InputActionReference Move => move;
    [SerializeField] public GameObject freeLookPrefab;
    private GameObject spawnedCamera;

    [Header("Animation")]
    [SerializeField] private Animator anim; // Drag your Animator here in the Inspector

    [Header("Free Movement Parameters")]
    public float speed = 4f;
    [SerializeField] public LayerMask collisionIgnoreMask;

    IMovementMode currentMode;
    IMovementMode freeMovement = new FreeMovement();
    IMovementMode noMovement = new NoMovement();

    private void Awake()
    {
        currentMode = freeMovement;
        // Safety check: if you forgot to drag it in, try to find it on this object
        if (anim == null) anim = GetComponent<Animator>();
    }

    private void Update()
    {
        if (!isLocalPlayer) return;

        if (Keyboard.current.iKey.wasPressedThisFrame) OnDeathSpectate();
        if (Keyboard.current.pKey.wasPressedThisFrame) OnRespawnCamera();

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
            Vector2 inputVector = player.Move.action.ReadValue<Vector2>().normalized;
            Vector3 moveDir = new Vector3(inputVector.x, 0f, inputVector.y);

            // --- ANIMATION LOGIC START ---
            // We use the magnitude of the input to drive the "Speed" float
            if (player.anim != null)
            {
                player.anim.SetFloat("Speed", inputVector.magnitude);
            }
            // --- ANIMATION LOGIC END ---

            float moveDistance = player.speed * Time.deltaTime;
            float playerRadius = 0.3f;
            float playerHeight = 1f;

            bool canMove = !Physics.CapsuleCast(player.transform.position + Vector3.up * 0.5f,
                player.transform.position + Vector3.up * playerHeight, playerRadius, moveDir,
                out RaycastHit hit, moveDistance, ~player.collisionIgnoreMask);

            float rotateSpeed = 25f;
            if (moveDir != Vector3.zero)
            {
                player.transform.forward = Vector3.Slerp(player.transform.forward, moveDir, Time.deltaTime * rotateSpeed);
            }

            if (!canMove)
            {
                Vector3 moveDirX = new Vector3(moveDir.x, 0, 0);
                canMove = !Physics.CapsuleCast(player.transform.position, player.transform.position + Vector3.up * playerHeight, playerRadius, moveDirX, moveDistance);
                if (canMove) moveDir = moveDirX;
                else
                {
                    Vector3 moveDirZ = new Vector3(0, 0, moveDir.z);
                    canMove = !Physics.CapsuleCast(player.transform.position, player.transform.position + Vector3.up * playerHeight, playerRadius, moveDirZ, moveDistance);
                    if (canMove) moveDir = moveDirZ;
                }
            }

            if (canMove)
            {
                player.transform.position += moveDir * moveDistance;
            }
        }
    }

    public class NoMovement : IMovementMode
    {
        public void Tick(PlayerMovement player)
        {
            // Set speed to 0 when movement is disabled
            if (player.anim != null) player.anim.SetFloat("Speed", 0f);
        }
    }

    // ... (Rest of your RpcHideUI, OnStartLocalPlayer, Spectate methods remain the same)
    [ClientRpc]
    public void RpcHideUI()
    {
        GameObject MainContainer = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == "ContainerPreGameUi");
        if (MainContainer != null) MainContainer.SetActive(false);
    }

    public override void OnStartLocalPlayer()
    {
        if (!isLocalPlayer) return;
        spawnedCamera = Instantiate(freeLookPrefab);
        var freelook = spawnedCamera.GetComponent<CinemachineCamera>();
        if (freelook != null)
        {
            freelook.Follow = this.transform;
            freelook.LookAt = this.transform;
        }
    }

    public override void OnStopClient()
    {
        if (spawnedCamera != null) Destroy(spawnedCamera);
    }

    public void DisableMovement() => currentMode = noMovement;
    public void EnableMovement() => currentMode = freeMovement;

    public void OnDeathSpectate()
    {
        if (!isLocalPlayer || spawnedCamera == null) return;
        var freelook = spawnedCamera.GetComponent<CinemachineCamera>();
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject p in players)
        {
            NetworkIdentity ni = p.GetComponent<NetworkIdentity>();
            if (ni != null && ni.netId != netId)
            {
                freelook.Follow = p.transform;
                freelook.LookAt = p.transform;
                return;
            }
        }
    }

    public void OnRespawnCamera()
    {
        if (!isLocalPlayer || spawnedCamera == null) return;
        var freelook = spawnedCamera.GetComponent<CinemachineCamera>();
        if (freelook != null)
        {
            freelook.Follow = this.transform;
            freelook.LookAt = this.transform;
        }
    }

    public bool IsMoving()
    {
        return Move.action.ReadValue<Vector2>().magnitude > 0f;
    }
}

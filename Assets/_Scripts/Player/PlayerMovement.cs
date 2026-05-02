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
    [SerializeField] private Animator anim;

    [Header("Walking Sound")]
    [SerializeField] private AudioSource footstepAudioSource;
    [SerializeField] private AudioClip footstepClip;
    [SerializeField] private float footstepMinDistance = 1f;  // Full volume within this range
    [SerializeField] private float footstepMaxDistance = 20f; // Silent beyond this range

    [Header("Free Movement Parameters")]
    public float speed = 4f;
    [SerializeField] public LayerMask collisionIgnoreMask;

    IMovementMode currentMode;
    IMovementMode freeMovement = new FreeMovement();
    IMovementMode noMovement = new NoMovement();

    [SerializeField][Range(0f, 1f)] private float footstepVolume = 0.4f;

    private void Awake()
    {
        currentMode = freeMovement;
        if (anim == null) anim = GetComponent<Animator>();

        if (footstepAudioSource == null)
            footstepAudioSource = gameObject.AddComponent<AudioSource>();

        footstepAudioSource.clip = footstepClip;
        footstepAudioSource.loop = true;
        footstepAudioSource.playOnAwake = false;
        footstepAudioSource.spatialBlend = 1f;           // Full 3D — Unity handles distance falloff
        footstepAudioSource.rolloffMode = AudioRolloffMode.Linear;
        footstepAudioSource.minDistance = footstepMinDistance;
        footstepAudioSource.maxDistance = footstepMaxDistance;
        footstepAudioSource.volume = footstepVolume;
    }

    private void Update()
    {
        // Input & camera controls only run for the local player
        if (isLocalPlayer)
        {
            if (Keyboard.current.iKey.wasPressedThisFrame) OnDeathSpectate();
            if (Keyboard.current.pKey.wasPressedThisFrame) OnRespawnCamera();
        }

        // Movement and footsteps run for ALL players (so remote players animate and make noise)
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
            // Only the local player reads input and moves via physics.
            // Remote players are moved by Mirror's NetworkTransform, so we only
            // need to drive their footstep audio based on whether they're actually moving.
            if (player.isLocalPlayer)
            {
                Vector2 inputVector = player.Move.action.ReadValue<Vector2>().normalized;
                Vector3 moveDir = new Vector3(inputVector.x, 0f, inputVector.y);

                if (player.anim != null)
                    player.anim.SetFloat("Speed", inputVector.magnitude);

                if (inputVector.magnitude > 0.1f)
                    player.ResumeFootsteps();
                else
                    player.PauseFootsteps();

                float moveDistance = player.speed * Time.deltaTime;
                float playerRadius = 0.3f;
                float playerHeight = 1f;

                bool canMove = !Physics.CapsuleCast(player.transform.position + Vector3.up * 0.5f,
                    player.transform.position + Vector3.up * playerHeight, playerRadius, moveDir,
                    out RaycastHit hit, moveDistance, ~player.collisionIgnoreMask);

                float rotateSpeed = 25f;
                if (moveDir != Vector3.zero)
                    player.transform.forward = Vector3.Slerp(player.transform.forward, moveDir, Time.deltaTime * rotateSpeed);

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
                    player.transform.position += moveDir * moveDistance;
            }
            else
            {
                // For remote players, check if they're actually moving (NetworkTransform is
                // updating their position) and drive footsteps + animation accordingly.
                bool remoteIsMoving = player.IsMoving();

                if (player.anim != null)
                    player.anim.SetFloat("Speed", remoteIsMoving ? 1f : 0f);

                if (remoteIsMoving)
                    player.ResumeFootsteps();
                else
                    player.PauseFootsteps();
            }
        }
    }

    public class NoMovement : IMovementMode
    {
        public void Tick(PlayerMovement player)
        {
            if (player.anim != null) player.anim.SetFloat("Speed", 0f);
            player.PauseFootsteps();
        }
    }

    public void ResumeFootsteps()
    {
        if (footstepAudioSource == null) return;
        if (!footstepAudioSource.isPlaying)
            footstepAudioSource.Play();
    }

    public void PauseFootsteps()
    {
        if (footstepAudioSource == null) return;
        if (footstepAudioSource.isPlaying)
            footstepAudioSource.Pause();
    }

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

    // For remote players: treat any velocity (position change) as movement.
    // We reuse the velocity Mirror's NetworkTransform gives us via position delta.
    private Vector3 _lastPosition;
    public bool IsMoving()
    {
        if (isLocalPlayer)
            return Move.action.ReadValue<Vector2>().magnitude > 0f;

        // Remote: detect movement from position change each frame
        bool moved = Vector3.Distance(transform.position, _lastPosition) > 0.001f;
        _lastPosition = transform.position;
        return moved;
    }
}
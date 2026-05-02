using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

public class PlayerInteraction : NetworkBehaviour
{
    // -------------------------------------------------------------------------
    // Static event — fired once by the local player when it spawns.
    // UITooltip (on the Canvas) subscribes to this to get a reference to
    // the correct PlayerInteraction without any Inspector drag-and-drop.
    // -------------------------------------------------------------------------
    public static event Action<PlayerInteraction> OnLocalPlayerSpawned;

    [SerializeField] private float interactRadius = 2f;
    [SerializeField] private InputActionReference pickupAction;
    [SerializeField] private InputActionReference objectInteractAction;

    public GameObject objectInRange;

    // Public properties so UITooltip can read detection state each frame.
    public IPickupable closestPickupableInRange { get; private set; }
    public IInteractable closestInteractableInRange { get; private set; }

    public PlayerManager playerManager;
    public IInteractable currentlyInteractingObject;

    // -------------------------------------------------------------------------

    private void Start()
    {
        playerManager = GetComponent<PlayerManager>();
    }

    public override void OnStartLocalPlayer()
    {
        // Tell any listening UI that the local player is now alive.
        OnLocalPlayerSpawned?.Invoke(this);

        pickupAction.action.Enable();
        objectInteractAction.action.Enable();

        pickupAction.action.performed += Action_performed;
        objectInteractAction.action.performed += objectInteractAction_performed;
    }

    public override void OnStopLocalPlayer()
    {
        pickupAction.action.performed -= Action_performed;
        objectInteractAction.action.performed -= objectInteractAction_performed;
    }

    private void objectInteractAction_performed(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer) return;
        Debug.Log("Interact action performed");

        if (currentlyInteractingObject != null)
        {
            currentlyInteractingObject.OnStopInteraction(playerManager);
            currentlyInteractingObject = null;
            Debug.Log("Stopped interacting with object");
            return;
        }

        if (closestInteractableInRange != null)
        {
            if (!closestInteractableInRange.CanInteract()) return;
            Debug.Log("Interacting with object: " + closestInteractableInRange.GameObject.name);
            closestInteractableInRange.OnInteract(playerManager);
        }
    }

    private void Action_performed(InputAction.CallbackContext obj)
    {
        if (!isLocalPlayer) return;
        Debug.Log("Pickup action performed");
        if (closestPickupableInRange != null)
        {
            closestPickupableInRange.OnPickup(playerManager.inventory);
        }
    }

    private void Update()
    {
        if (!isLocalPlayer) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, interactRadius);

        List<IPickupable> pickupables = new List<IPickupable>();
        List<IInteractable> interactables = new List<IInteractable>();

        foreach (Collider hit in hits)
        {
            IPickupable item = hit.GetComponent<IPickupable>();
            if (item != null) pickupables.Add(item);

            NetworkIdentity ni = hit.GetComponent<NetworkIdentity>();
            if (ni != null)
            {
                IInteractable interactable = hit.GetComponent<IInteractable>();
                if (interactable != null) interactables.Add(interactable);
            }
        }

        // --- Handle Pickupables ---
        if (pickupables.Count > 0)
        {
            IPickupable closest = null;
            float minDist = Mathf.Infinity;
            Vector3 playerPos = transform.position;

            foreach (IPickupable item in pickupables)
            {
                float d = Vector3.Distance(playerPos, item.GameObject.transform.position);
                if (d < minDist) { minDist = d; closest = item; }
            }

            closestPickupableInRange = closest;
            objectInRange = closest.GameObject;
        }
        else
        {
            closestPickupableInRange = null;
        }

        // --- Handle Interactables ---
        if (interactables.Count > 0)
        {
            IInteractable closest = null;
            float minDist = Mathf.Infinity;
            Vector3 playerPos = transform.position;

            foreach (IInteractable item in interactables)
            {
                float d = Vector3.Distance(playerPos, item.GameObject.transform.position);
                if (d < minDist) { minDist = d; closest = item; }
            }

            closestInteractableInRange = closest;
            objectInRange = closest.GameObject;
        }
        else
        {
            closestInteractableInRange = null;
        }

        if (closestPickupableInRange == null && closestInteractableInRange == null)
            objectInRange = null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}
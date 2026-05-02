using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Mirror; // Switched from Netcode

public class PlayerInteraction : NetworkBehaviour
{
    [SerializeField] private float interactRadius = 2f;
    [SerializeField] private InputActionReference pickupAction;
    [SerializeField] private InputActionReference objectInteractAction;

    public GameObject objectInRange;
    private IPickupable closestPickupableInRange;
    private IInteractable closestInteractableInRange;

    public PlayerManager playerManager;
    public IInteractable currentlyInteractingObject;

    private void Start()
    {
        playerManager = GetComponent<PlayerManager>();
    }

    public override void OnStartLocalPlayer()
    {
        // In Mirror, we only want the actual player to listen for inputs
        pickupAction.action.Enable();
        objectInteractAction.action.Enable();

        pickupAction.action.performed += Action_performed;
        objectInteractAction.action.performed += objectInteractAction_performed;
    }

    public override void OnStopLocalPlayer()
    {
        // Clean up listeners when the client stops
        pickupAction.action.performed -= Action_performed;
        objectInteractAction.action.performed -= objectInteractAction_performed;
    }

    private void objectInteractAction_performed(InputAction.CallbackContext context)
    {
        // isLocalPlayer is the Mirror equivalent of IsOwner for the player object
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
            // This triggers the Command inside the interactable object (like the Panel)
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

        // Only the local player needs to run the detection logic for UI/Interaction
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
            float minDistance = Mathf.Infinity;
            Vector3 playerPos = transform.position;

            foreach (IPickupable item in pickupables)
            {
                float distance = Vector3.Distance(playerPos, item.GameObject.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = item;
                }
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
            float minDistance = Mathf.Infinity;
            Vector3 playerPos = transform.position;

            foreach (IInteractable item in interactables)
            {
                float distance = Vector3.Distance(playerPos, item.GameObject.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = item;
                }
            }
            closestInteractableInRange = closest;
            objectInRange = closest.GameObject;
        }
        else
        {
            closestInteractableInRange = null;
        }

        // If nothing is in range, clear the objectInRange reference
        if (closestPickupableInRange == null && closestInteractableInRange == null)
        {
            objectInRange = null;
        }

    }

    // Optional: Draw the interaction radius in the editor
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}
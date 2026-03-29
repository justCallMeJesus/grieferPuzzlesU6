using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : NetworkBehaviour
{

    [SerializeField] private float interactRadius;
    [SerializeField] private InputActionReference pickupAction;
    [SerializeField] private InputActionReference objectInteractAction;

    public GameObject objectInRange;
    private IPickupable closestPickupableInRange;
    private IInteractable closestInteractableInRange;

    public PlayerManager playerManager;

    public IInteractable currentlyInteractingObject;

    private void Start()
    {
        // get player manager
        playerManager = GetComponent<PlayerManager>();
    }

    private void OnEnable()
    {
        // listen to interaction action
        pickupAction.action.performed += Action_performed;
        objectInteractAction.action.performed += objectInteractAction_performed;
    }

    private void objectInteractAction_performed(InputAction.CallbackContext context)
    {
        if (!IsOwner) { return; }

        if(currentlyInteractingObject != null) 
        { 
            currentlyInteractingObject.OnStopInteraction(playerManager);
            currentlyInteractingObject = null;
            return; 
        }

        // try interact
        if(closestInteractableInRange != null)
        {
            if(!closestInteractableInRange.CanInteract()) return;

            closestInteractableInRange.OnInteract(playerManager);
        }
    }

    private void OnDisable()
    {
        pickupAction.action.performed -= Action_performed;
        objectInteractAction.action.performed -= objectInteractAction_performed;
    }
    private void Action_performed(InputAction.CallbackContext obj)
    {
        if(!IsOwner) { return; }
        // try interact
        if (closestPickupableInRange != null)
        {
            closestPickupableInRange.OnPickup(playerManager.inventory);
        }
    }

    private void Update()
    {
        // get all object in radius
        Collider[] hits = Physics.OverlapSphere(transform.position, interactRadius);

        List<IPickupable> pickupables = new List<IPickupable>();
        List<IInteractable> interactables = new List<IInteractable>();

        // add all interactable & pickupable objects to list
        foreach (Collider hit in hits)
        {
            // check if this object has an IPickupable component
            IPickupable item = hit.GetComponent<IPickupable>();
            if (item != null)
            {
                pickupables.Add(item);
            }
            IInteractable interactable = hit.GetComponent<IInteractable>();
            if(interactable != null)
            {
                interactables.Add(interactable);
            }

        }

        // if pickupables objects list is not 0, get closest object. else set it to null
        if( pickupables.Count > 0 )
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

            objectInRange = closest.GameObject;
            closestPickupableInRange = closest;
        }
        else
        {
            closestPickupableInRange = null;
        }

        // if interactable objects list is not 0, get closest object. else set it to null
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

            objectInRange = closest.GameObject;
            closestInteractableInRange = closest;
        }
        else
        {
            closestInteractableInRange = null;
        }


    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{

    [SerializeField] private float interactRadius;
    [SerializeField] private InputActionReference interactAction;

    public GameObject objectInRange;
    private IInteractable closestInteractableInRange;

    PlayerManager playerManager;

    private void Start()
    {
        // get player manager
        playerManager = GetComponent<PlayerManager>();
    }

    private void OnEnable()
    {
        // listen to interaction action
        interactAction.action.performed += Action_performed;
    }

    private void OnDisable()
    {
        interactAction.action.performed -= Action_performed;
    }
    private void Action_performed(InputAction.CallbackContext obj)
    {
        // try interact
        if (closestInteractableInRange != null)
        {
            closestInteractableInRange.OnInteract(playerManager.inventory);
        }

    }

    private void Update()
    {
        // get all object in radius
        Collider[] hits = Physics.OverlapSphere(transform.position, interactRadius);

        List<IInteractable> interactables = new List<IInteractable>();

        // add all interactable objects to list
        foreach (Collider hit in hits)
        {
            // check if this object has an InteractableItem component
            IInteractable item = hit.GetComponent<IInteractable>();
            if (item != null)
            {
                interactables.Add(item);
            }
        }

        // if interactbale objects list is not 0, get closest object. else set it to null
        if( interactables.Count > 0 )
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

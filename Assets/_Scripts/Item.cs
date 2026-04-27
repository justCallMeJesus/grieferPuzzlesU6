using UnityEngine;
using Mirror;

public class Item : NetworkBehaviour, IPickupable
{
    public ItemData ItemData;
    public GameObject GameObject => this.gameObject;

    // --- SyncVars (Mirror equivalent of NetworkVariable) ---

    [SyncVar(hook = nameof(OnCollectedChanged))]
    private bool isCollected = false;

    [SyncVar]
    private bool isGrounded = true;

    // Run when the object is initialized on clients
    public override void OnStartClient()
    {
        base.OnStartClient();
        // Set initial visibility based on state
        gameObject.SetActive(!isCollected);
    }

    private void OnCollectedChanged(bool oldVal, bool newVal)
    {
        gameObject.SetActive(!newVal);
    }

    // ── Grounded state ────────────────────────────────────────────────────────

    public void SetGrounded(bool grounded)
    {
        // In Mirror, if we are the server, we set it directly.
        // If client, we send a Command.
        if (isServer)
        {
            isGrounded = grounded;
        }
        else
        {
            CmdSetGrounded(grounded);
        }
    }

    [Command(requiresAuthority = false)]
    private void CmdSetGrounded(bool grounded)
    {
        isGrounded = grounded;
    }

    // ── Pickup ────────────────────────────────────────────────────────────────

    public void OnPickup(PlayerInventory player)
    {
        // Client-side early exit to prevent spamming the server
        if (isCollected || !isGrounded) return;

        if (!ItemData.largeItem)
        {
            if (!player.HasSmallSpace()) return;
            CmdPickup(player.gameObject);
        }
        else
        {
            if (!player.HasBigSpace()) return;
            CmdPickupLarge(player.gameObject);
        }
    }

    // External callers
    public void RequestPickup(PlayerInventory player) => CmdPickup(player.gameObject);
    public void RequestPickupLarge(PlayerInventory player) => CmdPickupLarge(player.gameObject);

    [Command(requiresAuthority = false)]
    private void CmdPickup(GameObject playerObj)
    {
        if (isCollected || !isGrounded) return;

        if (playerObj != null)
        {
            PlayerInventory inventory = playerObj.GetComponent<PlayerInventory>();
            if (inventory != null)
            {
                inventory.StoreSmallItem(ItemData);
                isCollected = true;
                // Mirror uses NetworkServer.Destroy to remove networked objects
                NetworkServer.Destroy(gameObject);
            }
        }
    }

    [Command(requiresAuthority = false)]
    private void CmdPickupLarge(GameObject playerObj)
    {
        if (isCollected || !isGrounded) return;

        if (playerObj != null)
        {
            PlayerInventory inventory = playerObj.GetComponent<PlayerInventory>();
            if (inventory != null)
            {
                inventory.StoreBigItem(ItemData);
                isCollected = true;
                NetworkServer.Destroy(gameObject);
            }
        }
    }
}

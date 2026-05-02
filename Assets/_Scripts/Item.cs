using UnityEngine;
using Mirror;

public class Item : NetworkBehaviour, IPickupable
{
    public ItemData ItemData;
    public GameObject GameObject => this.gameObject;

    [SyncVar(hook = nameof(OnCollectedChanged))]
    private bool isCollected = false;

    [SyncVar]
    private bool isGrounded = true;

    // Cache the renderer so we're not calling GetComponent every time
    private MeshRenderer _meshRenderer;

    public override void OnStartClient()
    {
        base.OnStartClient();
        _meshRenderer = GetComponent<MeshRenderer>();
        SetVisible(!isCollected);
    }

    private void OnCollectedChanged(bool oldVal, bool newVal)
    {
        SetVisible(!newVal);
    }

    private void SetVisible(bool visible)
    {
        if (_meshRenderer != null)
            _meshRenderer.enabled = visible;
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
            SoundManager.Instance?.PlayOneShot("ItemGrab");
        }
        else
        {
            if (!player.HasBigSpace()) return;
            CmdPickupLarge(player.gameObject);
            SoundManager.Instance?.PlayOneShot("ItemGrab");
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

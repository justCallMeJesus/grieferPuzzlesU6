using UnityEngine;
using System.Collections.Generic;
using Mirror;

public class ItemSpawnPoint : NetworkBehaviour
{
    // These indices correspond to the list in the ItemSpawnerManager
    public List<int> allowedItemIndices = new List<int>();

    // Visual helper in the editor
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }
}

using UnityEngine;
using Mirror;
using TMPro;
using Steamworks; // Requires Facepunch.Steamworks

public class PlayerColor : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private Texture2D[] playerTextures;
    [SerializeField] private Color[] nameColors; // Match these to your textures
    [SerializeField] private Renderer[] playerMeshParts;
    [SerializeField] private TMP_Text nameLabel;

    [SyncVar(hook = nameof(OnTextureChanged))]
    private int textureIndex = -1;

    [SyncVar(hook = nameof(OnNameChanged))]
    private string steamName;

    // Called on clients when the Steam Name is synced
    void OnNameChanged(string oldName, string newName)
    {
        nameLabel.text = newName;
    }

    // Called on clients when the Texture/Color index is synced
    void OnTextureChanged(int oldIndex, int newIndex)
    {
        if (newIndex >= 0 && newIndex < playerTextures.Length)
        {
            // 1. Update Mesh Textures
            Texture2D selectedTex = playerTextures[newIndex];
            foreach (Renderer mesh in playerMeshParts)
            {
                if (mesh != null) mesh.material.mainTexture = selectedTex;
            }

            // 2. Update Name Label Color
            if (newIndex < nameColors.Length)
            {
                nameLabel.color = nameColors[newIndex];
            }
        }
    }

    public override void OnStartLocalPlayer()
    {
        // Find the canvas/label and disable it so I don't see my own name
        if (nameLabel != null)
        {
            // You can either disable the text or the whole canvas
            nameLabel.gameObject.SetActive(false);
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // Force refresh the name and color when the client starts
        // even if the SyncVar hook already fired
        if (!string.IsNullOrEmpty(steamName))
        {
            OnNameChanged("", steamName);
        }

        if (textureIndex != -1)
        {
            OnTextureChanged(-1, textureIndex);
        }
    }

    [Server]
    public void SetPlayerIdentity(int index, string name)
    {
        textureIndex = index;
        steamName = name;
    }
}

using Mirror;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Placed on a GameObject with a CapsuleCollider and MeshRenderer.
/// The GameObject must be on its own dedicated layer (e.g. "PanelGate").
///
/// - No panels full    → default material, blocks all players.
/// - One panel full    → that panel's team material, pulsating alpha.
/// - Multiple full     → cycles through all filled team materials, one per pulse.
///
/// Any player who owns a filled panel has the gate's layer added to their
/// collisionIgnoreMask so PlayerMovement's CapsuleCast skips it entirely.
/// </summary>
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(MeshRenderer))]
public class PanelGate : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private PanelStateTracker panelStateTracker;
    [SerializeField] private Material defaultMaterial;

    [Header("Pulse Settings")]
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseAlphaMin = 0.3f;
    [SerializeField] private float pulseAlphaMax = 0.8f;

    private MeshRenderer meshRenderer;
    private CapsuleCollider capsuleCollider;

    // The ordered list of materials to display/cycle, owned by this client.
    private readonly List<Material> activeMaterials = new();

    // Runtime instance of the currently displayed material (never modify shared assets).
    private Material activeMaterialInstance;

    private int cycleIndex = 0;
    private float previousSine = 1f;

    // Server-only: connectionIds currently allowed to pass through.
    private readonly HashSet<int> passThroughOwners = new();

    // -- Unity Lifecycle --

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        capsuleCollider = GetComponent<CapsuleCollider>();
    }

    private void OnEnable()
    {
        if (panelStateTracker != null)
            panelStateTracker.OnFilledPanelsChanged += OnFilledPanelsChanged;

        ApplyDefault();
    }

    private void OnDisable()
    {
        if (panelStateTracker != null)
            panelStateTracker.OnFilledPanelsChanged -= OnFilledPanelsChanged;

        DestroyActiveMaterialInstance();
    }

    private void Update()
    {
        if (activeMaterials.Count == 0) return;

        float sine = Mathf.Sin(Time.time * pulseSpeed);
        float alpha = Mathf.Lerp(pulseAlphaMin, pulseAlphaMax, (sine + 1f) * 0.5f);

        if (activeMaterialInstance != null)
        {
            Color c = activeMaterialInstance.color;
            c.a = alpha;
            activeMaterialInstance.color = c;
        }

        // Advance once per full pulse at the trough crossing.
        if (activeMaterials.Count > 1 && previousSine < 0f && sine >= 0f)
        {
            cycleIndex = (cycleIndex + 1) % activeMaterials.Count;
            SetDisplayMaterial(activeMaterials[cycleIndex]);
        }

        previousSine = sine;
    }

    // -- Server: react to panel state changes --

    private void OnFilledPanelsChanged()
    {
        if (!isServer) return;

        // Build the new owner set and material name list from the tracker.
        HashSet<int> newOwners = new();
        List<string> materialNames = new();

        foreach (var kvp in panelStateTracker.FilledPanels)
        {
            var info = kvp.Value;
            if (info.OwnerId != -1)
                newOwners.Add(info.OwnerId);
            if (info.TeamColor != null)
                materialNames.Add(info.TeamColor.name);
        }

        // Update passthrough: grant to new owners, revoke from removed ones.
        foreach (int connId in newOwners)
            if (!passThroughOwners.Contains(connId))
                SetPassthroughForConnection(connId, true);

        foreach (int connId in passThroughOwners)
            if (!newOwners.Contains(connId))
                SetPassthroughForConnection(connId, false);

        passThroughOwners.Clear();
        foreach (int id in newOwners)
            passThroughOwners.Add(id);

        // Push visual state to all clients.
        // Pass material names as a string array — clients resolve them locally
        // from their Panel references, which are always available via the scene.
        if (materialNames.Count == 0)
            RpcApplyDefault();
        else
            RpcApplyMaterials(materialNames.ToArray());
    }

    // -- Passthrough (server → specific client) --

    private void SetPassthroughForConnection(int connId, bool ignore)
    {
        if (!NetworkServer.connections.TryGetValue(connId, out NetworkConnectionToClient conn)) return;
        TargetSetPassthrough(conn, gameObject.layer, ignore);
    }

    [TargetRpc]
    private void TargetSetPassthrough(NetworkConnection target, int gateLayer, bool ignore)
    {
        PlayerMovement pm = NetworkClient.localPlayer?.GetComponent<PlayerMovement>();
        if (pm == null) return;

        if (ignore)
            pm.collisionIgnoreMask |= (1 << gateLayer);
        else
            pm.collisionIgnoreMask &= ~(1 << gateLayer);
    }

    // -- Visual RPCs (server → all clients) --

    [ClientRpc]
    private void RpcApplyDefault()
    {
        activeMaterials.Clear();
        cycleIndex = 0;
        ApplyDefault();
    }

    [ClientRpc]
    private void RpcApplyMaterials(string[] materialNames)
    {
        // Resolve material names to actual Material assets via the Panel references
        // on this client. Panels are scene objects so their serialized fields
        // (teamColor) are always available regardless of who has them open.
        activeMaterials.Clear();
        cycleIndex = 0;

        foreach (string matName in materialNames)
        {
            Material mat = FindMaterialByName(matName);
            if (mat != null)
                activeMaterials.Add(mat);
        }

        if (activeMaterials.Count == 0) { ApplyDefault(); return; }

        SetDisplayMaterial(activeMaterials[0]);
    }

    // -- Material Helpers --

    private void SetDisplayMaterial(Material source)
    {
        DestroyActiveMaterialInstance();
        activeMaterialInstance = new Material(source);
        meshRenderer.material = activeMaterialInstance;
    }

    private void ApplyDefault()
    {
        DestroyActiveMaterialInstance();
        meshRenderer.material = defaultMaterial;
        activeMaterialInstance = null;
    }

    private void DestroyActiveMaterialInstance()
    {
        if (activeMaterialInstance != null)
        {
            Destroy(activeMaterialInstance);
            activeMaterialInstance = null;
        }
    }

    /// <summary>
    /// Finds a Material asset by name by scanning the Panel references
    /// in the PanelStateTracker. This avoids a Resources.Load dependency.
    /// </summary>
    private Material FindMaterialByName(string matName)
    {
        // Access panels through the tracker's serialized field via reflection
        // would be messy — instead we just scan all Panel scene objects directly.
        foreach (Panel panel in FindObjectsByType<Panel>(FindObjectsSortMode.None))
        {
            if (panel.teamColor != null && panel.teamColor.name == matName)
                return panel.teamColor;
        }
        return null;
    }
}
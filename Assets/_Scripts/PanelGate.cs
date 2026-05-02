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
/// The layer is removed again if their panel is no longer full.
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

    // Runtime material instance — never modify the shared asset directly.
    private Material activeMaterialInstance;

    // Snapshot of filled panels rebuilt whenever OnFilledPanelsChanged fires.
    private readonly List<PanelStateTracker.FilledPanelInfo> filledList = new();

    // Which entry in filledList is currently displayed.
    private int cycleIndex = 0;

    // Tracks the previous pulse phase so we can detect a completed pulse cycle.
    private float previousSine = 1f;

    // connectionIds of owners currently allowed to pass through.
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
        if (filledList.Count == 0) return;

        float sine = Mathf.Sin(Time.time * pulseSpeed);
        float alpha = Mathf.Lerp(pulseAlphaMin, pulseAlphaMax, (sine + 1f) * 0.5f);

        if (activeMaterialInstance != null)
        {
            Color c = activeMaterialInstance.color;
            c.a = alpha;
            activeMaterialInstance.color = c;
        }

        // Advance the cycle index once per full pulse (at the trough crossing).
        if (filledList.Count > 1 && previousSine < 0f && sine >= 0f)
        {
            cycleIndex = (cycleIndex + 1) % filledList.Count;
            ApplyMaterial(filledList[cycleIndex].TeamColor);
        }

        previousSine = sine;
    }

    // -- State Management --

    private void OnFilledPanelsChanged()
    {
        if (!isServer) return;

        HashSet<int> newOwners = new();
        foreach (var kvp in panelStateTracker.FilledPanels)
        {
            if (kvp.Value.OwnerId != -1)
                newOwners.Add(kvp.Value.OwnerId);
        }

        // Grant passthrough to newly filled owners.
        foreach (int connId in newOwners)
        {
            if (!passThroughOwners.Contains(connId))
                SetPassthroughForConnection(connId, true);
        }

        // Revoke passthrough from owners whose panel is no longer full.
        foreach (int connId in passThroughOwners)
        {
            if (!newOwners.Contains(connId))
                SetPassthroughForConnection(connId, false);
        }

        passThroughOwners.Clear();
        foreach (int id in newOwners)
            passThroughOwners.Add(id);

        // Rebuild the visual list.
        filledList.Clear();
        foreach (var kvp in panelStateTracker.FilledPanels)
            filledList.Add(kvp.Value);

        if (filledList.Count == 0)
        {
            RpcApplyDefault();
            return;
        }

        cycleIndex = Mathf.Clamp(cycleIndex, 0, filledList.Count - 1);
        RpcApplyMaterial(filledList[cycleIndex].TeamColor?.name ?? "");
    }

    // -- Passthrough Helpers (Server to Client) --

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

    // -- Visual RPCs --

    [ClientRpc]
    private void RpcApplyDefault()
    {
        filledList.Clear();
        ApplyDefault();
    }

    [ClientRpc]
    private void RpcApplyMaterial(string materialName)
    {
        // Rebuild filledList from the tracker on the client side.
        filledList.Clear();
        foreach (var kvp in panelStateTracker.FilledPanels)
            filledList.Add(kvp.Value);

        cycleIndex = Mathf.Clamp(cycleIndex, 0, filledList.Count - 1);

        // Match by name so all clients display the same material.
        Material mat = null;
        foreach (var info in filledList)
        {
            if (info.TeamColor != null && info.TeamColor.name == materialName)
            {
                mat = info.TeamColor;
                break;
            }
        }

        ApplyMaterial(mat ?? (filledList.Count > 0 ? filledList[0].TeamColor : null));
    }

    // -- Material Helpers --

    private void ApplyDefault()
    {
        DestroyActiveMaterialInstance();
        meshRenderer.material = defaultMaterial;
        activeMaterialInstance = null;
    }

    private void ApplyMaterial(Material source)
    {
        if (source == null) { ApplyDefault(); return; }

        DestroyActiveMaterialInstance();
        activeMaterialInstance = new Material(source);
        meshRenderer.material = activeMaterialInstance;
    }

    private void DestroyActiveMaterialInstance()
    {
        if (activeMaterialInstance != null)
        {
            Destroy(activeMaterialInstance);
            activeMaterialInstance = null;
        }
    }
}
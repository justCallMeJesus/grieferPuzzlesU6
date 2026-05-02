using Mirror;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks which of the map's four panels are currently full,
/// along with the owner and team colour of each filled panel.
/// Subscribe to <see cref="OnFilledPanelsChanged"/> to react to changes.
/// </summary>
public class PanelStateTracker : MonoBehaviour
{
    public readonly struct FilledPanelInfo
    {
        public readonly Panel Panel;
        public readonly int OwnerId;
        public readonly Material TeamColor;

        public FilledPanelInfo(Panel panel, int ownerId, Material teamColor)
        {
            Panel = panel;
            OwnerId = ownerId;
            TeamColor = teamColor;
        }
    }

    [SerializeField] private Panel[] panels = new Panel[4];

    /// <summary>Raised whenever the set of filled panels changes.</summary>
    public event System.Action OnFilledPanelsChanged;

    private readonly Dictionary<Panel, FilledPanelInfo> filledPanels = new();

    // -- Public read-only access --

    public IReadOnlyDictionary<Panel, FilledPanelInfo> FilledPanels => filledPanels;
    public int FilledCount => filledPanels.Count;

    // -- Unity lifecycle --

    private void OnEnable()
    {
        foreach (Panel panel in panels)
            Subscribe(panel);
    }

    private void OnDisable()
    {
        foreach (Panel panel in panels)
            Unsubscribe(panel);
    }

    // -- Subscription helpers --

    private void Subscribe(Panel panel)
    {
        if (panel == null) return;
        panel.OnPanelFull += () => HandlePanelFull(panel);
        panel.OnPanelNoLongerFull += () => HandlePanelNoLongerFull(panel);
    }

    private void Unsubscribe(Panel panel)
    {
        if (panel == null) return;
        panel.OnPanelFull -= () => HandlePanelFull(panel);
        panel.OnPanelNoLongerFull -= () => HandlePanelNoLongerFull(panel);
    }

    // -- Event handlers --

    private void HandlePanelFull(Panel panel)
    {
        int ownerId = panel.GetOwnerId();

        filledPanels[panel] = new FilledPanelInfo(panel, ownerId, panel.teamColor);

        Debug.Log($"[PanelStateTracker] Panel '{panel.name}' full — owner {ownerId}, colour '{panel.teamColor?.name}'.");

        OnFilledPanelsChanged?.Invoke();
    }

    private void HandlePanelNoLongerFull(Panel panel)
    {
        if (!filledPanels.Remove(panel)) return;

        Debug.Log($"[PanelStateTracker] Panel '{panel.name}' no longer full.");

        OnFilledPanelsChanged?.Invoke();
    }
}
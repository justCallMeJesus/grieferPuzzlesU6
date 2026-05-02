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

    /// <summary>
    /// Holds stable delegate references for a single panel so they can be
    /// cleanly added and removed without lambda capture issues.
    /// </summary>
    private class PanelListener
    {
        private readonly Panel panel;
        private readonly PanelStateTracker tracker;

        public PanelListener(Panel panel, PanelStateTracker tracker)
        {
            this.panel = panel;
            this.tracker = tracker;
        }

        public void Subscribe()
        {
            panel.OnPanelFull += OnFull;
            panel.OnPanelNoLongerFull += OnNoLongerFull;
        }

        public void Unsubscribe()
        {
            panel.OnPanelFull -= OnFull;
            panel.OnPanelNoLongerFull -= OnNoLongerFull;
        }

        private void OnFull() => tracker.HandlePanelFull(panel);
        private void OnNoLongerFull() => tracker.HandlePanelNoLongerFull(panel);
    }

    [SerializeField] private Panel[] panels = new Panel[4];

    /// <summary>Raised whenever the set of filled panels changes.</summary>
    public event System.Action OnFilledPanelsChanged;

    private readonly Dictionary<Panel, FilledPanelInfo> filledPanels = new();
    private readonly List<PanelListener> listeners = new();

    // -- Public read-only access --

    public IReadOnlyDictionary<Panel, FilledPanelInfo> FilledPanels => filledPanels;
    public int FilledCount => filledPanels.Count;

    // -- Unity lifecycle --

    private void OnEnable()
    {
        listeners.Clear();
        foreach (Panel panel in panels)
        {
            if (panel == null) continue;
            var listener = new PanelListener(panel, this);
            listener.Subscribe();
            listeners.Add(listener);

            // Catch panels that were already full before we subscribed
            // (e.g. a client that connects mid-game receives the SyncVar value
            // immediately, but the hook has already fired on the server).
            if (panel.IsFull)
                HandlePanelFull(panel);
        }
    }

    private void OnDisable()
    {
        foreach (PanelListener listener in listeners)
            listener.Unsubscribe();
        listeners.Clear();
    }

    // -- Event handlers --

    private void HandlePanelFull(Panel panel)
    {
        int ownerId = panel.GetOwnerId();
        filledPanels[panel] = new FilledPanelInfo(panel, ownerId, panel.teamColor);

        Debug.Log($"[PanelStateTracker] Panel '{panel.name}' full � owner {ownerId}, colour '{panel.teamColor?.name}'.");
        OnFilledPanelsChanged?.Invoke();
    }

    private void HandlePanelNoLongerFull(Panel panel)
    {
        if (!filledPanels.Remove(panel)) return;

        Debug.Log($"[PanelStateTracker] Panel '{panel.name}' no longer full.");
        OnFilledPanelsChanged?.Invoke();
    }
}
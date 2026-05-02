using UnityEngine;

/// <summary>
/// Attach this to a persistent Canvas GameObject (NOT the player prefab).
///
/// It wires itself up automatically when the local player spawns via the
/// static PlayerInteraction.OnLocalPlayerSpawned event, so there is no
/// Inspector reference to set and it works correctly with Mirror's
/// runtime instantiation and multiple players.
/// </summary>
public class UITooltip : MonoBehaviour
{
    [Header("Tooltip GameObjects")]
    [Tooltip("Shown when a pickupable item is in range.")]
    [SerializeField] private GameObject pickupTooltip;

    [Tooltip("Shown when an interactable object is in range.")]
    [SerializeField] private GameObject interactTooltip;

    [Header("Fade Settings")]
    [Tooltip("Alpha fade duration in seconds. 0 = instant.")]
    [SerializeField] private float fadeDuration = 0.15f;

    private PlayerInteraction _player;

    private CanvasGroup _pickupGroup;
    private CanvasGroup _interactGroup;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (fadeDuration > 0f)
        {
            _pickupGroup = GetOrAddCanvasGroup(pickupTooltip);
            _interactGroup = GetOrAddCanvasGroup(interactTooltip);
        }

        HideAll(instant: true);
    }

    private void OnEnable()
    {
        // Subscribe to the event fired by PlayerInteraction when the local
        // player spawns. Safe to call before any player exists.
        PlayerInteraction.OnLocalPlayerSpawned += OnLocalPlayerSpawned;
    }

    private void OnDisable()
    {
        PlayerInteraction.OnLocalPlayerSpawned -= OnLocalPlayerSpawned;
        _player = null;
    }

    private void OnLocalPlayerSpawned(PlayerInteraction localPlayer)
    {
        _player = localPlayer;
    }

    // -------------------------------------------------------------------------
    // Update
    // -------------------------------------------------------------------------

    private void Update()
    {
        // No local player yet (still loading / between scenes)
        if (_player == null)
        {
            HideAll(instant: true);
            return;
        }

        bool showPickup = _player.closestPickupableInRange != null;
        bool showInteract = _player.closestInteractableInRange != null;

        if (fadeDuration > 0f)
        {
            Fade(_pickupGroup, pickupTooltip, showPickup ? 1f : 0f);
            Fade(_interactGroup, interactTooltip, showInteract ? 1f : 0f);
        }
        else
        {
            SetVisible(pickupTooltip, _pickupGroup, showPickup, instant: true);
            SetVisible(interactTooltip, _interactGroup, showInteract, instant: true);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void Fade(CanvasGroup group, GameObject tooltipGO, float target)
    {
        if (group == null || tooltipGO == null) return;

        if (target > 0f && !tooltipGO.activeSelf)
            tooltipGO.SetActive(true);

        group.alpha = Mathf.MoveTowards(group.alpha, target, Time.deltaTime / fadeDuration);

        if (group.alpha <= 0f && tooltipGO.activeSelf)
            tooltipGO.SetActive(false);
    }

    private void SetVisible(GameObject tooltipGO, CanvasGroup group, bool visible, bool instant)
    {
        if (tooltipGO == null) return;
        tooltipGO.SetActive(visible);
        if (group != null) group.alpha = visible ? 1f : 0f;
    }

    private void HideAll(bool instant)
    {
        SetVisible(pickupTooltip, _pickupGroup, false, instant);
        SetVisible(interactTooltip, _interactGroup, false, instant);
    }

    private static CanvasGroup GetOrAddCanvasGroup(GameObject go)
    {
        if (go == null) return null;
        var cg = go.GetComponent<CanvasGroup>();
        return cg != null ? cg : go.AddComponent<CanvasGroup>();
    }
}
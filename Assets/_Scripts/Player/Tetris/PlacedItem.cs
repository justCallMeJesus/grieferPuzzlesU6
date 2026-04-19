using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class PlacedItem : MonoBehaviour, IStorable, ICanvasRaycastFilter
{
    public ItemData _data;
    public static PlacedItem Create(
        RectTransform container,
        Vector2 anchoredPosition,
        Vector2Int origin,
        ItemTetrisSO.Dir dir,
        ItemTetrisSO itemSO,
        float cellSize)
    {
        GameObject go = new GameObject(itemSO.itemName, typeof(RectTransform), typeof(CanvasGroup));
        RectTransform rt = go.GetComponent<RectTransform>();

        rt.SetParent(container, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = new Vector2(itemSO.width * cellSize, itemSO.height * cellSize);

        // Sprite child fills the rect
        GameObject imgGo = new GameObject("Sprite", typeof(Image));
        RectTransform imgRt = imgGo.GetComponent<RectTransform>();
        imgRt.SetParent(rt, false);
        imgRt.anchorMin = Vector2.zero;
        imgRt.anchorMax = Vector2.one;
        imgRt.offsetMin = Vector2.zero;
        imgRt.offsetMax = Vector2.zero;

        Image img = imgGo.GetComponent<Image>();
        img.sprite = itemSO.sprite;
        img.preserveAspect = true;

        PlacedItem placed = go.AddComponent<PlacedItem>();
        placed.itemSO = itemSO;
        placed.origin = origin;
        placed.dir = dir;
        placed.cellSize = cellSize;
        placed._data = itemSO.itemData;

        return placed;
    }

    public ItemTetrisSO itemSO { get; private set; }
    public Vector2Int origin { get; private set; }
    public ItemTetrisSO.Dir dir { get; private set; }
    private float cellSize;

    // Cached set of local-space cell positions for IsRaycastLocationValid.
    // Rebuilt whenever origin/dir/cellSize change.
    private readonly HashSet<Vector2Int> _localCellSet = new HashSet<Vector2Int>();

    public List<Vector2Int> GetGridPositionList() =>
        itemSO.GetGridPositionList(origin, dir);

    public void SetOriginAndDir(Vector2Int newOrigin, ItemTetrisSO.Dir newDir)
    {
        origin = newOrigin;
        dir = newDir;
        RebuildCellSet();
    }

    // ── ICanvasRaycastFilter ──────────────────────────────────────────────

    /// <summary>
    /// Called by Unity's GraphicRaycaster for every pointer event.
    /// Returns true only when <paramref name="sp"/> lands on a cell that is
    /// actually part of this item's shape, giving pixel-perfect hit testing
    /// for non-rectangular (e.g. L-shaped) items.
    /// </summary>
    public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
    {
        RectTransform rt = GetComponent<RectTransform>();

        // Convert screen point → local point of this RectTransform
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rt, sp, eventCamera, out Vector2 local))
            return false;

        // local is relative to pivot (bottom-left, since pivot = (0,0)).
        // Determine which cell column/row this point falls in.
        int cellX = Mathf.FloorToInt(local.x / cellSize);
        int cellY = Mathf.FloorToInt(local.y / cellSize);

        return _localCellSet.Contains(new Vector2Int(cellX, cellY));
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the set of local cell coordinates that IsRaycastLocationValid
    /// checks against.
    ///
    /// Key insight: the RectTransform is always sized width × height (unrotated)
    /// and visually rotated via Quaternion.Euler(0,0,-angle).
    /// RectTransformUtility.ScreenPointToLocalPointInRectangle returns a point
    /// in the rect's OWN pre-rotation local space — i.e. always the unrotated
    /// width × height coordinate frame, regardless of how the GameObject is
    /// rotated in the scene.
    ///
    /// Therefore the cell set must also be in that same unrotated frame, which
    /// is simply Dir.Down (no rotation applied).
    /// </summary>
    private void RebuildCellSet()
    {
        _localCellSet.Clear();
        if (itemSO == null) return;

        // Always Dir.Down: gives cells in the rect's own unrotated local space.
        foreach (var localCell in itemSO.GetGridPositionList(Vector2Int.zero, ItemTetrisSO.Dir.Down))
            _localCellSet.Add(localCell);
    }

    public void DestroySelf() => Destroy(gameObject);

    public void ConfigureNewBlock()
    {
        InventoryDragHandler handler = GetComponent<InventoryDragHandler>();
        handler.OnBeginDragNewBlock();
    }

    public ItemData GetItemData()
    {
        return _data;
    }

    // ── Unity lifecycle ───────────────────────────────────────────────────

    private void Awake()
    {
        // cellSize and itemSO are set by Create(), so RebuildCellSet() is
        // called from SetOriginAndDir() which is always called right after
        // Create(). Nothing extra needed here.
    }
}
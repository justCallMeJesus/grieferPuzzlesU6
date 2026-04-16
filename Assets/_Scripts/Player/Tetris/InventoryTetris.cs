using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Core inventory component. Manages the data grid and placed item visuals.
/// One per inventory panel (player backpack, chest, etc.).
///
/// SETUP:
///   1. Add to a UI GameObject that has a RectTransform.
///   2. Create a child named "ItemContainer" (RectTransform, no layout group).
///   3. Assign itemSODatabase in the Inspector.
///   4. Optionally attach InventoryBackground on a sibling for the grid visual.
///   5. Optionally assign ownerPlayer to restrict access to a specific player.
/// </summary>
public class InventoryTetris : MonoBehaviour
{

    // ── Events ────────────────────────────────────────────────────────────

    public event Action<PlacedItem> OnItemPlaced;
    public event Action<PlacedItem> OnItemRemoved;

    /// <summary>
    /// Fired once when every cell in the grid is occupied.
    /// Resets when any item is removed so it can fire again after the next fill cycle.
    /// </summary>
    public event Action OnGridFull;

    // ── Inspector ─────────────────────────────────────────────────────────

    [Header("Grid settings")]
    [SerializeField] private int gridWidth = 10;
    [SerializeField] private int gridHeight = 10;
    [SerializeField] private float cellSize = 50f;

    [Header("Item database")]
    [Tooltip("All ItemTetrisSO assets in your project. Used for save/load by name.")]
    [SerializeField] private ItemTetrisSO[] itemSODatabase;

    // ── Private state ─────────────────────────────────────────────────────

    private Grid<GridCell> grid;
    private RectTransform itemContainer;

    /// <summary>
    /// Prevents OnGridFull from firing more than once per fill cycle.
    /// Resets to false whenever any item is removed.
    /// </summary>
    private bool gridFullEventFired = false;

    // ── Static / singleton state ──────────────────────────────────────────

    public static InventoryTetris Instance { get; private set; }

    /// <summary>True while any panel is open locally.</summary>
    public static bool IsPanelOpen { get; private set; }

    /// <summary>
    /// True when the local player has write-access to the currently open panel.
    /// False when viewing as a non-owner (read-only).
    /// Set by Panel before activating the panel UI.
    /// </summary>
    public static bool IsLocalPlayerEditor { get; private set; }

    public void SetPanelIsOpen(bool state)
    {
        IsPanelOpen = state;
    }

    /// <summary>Called by Panel when granting access to indicate edit vs view mode.</summary>
    public void SetLocalPlayerEditor(bool canEdit)
    {
        IsLocalPlayerEditor = canEdit;
    }

    // ── Inner type ────────────────────────────────────────────────────────

    public class GridCell
    {
        private Grid<GridCell> grid;
        private int x, y;
        private PlacedItem occupant;

        public GridCell(Grid<GridCell> grid, int x, int y)
        {
            this.grid = grid;
            this.x = x;
            this.y = y;
        }

        public bool IsEmpty() => occupant == null;
        public PlacedItem GetOccupant() => occupant;

        public void Occupy(PlacedItem item)
        {
            occupant = item;
            grid.TriggerGridObjectChanged(x, y);
        }

        public void Clear()
        {
            occupant = null;
            grid.TriggerGridObjectChanged(x, y);
        }
    }

    // ── Unity lifecycle ───────────────────────────────────────────────────

    private void Awake()
    {
        grid = new Grid<GridCell>(
            gridWidth, gridHeight, cellSize, Vector3.zero,
            (g, x, y) => new GridCell(g, x, y));

        itemContainer = transform.Find("ItemContainer")?.GetComponent<RectTransform>();
        if (itemContainer == null)
            Debug.LogError($"[InventoryTetris] '{name}' needs a child called 'ItemContainer'.");

        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Public getters ────────────────────────────────────────────────────

    public Grid<GridCell> GetGrid() => grid;
    public RectTransform GetItemContainer() => itemContainer;
    public float GetCellSize() => cellSize;

    /// <summary>Converts a local point inside ItemContainer to a grid coordinate.</summary>
    public Vector2Int GetGridPosition(Vector2 localPoint)
    {
        grid.GetXY(localPoint, out int x, out int y);
        return new Vector2Int(x, y);
    }

    public bool IsValidGridPosition(Vector2Int pos) =>
        grid.IsValidGridPosition(pos);

    // ── Placement ─────────────────────────────────────────────────────────

    /// <summary>
    /// Tries to place <paramref name="itemSO"/> at <paramref name="origin"/> with rotation <paramref name="dir"/>.
    /// Returns the created PlacedItem on success, null on failure.
    /// </summary>
    public PlacedItem TryPlaceItem(ItemTetrisSO itemSO, Vector2Int origin, ItemTetrisSO.Dir dir)
    {
        Debug.Log($"TryPlaceItem: origin={origin}, dir={dir}");
        List<Vector2Int> debugPositions = itemSO.GetGridPositionList(origin, dir);
        foreach (var p in debugPositions)
            Debug.Log($"  needs cell {p}, valid={grid.IsValidGridPosition(p)}, empty={(grid.IsValidGridPosition(p) ? grid.GetGridObject(p).IsEmpty() : false)}");

        List<Vector2Int> positions = itemSO.GetGridPositionList(origin, dir);

        // Validate every required cell
        foreach (var pos in positions)
        {
            if (!grid.IsValidGridPosition(pos)) return null;
            if (!grid.GetGridObject(pos).IsEmpty()) return null;
        }

        // Calculate visual position
        Vector2Int rotOffset = itemSO.GetRotationOffset(dir);
        Vector2 anchoredPos = (Vector2)grid.GetWorldPosition(origin.x, origin.y)
                                   + new Vector2(rotOffset.x, rotOffset.y) * cellSize;

        // Spawn visual
        PlacedItem placed = PlacedItem.Create(itemContainer, anchoredPos, origin, dir, itemSO, cellSize);
        placed.transform.rotation = Quaternion.Euler(0, 0, -itemSO.GetRotationAngle(dir));
        placed.SetOriginAndDir(origin, dir);

        var dragHandler = placed.gameObject.AddComponent<InventoryDragHandler>();
        dragHandler.Init(this);

        // Mark grid cells
        foreach (var pos in positions)
            grid.GetGridObject(pos).Occupy(placed);

        OnItemPlaced?.Invoke(placed);

        // Check if this placement completed the grid
        CheckGridFull();

        return placed;
    }

    // ── Removal ───────────────────────────────────────────────────────────

    /// <summary>
    /// Removes whichever item is at the given grid position.
    /// Returns the removed PlacedItem (caller may re-place it), or null if empty.
    /// The visual is NOT destroyed — caller decides what to do with it.
    /// </summary>
    public PlacedItem PickUpItemAt(Vector2Int gridPos)
    {
        if (!grid.IsValidGridPosition(gridPos)) return null;

        PlacedItem item = grid.GetGridObject(gridPos).GetOccupant();
        if (item == null) return null;

        var cellsToClear = new List<Vector2Int>();
        for (int x = 0; x < grid.GetWidth(); x++)
            for (int y = 0; y < grid.GetHeight(); y++)
                if (grid.GetGridObject(x, y).GetOccupant() == item)
                    cellsToClear.Add(new Vector2Int(x, y));

        foreach (var cell in cellsToClear)
            grid.GetGridObject(cell).Clear();

        gridFullEventFired = false;
        OnItemRemoved?.Invoke(item);
        return item;
    }

    /// <summary>Removes and destroys the item at gridPos.</summary>
    public void DeleteItemAt(Vector2Int gridPos)
    {
        PlacedItem item = PickUpItemAt(gridPos);
        item?.DestroySelf();
    }

    // ── Save / Load ───────────────────────────────────────────────────────

    [Serializable]
    private struct SaveEntry
    {
        public string itemName;
        public int originX, originY;
        public ItemTetrisSO.Dir dir;
    }
    [Serializable]
    private struct SaveData
    {
        public List<SaveEntry> entries;
    }

    /// <summary>Serialises the current inventory state to a JSON string.</summary>
    public string Save()
    {
        var seen = new HashSet<PlacedItem>();
        var entries = new List<SaveEntry>();

        for (int x = 0; x < grid.GetWidth(); x++)
        {
            for (int y = 0; y < grid.GetHeight(); y++)
            {
                PlacedItem item = grid.GetGridObject(x, y).GetOccupant();
                if (item != null && seen.Add(item))
                {
                    entries.Add(new SaveEntry
                    {
                        itemName = item.itemSO.name,
                        originX = item.origin.x,
                        originY = item.origin.y,
                        dir = item.dir,
                    });
                }
            }
        }

        return JsonUtility.ToJson(new SaveData { entries = entries });
    }

    /// <summary>Clears the inventory and restores state from a JSON string.</summary>
    public void Load(string json)
    {
        ClearAll();
        SaveData data = JsonUtility.FromJson<SaveData>(json);
        foreach (var e in data.entries)
        {
            ItemTetrisSO so = GetSOByName(e.itemName);
            if (so == null) { Debug.LogWarning($"[InventoryTetris] Unknown item '{e.itemName}'"); continue; }
            TryPlaceItem(so, new Vector2Int(e.originX, e.originY), e.dir);
        }
    }

    /// <summary>Removes and destroys all items.</summary>
    public void ClearAll()
    {
        var seen = new HashSet<PlacedItem>();
        for (int x = 0; x < grid.GetWidth(); x++)
        {
            for (int y = 0; y < grid.GetHeight(); y++)
            {
                PlacedItem item = grid.GetGridObject(x, y).GetOccupant();
                if (item != null && seen.Add(item))
                {
                    ClearCells(item);
                    item.DestroySelf();
                }
            }
        }

        // Reset so OnGridFull can fire again after the next fill cycle
        gridFullEventFired = false;
    }

    // ── Grid-full check ───────────────────────────────────────────────────

    /// <summary>Returns true if every cell in the grid is currently occupied.</summary>
    public bool IsGridFull()
    {
        for (int x = 0; x < grid.GetWidth(); x++)
            for (int y = 0; y < grid.GetHeight(); y++)
                if (grid.GetGridObject(x, y).IsEmpty())
                    return false;
        return true;
    }

    /// <summary>
    /// Called after every successful placement.
    /// Fires OnGridFull once per fill cycle; resets when any item is removed.
    /// </summary>
    private void CheckGridFull()
    {
        if (gridFullEventFired) return;
        if (!IsGridFull()) return;

        gridFullEventFired = true;
        Debug.Log("[InventoryTetris] Grid is full!");
        OnGridFull?.Invoke();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void ClearCells(PlacedItem item)
    {
        foreach (var pos in item.GetGridPositionList())
            grid.GetGridObject(pos)?.Clear();
    }

    private ItemTetrisSO GetSOByName(string soName)
    {
        foreach (var so in itemSODatabase)
            if (so.name == soName) return so;
        return null;
    }

    public Vector2Int GetActualOrigin(PlacedItem item)
    {
        int minX = int.MaxValue, minY = int.MaxValue;
        for (int x = 0; x < grid.GetWidth(); x++)
            for (int y = 0; y < grid.GetHeight(); y++)
                if (grid.GetGridObject(x, y).GetOccupant() == item)
                {
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                }
        return new Vector2Int(minX, minY);
    }

    public void PickUpItem(PlacedItem item)
    {
        var cellsToClear = new List<Vector2Int>();
        for (int x = 0; x < grid.GetWidth(); x++)
            for (int y = 0; y < grid.GetHeight(); y++)
                if (grid.GetGridObject(x, y).GetOccupant() == item)
                    cellsToClear.Add(new Vector2Int(x, y));

        foreach (var cell in cellsToClear)
            grid.GetGridObject(cell).Clear();

        gridFullEventFired = false;
        OnItemRemoved?.Invoke(item);
    }

    public PlacedItem SpawnItemAtMouse(ItemTetrisSO itemSO)
    {
        PlacedItem spawned = PlacedItem.Create(
            itemContainer, Vector2.zero, Vector2Int.zero,
            ItemTetrisSO.Dir.Down, itemSO, cellSize);

        spawned.transform.rotation = Quaternion.Euler(0, 0, 0);

        var dragHandler = spawned.gameObject.AddComponent<InventoryDragHandler>();
        dragHandler.Init(this);

        InventoryDragDropSystem.Instance.BeginDragNewItem(this, spawned);
        spawned.ConfigureNewBlock();

        return spawned;
    }
}
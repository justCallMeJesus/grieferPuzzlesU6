using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject describing one item's shape, sprite and display name.
/// Create via Assets > Create > Inventory Tetris > Item.
///
/// HOW TO DEFINE A SHAPE:
///   width / height set the bounding box.
///   cells lists which (x, y) offsets within that box are actually occupied.
///   Example – L-shape (2 wide, 3 tall):
///     width = 2, height = 3
///     cells = [(0,0),(0,1),(0,2),(1,2)]
///
///   Leave cells empty to auto-fill the entire bounding box (solid rectangle).
/// </summary>
[CreateAssetMenu(menuName = "Inventory Tetris/Item")]
public class ItemTetrisSO : ScriptableObject
{

    // ── Inspector fields ──────────────────────────────────────────────────

    [Tooltip("Display name shown in tooltips.")]
    public string itemName;

    [Tooltip("Sprite rendered for this item on the inventory grid.")]
    public Sprite sprite;

    [Tooltip("Bounding-box width in grid cells.")]
    public int width = 1;

    [Tooltip("Bounding-box height in grid cells.")]
    public int height = 1;

    [Tooltip("Which cells within the bounding box are occupied. Leave empty = solid rectangle.")]
    public List<Vector2Int> cells = new();

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the list of world-grid positions this item occupies
    /// when its origin is at <paramref name="placedOrigin"/> and rotated by <paramref name="dir"/>.
    /// </summary>
    public List<Vector2Int> GetGridPositionList(Vector2Int placedOrigin, Dir dir)
    {
        var list = new List<Vector2Int>();
        foreach (var cell in GetLocalCells())
        {
            Vector2Int rotated = Rotate(cell, dir);
            list.Add(placedOrigin + rotated);
        }
        return list;
    }

    /// <summary>
    /// The rotation offset that must be added to the world position so the item
    /// renders correctly after rotation (keeps the visual inside the placed origin cell).
    /// </summary>
    public Vector2Int GetRotationOffset(Dir dir)
    {
        return dir switch
        {
            Dir.Down => new Vector2Int(0, 0),
            Dir.Left => new Vector2Int(0, width - 1),
            Dir.Up => new Vector2Int(width - 1, height - 1),
            Dir.Right => new Vector2Int(height - 1, 0),
            _ => Vector2Int.zero,
        };
    }

    /// <summary>
    /// Returns the effective width and height of the item after rotation.
    /// A 2×3 item rotated 90° becomes 3×2.
    /// </summary>
    public Vector2Int GetRotatedSize(Dir dir)
    {
        return (dir == Dir.Left || dir == Dir.Right)
            ? new Vector2Int(height, width)
            : new Vector2Int(width, height);
    }

    /// <summary>Rotation in degrees for Quaternion.Euler(0,0,-angle).</summary>
    public float GetRotationAngle(Dir dir)
    {
        return dir switch
        {
            Dir.Down => 0f,
            Dir.Left => 90f,
            Dir.Up => 180f,
            Dir.Right => 270f,
            _ => 0f,
        };
    }

    public static Dir GetNextDir(Dir dir) =>
        (Dir)(((int)dir + 1) % 4);

    // ── Direction enum ────────────────────────────────────────────────────

    public enum Dir { Down, Left, Up, Right }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>Local cell offsets (unrotated).</summary>
    private List<Vector2Int> GetLocalCells()
    {
        if (cells != null && cells.Count > 0)
            return cells;

        // Auto-fill solid rectangle
        var solid = new List<Vector2Int>();
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                solid.Add(new Vector2Int(x, y));
        return solid;
    }

    /// <summary>
    /// Rotates a local cell offset according to Dir.
    /// After rotation, all coords remain positive (origin = bottom-left).
    /// </summary>
    private Vector2Int Rotate(Vector2Int cell, Dir dir)
    {
        return dir switch
        {
            Dir.Down => new Vector2Int(cell.x, cell.y),
            Dir.Left => new Vector2Int(cell.y, width - 1 - cell.x),
            Dir.Up => new Vector2Int(width - 1 - cell.x, height - 1 - cell.y),
            Dir.Right => new Vector2Int(height - 1 - cell.y, cell.x),
            _ => cell,
        };
    }
}
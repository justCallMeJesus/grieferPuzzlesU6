using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Inventory Tetris/Item")]
public class ItemTetrisSO : ScriptableObject
{

    public string itemName;
    public Sprite sprite;
    public int width = 1;
    public int height = 1;
    public List<Vector2Int> cells = new();

    public enum Dir { Down, Left, Up, Right }

    public static Dir GetNextDir(Dir dir) => (Dir)(((int)dir + 1) % 4);

    /// <summary>
    /// All grid positions this item occupies when placed at origin with dir.
    /// </summary>
    public List<Vector2Int> GetGridPositionList(Vector2Int origin, Dir dir)
    {
        var list = new List<Vector2Int>();
        foreach (var cell in GetLocalCells())
            list.Add(origin + Rotate(cell, dir));
        return list;
    }

    /// <summary>
    /// The anchoredPosition of the RectTransform = GetWorldPosition(origin) + offset * cellSize.
    /// Derived from: Unity rotates CW (Euler 0,0,-angle), pivot=(0,0).
    /// Offset shifts the visual so its bottom-left aligns with the origin grid cell.
    ///   Down  (0 deg CW):   (0,     0)
    ///   Left  (90 deg CW):  (0,     width)
    ///   Up    (180 deg CW): (width, height)
    ///   Right (270 deg CW): (height,0)
    /// </summary>
    public Vector2Int GetRotationOffset(Dir dir)
    {
        return dir switch
        {
            Dir.Down => new Vector2Int(0, 0),
            Dir.Left => new Vector2Int(0, width),
            Dir.Up => new Vector2Int(width, height),
            Dir.Right => new Vector2Int(height, 0),
            _ => Vector2Int.zero,
        };
    }

    /// <summary>Effective bounding box size after rotation.</summary>
    public Vector2Int GetRotatedSize(Dir dir)
    {
        return (dir == Dir.Left || dir == Dir.Right)
            ? new Vector2Int(height, width)
            : new Vector2Int(width, height);
    }

    /// <summary>CW rotation angle for Quaternion.Euler(0, 0, -angle).</summary>
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

    // ── Private ───────────────────────────────────────────────────────────

    private List<Vector2Int> GetLocalCells()
    {
        if (cells != null && cells.Count > 0) return cells;
        var solid = new List<Vector2Int>();
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                solid.Add(new Vector2Int(x, y));
        return solid;
    }

    // Rotate cell so all coords stay >= 0 (origin = bottom-left of bounding box).
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
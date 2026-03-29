using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generic 2D grid. Stores any type T per cell.
/// Coordinate origin is bottom-left.
/// </summary>
public class Grid<T>
{

    public event EventHandler<OnGridObjectChangedEventArgs> OnGridObjectChanged;
    public class OnGridObjectChangedEventArgs : EventArgs
    {
        public int x, y;
    }

    private readonly int width;
    private readonly int height;
    private readonly float cellSize;
    private readonly Vector3 originPosition;
    private readonly T[,] gridArray;

    public Grid(int width, int height, float cellSize, Vector3 originPosition,
                Func<Grid<T>, int, int, T> createGridObject)
    {
        this.width = width;
        this.height = height;
        this.cellSize = cellSize;
        this.originPosition = originPosition;

        gridArray = new T[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                gridArray[x, y] = createGridObject(this, x, y);
    }

    // ── Accessors ─────────────────────────────────────────────────────────

    public int GetWidth() => width;
    public int GetHeight() => height;
    public float GetCellSize() => cellSize;

    /// <summary>World-space bottom-left corner of cell (x, y).</summary>
    public Vector3 GetWorldPosition(int x, int y) =>
        new Vector3(x, y) * cellSize + originPosition;

    /// <summary>Converts a local-space point (from ItemContainer) to grid x/y.</summary>
    public void GetXY(Vector3 localPosition, out int x, out int y)
    {
        x = Mathf.FloorToInt((localPosition - originPosition).x / cellSize);
        y = Mathf.FloorToInt((localPosition - originPosition).y / cellSize);
    }

    public bool IsValidGridPosition(int x, int y) =>
        x >= 0 && y >= 0 && x < width && y < height;

    public bool IsValidGridPosition(Vector2Int pos) =>
        IsValidGridPosition(pos.x, pos.y);

    // ── Read / Write ──────────────────────────────────────────────────────

    public T GetGridObject(int x, int y)
    {
        if (!IsValidGridPosition(x, y)) return default;
        return gridArray[x, y];
    }

    public T GetGridObject(Vector2Int pos) => GetGridObject(pos.x, pos.y);

    public void SetGridObject(int x, int y, T value)
    {
        if (!IsValidGridPosition(x, y)) return;
        gridArray[x, y] = value;
        TriggerGridObjectChanged(x, y);
    }

    public void TriggerGridObjectChanged(int x, int y)
    {
        OnGridObjectChanged?.Invoke(this, new OnGridObjectChangedEventArgs { x = x, y = y });
    }
}
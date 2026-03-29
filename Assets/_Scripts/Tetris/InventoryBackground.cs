using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the checkerboard / grid-cell background for an InventoryTetris panel.
///
/// SETUP:
///   1. Create a UI Panel as a SIBLING (or parent) of the InventoryTetris GameObject.
///   2. Add this component to it.
///   3. Assign the inventoryTetris reference.
///   4. Assign cellNormalSprite and (optionally) cellAlternateSprite.
///      If no sprite is assigned, cells render as flat coloured squares.
///
/// The component creates one Image per cell and sizes itself automatically.
/// </summary>
[RequireComponent(typeof(GridLayoutGroup))]
[RequireComponent(typeof(RectTransform))]
public class InventoryBackground : MonoBehaviour
{

    [SerializeField] private InventoryTetris inventoryTetris;
    [SerializeField] private Sprite cellNormalSprite;
    [SerializeField] private Sprite cellAlternateSprite; // optional checkerboard

    [SerializeField] private Color normalColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
    [SerializeField] private Color alternateColor = new Color(0.12f, 0.12f, 0.12f, 0.8f);

    private void Start()
    {
        if (inventoryTetris == null)
        {
            Debug.LogError("[InventoryBackground] inventoryTetris not assigned.");
            return;
        }

        var grid = inventoryTetris.GetGrid();
        int w = grid.GetWidth();
        int h = grid.GetHeight();
        float cell = grid.GetCellSize();

        // Size the background to match the grid exactly
        var rt = GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w * cell, h * cell);

        // Match position to the InventoryTetris RectTransform
        rt.anchoredPosition = inventoryTetris.GetComponent<RectTransform>().anchoredPosition;

        // Configure GridLayoutGroup
        var glg = GetComponent<GridLayoutGroup>();
        glg.cellSize = Vector2.one * cell;
        glg.spacing = Vector2.zero;
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = w;
        glg.startCorner = GridLayoutGroup.Corner.LowerLeft;
        glg.startAxis = GridLayoutGroup.Axis.Horizontal;

        // Spawn one cell image per grid cell
        // GridLayoutGroup fills left-to-right, so we iterate y then x
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool alternate = (x + y) % 2 == 1;

                var cellGo = new GameObject($"Cell_{x}_{y}", typeof(Image));
                var cellImg = cellGo.GetComponent<Image>();
                cellGo.transform.SetParent(transform, false);

                cellImg.sprite = alternate && cellAlternateSprite != null
                    ? cellAlternateSprite
                    : cellNormalSprite;
                cellImg.color = alternate ? alternateColor : normalColor;
                cellImg.type = Image.Type.Sliced;
            }
        }
    }
}
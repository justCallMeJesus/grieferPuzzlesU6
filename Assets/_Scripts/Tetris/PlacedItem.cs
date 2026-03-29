using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class PlacedItem : MonoBehaviour
{

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
        return placed;
    }

    public ItemTetrisSO itemSO { get; private set; }
    public Vector2Int origin { get; private set; }
    public ItemTetrisSO.Dir dir { get; private set; }
    private float cellSize;

    public List<Vector2Int> GetGridPositionList() =>
        itemSO.GetGridPositionList(origin, dir);

    public void SetOriginAndDir(Vector2Int newOrigin, ItemTetrisSO.Dir newDir)
    {
        origin = newOrigin;
        dir = newDir;
    }

    public void DestroySelf() => Destroy(gameObject);
}
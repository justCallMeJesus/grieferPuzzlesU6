using UnityEngine;

[CreateAssetMenu(fileName = "Item", menuName = "Scriptable Objects/Item")]
public class ItemData : ScriptableObject
{
    public ItemType type;

    public bool largeItem = false;

    public Sprite sprite;

    public GameObject prefab;



}

public enum ItemType
{
    Banana,
    TetrisBlock
}



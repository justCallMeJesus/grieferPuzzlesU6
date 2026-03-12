using UnityEngine;

[CreateAssetMenu(fileName = "Item", menuName = "Scriptable Objects/Item")]
public class ItemData : ScriptableObject
{
    public ItemType type;

    public bool largeItem = false;

    
}

public enum ItemType
{
    Banana
}

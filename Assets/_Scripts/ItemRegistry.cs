using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds references to all ItemData assets so they can be looked up by name.
/// Attach this to a GameObject in your scene and populate the list in the Inspector.
/// </summary>
public class ItemRegistry : MonoBehaviour
{
    public static ItemRegistry Instance { get; private set; }

    [SerializeField] private List<ItemData> items = new();

    private Dictionary<string, ItemData> lookup;

    private void Awake()
    {
        Instance = this;
        lookup = new Dictionary<string, ItemData>();
        foreach (var item in items)
            if (item != null)
                lookup[item.name] = item;
    }

    public static ItemData Get(string itemName)
    {
        if (Instance == null)
        {
            Debug.LogError("[ItemRegistry] No instance in scene!");
            return null;
        }
        Instance.lookup.TryGetValue(itemName, out var result);
        return result;
    }
}
using System.Linq;
using TMPro;
using UnityEngine;

public class PlayerInventoryUI : MonoBehaviour
{
    public PlayerManager playerManager;

    // Optional count label on the small slot (assign in the Inspector).
    // If left null the count is simply not shown.
    [SerializeField] private TextMeshProUGUI smallItemCountLabel;

    private GameObject bigSlotGO;
    private GameObject smallSlotGO;

    private void Awake()
    {
        // Find all InventorySlot children at runtime ? these are actual scene
        // instances, not prefab asset references.
        InventorySlot[] allSlots = GetComponentsInChildren<InventorySlot>(true);

        InventorySlot big = allSlots.FirstOrDefault(s => s.IsBigSlot);
        InventorySlot small = allSlots.FirstOrDefault(s => !s.IsBigSlot);

        bigSlotGO = big != null ? big.gameObject : null;
        smallSlotGO = small != null ? small.gameObject : null;
    }

    // -------------------------------------------------------------------------
    // Big slot ? still uses DraggableItem so it can be dragged into panels.
    // -------------------------------------------------------------------------

    private void RefreshBigSlot(ItemData item)
    {
        if (bigSlotGO == null) return;

        // Destroy any existing child draggable.
        for (int i = bigSlotGO.transform.childCount - 1; i >= 0; i--)
            Destroy(bigSlotGO.transform.GetChild(i).gameObject);

        if (item != null)
            DraggableItem.Create(item, bigSlotGO, playerManager.inventory);
    }

    // -------------------------------------------------------------------------
    // Small slot ? shows the item icon and a count badge; not drag-rearrangeable
    // outside of a panel context.
    // -------------------------------------------------------------------------

    private void RefreshSmallSlot(ItemData itemType, int count)
    {
        if (smallSlotGO == null) return;

        // Destroy any previous icon child.
        for (int i = smallSlotGO.transform.childCount - 1; i >= 0; i--)
            Destroy(smallSlotGO.transform.GetChild(i).gameObject);

        bool hasItems = itemType != null && count > 0;

        if (hasItems)
        {
            // Create a plain Image child for the icon (no drag component needed).
            var iconGO = new GameObject("SmallItemIcon", typeof(UnityEngine.RectTransform));
            iconGO.transform.SetParent(smallSlotGO.transform, false);

            var img = iconGO.AddComponent<UnityEngine.UI.Image>();
            img.sprite = itemType.sprite;

            // Fill the slot.
            var rt = iconGO.GetComponent<UnityEngine.RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // Update the count label (works whether or not there are items).
        if (smallItemCountLabel != null)
            smallItemCountLabel.text = hasItems ? count.ToString() : "";
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public void RefreshAll(PlayerInventory playerInventory)
    {
        RefreshBigSlot(playerInventory.BigItem);
        RefreshSmallSlot(playerInventory.SmallItemType, playerInventory.SmallItemCount);
        UpdateSelectionIndicators(playerInventory.SelectedSlot);
    }

    private void UpdateSelectionIndicators(int selectedSlot)
    {
        if (bigSlotGO != null)
        {
            var slot = bigSlotGO.GetComponent<InventorySlot>();
            slot?.SetSelected(selectedSlot == 0);
        }

        if (smallSlotGO != null)
        {
            var slot = smallSlotGO.GetComponent<InventorySlot>();
            slot?.SetSelected(selectedSlot == 1);
        }
    }

    public void Init(PlayerInventory inventory)
    {
        RefreshAll(inventory);
    }
}
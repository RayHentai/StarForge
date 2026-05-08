// Assets/_StarForge/Scripts/UI/InventoryUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class InventoryUI : MonoBehaviour
{
    [Header("UI 引用")]
    [SerializeField] private Transform itemContainer;   // 拖 Content 对象
    [SerializeField] private GameObject itemSlotPrefab; // 拖物品槽预制体

    // 缓存已创建的槽位，避免重复创建
    private Dictionary<string, TextMeshProUGUI> slotTexts = new();

    void Start()
    {
        // 订阅背包变化事件
        InventoryManager.Instance.OnItemChanged += OnItemChanged;

        // 初始化时刷新一次（防止进场景已有数据没显示）
        RefreshAll();
    }

    void OnDestroy()
    {
        // 取消订阅，防止内存泄漏
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnItemChanged -= OnItemChanged;
    }

    // 单个物品变化时调用
    private void OnItemChanged(string resourceId, float newAmount)
    {
        if (slotTexts.TryGetValue(resourceId, out var text))
        {
            // 槽位已存在，直接更新数量
            // 改后（显示中文名，比如 "铁矿石"）
            string displayName = ResourceManager.Instance.GetDisplayName(resourceId);
            text.text = $"{displayName}\n{newAmount}";
        }
        else
        {
            // 新物品，创建一个新槽位
            CreateSlot(resourceId, newAmount);
        }
    }

    // 全量刷新（初始化用）
    private void RefreshAll()
    {
        var allItems = InventoryManager.Instance.GetAllItems();
        foreach (var kvp in allItems)
            CreateSlot(kvp.Key, kvp.Value);
    }

    // 创建一个物品槽
    private void CreateSlot(string resourceId, float amount)
    {
        if (itemSlotPrefab == null || itemContainer == null) return;

        var slot = Instantiate(itemSlotPrefab, itemContainer);
        slot.name = $"Slot_{resourceId}";

        var text = slot.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            string displayName = ResourceManager.Instance.GetDisplayName(resourceId);
            text.text = $"{displayName}\n{amount}";
            slotTexts[resourceId] = text;
        }
    }
}

// Assets/_StarForge/Scripts/Managers/InventoryManager.cs
// 职责：管理玩家背包（玩家实际持有的物品）
//
// ── 和 ResourceManager 的分工 ────────────────────────────
// ResourceManager  = 世界数据（矿藏储量、自动产出速率、全局追踪）
// InventoryManager = 玩家背包（玩家实际能使用的物品数量）
//
// 所有"玩家得到东西"的操作都进这里：
//   点击采集点   → InventoryManager.Add()
//   机器取货     → InventoryManager.Add()
//   建筑/合成费用 → InventoryManager.Spend()
//
// ── 测试阶段：无限容量 ───────────────────────────────────
// TODO: 后续替换为有槽位限制的背包系统
// 只需把 maxAmount 从 float.MaxValue 改为实际槽位上限即可，
// 其他代码无需改动（接口保持一致）

using System;
using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    // 存储结构：resourceId → 数量
    private Dictionary<string, float> items = new();

    // 事件：物品数量变化时广播（UI、任务系统订阅）
    public event Action<string, float> OnItemChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ─────────────────────────────────────────────────────
    //  核心 API
    // ─────────────────────────────────────────────────────

    /// <summary>添加物品到背包</summary>
    public void Add(string resourceId, float amount)
    {
        if (string.IsNullOrEmpty(resourceId) || amount <= 0) return;
        items.TryGetValue(resourceId, out float current);
        // 测试阶段无限容量，直接累加
        items[resourceId] = current + amount;
        OnItemChanged?.Invoke(resourceId, items[resourceId]);
        Debug.Log($"[Inventory] +{amount} {ResourceManager.Instance.GetDisplayName(resourceId)} " +
                  $"（共{items[resourceId]}）");
    }

    /// <summary>从背包消耗物品，不足返回 false</summary>
    public bool Spend(string resourceId, float amount)
    {
        if (!HasEnough(resourceId, amount))
        {
            Debug.Log($"[Inventory] 背包不足：{ResourceManager.Instance.GetDisplayName(resourceId)} " +
                      $"需要{amount}，当前{Get(resourceId)}");
            return false;
        }
        items[resourceId] -= amount;
        OnItemChanged?.Invoke(resourceId, items[resourceId]);
        return true;
    }

    /// <summary>消耗多种物品（建筑/合成费用），全部够才扣，否则一个都不扣</summary>
    public bool SpendMultiple(RecipeData.ItemStack[] costs)
    {
        // 先检查
        foreach (var cost in costs)
            if (!HasEnough(cost.resourceId, cost.amount)) return false;
        // 再扣除
        foreach (var cost in costs)
            Spend(cost.resourceId, cost.amount);
        return true;
    }

    /// <summary>查询数量</summary>
    public float Get(string resourceId)
    {
        items.TryGetValue(resourceId, out float v);
        return v;
    }

    /// <summary>是否有足够物品</summary>
    public bool HasEnough(string resourceId, float amount) => Get(resourceId) >= amount;

    /// <summary>获取全部物品（用于UI遍历显示）</summary>
    public Dictionary<string, float> GetAllItems() => items;

    /// <summary>获取某类别的所有物品（用于背包分页）</summary>
    public List<(string id, float amount)> GetByTier(ResourceManager.ResourceTier tier)
    {
        var result = new List<(string, float)>();
        foreach (var kvp in items)
        {
            // 查询 ResourceManager 获取该资源的层级
            var allRes = ResourceManager.Instance.GetAllResources();
            var res = allRes.Find(r => r.id == kvp.Key);
            if (res != null && res.tier == tier)
                result.Add((kvp.Key, kvp.Value));
        }
        return result;
    }
}

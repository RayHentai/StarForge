// Assets/_StarForge/Scripts/Managers/ResourceManager.cs
// 职责：统一管理游戏内所有资源的增减、自动产出、事件通知
// 架构模式：单例（Singleton）
//
// ── 资源链（玩家设计） ──────────────────────────────────
//  第1层  矿物资源          → 直接从星球表面采集
//  第2层  矿物粉末          → 矿物资源 经【粉碎机】加工
//  第3层  矿物处理中间产物   → 矿物粉末 经【冶炼炉】加工
//  第4层  二次加工合金       → 中间产物 经【合金炉】合成
//  第4层  二次加工化工品     → 中间产物 经【化工厂】合成
//  特殊   星核碎片           → 战斗击杀精英怪掉落，不可自动生产
// ────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using UnityEngine;

public class ResourceManager : MonoBehaviour
{
    // ── 单例 ─────────────────────────────────────────────
    public static ResourceManager Instance { get; private set; }

    // ── 资源层级枚举（方便其他系统判断资源属于哪一层） ────
    public enum ResourceTier
    {
        Raw,            // 第1层：矿物资源（原矿）
        Powder,         // 第2层：矿物粉末
        Intermediate,   // 第3层：矿物处理中间产物
        Alloy,          // 第4层：二次加工合金
        Chemical,       // 第4层：二次加工化工品
        Special         // 特殊：星核碎片
    }

    // ── 单个资源的数据结构 ────────────────────────────────
    [Serializable]
    public class ResourceData
    {
        public string id;               // 资源唯一ID
        public string displayName;      // 中文显示名
        public ResourceTier tier;       // 所属层级
        public float amount;            // 当前数量
        public float perSecond;         // 每秒自动产出（由建筑驱动）
        public float maxAmount = 9999f; // 存储上限（后期科技树可升级）
    }

    // ── 事件：资源变化时通知所有监听者（UI、任务系统等） ──
    public event Action<string, float> OnResourceChanged;

    // ── 内部数据 ──────────────────────────────────────────
    private List<ResourceData> resources = new();
    private Dictionary<string, ResourceData> resourceDict = new();

    // ─────────────────────────────────────────────────────
    //  生命周期
    // ─────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitResources();
    }

    void Update()
    {
        // 每帧根据 perSecond 自动累加（放置核心）
        // 注意：perSecond 由建筑系统写入，ResourceManager 只负责累加
        foreach (var r in resources)
        {
            if (r.perSecond <= 0) continue;
            float delta = r.perSecond * Time.deltaTime;
            r.amount = Mathf.Min(r.amount + delta, r.maxAmount);
            OnResourceChanged?.Invoke(r.id, r.amount);
        }
    }

    // ─────────────────────────────────────────────────────
    //  初始化资源表
    //  ⚠️ 这里只是 MVP 占位资源，具体矿物名称
    //     等玩家设计星球时再来填入
    // ─────────────────────────────────────────────────────
    void InitResources()
    {
        resources = new List<ResourceData>
        {
            // ── 第1层：矿物资源（原矿）────────────────────
            new() { id = "raw_a", displayName = "原矿A（待命名）", tier = ResourceTier.Raw },
            new() { id = "raw_b", displayName = "原矿B（待命名）", tier = ResourceTier.Raw },

            // ── 第2层：矿物粉末 ───────────────────────────
            new() { id = "powder_a", displayName = "粉末A（待命名）", tier = ResourceTier.Powder },
            new() { id = "powder_b", displayName = "粉末B（待命名）", tier = ResourceTier.Powder },

            // ── 第3层：中间产物 ───────────────────────────
            new() { id = "inter_a", displayName = "中间产物A（待命名）", tier = ResourceTier.Intermediate },

            // ── 第4层：合金 ───────────────────────────────
            new() { id = "alloy_a", displayName = "合金A（待命名）", tier = ResourceTier.Alloy },

            // ── 第4层：化工品 ─────────────────────────────
            new() { id = "chem_a", displayName = "化工品A（待命名）", tier = ResourceTier.Chemical },

            // ── 特殊：星核碎片（战斗掉落，保留） ──────────
            new() { id = "core_shard", displayName = "星核碎片", tier = ResourceTier.Special },
        };

        foreach (var r in resources)
        {
            r.amount = 0;
            r.perSecond = 0;
            resourceDict[r.id] = r;
        }

        Debug.Log($"[ResourceManager] 初始化完成，共注册 {resources.Count} 种资源。");
    }

    // ─────────────────────────────────────────────────────
    //  公开 API
    // ─────────────────────────────────────────────────────

    /// <summary>增加资源</summary>
    public void Add(string id, float amount)
    {
        if (!resourceDict.TryGetValue(id, out var r))
        {
            Debug.LogWarning($"[ResourceManager] 未知资源ID：{id}");
            return;
        }
        r.amount = Mathf.Min(r.amount + amount, r.maxAmount);
        OnResourceChanged?.Invoke(id, r.amount);
    }

    /// <summary>消耗资源，余量不足返回 false</summary>
    public bool Spend(string id, float amount)
    {
        if (!resourceDict.TryGetValue(id, out var r)) return false;
        if (r.amount < amount)
        {
            Debug.Log($"[ResourceManager] 资源不足：{r.displayName} 需要{amount}，当前{r.amount}");
            return false;
        }
        r.amount -= amount;
        OnResourceChanged?.Invoke(id, r.amount);
        return true;
    }

    /// <summary>查询数量</summary>
    public float Get(string id) =>
        resourceDict.TryGetValue(id, out var r) ? r.amount : 0;

    /// <summary>是否有足够资源</summary>
    public bool HasEnough(string id, float amount) => Get(id) >= amount;

    /// <summary>添加每秒自动产出（建造采集器/工厂时调用）</summary>
    public void AddPerSecond(string id, float rate)
    {
        if (resourceDict.TryGetValue(id, out var r))
            r.perSecond += rate;
    }

    /// <summary>移除每秒产出（拆除建筑时调用）</summary>
    public void RemovePerSecond(string id, float rate)
    {
        if (resourceDict.TryGetValue(id, out var r))
            r.perSecond = Mathf.Max(0, r.perSecond - rate);
    }

    /// <summary>获取显示名</summary>
    public string GetDisplayName(string id) =>
        resourceDict.TryGetValue(id, out var r) ? r.displayName : id;

    /// <summary>获取所有资源（用于UI遍历显示）</summary>
    public List<ResourceData> GetAllResources() => resources;

    /// <summary>按层级获取资源（用于分组显示）</summary>
    public List<ResourceData> GetByTier(ResourceTier tier) =>
        resources.FindAll(r => r.tier == tier);
}

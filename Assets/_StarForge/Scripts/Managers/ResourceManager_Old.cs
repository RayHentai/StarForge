// Assets/_StarForge/Scripts/Managers/ResourceManager.cs
// 职责：统一管理游戏内所有资源的增减、自动产出、事件通知
// 架构模式：单例（Singleton）

using System;
using System.Collections.Generic;
using UnityEngine;

public class ResourceManager_Old : MonoBehaviour
{
    // ── 单例 ──────────────────────────────────────────────
    public static ResourceManager_Old Instance { get; private set; }

    // ── 数据结构 ──────────────────────────────────────────
    [Serializable]
    public class ResourceData
    {
        public string id;           // 资源唯一ID，如 "ore"
        public string displayName;  // 显示名称，如 "矿石"
        public float amount;        // 当前数量
        public float perSecond;     // 每秒自动产出量
    }

    // ── 事件：任何资源变化时触发，UI 监听这个事件来刷新显示 ──
    public event Action<string, float> OnResourceChanged;

    // ── 内部数据 ──────────────────────────────────────────
    private List<ResourceData> resources = new();
    private Dictionary<string, ResourceData> resourceDict = new();

    // ─────────────────────────────────────────────────────
    //  生命周期
    // ─────────────────────────────────────────────────────

    void Awake()
    {
        // 单例保护：场景中只保留一个 ResourceManager
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // 切换场景时不销毁

        InitResources();
    }

    void Update()
    {
        // 每帧根据 perSecond 自动累加资源（放置核心）
        foreach (var r in resources)
        {
            if (r.perSecond <= 0) continue;
            r.amount += r.perSecond * Time.deltaTime;
            OnResourceChanged?.Invoke(r.id, r.amount);
        }
    }

    // ─────────────────────────────────────────────────────
    //  初始化
    // ─────────────────────────────────────────────────────

    void InitResources()
    {
        resources = new List<ResourceData>
        {
            new() { id = "ore",        displayName = "矿石",     amount = 0, perSecond = 0 },
            new() { id = "crystal",    displayName = "结晶",     amount = 0, perSecond = 0 },
            new() { id = "metal",      displayName = "金属",     amount = 0, perSecond = 0 },
            new() { id = "core_shard", displayName = "星核碎片", amount = 0, perSecond = 0 },
        };

        foreach (var r in resources)
            resourceDict[r.id] = r;

        Debug.Log("[ResourceManager] 初始化完成，共注册 " + resources.Count + " 种资源。");
    }

    // ─────────────────────────────────────────────────────
    //  公开 API
    // ─────────────────────────────────────────────────────

    /// <summary>增加指定资源</summary>
    public void Add(string id, float amount)
    {
        if (!resourceDict.TryGetValue(id, out var r))
        {
            Debug.LogWarning($"[ResourceManager] 未知资源ID：{id}");
            return;
        }
        r.amount += amount;
        OnResourceChanged?.Invoke(id, r.amount);
    }

    /// <summary>消耗指定资源，余量不足时返回 false</summary>
    public bool Spend(string id, float amount)
    {
        if (!resourceDict.TryGetValue(id, out var r)) return false;
        if (r.amount < amount)
        {
            Debug.Log($"[ResourceManager] 资源不足：{r.displayName} 需要 {amount}，当前 {r.amount}");
            return false;
        }
        r.amount -= amount;
        OnResourceChanged?.Invoke(id, r.amount);
        return true;
    }

    /// <summary>查询指定资源当前数量</summary>
    public float Get(string id) =>
        resourceDict.TryGetValue(id, out var r) ? r.amount : 0;

    /// <summary>查询是否有足够资源（用于UI显示按钮是否可点击）</summary>
    public bool HasEnough(string id, float amount) => Get(id) >= amount;

    /// <summary>为指定资源添加每秒自动产出（建造采集器时调用）</summary>
    public void AddPerSecond(string id, float rate)
    {
        if (resourceDict.TryGetValue(id, out var r))
        {
            r.perSecond += rate;
            Debug.Log($"[ResourceManager] {r.displayName} 每秒产出 +{rate}，当前共 {r.perSecond}/s");
        }
    }

    /// <summary>获取资源显示名称</summary>
    public string GetDisplayName(string id) =>
        resourceDict.TryGetValue(id, out var r) ? r.displayName : id;
}

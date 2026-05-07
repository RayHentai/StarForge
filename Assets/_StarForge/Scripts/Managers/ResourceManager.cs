// Assets/_StarForge/Scripts/Managers/ResourceManager.cs
// 职责：统一管理游戏内所有资源的增减、自动产出、事件通知
//
// ── 资源框架 ─────────────────────────────────────────────
// 自然资源（本文件定义）：世界中存在的原始材料
//   Ore      = 单质矿石（需要采矿工具，有矿藏量）
//   Natural  = 可采集资源（地表环境，部分无限）
//   Special  = 星核碎片等特殊掉落
//
// 加工产物（由自动化系统定义，此处暂不注册）：
//   Powder / Intermediate / Alloy / Chemical
// ─────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using UnityEngine;

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance { get; private set; }

    // ── 资源分类枚举 ──────────────────────────────────────
    public enum ResourceTier
    {
        Ore,         // 单质矿石（采矿获取，矿藏有限）
        Natural,     // 可采集自然资源（地表，部分无限）
        Special,     // 特殊资源（战斗掉落、任务奖励）
        // 以下留给自动化系统扩展
        Powder,
        Intermediate,
        Alloy,
        Chemical,
    }

    // ── 资源数据结构 ──────────────────────────────────────
    [Serializable]
    public class ResourceData
    {
        public string id;
        public string displayName;
        public ResourceTier tier;
        public float amount;
        public float perSecond;
        public float maxAmount = 99999f;

        // 矿藏相关（仅 Ore 和部分 Natural 使用）
        public bool hasDeposit = false;     // 是否有矿藏量限制
        public float depositAmount = 0f;    // 当前矿藏储量
        public float maxDeposit = 0f;       // 初始最大矿藏量（由难度系数决定）

        // 燃料相关
        public bool isFuel = false;
        public float heatValue = 0f;        // 热值（HV）

        // 可再生
        public bool isRenewable = false;
    }

    // ── 事件 ──────────────────────────────────────────────
    public event Action<string, float> OnResourceChanged;
    public event Action<string, float> OnDepositChanged;   // 矿藏量变化（UI矿脉显示用）

    // ── 内部数据 ──────────────────────────────────────────
    private List<ResourceData> resources = new();
    private Dictionary<string, ResourceData> resourceDict = new();

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
        foreach (var r in resources)
        {
            if (r.perSecond <= 0) continue;

            // 有矿藏限制时，消耗矿藏量
            if (r.hasDeposit)
            {
                if (r.depositAmount <= 0)
                {
                    // 矿藏耗尽，停止产出
                    r.perSecond = 0;
                    Debug.Log($"[ResourceManager] {r.displayName} 矿藏已耗尽");
                    continue;
                }
                float delta = r.perSecond * Time.deltaTime;
                float actual = Mathf.Min(delta, r.depositAmount);
                r.depositAmount -= actual;
                r.amount = Mathf.Min(r.amount + actual, r.maxAmount);
                OnDepositChanged?.Invoke(r.id, r.depositAmount);
            }
            else
            {
                r.amount = Mathf.Min(r.amount + r.perSecond * Time.deltaTime, r.maxAmount);
            }

            OnResourceChanged?.Invoke(r.id, r.amount);
        }
    }

    // ─────────────────────────────────────────────────────
    //  地球资源初始化
    // ─────────────────────────────────────────────────────
    void InitResources()
    {
        resources = new List<ResourceData>
        {
            // ══ 单质矿石 · 普通 ═══════════════════════════════
            new() { id="ore_coal",     displayName="煤炭矿石", tier=ResourceTier.Ore,
                    hasDeposit=true, maxDeposit=50000f, depositAmount=50000f,
                    isFuel=true, heatValue=80f },

            new() { id="ore_copper",   displayName="铜矿石",   tier=ResourceTier.Ore,
                    hasDeposit=true, maxDeposit=40000f, depositAmount=40000f },

            new() { id="ore_tin",      displayName="锡矿石",   tier=ResourceTier.Ore,
                    hasDeposit=true, maxDeposit=35000f, depositAmount=35000f },

            new() { id="ore_iron",     displayName="铁矿石",   tier=ResourceTier.Ore,
                    hasDeposit=true, maxDeposit=60000f, depositAmount=60000f },

            // ══ 单质矿石 · 稀有 ═══════════════════════════════
            new() { id="ore_gold",     displayName="金矿石",   tier=ResourceTier.Ore,
                    hasDeposit=true, maxDeposit=5000f,  depositAmount=5000f  },

            new() { id="ore_silicon",  displayName="硅矿石",   tier=ResourceTier.Ore,
                    hasDeposit=true, maxDeposit=8000f,  depositAmount=8000f  },

            new() { id="ore_aluminum", displayName="铝土矿",   tier=ResourceTier.Ore,
                    hasDeposit=true, maxDeposit=10000f, depositAmount=10000f },

            new() { id="ore_sulfur",   displayName="硫磺",     tier=ResourceTier.Ore,
                    hasDeposit=true, maxDeposit=4000f,  depositAmount=4000f  },

            new() { id="ore_lava",     displayName="熔岩矿石", tier=ResourceTier.Ore,
                    hasDeposit=true, maxDeposit=6000f,  depositAmount=6000f  },

            new() { id="ore_diamond",  displayName="钻石矿石", tier=ResourceTier.Ore,
                    hasDeposit=true, maxDeposit=500f,   depositAmount=500f   },

            // ══ 可采集资源 · 普通 ══════════════════════════════
            new() { id="res_stone",    displayName="石材",     tier=ResourceTier.Natural,
                    hasDeposit=true, maxDeposit=80000f, depositAmount=80000f },

            new() { id="res_wood",     displayName="木头",     tier=ResourceTier.Natural,
                    hasDeposit=false, isRenewable=true,
                    isFuel=true, heatValue=30f },

            new() { id="res_sand",     displayName="沙子",     tier=ResourceTier.Natural,
                    hasDeposit=true, maxDeposit=70000f, depositAmount=70000f },

            new() { id="res_limestone",displayName="石灰石",   tier=ResourceTier.Natural,
                    hasDeposit=true, maxDeposit=50000f, depositAmount=50000f },

            // ══ 可采集资源 · 特定地形 ════════════════════════
            new() { id="res_water",    displayName="水",       tier=ResourceTier.Natural,
                    hasDeposit=false, isRenewable=true, maxAmount=99999f },

            new() { id="res_oil",      displayName="原油",     tier=ResourceTier.Natural,
                    hasDeposit=true, maxDeposit=20000f, depositAmount=20000f,
                    isFuel=true, heatValue=200f },

            new() { id="res_gas",      displayName="天然气",   tier=ResourceTier.Natural,
                    hasDeposit=true, maxDeposit=15000f, depositAmount=15000f,
                    isFuel=true, heatValue=350f },

            new() { id="res_lava_liquid", displayName="熔岩（液态）", tier=ResourceTier.Natural,
                    hasDeposit=false, isRenewable=true },

            // ══ 特殊资源 ═══════════════════════════════════════
            new() { id="core_shard",   displayName="星核碎片", tier=ResourceTier.Special },
        };

        foreach (var r in resources)
        {
            r.amount = 0;
            if (r.perSecond == 0) r.perSecond = 0;
            resourceDict[r.id] = r;
        }

        Debug.Log($"[ResourceManager] 初始化完成，共注册 {resources.Count} 种资源（地球篇）");
    }

    // ─────────────────────────────────────────────────────
    //  公开 API
    // ─────────────────────────────────────────────────────

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

    public bool Spend(string id, float amount)
    {
        if (!resourceDict.TryGetValue(id, out var r)) return false;
        if (r.amount < amount) return false;
        r.amount -= amount;
        OnResourceChanged?.Invoke(id, r.amount);
        return true;
    }

    public float Get(string id) =>
        resourceDict.TryGetValue(id, out var r) ? r.amount : 0;

    public bool HasEnough(string id, float amount) => Get(id) >= amount;

    public void AddPerSecond(string id, float rate)
    {
        if (resourceDict.TryGetValue(id, out var r))
            r.perSecond += rate;
    }

    public void RemovePerSecond(string id, float rate)
    {
        if (resourceDict.TryGetValue(id, out var r))
            r.perSecond = Mathf.Max(0, r.perSecond - rate);
    }

    public string GetDisplayName(string id) =>
        resourceDict.TryGetValue(id, out var r) ? r.displayName : id;

    public List<ResourceData> GetAllResources() => resources;

    public List<ResourceData> GetByTier(ResourceTier tier) =>
        resources.FindAll(r => r.tier == tier);

    public bool IsFuel(string id) =>
        resourceDict.TryGetValue(id, out var r) && r.isFuel;

    public float GetHeatValue(string id) =>
        resourceDict.TryGetValue(id, out var r) ? r.heatValue : 0f;

    public float GetDepositAmount(string id) =>
        resourceDict.TryGetValue(id, out var r) ? r.depositAmount : 0f;

    /// <summary>
    /// 难度系数应用入口：在游戏开始时调用，按倍率缩放所有矿藏量
    /// </summary>
    public void ApplyDifficultyMultiplier(float multiplier)
    {
        foreach (var r in resources)
        {
            if (!r.hasDeposit) continue;
            r.depositAmount = r.maxDeposit * multiplier;
            r.maxDeposit = r.depositAmount;
        }
        Debug.Log($"[ResourceManager] 难度系数={multiplier}，矿藏量已调整");
    }
}

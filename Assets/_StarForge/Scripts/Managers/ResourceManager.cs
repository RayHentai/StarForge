// Assets/_StarForge/Scripts/Managers/ResourceManager.cs
// 职责：统一管理游戏内所有资源的增减、自动产出、事件通知
//
// ── 资源框架 ─────────────────────────────────────────────
//  Mineral  普通矿石        需要矿物采集点，粉碎/熔炼加工路线
//  Gem      宝石类          需要矿物采集点，过渡采矿机直出gem_成品
//  Mineable 可采掘直接用    需要采集点/采矿机，无加工路线（石材/石灰石）
//  Natural  可采集自然资源  地表普通采集（木头）
//  Terrain  地形采集资源    靠近特定地形点击/机器抽取（水/油/熔岩/沙子）
//  Special  特殊掉落        战斗/任务奖励
//
// ── 加工产物命名规范 ─────────────────────────────────────
//  crushed_[矿]         粉碎矿石
//  ore_washed_[矿]      洗净矿石
//  powder_impure_[矿]   含杂粉
//  powder_washed_[矿]   矿粉（洗矿后）
//  powder_clean_[矿]    洁净粉
//  ingot_[矿]           金属锭
//  crystal_[矿]         晶体产物（单晶硅）
//  gem_[宝石]           宝石成品

using System;
using System.Collections.Generic;
using UnityEngine;

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance { get; private set; }

    // ── 资源层级枚举 ──────────────────────────────────────
    /// <summary>
    /// 资源层级枚举。
    /// 决定资源的获取方式和加工路线，也用于UI背包分页和物流过滤器匹配。
    /// </summary>
    public enum ResourceTier
    {
        Mineral,        // 普通矿石（需要矿物采集点，有粉碎/熔炼加工路线）
        Gem,            // 宝石类（过渡采矿机直出gem_成品，电力采矿机出ore_矿石）
        Mineable,       // 可采掘直接使用（需要采集点/采矿机，无加工路线，如石材/石灰石）
        Natural,        // 可采集自然资源（地表普通采集，如木头）
        Terrain,        // 地形采集资源（靠近特定地形点击/机器抽取，如水/油/熔岩/沙子）
        Special,        // 特殊掉落（战斗/任务奖励，如星核碎片）
        // ── 加工产物（机器生产，不在星球表面自然存在）──────
        Crushed,        // 粉碎矿石（粉碎机第一步产出，如 crushed_iron）
        OreWashed,      // 洗净矿石（洗矿机对粉碎矿石的产出，如 ore_washed_iron）
        PowderImpure,   // 含杂粉（粉碎机二次加工产出，如 powder_impure_iron）
        PowderWashed,   // 矿粉（含杂粉经洗矿后产出，如 powder_washed_iron）
        PowderClean,    // 洁净粉（洗净矿石经粉碎机产出，最高增产路线，如 powder_clean_iron）
        Ingot,          // 金属锭/晶体（熔炉/高炉最终产出，如 ingot_iron / crystal_silicon）
        Processed,      // 其他加工成品（宝石成品等，如 gem_diamond / gem_coal）
    }

    /// <summary>
    /// 单种资源的完整数据。
    /// ResourceManager 内部用 List 存储所有资源，同时维护一个 Dictionary 用于快速查找。
    /// </summary>
    [Serializable]
    public class ResourceData
    {
        /// <summary>资源唯一标识符，命名规范见文件头注释。不可重复，不可运行时修改。</summary>
        public string id;

        /// <summary>UI显示名称，中文，如"铁矿石"。</summary>
        public string displayName;

        /// <summary>资源层级，决定采集方式和加工路线，也用于背包分页。</summary>
        public ResourceTier tier;

        /// <summary>当前玩家/世界持有量（注意：玩家背包量在 InventoryManager，这里是世界追踪量）。</summary>
        public float amount;

        /// <summary>每秒自动产出量，由采集建筑调用 AddPerSecond() 注册。0 表示无自动产出。</summary>
        public float perSecond;

        /// <summary>存储上限，默认 99999。可被科技树升级扩展。</summary>
        public float maxAmount = 99999f;

        /// <summary>是否有矿藏量限制。true = 采完会枯竭（矿石/原油），false = 无限（木头/水）。</summary>
        public bool hasDeposit = false;

        /// <summary>当前矿藏储量。每次采集会减少此值，降为0后自动产出停止。</summary>
        public float depositAmount = 0f;

        /// <summary>初始最大矿藏量，用于难度系数重置。</summary>
        public float maxDeposit = 0f;

        /// <summary>是否为燃料。true = 可投入燃料机器或发电机，会读取 heatValue。</summary>
        public bool isFuel = false;

        /// <summary>燃料热值（HV）。热值 / 机器用电当量(A) = 运行秒数。非燃料填0。</summary>
        public float heatValue = 0f;

        /// <summary>是否可再生。true = 矿藏不会枯竭（水/木头），false = 有限资源。</summary>
        public bool isRenewable = false;
    }

    /// <summary>任何资源数量变化时触发。参数：(resourceId, 新数量)。UI和任务系统订阅此事件。</summary>
    public event Action<string, float> OnResourceChanged;

    /// <summary>矿藏储量变化时触发。参数：(resourceId, 剩余矿藏量)。矿脉UI订阅此事件显示剩余量。</summary>
    public event Action<string, float> OnDepositChanged;

    // ── 内部数据存储 ──────────────────────────────────────
    // resources：有序列表，用于遍历（UI显示、存档序列化）
    // resourceDict：id→数据的哈希表，O(1)快速查找，避免每次遍历整个列表
    private List<ResourceData> resources = new();
    private Dictionary<string, ResourceData> resourceDict = new();

    // ── 生命周期 ──────────────────────────────────────────

    /// <summary>
    /// 单例初始化。场景中只保留一个 ResourceManager，
    /// DontDestroyOnLoad 保证切换星球场景时数据不丢失。
    /// </summary>
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitResources();
    }

    /// <summary>
    /// 每帧自动累加有 perSecond 产出的资源。
    /// 有矿藏限制的资源同时消耗 depositAmount，耗尽后自动清零 perSecond。
    /// </summary>
    void Update()
    {
        foreach (var r in resources)
        {
            if (r.perSecond <= 0) continue;
            if (r.hasDeposit)
            {
                if (r.depositAmount <= 0) { r.perSecond = 0; continue; }
                float delta = Mathf.Min(r.perSecond * Time.deltaTime, r.depositAmount);
                r.depositAmount -= delta;
                r.amount = Mathf.Min(r.amount + delta, r.maxAmount);
                OnDepositChanged?.Invoke(r.id, r.depositAmount);
            }
            else
            {
                r.amount = Mathf.Min(r.amount + r.perSecond * Time.deltaTime, r.maxAmount);
            }
            OnResourceChanged?.Invoke(r.id, r.amount);
        }
    }

    /// <summary>
    /// 注册地球篇所有资源。
    /// 新星球的资源在对应的 PlanetManager 里动态追加注册，不在此处硬编码。
    /// 资源 ID 命名规范见文件头注释。
    /// </summary>
    void InitResources()
    {
        resources = new List<ResourceData>
        {
            // ══ 普通矿石 · 普通 ════════════════════════════
            new() { id="ore_copper",    displayName="铜矿石",   tier=ResourceTier.Mineral,
                    hasDeposit=true, maxDeposit=40000f, depositAmount=40000f },
            new() { id="ore_tin",       displayName="锡矿石",   tier=ResourceTier.Mineral,
                    hasDeposit=true, maxDeposit=35000f, depositAmount=35000f },
            new() { id="ore_iron",      displayName="铁矿石",   tier=ResourceTier.Mineral,
                    hasDeposit=true, maxDeposit=60000f, depositAmount=60000f },

            // ══ 普通矿石 · 稀有 ════════════════════════════
            new() { id="ore_gold",      displayName="金矿石",   tier=ResourceTier.Mineral,
                    hasDeposit=true, maxDeposit=5000f,  depositAmount=5000f  },
            new() { id="ore_silicon",   displayName="硅矿石",   tier=ResourceTier.Mineral,
                    hasDeposit=true, maxDeposit=8000f,  depositAmount=8000f  },
            new() { id="ore_bauxite",   displayName="铝土矿石", tier=ResourceTier.Mineral,
                    hasDeposit=true, maxDeposit=10000f, depositAmount=10000f },
            new() { id="ore_sulfur",    displayName="硫磺矿石", tier=ResourceTier.Mineral,
                    hasDeposit=true, maxDeposit=4000f,  depositAmount=4000f  },

            // ══ 地形采集 · 矿石类 ══════════════════════════
            // 熔岩：靠近熔岩地形点击/机器抽取，矿藏有限（同原油）
            new() { id="ore_lava",      displayName="熔岩",     tier=ResourceTier.Terrain,
                    hasDeposit=true, maxDeposit=20000f, depositAmount=20000f },

            // ══ 宝石类 ════════════════════════════════════
            // 煤炭和钻石走宝石加工线，过渡采矿机直出成品
            new() { id="ore_coal",      displayName="煤炭矿石", tier=ResourceTier.Gem,
                    hasDeposit=true, maxDeposit=50000f, depositAmount=50000f,
                    isFuel=true, heatValue=80f },
            new() { id="ore_diamond",   displayName="钻石矿石", tier=ResourceTier.Gem,
                    hasDeposit=true, maxDeposit=500f,   depositAmount=500f   },

            // ══ 可采集资源 · 普通 ══════════════════════════
            new() { id="res_wood",      displayName="木头",     tier=ResourceTier.Natural,
                    isRenewable=true, isFuel=true, heatValue=30f },

            // ══ 可采掘直接使用 ══════════════════════════════
            new() { id="res_stone",     displayName="石材",   tier=ResourceTier.Mineable,
                    hasDeposit=true, maxDeposit=80000f, depositAmount=80000f },
            new() { id="res_limestone", displayName="石灰石", tier=ResourceTier.Mineable,
                    hasDeposit=true, maxDeposit=50000f, depositAmount=50000f },

            // ══ 地形采集资源 ════════════════════════════════
            // 沙子：沙地地形，矿藏丰富
            new() { id="res_sand",      displayName="沙子",   tier=ResourceTier.Terrain,
                    hasDeposit=true, maxDeposit=70000f, depositAmount=70000f },
            // 水：无限，水域大小决定点击/抽取速度（由地形对象配置）
            new() { id="res_water",     displayName="水",       tier=ResourceTier.Terrain,
                    isRenewable=true },
            // 原油：油田地形，矿藏有限
            new() { id="res_oil",       displayName="原油",     tier=ResourceTier.Terrain,
                    hasDeposit=true, maxDeposit=20000f, depositAmount=20000f,
                    isFuel=true, heatValue=200f },
            // 天然气：气田地形，矿藏有限
            new() { id="res_gas",       displayName="天然气",   tier=ResourceTier.Terrain,
                    hasDeposit=true, maxDeposit=15000f, depositAmount=15000f,
                    isFuel=true, heatValue=350f },

            // ══ 特殊 ════════════════════════════════════════
            new() { id="core_shard",    displayName="星核碎片", tier=ResourceTier.Special },

            // ══ 加工产物 · 粉碎矿石 ════════════════════════
            new() { id="crushed_iron",     displayName="粉碎铁矿石",  tier=ResourceTier.Crushed },
            new() { id="crushed_copper",   displayName="粉碎铜矿石",  tier=ResourceTier.Crushed },
            new() { id="crushed_tin",      displayName="粉碎锡矿石",  tier=ResourceTier.Crushed },
            new() { id="crushed_gold",     displayName="粉碎金矿石",  tier=ResourceTier.Crushed },
            new() { id="crushed_bauxite",  displayName="粉碎铝土矿石", tier=ResourceTier.Crushed },
            new() { id="crushed_silicon",  displayName="粉碎硅矿石",  tier=ResourceTier.Crushed },
            new() { id="crushed_sulfur",   displayName="粉碎硫磺矿石",tier=ResourceTier.Crushed },
            new() { id="crushed_diamond",  displayName="粉碎钻石矿石",tier=ResourceTier.Crushed },
            new() { id="crushed_coal",     displayName="粉碎煤炭矿石",tier=ResourceTier.Crushed },

            // ══ 加工产物 · 洗净矿石 ════════════════════════
            new() { id="ore_washed_iron",     displayName="洗净铁矿石",  tier=ResourceTier.OreWashed },
            new() { id="ore_washed_copper",   displayName="洗净铜矿石",  tier=ResourceTier.OreWashed },
            new() { id="ore_washed_tin",      displayName="洗净锡矿石",  tier=ResourceTier.OreWashed },
            new() { id="ore_washed_gold",     displayName="洗净金矿石",  tier=ResourceTier.OreWashed },
            new() { id="ore_washed_bauxite",  displayName="洗净铝土矿石", tier=ResourceTier.OreWashed },
            new() { id="ore_washed_silicon",  displayName="洗净硅矿石",  tier=ResourceTier.OreWashed },
            new() { id="ore_washed_sulfur",   displayName="洗净硫磺矿石",tier=ResourceTier.OreWashed },
            new() { id="ore_washed_diamond",  displayName="洗净钻石矿石",tier=ResourceTier.OreWashed },
            new() { id="ore_washed_coal",     displayName="洗净煤炭矿石",tier=ResourceTier.OreWashed },

            // ══ 加工产物 · 含杂粉 ══════════════════════════
            new() { id="powder_impure_iron",     displayName="含杂铁粉",  tier=ResourceTier.PowderImpure },
            new() { id="powder_impure_copper",   displayName="含杂铜粉",  tier=ResourceTier.PowderImpure },
            new() { id="powder_impure_tin",      displayName="含杂锡粉",  tier=ResourceTier.PowderImpure },
            new() { id="powder_impure_gold",     displayName="含杂金粉",  tier=ResourceTier.PowderImpure },
            new() { id="powder_impure_bauxite",   displayName="含杂铝土粉", tier=ResourceTier.PowderImpure },
            new() { id="powder_impure_silicon",  displayName="含杂硅粉",  tier=ResourceTier.PowderImpure },
            new() { id="powder_impure_sulfur",   displayName="含杂硫磺粉",tier=ResourceTier.PowderImpure },

            // ══ 加工产物 · 矿粉（洗矿后）══════════════════
            new() { id="powder_washed_iron",     displayName="铁粉",      tier=ResourceTier.PowderWashed },
            new() { id="powder_washed_copper",   displayName="铜粉",      tier=ResourceTier.PowderWashed },
            new() { id="powder_washed_tin",      displayName="锡粉",      tier=ResourceTier.PowderWashed },
            new() { id="powder_washed_gold",     displayName="金粉",      tier=ResourceTier.PowderWashed },
            new() { id="powder_washed_bauxite",   displayName="铝土粉",  tier=ResourceTier.PowderWashed },
            new() { id="powder_washed_silicon",  displayName="硅粉",      tier=ResourceTier.PowderWashed },
            new() { id="powder_washed_sulfur",   displayName="硫磺粉",    tier=ResourceTier.PowderWashed },

            // ══ 加工产物 · 洁净粉 ══════════════════════════
            new() { id="powder_clean_iron",     displayName="洁净铁粉",  tier=ResourceTier.PowderClean },
            new() { id="powder_clean_copper",   displayName="洁净铜粉",  tier=ResourceTier.PowderClean },
            new() { id="powder_clean_tin",      displayName="洁净锡粉",  tier=ResourceTier.PowderClean },
            new() { id="powder_clean_gold",     displayName="洁净金粉",  tier=ResourceTier.PowderClean },
            new() { id="powder_clean_bauxite",   displayName="洁净铝土粉", tier=ResourceTier.PowderClean },
            new() { id="powder_clean_silicon",  displayName="洁净硅粉",  tier=ResourceTier.PowderClean },
            new() { id="powder_clean_sulfur",   displayName="洁净硫磺粉",tier=ResourceTier.PowderClean },

            // ══ 加工产物 · 金属锭/晶体 ═════════════════════
            new() { id="ingot_iron",        displayName="铁锭",   tier=ResourceTier.Ingot },
            new() { id="ingot_copper",      displayName="铜锭",   tier=ResourceTier.Ingot },
            new() { id="ingot_tin",         displayName="锡锭",   tier=ResourceTier.Ingot },
            new() { id="ingot_gold",        displayName="金锭",   tier=ResourceTier.Ingot },
            new() { id="ingot_aluminum",    displayName="铝锭",   tier=ResourceTier.Ingot },
            new() { id="crystal_silicon",   displayName="单晶硅", tier=ResourceTier.Ingot },

            // ══ 宝石成品 ════════════════════════════════════
            new() { id="gem_diamond",       displayName="钻石",   tier=ResourceTier.Processed },
            new() { id="gem_coal",          displayName="煤炭",   tier=ResourceTier.Processed },
        };

        foreach (var r in resources)
        {
            r.amount = 0;
            r.perSecond = 0;
            resourceDict[r.id] = r;
        }

        Debug.Log($"[ResourceManager] 初始化完成，共注册 {resources.Count} 种资源");
    }

    // ── 公开 API ──────────────────────────────────────────
    // 所有外部脚本只通过这些方法操作资源，不直接访问 resources 列表。
    // 这是"封装"原则：内部数据结构可以随时重构，外部调用方不受影响。

    /// <summary>
    /// 增加指定资源的世界追踪量。不超过 maxAmount 上限。
    /// 注意：玩家背包量请用 InventoryManager.Add()，此方法仅用于世界数据追踪。
    /// </summary>
    public void Add(string id, float amount)
    {
        if (!resourceDict.TryGetValue(id, out var r))
        { Debug.LogWarning($"[ResourceManager] 未知ID：{id}"); return; }
        r.amount = Mathf.Min(r.amount + amount, r.maxAmount);
        OnResourceChanged?.Invoke(id, r.amount);
    }

    /// <summary>
    /// 消耗指定资源。余量不足时返回 false，不扣除任何资源。
    /// 调用前建议先用 HasEnough() 检查，避免静默失败。
    /// </summary>
    public bool Spend(string id, float amount)
    {
        if (!resourceDict.TryGetValue(id, out var r)) return false;
        if (r.amount < amount) return false;
        r.amount -= amount;
        OnResourceChanged?.Invoke(id, r.amount);
        return true;
    }

    /// <summary>查询指定资源当前数量。未注册的 ID 返回 0。</summary>
    public float Get(string id) =>
        resourceDict.TryGetValue(id, out var r) ? r.amount : 0;

    /// <summary>查询是否有足够数量。常用于 UI 按钮的可用状态判断。</summary>
    public bool HasEnough(string id, float amount) => Get(id) >= amount;

    /// <summary>
    /// 增加每秒自动产出速率。建造采集建筑时调用。
    /// 例：建造一台铁矿采集器 → AddPerSecond("ore_iron", 1f)
    /// </summary>
    public void AddPerSecond(string id, float rate)
    {
        if (resourceDict.TryGetValue(id, out var r)) r.perSecond += rate;
    }

    /// <summary>减少每秒自动产出速率。拆除采集建筑时调用。不会低于0。</summary>
    public void RemovePerSecond(string id, float rate)
    {
        if (resourceDict.TryGetValue(id, out var r))
            r.perSecond = Mathf.Max(0, r.perSecond - rate);
    }

    /// <summary>获取资源的中文显示名称。未注册的 ID 直接返回 ID 本身（方便调试）。</summary>
    public string GetDisplayName(string id) =>
        resourceDict.TryGetValue(id, out var r) ? r.displayName : id;

    /// <summary>获取所有资源列表。用于 UI 遍历显示、存档序列化等。</summary>
    public List<ResourceData> GetAllResources() => resources;

    /// <summary>按层级筛选资源列表。用于背包 UI 分页显示（如只显示 Ingot 层级的金属锭）。</summary>
    public List<ResourceData> GetByTier(ResourceTier tier) =>
        resources.FindAll(r => r.tier == tier);

    /// <summary>查询资源是否为燃料。用于 FuelManager 和燃料机器的投料校验。</summary>
    public bool IsFuel(string id) =>
        resourceDict.TryGetValue(id, out var r) && r.isFuel;

    /// <summary>获取燃料热值（HV）。非燃料返回 0。</summary>
    public float GetHeatValue(string id) =>
        resourceDict.TryGetValue(id, out var r) ? r.heatValue : 0f;

    /// <summary>获取矿藏剩余储量。用于采集点 UI 显示进度条。</summary>
    public float GetDepositAmount(string id) =>
        resourceDict.TryGetValue(id, out var r) ? r.depositAmount : 0f;

    /// <summary>
    /// 按难度系数缩放所有有限矿藏的储量。
    /// 在游戏开始时由 GameManager 调用一次。
    /// 例：multiplier=0.5 → 困难模式，所有矿藏减半。
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

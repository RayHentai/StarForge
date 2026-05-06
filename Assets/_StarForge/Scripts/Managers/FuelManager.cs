// Assets/_StarForge/Scripts/Managers/FuelManager.cs
// 职责：管理燃料物品的热值数据、燃料机器的燃烧消耗
//
// ── 设计决策说明 ────────────────────────────────────────
// 燃料系统独立于电力系统存在，因为：
// 1. 燃料机器在早期科技树中使用，是电力机器出现之前的过渡
// 2. 在电网覆盖不到的边缘场景（v2.0有线电网时）仍需使用
// 3. 燃料的"热值"概念可以扩展到发电机（热值决定发电时长）
//
// ── 热值单位：HV（Heat Value）──────────────────────────
// 1 HV = 驱动1A需求的机器运行1秒
// 例：木头热值=50HV → 驱动1A机器运行50秒，或驱动2A机器运行25秒

using System.Collections.Generic;
using UnityEngine;

public class FuelManager : MonoBehaviour
{
    public static FuelManager Instance { get; private set; }

    // ── 燃料数据配置（在 Inspector 里填入，或由 ScriptableObject 驱动）──
    [System.Serializable]
    public class FuelData
    {
        public string resourceId;       // 对应 ResourceManager 里的资源ID
        public string displayName;      // 显示名
        public float heatValue;         // 热值（HV）
        [TextArea(1,2)]
        public string description;      // 描述，如"普通可燃物，热值低"
    }

    [Header("已知燃料列表（可在Inspector扩展）")]
    public List<FuelData> fuelDatabase = new();

    // 运行时查找表
    private Dictionary<string, FuelData> fuelDict = new();

    // ── 事件 ────────────────────────────────────────────
    public event System.Action<string> OnFuelStartBurning;  // 开始燃烧（资源ID）
    public event System.Action<string> OnFuelExhausted;     // 燃料耗尽（资源ID）

    // ─────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildDict();
        InitDefaultFuels();
    }

    void BuildDict()
    {
        fuelDict.Clear();
        foreach (var f in fuelDatabase)
            if (!string.IsNullOrEmpty(f.resourceId))
                fuelDict[f.resourceId] = f;
    }

    // ── 默认燃料预设（以后由玩家星球设计覆盖）────────────
    void InitDefaultFuels()
    {
        // 如果 Inspector 里已经填了，就不再添加默认值
        if (fuelDatabase.Count > 0) return;

        fuelDatabase = new List<FuelData>
        {
            // 具体资源ID等星球设计确定后替换，这里先用占位符
            new() { resourceId = "raw_a",   displayName = "原矿A（待命名）", heatValue = 20f,  description = "低热值，早期应急燃料" },
            new() { resourceId = "raw_b",   displayName = "原矿B（待命名）", heatValue = 50f,  description = "中热值，基础燃料" },
            new() { resourceId = "chem_a",  displayName = "化工品A（待命名）", heatValue = 150f, description = "高热值，进阶燃料" },
        };

        BuildDict();
    }

    // ─────────────────────────────────────────────────────
    //  公开 API
    // ─────────────────────────────────────────────────────

    /// <summary>查询资源是否为可燃物</summary>
    public bool IsFuel(string resourceId) => fuelDict.ContainsKey(resourceId);

    /// <summary>获取燃料热值，非燃料返回0</summary>
    public float GetHeatValue(string resourceId) =>
        fuelDict.TryGetValue(resourceId, out var f) ? f.heatValue : 0f;

    /// <summary>
    /// 尝试消耗燃料来驱动机器运行。
    /// 返回本次能运行的秒数（消耗1单位燃料 / 机器用电需求）。
    /// 如果资源不足则返回0。
    /// </summary>
    public float ConsumeFuel(string fuelId, float machineDemandA)
    {
        if (!IsFuel(fuelId)) return 0f;
        if (!ResourceManager.Instance.HasEnough(fuelId, 1f)) return 0f;

        ResourceManager.Instance.Spend(fuelId, 1f);
        float hv = GetHeatValue(fuelId);

        // 运行秒数 = 热值 / 用电需求
        // 需求越大，同样热值撑的时间越短
        float runSeconds = machineDemandA > 0 ? hv / machineDemandA : hv;

        OnFuelStartBurning?.Invoke(fuelId);
        Debug.Log($"[FuelManager] 消耗{fuelId}×1，热值{hv}HV，驱动{machineDemandA}A机器运行{runSeconds:F1}秒");
        return runSeconds;
    }

    /// <summary>获取所有燃料数据（用于UI显示）</summary>
    public List<FuelData> GetAllFuels() => fuelDatabase;
}

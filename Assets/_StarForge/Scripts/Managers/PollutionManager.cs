// Assets/_StarForge/Scripts/Managers/PollutionManager.cs
// 职责：追踪全局污染值，触发阈值惩罚，向其他系统广播污染状态
//
// ── 设计决策说明 ────────────────────────────────────────
// 污染是一个"反向资源"：其他资源越多越好，污染越低越好。
// 它作为一个独立 Manager 存在，而不是 ResourceManager 里的负资源，
// 原因是：污染需要触发跨系统的"惩罚效果"，
// 这超出了 ResourceManager 纯数据管理的职责范围。
//
// ── 污染来源（建筑运行时每秒产生）──────────────────────
// 燃料机器：每秒 +0.5 污染（高，驱使玩家尽快转向电力）
// 普通工厂：每秒 +0.1 污染（低，缓慢积累，制造长期压力）
// 发电机（火力）：每秒 +0.3 污染（中）
//
// ── 惩罚效果（通过事件广播，由对应系统响应）────────────
// level >= 50：怪物强度+20%（EnemySpawner 监听 OnWarningReached）
// level >= 80：采集效率-30%（ClickCollector 监听 OnCriticalReached）
// 净化建筑可以降低 perSecond，未来扩展

using System;
using UnityEngine;

public class PollutionManager : MonoBehaviour
{
    public static PollutionManager Instance { get; private set; }

    // ── 阈值 ────────────────────────────────────────────
    [Header("污染阈值")]
    [Range(0, 100)] public float warningThreshold  = 50f;  // 警告阈值
    [Range(0, 100)] public float criticalThreshold = 80f;  // 严重阈值

    // ── 运行时数据 ───────────────────────────────────────
    private float level = 0f;       // 当前污染值 [0, 100]
    private float perSecond = 0f;   // 每秒净增量（建筑产生 - 净化设备消减）

    // ── 事件（其他系统订阅，响应惩罚效果）──────────────
    public event Action<float> OnPollutionChanged;  // 污染值变化（传入新值）
    public event Action OnWarningReached;           // 首次越过警告阈值
    public event Action OnCriticalReached;          // 首次越过严重阈值
    public event Action OnPollutionCleared;         // 从警告状态降回安全

    // ── 状态缓存 ─────────────────────────────────────────
    private bool wasWarning  = false;
    private bool wasCritical = false;

    // ── 公开属性 ─────────────────────────────────────────
    public float Level => level;
    public float PerSecond => perSecond;
    public bool IsWarning  => level >= warningThreshold;
    public bool IsCritical => level >= criticalThreshold;

    // 采集效率修正系数（ClickCollector 用这个值乘以采集量）
    public float CollectEfficiencyMultiplier => IsCritical ? 0.7f : 1.0f;

    // 怪物强度修正系数（EnemySpawner 用这个值）
    public float EnemyStrengthMultiplier => IsWarning ? 1.2f : 1.0f;

    // ─────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        if (perSecond == 0) return;

        // 更新污染值，钳位在 [0, 100]
        level = Mathf.Clamp(level + perSecond * Time.deltaTime, 0f, 100f);
        OnPollutionChanged?.Invoke(level);

        // 检查阈值状态变化
        bool warningNow  = IsWarning;
        bool criticalNow = IsCritical;

        if (warningNow && !wasWarning)
        {
            OnWarningReached?.Invoke();
            Debug.LogWarning($"[PollutionManager] ☣ 污染警告！当前={level:F1}，怪物强度+20%");
        }
        if (criticalNow && !wasCritical)
        {
            OnCriticalReached?.Invoke();
            Debug.LogWarning($"[PollutionManager] ☣ 污染严重！当前={level:F1}，采集效率-30%");
        }
        if (!warningNow && wasWarning)
        {
            OnPollutionCleared?.Invoke();
            Debug.Log("[PollutionManager] 污染恢复正常");
        }

        wasWarning  = warningNow;
        wasCritical = criticalNow;
    }

    // ─────────────────────────────────────────────────────
    //  公开 API：供 BuildingManager 调用
    // ─────────────────────────────────────────────────────

    /// <summary>注册污染源（建造污染建筑时调用）</summary>
    public void AddPollutionPerSecond(float rate)
    {
        perSecond += rate;
        Debug.Log($"[PollutionManager] 污染源+{rate}/s，总计{perSecond}/s");
    }

    /// <summary>注销污染源（拆除建筑时调用）</summary>
    public void RemovePollutionPerSecond(float rate)
    {
        perSecond = Mathf.Max(0, perSecond - rate);
    }

    /// <summary>直接增加污染值（特殊事件触发，如燃料爆炸）</summary>
    public void AddInstantPollution(float amount)
    {
        level = Mathf.Clamp(level + amount, 0f, 100f);
        OnPollutionChanged?.Invoke(level);
    }
}

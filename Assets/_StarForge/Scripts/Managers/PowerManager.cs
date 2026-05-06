// Assets/_StarForge/Scripts/Managers/PowerManager.cs
// 职责：管理全局电力的发电量、用电量、短路判定
//
// ── 设计决策说明 ────────────────────────────────────────
// MVP 阶段：整个星球只有一张"无线电网"，即本 Manager 的单例。
// 所有安装了无线芯片的建筑共享这张网。
// 这样做的好处是：电力逻辑简单（只需比较两个浮点数），
// 后续 v2.0 扩展有线电网时，只需在本类基础上增加"电网分区"概念，
// 不需要推翻现有架构。
//
// ── 电力单位：A（安培，游戏内自定义单位）───────────────
// 1台基础发电机 = 2A，1台基础采集器（电力版）= 1A
// 数值可通过 BuildingData 的扩展字段配置，不硬编码在此处。

using System;
using UnityEngine;

public class PowerManager : MonoBehaviour
{
    public static PowerManager Instance { get; private set; }

    // ── 阈值常量 ────────────────────────────────────────
    private const float WARNING_RATIO = 0.85f;  // 用电超过发电85%时警告
    private const float OVERLOAD_RATIO = 1.0f;  // 用电超过发电100%时短路

    // ── 电力状态 ────────────────────────────────────────
    private float totalGeneration;  // 当前总发电量（A）
    private float totalDemand;      // 当前总用电需求（A）

    // ── 事件 ────────────────────────────────────────────
    // UI和其他系统监听这些事件来响应电力状态变化
    public event Action<float, float> OnPowerChanged;   // (发电量, 用电量)
    public event Action OnOverloadStart;                // 短路开始
    public event Action OnOverloadEnd;                  // 短路结束
    public event Action OnWarning;                      // 进入警告状态

    // ── 状态缓存（用于判断状态是否变化，避免重复触发事件）──
    private bool wasOverloaded = false;

    // ── 公开属性（只读） ─────────────────────────────────
    public float TotalGeneration => totalGeneration;
    public float TotalDemand => totalDemand;
    public float AvailablePower => totalGeneration - totalDemand;
    public bool IsOverloaded => totalDemand > totalGeneration * OVERLOAD_RATIO;
    public bool IsWarning => !IsOverloaded && totalDemand > totalGeneration * WARNING_RATIO;
    // 用电占比，用于UI进度条显示（0~1+）
    public float UsageRatio => totalGeneration > 0 ? totalDemand / totalGeneration : 0f;

    // ─────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        // 每帧检查短路状态变化，触发相应事件
        bool overloadedNow = IsOverloaded;
        if (overloadedNow && !wasOverloaded)
        {
            OnOverloadStart?.Invoke();
            Debug.LogWarning($"[PowerManager] ⚡ 短路！用电{totalDemand:F1}A 超过发电{totalGeneration:F1}A");
        }
        else if (!overloadedNow && wasOverloaded)
        {
            OnOverloadEnd?.Invoke();
            Debug.Log("[PowerManager] 短路解除");
        }
        wasOverloaded = overloadedNow;
    }

    // ─────────────────────────────────────────────────────
    //  公开 API：供 BuildingManager 调用
    // ─────────────────────────────────────────────────────

    /// <summary>注册发电量（建造发电机时调用）</summary>
    public void AddGeneration(float amount)
    {
        totalGeneration += amount;
        OnPowerChanged?.Invoke(totalGeneration, totalDemand);
        Debug.Log($"[PowerManager] 发电+{amount}A，总发电={totalGeneration}A");
    }

    /// <summary>注销发电量（拆除发电机时调用）</summary>
    public void RemoveGeneration(float amount)
    {
        totalGeneration = Mathf.Max(0, totalGeneration - amount);
        OnPowerChanged?.Invoke(totalGeneration, totalDemand);
    }

    /// <summary>注册用电需求（建造用电建筑时调用）</summary>
    public void AddDemand(float amount)
    {
        totalDemand += amount;
        OnPowerChanged?.Invoke(totalGeneration, totalDemand);
        if (IsWarning) OnWarning?.Invoke();
        Debug.Log($"[PowerManager] 用电+{amount}A，总需求={totalDemand}A");
    }

    /// <summary>注销用电需求（拆除建筑时调用）</summary>
    public void RemoveDemand(float amount)
    {
        totalDemand = Mathf.Max(0, totalDemand - amount);
        OnPowerChanged?.Invoke(totalGeneration, totalDemand);
    }

    /// <summary>
    /// 查询建筑是否有足够电力运行。
    /// 短路时所有用电建筑停止产出，燃料建筑不受影响。
    /// </summary>
    public bool IsPowered() => !IsOverloaded;
}

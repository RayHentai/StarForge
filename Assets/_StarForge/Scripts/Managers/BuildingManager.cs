// Assets/_StarForge/Scripts/Managers/BuildingManager.cs
// 职责：处理建筑的建造、计数、移除，调用 ResourceManager 完成资源消耗和产出
// 架构模式：单例（Singleton）
//
// ── 和 ResourceManager 的关系 ────────────────────────────
//   BuildingManager 负责"建造决策"（能不能建、消耗什么）
//   ResourceManager 负责"数据存储"（实际加减数字）
//   两者通过公开 API 通信，不直接访问对方的私有数据

using System.Collections.Generic;
using UnityEngine;

public class BuildingManager : MonoBehaviour
{
    // ── 单例 ─────────────────────────────────────────────
    public static BuildingManager Instance { get; private set; }

    // ── 事件：建造成功时触发，UI 监听用来刷新按钮状态 ────
    public event System.Action<string, int> OnBuildingCountChanged; // (buildingId, newCount)

    // ── 记录每种建筑当前建造了几个 ──────────────────────
    private Dictionary<string, int> buildingCounts = new();

    // ─────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ─────────────────────────────────────────────────────
    //  核心方法：尝试建造一栋建筑
    // ─────────────────────────────────────────────────────

    /// <summary>
    /// 尝试建造一个建筑。
    /// 流程：检查上限 → 检查并扣除资源 → 注册产出 → 触发事件
    /// 返回：是否建造成功
    /// </summary>
    public bool TryBuild(BuildingData data)
    {
        if (data == null) return false;

        // 1. 检查建造上限
        int currentCount = GetCount(data.buildingId);
        if (data.maxCount >= 0 && currentCount >= data.maxCount)
        {
            Debug.Log($"[BuildingManager] {data.displayName} 已达建造上限 {data.maxCount}");
            return false;
        }

        // 2. 检查资源是否足够（先检查，再扣除，避免部分扣除）
        foreach (var cost in data.costs)
        {
            if (!ResourceManager.Instance.HasEnough(cost.resourceId, cost.amount))
            {
                string name = ResourceManager.Instance.GetDisplayName(cost.resourceId);
                Debug.Log($"[BuildingManager] 资源不足：需要 {name} ×{cost.amount}");
                return false;
            }
        }

        // 3. 扣除所有费用
        foreach (var cost in data.costs)
            ResourceManager.Instance.Spend(cost.resourceId, cost.amount);

        // 4. 注册产出到 ResourceManager
        if (!string.IsNullOrEmpty(data.outputResourceId) && data.outputPerSecond > 0)
            ResourceManager.Instance.AddPerSecond(data.outputResourceId, data.outputPerSecond);

        // 5. 记录数量，触发事件
        buildingCounts[data.buildingId] = currentCount + 1;
        OnBuildingCountChanged?.Invoke(data.buildingId, buildingCounts[data.buildingId]);

        Debug.Log($"[BuildingManager] 建造成功：{data.displayName}（当前共 {buildingCounts[data.buildingId]} 个）");
        return true;
    }

    // ─────────────────────────────────────────────────────
    //  辅助查询
    // ─────────────────────────────────────────────────────

    /// <summary>查询某建筑当前建了多少个</summary>
    public int GetCount(string buildingId) =>
        buildingCounts.TryGetValue(buildingId, out int c) ? c : 0;

    /// <summary>检查某建筑是否还能继续建造（用于UI显示按钮是否可点击）</summary>
    public bool CanBuild(BuildingData data)
    {
        if (data == null) return false;

        // 检查上限
        if (data.maxCount >= 0 && GetCount(data.buildingId) >= data.maxCount)
            return false;

        // 检查资源
        foreach (var cost in data.costs)
            if (!ResourceManager.Instance.HasEnough(cost.resourceId, cost.amount))
                return false;

        return true;
    }
}

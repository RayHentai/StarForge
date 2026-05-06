// Assets/_StarForge/Scripts/Managers/BuildingManager.cs
// 职责：建筑的建造、计数、移除，协调 Resource/Power/Pollution 三个系统
//
// ── 建造时的完整流程 ─────────────────────────────────────
// 1. 检查建造上限
// 2. 检查资源是否足够
// 3. 扣除资源
// 4. 注册资源产出（ResourceManager）
// 5. 注册电力变化（PowerManager）：发电机+发电量，用电建筑+用电需求
// 6. 注册污染（PollutionManager）
// 7. 记录数量，触发事件

using System.Collections.Generic;
using UnityEngine;

public class BuildingManager : MonoBehaviour
{
    public static BuildingManager Instance { get; private set; }

    public event System.Action<string, int> OnBuildingCountChanged;

    private Dictionary<string, int> buildingCounts = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ─────────────────────────────────────────────────────
    //  核心方法：尝试建造
    // ─────────────────────────────────────────────────────
    public bool TryBuild(BuildingData data)
    {
        if (data == null) return false;

        // 1. 检查上限
        int currentCount = GetCount(data.buildingId);
        if (data.maxCount >= 0 && currentCount >= data.maxCount)
        {
            Debug.Log($"[BuildingManager] {data.displayName} 已达上限");
            return false;
        }

        // 2. 检查资源
        foreach (var cost in data.costs)
            if (!ResourceManager.Instance.HasEnough(cost.resourceId, cost.amount))
            {
                Debug.Log($"[BuildingManager] 资源不足：{cost.resourceId} ×{cost.amount}");
                return false;
            }

        // 3. 扣除资源
        foreach (var cost in data.costs)
            ResourceManager.Instance.Spend(cost.resourceId, cost.amount);

        // 4. 注册资源产出
        if (!string.IsNullOrEmpty(data.outputResourceId) && data.outputPerSecond > 0)
            ResourceManager.Instance.AddPerSecond(data.outputResourceId, data.outputPerSecond);

        // 5. 注册电力
        if (data.powerGeneration > 0)
            PowerManager.Instance.AddGeneration(data.powerGeneration);
        else if (data.powerDemand > 0 && !data.isFuelPowered)
            PowerManager.Instance.AddDemand(data.powerDemand);

        // 6. 注册污染
        if (data.pollutionPerSecond != 0)
            PollutionManager.Instance.AddPollutionPerSecond(data.pollutionPerSecond);

        // 7. 记录数量，触发事件
        buildingCounts[data.buildingId] = currentCount + 1;
        OnBuildingCountChanged?.Invoke(data.buildingId, buildingCounts[data.buildingId]);

        Debug.Log($"[BuildingManager] ✓ 建造：{data.displayName}（{data.BuildingType}）× {buildingCounts[data.buildingId]}");
        return true;
    }

    // ─────────────────────────────────────────────────────
    //  查询
    // ─────────────────────────────────────────────────────
    public int GetCount(string buildingId) =>
        buildingCounts.TryGetValue(buildingId, out int c) ? c : 0;

    public bool CanBuild(BuildingData data)
    {
        if (data == null) return false;
        if (data.maxCount >= 0 && GetCount(data.buildingId) >= data.maxCount) return false;
        foreach (var cost in data.costs)
            if (!ResourceManager.Instance.HasEnough(cost.resourceId, cost.amount)) return false;
        return true;
    }
}

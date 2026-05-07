// Assets/_StarForge/Scripts/Managers/BuildingManager.cs
// 职责：建筑的建造、计数，协调 Inventory/Power/Pollution 三个系统
//
// ── 费用来源变更 ─────────────────────────────────────────
// 旧版：从 ResourceManager 扣除（世界数据，不对）
// 新版：从 InventoryManager 扣除（玩家背包，正确）

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

    public bool TryBuild(BuildingData data)
    {
        if (data == null) return false;

        int cur = GetCount(data.buildingId);
        if (data.maxCount >= 0 && cur >= data.maxCount) return false;

        // 检查背包材料
        foreach (var cost in data.costs)
            if (!InventoryManager.Instance.HasEnough(cost.resourceId, cost.amount))
            {
                Debug.Log($"[BuildingManager] 背包不足：{cost.resourceId} ×{cost.amount}");
                return false;
            }

        // 从背包扣除
        foreach (var cost in data.costs)
            InventoryManager.Instance.Spend(cost.resourceId, cost.amount);

        // 电力
        if (data.powerGeneration > 0)
            PowerManager.Instance.AddGeneration(data.powerGeneration);
        else if (data.powerDemand > 0 && !data.isFuelPowered)
            PowerManager.Instance.AddDemand(data.powerDemand);

        // 污染
        if (data.pollutionPerSecond != 0)
            PollutionManager.Instance.AddPollutionPerSecond(data.pollutionPerSecond);

        buildingCounts[data.buildingId] = cur + 1;
        OnBuildingCountChanged?.Invoke(data.buildingId, buildingCounts[data.buildingId]);

        Debug.Log($"[BuildingManager] ✓ {data.displayName}（{data.BuildingType}）×{buildingCounts[data.buildingId]}");
        return true;
    }

    public int GetCount(string id) =>
        buildingCounts.TryGetValue(id, out int c) ? c : 0;

    public bool CanBuild(BuildingData data)
    {
        if (data == null) return false;
        if (data.maxCount >= 0 && GetCount(data.buildingId) >= data.maxCount) return false;
        foreach (var cost in data.costs)
            if (!InventoryManager.Instance.HasEnough(cost.resourceId, cost.amount)) return false;
        return true;
    }
}

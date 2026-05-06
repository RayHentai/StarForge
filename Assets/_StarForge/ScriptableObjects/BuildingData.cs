// Assets/_StarForge/ScriptableObjects/BuildingData.cs
// 建筑配置数据，通过 ScriptableObject 在 Inspector 里填写
// 右键 → Create → StarForge → Building Data

using UnityEngine;

[CreateAssetMenu(menuName = "StarForge/Building Data", fileName = "New Building")]
public class BuildingData : ScriptableObject
{
    [Header("基本信息")]
    public string buildingId;
    public string displayName;
    [TextArea] public string description;

    [Header("建造费用")]
    public ResourceCost[] costs;

    [Header("资源产出（留空则无产出）")]
    public string outputResourceId;
    public float outputPerSecond;

    // ── 电力配置 ─────────────────────────────────────────
    [Header("电力配置")]
    [Tooltip("发电量（A）。发电机填此项，普通建筑填0")]
    public float powerGeneration = 0f;

    [Tooltip("用电需求（A）。电力建筑填此项，燃料建筑填0")]
    public float powerDemand = 0f;

    // ── 燃料配置 ─────────────────────────────────────────
    [Header("燃料配置")]
    [Tooltip("是否为燃料驱动建筑（早期过渡，不消耗电力）")]
    public bool isFuelPowered = false;

    [Tooltip("燃料建筑的用电当量（A）。决定燃料消耗速度：热值/此值=运行秒数）")]
    public float fuelDemandEquivalent = 1f;

    // ── 污染配置 ─────────────────────────────────────────
    [Header("污染配置")]
    [Tooltip("建筑运行时每秒产生的污染值。燃料建筑填高，电力建筑填低，净化建筑填负数")]
    public float pollutionPerSecond = 0f;

    [Header("限制")]
    public int maxCount = 999;

    // ── 嵌套类 ───────────────────────────────────────────
    [System.Serializable]
    public class ResourceCost
    {
        public string resourceId;
        public float amount;
    }

    // ── 编辑器辅助：在 Inspector 里显示建筑类型标签 ──────
    public string BuildingType
    {
        get
        {
            if (powerGeneration > 0) return "发电机";
            if (isFuelPowered)       return "燃料建筑";
            if (powerDemand > 0)     return "电力建筑";
            return "基础建筑";
        }
    }
}

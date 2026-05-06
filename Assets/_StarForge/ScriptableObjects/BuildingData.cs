// Assets/_StarForge/ScriptableObjects/BuildingData.cs
// 这是一个 ScriptableObject，用来存储建筑的配置数据
// 使用方式：在 Unity 里右键 → Create → StarForge → Building Data
//           创建出来的文件就是一个"采集器配置"，在 Inspector 里填写数值
//
// ── 为什么用 ScriptableObject 而不是直接写在代码里？ ──────
//   1. 数据和逻辑分离：改数值不用动代码，不用重新编译
//   2. 策划友好：直接在 Inspector 里调整，所见即所得
//   3. 复用性强：一份代码，十几种建筑配置，每种配置一个文件

using UnityEngine;

[CreateAssetMenu(menuName = "StarForge/Building Data", fileName = "New Building")]
public class BuildingData : ScriptableObject
{
    [Header("基本信息")]
    public string buildingId;           // 唯一ID，如 "collector_basic"
    public string displayName;          // 显示名称，如 "基础采集器"
    [TextArea] public string description; // 描述文字

    [Header("建造费用（消耗的资源）")]
    public ResourceCost[] costs;        // 支持多种资源同时消耗

    [Header("产出设置")]
    public string outputResourceId;     // 产出的资源ID
    public float outputPerSecond;       // 每秒产出量

    [Header("限制")]
    public int maxCount = 999;          // 最多可建造几个（-1 = 无限）

    // ── 嵌套类：单项费用 ────────────────────────────────────
    [System.Serializable]
    public class ResourceCost
    {
        public string resourceId;       // 消耗的资源ID
        public float amount;            // 消耗数量
    }
}

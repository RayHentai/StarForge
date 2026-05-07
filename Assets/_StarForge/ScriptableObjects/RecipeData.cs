// Assets/_StarForge/ScriptableObjects/RecipeData.cs
// 每一个加工步骤都是一个 RecipeData 文件
// 右键 → Create → StarForge → Recipe Data
//
// ── 设计模式：数据驱动（Data-Driven Design）──────────────
// 所有配方逻辑都在这个数据文件里定义，
// ProcessingBuilding 只需读取这个文件来运行，
// 增加新配方 = 新建一个 ScriptableObject 文件，不改任何代码。
//
// ── 命名规范（物流过滤器依赖此规范）────────────────────
// ore_[矿物]          原矿        ore_iron
// crushed_[矿物]      粉碎矿石    crushed_iron
// ore_washed_[矿物]   洗净矿石    ore_washed_iron
// powder_impure_[矿]  含杂粉      powder_impure_iron
// powder_washed_[矿]  矿粉        powder_washed_iron
// powder_clean_[矿]   洁净粉      powder_clean_iron
// ingot_[矿物]        金属锭      ingot_iron
// crystal_[矿物]      晶体产物    crystal_silicon
// gem_[宝石]          宝石成品    gem_diamond

using UnityEngine;

[CreateAssetMenu(menuName = "StarForge/Recipe Data", fileName = "New Recipe")]
public class RecipeData : ScriptableObject
{
    [Header("配方基本信息")]
    public string recipeId;             // 唯一ID，如 "crush_iron_basic"
    public string displayName;          // 显示名，如 "粉碎铁矿石"
    [TextArea] public string description;

    [Header("适用建筑类型")]
    public MachineType[] allowedMachines; // 哪些机器可以执行此配方

    [Header("科技树解锁（留空=默认解锁）")]
    public string requiredTechId;       // 需要解锁的科技ID，空=直接可用

    [Header("加工时间")]
    public float processingTime = 2f;   // 单次加工耗时（秒）

    [Header("输入材料（支持多输入）")]
    public ItemStack[] inputs;          // 输入槽，配方决定槽位数量

    [Header("固定输出")]
    public ItemStack output;            // 主产物

    [Header("副产物（概率触发）")]
    public ByproductEntry[] byproducts; // 可配置多个副产物

    // ── 建筑类型枚举 ─────────────────────────────────────
    // 新增建筑类型在这里添加，RecipeData 自动支持
    public enum MachineType
    {
        Crusher,        // 粉碎机
        Washer,         // 洗矿机
        Smelter,        // 熔炉
        BlastFurnace,   // 高炉
        Sorter,         // 筛选机
    }

    // ── 数据结构：物品堆叠 ───────────────────────────────
    [System.Serializable]
    public class ItemStack
    {
        public string resourceId;   // 资源ID
        public float amount;        // 数量
    }

    // ── 数据结构：副产物条目 ─────────────────────────────
    [System.Serializable]
    public class ByproductEntry
    {
        public string resourceId;   // 副产物资源ID
        public float amount;        // 数量
        [Range(0f, 1f)]
        public float probability;   // 触发概率（0~1，如0.1=10%）
    }

    // ─────────────────────────────────────────────────────
    //  查询方法（供 ProcessingBuilding 调用）
    // ─────────────────────────────────────────────────────

    /// <summary>此配方是否可在指定机器类型上运行</summary>
    public bool IsCompatibleWith(MachineType machine)
    {
        foreach (var m in allowedMachines)
            if (m == machine) return true;
        return false;
    }

    /// <summary>检查是否已通过科技树解锁</summary>
    public bool IsUnlocked()
    {
        if (string.IsNullOrEmpty(requiredTechId)) return true;
        // TODO：接入 TechTreeManager 后替换此处
        // return TechTreeManager.Instance.IsUnlocked(requiredTechId);
        return false; // 暂时所有需要科技的配方默认锁定
    }

    /// <summary>检查 ResourceManager 里是否有足够的输入材料</summary>
    public bool CanProcess()
    {
        foreach (var input in inputs)
            if (!ResourceManager.Instance.HasEnough(input.resourceId, input.amount))
                return false;
        return true;
    }

    /// <summary>
    /// 执行一次加工：扣除输入，产出结果，计算副产物。
    /// 副产物使用 System.Random（非 Unity Random）以获得更好的性能。
    /// 返回本次实际产出的所有物品（用于日志和UI飘字）。
    /// </summary>
    public System.Collections.Generic.List<ItemStack> Execute(System.Random rng)
    {
        var results = new System.Collections.Generic.List<ItemStack>();

        // 1. 扣除所有输入
        foreach (var input in inputs)
            ResourceManager.Instance.Spend(input.resourceId, input.amount);

        // 2. 固定产出
        ResourceManager.Instance.Add(output.resourceId, output.amount);
        results.Add(new ItemStack { resourceId = output.resourceId, amount = output.amount });

        // 3. 副产物（概率触发，用 System.Random 而非 Unity Random.value）
        // 原因：System.Random 在非主线程也可使用，且不依赖 Unity 生命周期
        foreach (var bp in byproducts)
        {
            if (rng.NextDouble() < bp.probability)
            {
                ResourceManager.Instance.Add(bp.resourceId, bp.amount);
                results.Add(new ItemStack { resourceId = bp.resourceId, amount = bp.amount });
            }
        }

        return results;
    }
}

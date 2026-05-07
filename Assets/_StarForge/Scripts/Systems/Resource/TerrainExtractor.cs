// Assets/_StarForge/Scripts/Systems/Resource/TerrainExtractor.cs
// 职责：机器自动从地形采集点（水域/油田/熔岩田）持续抽取资源
// 挂载位置：挂在抽水机/采油机/熔岩泵等建筑上
//
// ── 工作流程 ─────────────────────────────────────────────
// 1. 在 Inspector 里把附近的地形采集点（ClickCollector）拖入 targetNode
// 2. 机器开机后，每秒按 extractRate 向采集点的缓存注入资源
// 3. 同时从采集点缓存取货，放入机器自身的输出缓存
// 4. 玩家点击机器 → 取走输出缓存 → 进背包
// 5. 输出缓存满 → 机器暂停，等待玩家取货
//
// ── 和 ClickCollector 的关系 ─────────────────────────────
// ClickCollector = 采集点（资源在哪）
// TerrainExtractor = 采集机器（谁在采）
// 两者分离，一个采集点可以有多台机器同时抽取

using UnityEngine;

public class TerrainExtractor : MonoBehaviour
{
    [Header("目标采集点（拖入场景中的地形节点）")]
    public ClickCollector targetNode;

    [Header("采集配置")]
    public float extractRate = 2f;          // 每秒采集量
    public float outputBufferCapacity = 100f; // 输出缓存上限

    [Header("电力配置")]
    public bool requiresPower = true;
    public float powerConsumption = 1f;

    // ── 运行时状态 ────────────────────────────────────────
    public bool IsOn { get; private set; } = false;
    public float OutputBuffer { get; private set; } = 0f;
    public bool IsOutputFull => OutputBuffer >= outputBufferCapacity;
    public string ResourceId => targetNode != null ? targetNode.resourceId : "";

    // 事件
    public event System.Action<float> OnOutputBufferChanged;
    public event System.Action<bool>  OnRunningStateChanged; // true=运行中

    private float extractTimer = 0f;

    // ─────────────────────────────────────────────────────
    void Update()
    {
        if (!IsOn || targetNode == null) return;
        if (requiresPower && !PowerManager.Instance.IsPowered()) return;
        if (IsOutputFull) return;

        extractTimer += Time.deltaTime;
        if (extractTimer < 1f) return; // 每秒采集一次
        extractTimer = 0f;

        // 向采集点缓存注入资源（由采集点控制矿藏消耗）
        bool success = targetNode.AutoCollect(extractRate);
        if (!success) return;

        // 从采集点缓存取出，放入本机输出缓存
        float pulled = targetNode.PullFromBuffer(extractRate);
        if (pulled <= 0) return;

        OutputBuffer = Mathf.Min(OutputBuffer + pulled, outputBufferCapacity);
        OnOutputBufferChanged?.Invoke(OutputBuffer);
    }

    // ─────────────────────────────────────────────────────
    //  公开 API
    // ─────────────────────────────────────────────────────

    public void SetPower(bool on)
    {
        IsOn = on;
        OnRunningStateChanged?.Invoke(on);
    }

    /// <summary>玩家取走输出缓存，进背包</summary>
    public float CollectOutput(float amount)
    {
        float actual = Mathf.Min(amount, OutputBuffer);
        if (actual <= 0) return 0;
        OutputBuffer -= actual;
        InventoryManager.Instance.Add(ResourceId, actual);
        OnOutputBufferChanged?.Invoke(OutputBuffer);
        return actual;
    }

    public void CollectAllOutput() => CollectOutput(OutputBuffer);
}

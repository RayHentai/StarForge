// Assets/_StarForge/Scripts/Systems/Resource/ClickCollector.cs
// 职责：挂在星球表面的【采集点】GameObject 上，玩家点击该采集点触发采集
//
// ── 采集点是什么 ──────────────────────────────────────────
// 采集点是生成在星球表面的特定对象，例如：
//   铁矿节点（挂 resourceId = ore_iron）
//   油田节点（挂 resourceId = res_oil，mode = Terrain）
//   水域节点（挂 resourceId = res_water，mode = Terrain）
//   熔岩田节点（挂 resourceId = ore_lava，mode = Terrain）
//
// 点击采集点 → 资源进玩家背包（InventoryManager）
//
// ── 两种模式 ─────────────────────────────────────────────
// Normal  ：矿石类，每次点击固定产出，受矿藏量限制
// Terrain ：地形资源，产出量受 terrainScale 影响，
//           水无限，原油/熔岩受矿藏量限制
//
// ── 产出资源区分（Normal 模式） ───────────────────────────
// 同一采集点根据采集方式产出不同资源：
//   玩家点击 / 燃料驱动采矿机 → outputResourceId（如 crushed_iron / gem_ruby）
//   电力采矿机               → electricOutputResourceId（如 ore_iron）
//   electricOutputResourceId 留空时，电力采矿机也使用 outputResourceId
//
// resourceId 仅用于矿藏量（deposit）计算，不再作为产出 ID。
// Terrain 模式不区分产出，始终产出 resourceId 对应资源（原逻辑不变）。

using UnityEngine;
using TMPro;

public class ClickCollector : MonoBehaviour
{
    public enum CollectMode { Normal, Terrain }

    [Header("采集点配置")]
    public CollectMode mode = CollectMode.Normal;

    /// <summary>
    /// 矿藏 ID。仅用于查询/消耗矿藏储量（deposit），不作为任何产出资源的 ID。
    /// Terrain 模式下同时作为产出 ID（保持原逻辑）。
    /// </summary>
    public string resourceId = "ore_iron";

    /// <summary>
    /// 【Normal 模式】玩家点击 / 燃料驱动采矿机的产出资源 ID。
    /// 例：矿石节点填 crushed_iron，宝石节点填 gem_ruby。
    /// </summary>
    public string outputResourceId = "";

    /// <summary>
    /// 【Normal 模式】电力采矿机的产出资源 ID。
    /// 留空时自动回退到 outputResourceId。
    /// 例：填 ore_iron 表示电力采矿机产出精矿。
    /// </summary>
    public string electricOutputResourceId = "";

    public float collectAmountPerClick = 1f;
    public float clickCooldown = 0.2f;

    [Header("地形模式")]
    [Tooltip("地形规模，影响每次采集量。水域/油田面积越大，值越高。")]
    public float terrainScale = 1f;

    [Header("内部缓存（机器自动采集时使用，手动点击跳过）")]
    public float bufferCapacity = 100f;
    [HideInInspector] public float currentBuffer = 0f;
    public bool IsBufferFull => currentBuffer >= bufferCapacity;

    [Header("视觉反馈")]
    public GameObject floatingTextPrefab;
    public Transform floatingTextSpawnPos;

    // 事件：供机器采集系统（TerrainExtractor）监听
    public event System.Action<float> OnBufferChanged;

    private float lastClickTime;
    private Camera mainCamera;

    void Start() => mainCamera = Camera.main;

    void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (Time.time - lastClickTime < clickCooldown) return;

        Vector2 worldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        var hit = Physics2D.Raycast(worldPos, Vector2.zero);
        if (hit.collider == null || hit.collider.gameObject != gameObject) return;

        TryCollect();
    }

    void TryCollect()
    {
        lastClickTime = Time.time;

        // 检查矿藏（非可再生资源）
        if (!IsRenewable() && ResourceManager.Instance.GetDepositAmount(resourceId) <= 0)
        {
            ShowFloatingText("资源已耗尽");
            return;
        }

        float amount = collectAmountPerClick;

        if (mode == CollectMode.Terrain)
        {
            // Terrain 模式：产出资源本身，产出量受 terrainScale 影响
            amount *= terrainScale;
            ConsumeDeposit(amount);
            InventoryManager.Instance.Add(resourceId, amount);
            ShowFloatingText($"+{amount} {ResourceManager.Instance.GetDisplayName(resourceId)}");
        }
        else
        {
            // Normal 模式：玩家点击产出 outputResourceId 对应资源
            string outId = ResolveOutputId(false);
            ConsumeDeposit(amount);
            InventoryManager.Instance.Add(outId, amount);
            ShowFloatingText($"+{amount} {ResourceManager.Instance.GetDisplayName(outId)}");
        }

        StartCoroutine(BounceEffect());
    }

    // ─────────────────────────────────────────────────────
    //  机器自动采集 API
    // ─────────────────────────────────────────────────────

    /// <summary>
    /// 燃料驱动采矿机（暂定名）每帧/每秒调用，尝试向内部缓存注入资源。
    /// 产出使用 outputResourceId（与玩家点击一致）。
    /// 缓存满 → 返回 false，机器停机等待。
    /// </summary>
    public bool AutoCollect(float amount)
    {
        if (IsBufferFull) return false;
        if (!IsRenewable() && ResourceManager.Instance.GetDepositAmount(resourceId) <= 0)
            return false;

        float actual = Mathf.Min(amount, bufferCapacity - currentBuffer);
        ConsumeDeposit(actual);
        currentBuffer += actual;
        OnBufferChanged?.Invoke(currentBuffer);
        return true;
    }

    /// <summary>
    /// 电力采矿机每帧/每秒调用，尝试向内部缓存注入资源。
    /// 产出使用 electricOutputResourceId；留空时回退到 outputResourceId。
    /// 缓存满 → 返回 false，机器停机等待。
    /// </summary>
    public bool AutoCollectElectric(float amount)
    {
        if (IsBufferFull) return false;
        if (!IsRenewable() && ResourceManager.Instance.GetDepositAmount(resourceId) <= 0)
            return false;

        float actual = Mathf.Min(amount, bufferCapacity - currentBuffer);
        ConsumeDeposit(actual);
        currentBuffer += actual;
        OnBufferChanged?.Invoke(currentBuffer);
        return true;
    }

    /// <summary>
    /// 机器/物流从缓存取货，进玩家背包（当前阶段）。
    /// isElectric 为 true 时按电力产出 ID 结算，否则按普通产出 ID 结算。
    /// 返回实际取出的数量。
    /// </summary>
    public float PullFromBuffer(float amount, bool isElectric = false)
    {
        float actual = Mathf.Min(amount, currentBuffer);
        if (actual <= 0) return 0;

        string outId = mode == CollectMode.Terrain
            ? resourceId
            : ResolveOutputId(isElectric);

        currentBuffer -= actual;
        InventoryManager.Instance.Add(outId, actual);
        OnBufferChanged?.Invoke(currentBuffer);
        return actual;
    }

    // ─────────────────────────────────────────────────────
    //  内部工具
    // ─────────────────────────────────────────────────────

    /// <summary>
    /// 解析实际产出资源 ID（仅 Normal 模式使用）。
    /// isElectric = true → electricOutputResourceId，留空则回退到 outputResourceId。
    /// isElectric = false → outputResourceId。
    /// outputResourceId 本身也留空时，作为最终兜底回退到 resourceId（向后兼容旧数据）。
    /// </summary>
    string ResolveOutputId(bool isElectric)
    {
        if (isElectric && !string.IsNullOrEmpty(electricOutputResourceId))
            return electricOutputResourceId;

        if (!string.IsNullOrEmpty(outputResourceId))
            return outputResourceId;

        // 兜底：旧采集点未填写 outputResourceId 时行为不变
        return resourceId;
    }

    bool IsRenewable()
    {
        var res = ResourceManager.Instance.GetAllResources()
            .Find(r => r.id == resourceId);
        return res != null && res.isRenewable;
    }

    void ConsumeDeposit(float amount)
    {
        // ResourceManager 只负责记录矿藏减少，不记录玩家持有量
        var res = ResourceManager.Instance.GetAllResources()
            .Find(r => r.id == resourceId);
        if (res != null && res.hasDeposit)
            res.depositAmount = Mathf.Max(0, res.depositAmount - amount);
    }

    void ShowFloatingText(string text)
    {
        if (floatingTextPrefab == null) return;
        Vector3 pos = floatingTextSpawnPos != null
            ? floatingTextSpawnPos.position
            : transform.position + Vector3.up;
        var obj = Instantiate(floatingTextPrefab, pos, Quaternion.identity);
        var tmp = obj.GetComponentInChildren<TextMeshPro>();
        if (tmp != null) tmp.text = text;
        Destroy(obj, 1.5f);
    }

    System.Collections.IEnumerator BounceEffect()
    {
        Vector3 orig = transform.localScale, big = orig * 1.15f;
        for (float t = 0; t < 0.08f; t += Time.deltaTime)
        { transform.localScale = Vector3.Lerp(orig, big, t / 0.08f); yield return null; }
        for (float t = 0; t < 0.1f; t += Time.deltaTime)
        { transform.localScale = Vector3.Lerp(big, orig, t / 0.1f); yield return null; }
        transform.localScale = orig;
    }
}

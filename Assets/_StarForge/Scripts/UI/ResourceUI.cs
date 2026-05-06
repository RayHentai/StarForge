// Assets/_StarForge/Scripts/UI/ResourceUI.cs
// 职责：动态显示所有资源的当前数量和每秒产出
// 升级点：不再硬编码资源ID，自动遍历 ResourceManager 里注册的所有资源
//         以后新增资源只需要在 ResourceManager 里注册，UI 自动跟上

using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ResourceUI : MonoBehaviour
{
    [Header("资源文字的父容器（运行时自动生成子Text）")]
    [SerializeField] private Transform resourceListContainer; // 拖入一个空的父物体

    [Header("单行资源文字预制体（可选，不填则自动创建）")]
    [SerializeField] private GameObject resourceLinePrefab;

    // 运行时缓存：resourceId → 对应的 TextMeshProUGUI
    private Dictionary<string, TextMeshProUGUI> resourceTexts = new();

    // ─────────────────────────────────────────────────────
    void Start()
    {
        BuildResourceList();
        ResourceManager.Instance.OnResourceChanged += OnResourceChanged;
        RefreshAll();
    }

    void OnDestroy()
    {
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.OnResourceChanged -= OnResourceChanged;
    }

    // ─────────────────────────────────────────────────────
    //  根据 ResourceManager 里的资源列表，动态创建UI行
    // ─────────────────────────────────────────────────────
    void BuildResourceList()
    {
        if (resourceListContainer == null)
        {
            Debug.LogWarning("[ResourceUI] 请在 Inspector 里设置 resourceListContainer");
            return;
        }

        // 清空旧内容
        foreach (Transform child in resourceListContainer)
            Destroy(child.gameObject);

        resourceTexts.Clear();

        // 遍历所有资源，为每种资源创建一行文字
        foreach (var r in ResourceManager.Instance.GetAllResources())
        {
            GameObject lineObj;

            if (resourceLinePrefab != null)
            {
                // 使用自定义预制体
                lineObj = Instantiate(resourceLinePrefab, resourceListContainer);
            }
            else
            {
                // 没有预制体时自动创建一个 TextMeshPro 对象
                lineObj = new GameObject($"Resource_{r.id}");
                lineObj.transform.SetParent(resourceListContainer, false);
                lineObj.AddComponent<TextMeshProUGUI>();

                // 基础样式
                var tmp = lineObj.GetComponent<TextMeshProUGUI>();
                tmp.fontSize = 30;
                tmp.color = Color.white;
            }

            var text = lineObj.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = FormatResource(r.displayName, r.amount, r.perSecond);
                resourceTexts[r.id] = text;
            }
        }
    }

    // ─────────────────────────────────────────────────────
    //  事件回调：某个资源变化时只更新那一行
    // ─────────────────────────────────────────────────────
    void OnResourceChanged(string id, float amount)
    {
        if (!resourceTexts.TryGetValue(id, out var text)) return;

        float perSec = 0;
        // 从 ResourceManager 里取最新的产出速率
        foreach (var r in ResourceManager.Instance.GetAllResources())
        {
            if (r.id == id) { perSec = r.perSecond; break; }
        }

        text.text = FormatResource(ResourceManager.Instance.GetDisplayName(id), amount, perSec);
    }

    // ─────────────────────────────────────────────────────
    //  刷新所有资源显示
    // ─────────────────────────────────────────────────────
    void RefreshAll()
    {
        foreach (var r in ResourceManager.Instance.GetAllResources())
        {
            if (resourceTexts.TryGetValue(r.id, out var text))
                text.text = FormatResource(r.displayName, r.amount, r.perSecond);
        }
    }

    // ─────────────────────────────────────────────────────
    //  格式化文字
    //  有产出时显示：矿石：12  (+1.0/s)
    //  没产出时显示：矿石：12
    // ─────────────────────────────────────────────────────
    string FormatResource(string displayName, float amount, float perSecond)
    {
        string base_ = $"{displayName}：{Mathf.FloorToInt(amount)}";
        if (perSecond > 0)
            base_ += $"  <color=#88ff88>(+{perSecond:F1}/s)</color>";
        return base_;
    }
}

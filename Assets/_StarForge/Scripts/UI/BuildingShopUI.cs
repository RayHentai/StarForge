// Assets/_StarForge/Scripts/UI/BuildingShopUI.cs
// 职责：显示建筑列表、建造按钮、费用说明，处理玩家点击建造
// 挂载位置：挂在 Canvas 下的 ShopPanel GameObject 上
//
// ── Inspector 配置步骤（完整说明在文件末尾）────────────────

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BuildingShopUI : MonoBehaviour
{
    [Header("要显示的建筑列表（拖入 ScriptableObject 文件）")]
    public BuildingData[] buildings;

    [Header("按钮模板（拖入预制体）")]
    public GameObject buildingButtonPrefab;

    [Header("按钮生成的父容器（拖入 ScrollView 的 Content）")]
    public Transform buttonContainer;

    // ─────────────────────────────────────────────────────
    void Start()
    {
        GenerateButtons();

        // 监听资源变化，实时更新按钮可用状态
        ResourceManager.Instance.OnResourceChanged += OnResourceChanged;
        BuildingManager.Instance.OnBuildingCountChanged += OnBuildingChanged;
    }

    void OnDestroy()
    {
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.OnResourceChanged -= OnResourceChanged;
        if (BuildingManager.Instance != null)
            BuildingManager.Instance.OnBuildingCountChanged -= OnBuildingChanged;
    }

    // ─────────────────────────────────────────────────────
    //  生成按钮
    // ─────────────────────────────────────────────────────

    void GenerateButtons()
    {
        if (buttonContainer == null || buildingButtonPrefab == null) return;

        // 清空旧按钮（防止重复生成）
        foreach (Transform child in buttonContainer)
            Destroy(child.gameObject);

        // 为每种建筑生成一个按钮
        foreach (var building in buildings)
        {
            if (building == null) continue;

            var btnObj = Instantiate(buildingButtonPrefab, buttonContainer);
            btnObj.name = $"Btn_{building.buildingId}";

            // 找到按钮上的子组件并填入数据
            // ⚠️ 这些名字要和你的预制体层级结构匹配
            SetText(btnObj, "NameText", building.displayName);
            SetText(btnObj, "DescText", building.description);
            SetText(btnObj, "OutputText", $"+{building.outputPerSecond}/s {ResourceManager.Instance.GetDisplayName(building.outputResourceId)}");

            // 费用文字：拼成 "矿石×10" 格式
            string costStr = "";
            foreach (var cost in building.costs)
                costStr += $"{ResourceManager.Instance.GetDisplayName(cost.resourceId)} ×{cost.amount}  ";
            SetText(btnObj, "CostText", costStr.TrimEnd());

            // 绑定建造按钮点击事件
            // 用局部变量捕获，避免闭包陷阱（常见 Unity 坑）
            var capturedBuilding = building;
            var btn = btnObj.GetComponentInChildren<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => OnBuildButtonClicked(capturedBuilding));
        }

        // 生成后刷新一次按钮状态
        RefreshAllButtons();
    }

    // ─────────────────────────────────────────────────────
    //  按钮点击处理
    // ─────────────────────────────────────────────────────

    void OnBuildButtonClicked(BuildingData data)
    {
        bool success = BuildingManager.Instance.TryBuild(data);
        if (success)
        {
            Debug.Log($"[ShopUI] 建造成功：{data.displayName}");
            RefreshAllButtons(); // 建造后立刻刷新按钮状态
        }
    }

    // ─────────────────────────────────────────────────────
    //  事件响应：资源或建筑数量变化时刷新按钮
    // ─────────────────────────────────────────────────────

    void OnResourceChanged(string id, float amount) => RefreshAllButtons();
    void OnBuildingChanged(string id, int count) => RefreshAllButtons();

    void RefreshAllButtons()
    {
        if (buttonContainer == null) return;

        for (int i = 0; i < buildings.Length; i++)
        {
            var data = buildings[i];
            if (data == null) continue;

            var btnObj = buttonContainer.Find($"Btn_{data.buildingId}");
            if (btnObj == null) continue;

            bool canBuild = BuildingManager.Instance.CanBuild(data);
            int count = BuildingManager.Instance.GetCount(data.buildingId);

            // 资源不足时按钮变灰
            var btn = btnObj.GetComponentInChildren<Button>();
            if (btn != null) btn.interactable = canBuild;

            // 显示当前已建数量
            SetText(btnObj.gameObject, "CountText", $"已建：{count}");
        }
    }

    // ─────────────────────────────────────────────────────
    //  工具方法：按名字找子物体并设置文字
    // ─────────────────────────────────────────────────────

    void SetText(GameObject parent, string childName, string text)
    {
        var child = parent.transform.Find(childName);
        if (child == null) return;
        var tmp = child.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.text = text;
    }
}

/*
── Inspector 配置步骤 ──────────────────────────────────────

1. 创建 BuildingData（ScriptableObject）：
   右键 Project 窗口 → Create → StarForge → Building Data
   命名为 "CollectorBasic"
   填写：
     buildingId:      collector_basic
     displayName:     基础采集器
     description:     每秒自动采集矿石
     costs:           Size=1, resourceId=raw_a, amount=10
     outputResourceId: raw_a
     outputPerSecond:  1

2. 在场景 Canvas 下创建 ShopPanel：
   Canvas → 右键 → Create Empty，命名 "ShopPanel"
   挂载 BuildingShopUI 脚本

3. 创建按钮预制体 BuildingButtonPrefab：
   Canvas 下创建一个 Button，结构如下：
   Button (有 Button 组件)
   ├── NameText    (TextMeshProUGUI)
   ├── DescText    (TextMeshProUGUI)
   ├── CostText    (TextMeshProUGUI)
   ├── OutputText  (TextMeshProUGUI)
   └── CountText   (TextMeshProUGUI)
   做好后拖到 Project 窗口变成 Prefab

4. 把 BuildingShopUI 的三个槽位填好：
   buildings[0]         → 拖入 CollectorBasic
   buildingButtonPrefab → 拖入刚做的 Prefab
   buttonContainer      → 拖入 ShopPanel 自身

5. 把 BuildingManager 脚本挂到场景里的 GameManager 对象上

──────────────────────────────────────────────────────────*/

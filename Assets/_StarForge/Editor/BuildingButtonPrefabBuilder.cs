// Assets/_StarForge/Editor/BuildingButtonPrefabBuilder.cs
// Editor 工具：一键生成建筑商店按钮预制体，层级与 BuildingShopUI.SetText 的 Find 名称一致。
//
// 使用方式：
//   菜单 StarForge → Create Building Button Prefab
//   输出路径：Assets/_StarForge/Prefabs/UI/BuildingButtonPrefab.prefab（目录不存在会自动创建）
//   生成后把 Prefab 拖到 BuildingShopUI.buildingButtonPrefab 即可。
//
// 前置条件：
//   需已导入 TextMeshPro Essentials（Window → TextMeshPro → Import TMP Essential Resources）

using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class BuildingButtonPrefabBuilder
{
    const string PrefabFolder = "Assets/_StarForge/Prefabs/UI";
    const string PrefabPath = PrefabFolder + "/BuildingButtonPrefab.prefab";

    [MenuItem("StarForge/Create Building Button Prefab")]
    public static void CreatePrefab()
    {
        EnsureFolder(PrefabFolder);

        var root = new GameObject(
            "BuildingButtonPrefab",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));

        var rt = (RectTransform)root.transform;
        rt.sizeDelta = new Vector2(260f, 140f);

        var bg = root.GetComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.15f);
        var btn = root.GetComponent<Button>();
        btn.targetGraphic = bg;

        var vlg = root.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.spacing = 2f;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        var fitter = root.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        AddText(root, "NameText", "建筑名称", 18f, FontStyles.Bold, TextAlignmentOptions.Left);
        AddText(root, "DescText", "建筑描述...", 12f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        AddText(root, "CostText", "矿石 ×10", 12f, FontStyles.Normal, TextAlignmentOptions.Left);
        AddText(root, "OutputText", "+1/s 矿石", 12f, FontStyles.Normal, TextAlignmentOptions.Left);
        AddText(root, "CountText", "已建：0", 12f, FontStyles.Normal, TextAlignmentOptions.Right);

        var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        EditorGUIUtility.PingObject(saved);
        Selection.activeObject = saved;
        Debug.Log($"[BuildingButtonPrefabBuilder] 已生成：{PrefabPath}");
    }

    static void AddText(
        GameObject parent,
        string name,
        string text,
        float size,
        FontStyles style,
        TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent.transform, false);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;

        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.enableWordWrapping = true;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        var parts = path.Split('/');
        if (parts.Length == 0)
            return;

        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}

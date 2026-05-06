// Assets/Editor/ProjectSetup.cs
// 用途：在 Unity 菜单栏一键创建 StarForge 标准项目结构
// 使用：保存后在 Unity 菜单栏点击 StarForge → Create Project Structure

using UnityEngine;
using UnityEditor;
using System.IO;

public class ProjectSetup
{
    [MenuItem("StarForge/Create Project Structure")]
    public static void CreateFolderStructure()
    {
        string[] folders =
        {
            "Assets/_StarForge/Scripts/Managers",
            "Assets/_StarForge/Scripts/Systems/Resource",
            "Assets/_StarForge/Scripts/Systems/Combat",
            "Assets/_StarForge/Scripts/Systems/Automation",
            "Assets/_StarForge/Scripts/Systems/TechTree",
            "Assets/_StarForge/Scripts/Systems/Quest",
            "Assets/_StarForge/Scripts/UI",
            "Assets/_StarForge/Scripts/Utils",
            "Assets/_StarForge/Prefabs/Buildings",
            "Assets/_StarForge/Prefabs/Enemies",
            "Assets/_StarForge/Prefabs/UI",
            "Assets/_StarForge/Prefabs/Effects",
            "Assets/_StarForge/Sprites/Characters",
            "Assets/_StarForge/Sprites/Buildings",
            "Assets/_StarForge/Sprites/Enemies",
            "Assets/_StarForge/Sprites/UI",
            "Assets/_StarForge/Sprites/Planets",
            "Assets/_StarForge/ScriptableObjects/Resources",
            "Assets/_StarForge/ScriptableObjects/TechTree",
            "Assets/_StarForge/ScriptableObjects/Quests",
            "Assets/_StarForge/Scenes",
            "Assets/_StarForge/Audio/Music",
            "Assets/_StarForge/Audio/SFX",
        };

        foreach (string folder in folders)
        {
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
                // 创建 .gitkeep 让 git 追踪空文件夹
                File.WriteAllText(folder + "/.gitkeep", "");
            }
        }

        AssetDatabase.Refresh();
        Debug.Log("[StarForge] 项目结构创建完成！共创建 " + folders.Length + " 个目录。");
    }
}

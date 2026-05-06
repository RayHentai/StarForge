// 临时测试用，直接挂在场景里的任意Button上
using UnityEngine;
using UnityEngine.UI;

public class QuickBuildTest : MonoBehaviour
{
    public BuildingData buildingData; // 拖入 CollectorBasic

    void Start()
    {
        GetComponent<Button>().onClick.AddListener(() =>
        {
            BuildingManager.Instance.TryBuild(buildingData);
        });
    }
}

// Assets/_StarForge/Scripts/UI/MachineUI.cs
// 职责：机器操作界面的显示与交互
// 挂载位置：挂在机器的 UI Panel GameObject 上
//
// ── Inspector 配置步骤 ────────────────────────────────────
// 1. 在场景中为每台机器创建一个 Canvas 下的 Panel（默认隐藏）
// 2. 将此脚本挂在 Panel 上
// 3. 将 ProcessingBuilding 脚本所在的机器 GameObject 拖入 building 槽
// 4. 配置好各 UI 组件引用（见 Header 分组）
// 5. 将该机器所有可用配方拖入 availableRecipes 列表

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MachineUI : MonoBehaviour
{
    [Header("关联的加工建筑")]
    public ProcessingBuilding building;

    [Header("配方系统")]
    public List<RecipeData> availableRecipes;   // 这台机器可用的所有配方（含锁定的）
    public TMP_Dropdown recipeDropdown;         // 配方选择下拉框
    public TextMeshProUGUI recipeDescText;      // 配方描述文字

    [Header("开关机")]
    public Toggle powerToggle;                  // 开关机 Toggle
    public TextMeshProUGUI powerStatusText;     // 显示"运行中"/"已关机"等状态

    [Header("输入槽（动态显示，根据配方槽位数变化）")]
    public Transform inputSlotContainer;        // 输入槽父容器
    public GameObject inputSlotPrefab;          // 单个输入槽预制体

    [Header("输出槽")]
    public TextMeshProUGUI outputResourceText;  // 输出资源名
    public TextMeshProUGUI outputAmountText;    // 输出数量
    public Button collectButton;                // 取走产物按钮

    [Header("进度条")]
    public Slider progressBar;
    public TextMeshProUGUI progressText;        // 显示 "1.2s / 2.0s"

    [Header("状态显示")]
    public TextMeshProUGUI stateText;           // 显示当前状态文字
    public Image stateIndicator;               // 状态指示灯（变色）

    // 状态颜色
    private static readonly Color ColorOff        = new Color(0.4f, 0.4f, 0.4f);
    private static readonly Color ColorIdle       = new Color(0.9f, 0.8f, 0.2f);
    private static readonly Color ColorProcessing = new Color(0.2f, 0.8f, 0.3f);
    private static readonly Color ColorOutputFull = new Color(0.9f, 0.3f, 0.2f);

    // 运行时缓存
    private List<GameObject> inputSlotObjects = new();

    // ─────────────────────────────────────────────────────
    void Start()
    {
        if (building == null) return;

        // 订阅建筑事件
        building.OnStateChanged      += OnStateChanged;
        building.OnRecipeChanged     += OnRecipeChanged;
        building.OnProgressChanged   += OnProgressChanged;
        building.OnProcessingComplete += OnProcessingComplete;

        // 初始化下拉框
        BuildRecipeDropdown();

        // 绑定 Toggle
        if (powerToggle != null)
            powerToggle.onValueChanged.AddListener(OnPowerToggleChanged);

        // 绑定取走按钮
        if (collectButton != null)
            collectButton.onClick.AddListener(OnCollectClicked);

        // 初始化显示
        RefreshAll();
    }

    void OnDestroy()
    {
        if (building == null) return;
        building.OnStateChanged       -= OnStateChanged;
        building.OnRecipeChanged      -= OnRecipeChanged;
        building.OnProgressChanged    -= OnProgressChanged;
        building.OnProcessingComplete -= OnProcessingComplete;
    }

    // ─────────────────────────────────────────────────────
    //  配方下拉框
    // ─────────────────────────────────────────────────────

    void BuildRecipeDropdown()
    {
        if (recipeDropdown == null) return;

        recipeDropdown.ClearOptions();

        var options = new List<TMP_Dropdown.OptionData>();

        // 第一项：未选择
        options.Add(new TMP_Dropdown.OptionData("── 选择配方 ──"));

        foreach (var recipe in availableRecipes)
        {
            // 锁定的配方加 🔒 前缀，置灰效果由颜色实现
            string label = recipe.IsUnlocked()
                ? recipe.displayName
                : $"🔒 {recipe.displayName}";
            options.Add(new TMP_Dropdown.OptionData(label));
        }

        recipeDropdown.AddOptions(options);
        recipeDropdown.onValueChanged.AddListener(OnRecipeDropdownChanged);
    }

    void OnRecipeDropdownChanged(int index)
    {
        // index=0 是"未选择"占位项
        if (index == 0)
        {
            building.SetRecipe(null);
            ClearInputSlots();
            if (recipeDescText != null) recipeDescText.text = "";
            return;
        }

        var recipe = availableRecipes[index - 1]; // -1 因为第0项是占位

        // 锁定配方：不允许选择，下拉框重置到0
        if (!recipe.IsUnlocked())
        {
            recipeDropdown.SetValueWithoutNotify(0);
            Debug.Log($"[MachineUI] 配方 {recipe.displayName} 未解锁");
            return;
        }

        bool success = building.SetRecipe(recipe);
        if (success)
        {
            BuildInputSlots(recipe);
            if (recipeDescText != null)
                recipeDescText.text = recipe.description;
        }
    }

    // ─────────────────────────────────────────────────────
    //  输入槽（根据配方动态生成）
    // ─────────────────────────────────────────────────────

    void BuildInputSlots(RecipeData recipe)
    {
        ClearInputSlots();
        if (inputSlotContainer == null || inputSlotPrefab == null) return;

        foreach (var input in recipe.inputs)
        {
            var slot = Instantiate(inputSlotPrefab, inputSlotContainer);
            inputSlotObjects.Add(slot);

            // 填入资源名和需求数量
            SetChildText(slot, "ResourceNameText",
                ResourceManager.Instance.GetDisplayName(input.resourceId));
            SetChildText(slot, "RequiredAmountText", $"×{input.amount}");

            // 实时显示当前库存（颜色区分够/不够）
            RefreshInputSlot(slot, input.resourceId, input.amount);
        }
    }

    void RefreshInputSlot(GameObject slot, string resourceId, float required)
    {
        float current = ResourceManager.Instance.Get(resourceId);
        bool enough = current >= required;

        SetChildText(slot, "CurrentAmountText", $"{Mathf.FloorToInt(current)}");

        // 够：绿色；不够：红色
        var tmp = slot.transform.Find("CurrentAmountText")?.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.color = enough ? new Color(0.2f, 0.8f, 0.3f) : new Color(0.9f, 0.3f, 0.2f);
    }

    void ClearInputSlots()
    {
        foreach (var obj in inputSlotObjects)
            Destroy(obj);
        inputSlotObjects.Clear();
    }

    // ─────────────────────────────────────────────────────
    //  开关机 Toggle
    // ─────────────────────────────────────────────────────

    void OnPowerToggleChanged(bool isOn)
    {
        building.SetPower(isOn);
    }

    // ─────────────────────────────────────────────────────
    //  取走产物
    // ─────────────────────────────────────────────────────

    void OnCollectClicked()
    {
        float amount = building.CollectOutput();
        if (amount > 0)
            Debug.Log($"[MachineUI] 取走 {building.OutputResourceId} ×{amount}");
        RefreshOutput();
    }

    // ─────────────────────────────────────────────────────
    //  事件回调
    // ─────────────────────────────────────────────────────

    void OnStateChanged(ProcessingBuilding.MachineState state)
    {
        RefreshStateDisplay(state);
        RefreshOutput();

        // 同步 Toggle 状态（不触发 onValueChanged）
        if (powerToggle != null)
            powerToggle.SetIsOnWithoutNotify(state != ProcessingBuilding.MachineState.Off);
    }

    void OnRecipeChanged(RecipeData recipe)
    {
        if (progressBar != null) progressBar.value = 0;
        if (progressText != null) progressText.text = "";
    }

    void OnProgressChanged(float progress)
    {
        if (progressBar != null) progressBar.value = progress;
        if (progressText != null && building.CurrentRecipe != null)
        {
            float elapsed = progress * building.CurrentRecipe.processingTime;
            progressText.text = $"{elapsed:F1}s / {building.CurrentRecipe.processingTime:F1}s";
        }

        // 同时刷新输入槽库存显示
        if (building.CurrentRecipe != null)
        {
            int i = 0;
            foreach (var input in building.CurrentRecipe.inputs)
            {
                if (i < inputSlotObjects.Count)
                    RefreshInputSlot(inputSlotObjects[i], input.resourceId, input.amount);
                i++;
            }
        }
    }

    void OnProcessingComplete(List<RecipeData.ItemStack> results)
    {
        // 可在此触发飘字特效，当前只打日志
        foreach (var r in results)
            Debug.Log($"[MachineUI] 产出：{ResourceManager.Instance.GetDisplayName(r.resourceId)} ×{r.amount}");

        RefreshOutput();
    }

    // ─────────────────────────────────────────────────────
    //  刷新显示
    // ─────────────────────────────────────────────────────

    void RefreshAll()
    {
        RefreshStateDisplay(building.State);
        RefreshOutput();
        if (progressBar != null) progressBar.value = 0;
    }

    void RefreshStateDisplay(ProcessingBuilding.MachineState state)
    {
        string label = state switch
        {
            ProcessingBuilding.MachineState.Off         => "已关机",
            ProcessingBuilding.MachineState.Idle        => "空闲中",
            ProcessingBuilding.MachineState.Processing  => "加工中",
            ProcessingBuilding.MachineState.OutputFull  => "输出已满",
            _ => ""
        };

        Color color = state switch
        {
            ProcessingBuilding.MachineState.Off         => ColorOff,
            ProcessingBuilding.MachineState.Idle        => ColorIdle,
            ProcessingBuilding.MachineState.Processing  => ColorProcessing,
            ProcessingBuilding.MachineState.OutputFull  => ColorOutputFull,
            _ => ColorOff
        };

        if (stateText != null) stateText.text = label;
        if (stateIndicator != null) stateIndicator.color = color;
        if (powerStatusText != null) powerStatusText.text = label;
    }

    void RefreshOutput()
    {
        if (outputResourceText != null)
            outputResourceText.text = string.IsNullOrEmpty(building.OutputResourceId)
                ? "──"
                : ResourceManager.Instance.GetDisplayName(building.OutputResourceId);

        if (outputAmountText != null)
            outputAmountText.text = $"{Mathf.FloorToInt(building.OutputAmount)}";

        if (collectButton != null)
            collectButton.interactable = building.OutputAmount > 0;
    }

    // ─────────────────────────────────────────────────────
    //  工具方法
    // ─────────────────────────────────────────────────────

    void SetChildText(GameObject parent, string childName, string text)
    {
        var child = parent.transform.Find(childName);
        if (child == null) return;
        var tmp = child.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.text = text;
    }
}

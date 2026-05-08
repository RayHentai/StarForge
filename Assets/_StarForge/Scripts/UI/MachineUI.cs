// Assets/_StarForge/Scripts/UI/MachineUI.cs
// 职责：机器操作界面的显示与交互
//
// ── 对应 ProcessingBuilding 的 API ───────────────────────
// SetPower(bool)              开关机
// SetRecipe(RecipeData)       设置配方
// PushToInputBuffer(id,amt)   向输入缓存投料（从背包扣）
// PullFromOutputBuffer(id,amt) 从指定输出槽取货（进背包）
// CollectAllOutput()          一键取走所有输出槽
// GetAllOutputSlots()         获取所有输出槽（Dictionary）
//
// ── 输出槽显示说明 ───────────────────────────────────────
// 输出区域动态遍历 GetAllOutputSlots()，
// 主产物和副产物各自显示在独立行，每行有单独取走按钮

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MachineUI : MonoBehaviour
{
    [Header("关联的加工建筑")]
    public ProcessingBuilding building;

    [Header("配方系统")]
    public List<RecipeData> availableRecipes;
    public TMP_Dropdown recipeDropdown;
    public TextMeshProUGUI recipeDescText;

    [Header("开关机")]
    public Toggle powerToggle;
    public TextMeshProUGUI powerStatusText;

    [Header("输入槽（根据配方动态生成）")]
    public Transform inputSlotContainer;
    public GameObject inputSlotPrefab;      // 子物体需有：ResourceNameText / RequiredAmountText / CurrentAmountText

    [Header("输出槽（动态生成，主产物+副产物各一行）")]
    public Transform outputSlotContainer;
    public GameObject outputSlotPrefab;     // 子物体需有：ResourceNameText / AmountText / TakeButton(Button)

    [Header("一键取走")]
    public Button collectAllButton;

    [Header("进度条")]
    public Slider progressBar;
    public TextMeshProUGUI progressText;

    [Header("状态显示")]
    public TextMeshProUGUI stateText;
    public Image stateIndicator;

    // 颜色常量
    private static readonly Color ColorOff        = new(0.4f, 0.4f, 0.4f);
    private static readonly Color ColorIdle       = new(0.9f, 0.8f, 0.2f);
    private static readonly Color ColorProcessing = new(0.2f, 0.8f, 0.3f);
    private static readonly Color ColorOutputFull = new(0.9f, 0.3f, 0.2f);

    // 运行时缓存
    private readonly List<GameObject> inputSlotObjects  = new();
    private readonly List<GameObject> outputSlotObjects = new();

    // ─────────────────────────────────────────────────────
    void Start()
    {
        if (building == null) return;

        building.OnStateChanged      += OnStateChanged;
        building.OnRecipeChanged     += OnRecipeChanged;
        building.OnProgressChanged   += OnProgressChanged;
        building.OnProcessingComplete += OnProcessingComplete;
        building.OnBufferChanged     += OnBufferChanged;

        BuildRecipeDropdown();

        if (powerToggle != null)
            powerToggle.onValueChanged.AddListener(OnPowerToggleChanged);

        if (collectAllButton != null)
            collectAllButton.onClick.AddListener(() => { building.CollectAllOutput(); RefreshOutputSlots(); });

        RefreshAll();
    }

    void OnDestroy()
    {
        if (building == null) return;
        building.OnStateChanged       -= OnStateChanged;
        building.OnRecipeChanged      -= OnRecipeChanged;
        building.OnProgressChanged    -= OnProgressChanged;
        building.OnProcessingComplete -= OnProcessingComplete;
        building.OnBufferChanged      -= OnBufferChanged;
    }

    // ─────────────────────────────────────────────────────
    //  配方下拉框
    // ─────────────────────────────────────────────────────
    void BuildRecipeDropdown()
    {
        if (recipeDropdown == null) return;
        recipeDropdown.ClearOptions();

        var options = new List<TMP_Dropdown.OptionData>
        {
            new("── 选择配方 ──")
        };

        foreach (var recipe in availableRecipes)
        {
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
        if (index == 0)
        {
            building.SetRecipe(null);
            ClearInputSlots();
            if (recipeDescText != null) recipeDescText.text = "";
            return;
        }

        var recipe = availableRecipes[index - 1];

        if (!recipe.IsUnlocked())
        {
            recipeDropdown.SetValueWithoutNotify(0);
            return;
        }

        if (building.SetRecipe(recipe))
        {
            BuildInputSlots(recipe);
            if (recipeDescText != null) recipeDescText.text = recipe.description;
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
            SetText(slot, "ResourceNameText",
                ResourceManager.Instance.GetDisplayName(input.resourceId));
            SetText(slot, "RequiredAmountText", $"需要 ×{input.amount}");
            RefreshInputSlot(slot, input.resourceId, input.amount);

            // 投料按钮（点击从背包取1份放入机器）
            var capturedId  = input.resourceId;
            var capturedAmt = input.amount;
            var btn = slot.transform.Find("PushButton")?.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() =>
                {
                    building.PushToInputBuffer(capturedId, capturedAmt);
                    RefreshInputSlots();
                });
        }
    }

    void RefreshInputSlots()
    {
        if (building.CurrentRecipe == null) return;
        int i = 0;
        foreach (var input in building.CurrentRecipe.inputs)
        {
            if (i < inputSlotObjects.Count)
                RefreshInputSlot(inputSlotObjects[i], input.resourceId, input.amount);
            i++;
        }
    }

    void RefreshInputSlot(GameObject slot, string resourceId, float required)
    {
        float inBuffer  = building.GetInputBufferAmount(resourceId);
        float inInventory = InventoryManager.Instance.Get(resourceId);
        bool  enough    = inInventory >= required;

        SetText(slot, "CurrentAmountText",
            $"缓存:{Mathf.FloorToInt(inBuffer)}  背包:{Mathf.FloorToInt(inInventory)}");

        var tmp = slot.transform.Find("CurrentAmountText")?.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.color = enough ? new Color(0.2f, 0.8f, 0.3f) : new Color(0.9f, 0.3f, 0.2f);
    }

    void ClearInputSlots()
    {
        foreach (var obj in inputSlotObjects) Destroy(obj);
        inputSlotObjects.Clear();
    }

    // ─────────────────────────────────────────────────────
    //  输出槽（遍历所有输出，包括副产物）
    // ─────────────────────────────────────────────────────
    void RefreshOutputSlots()
    {
        // 清空旧槽位
        foreach (var obj in outputSlotObjects) Destroy(obj);
        outputSlotObjects.Clear();

        if (outputSlotContainer == null || outputSlotPrefab == null) return;

        var allSlots = building.GetAllOutputSlots();

        if (allSlots.Count == 0)
        {
            // 没有产物时显示空状态
            var emptySlot = Instantiate(outputSlotPrefab, outputSlotContainer);
            outputSlotObjects.Add(emptySlot);
            SetText(emptySlot, "ResourceNameText", "── 暂无产物 ──");
            SetText(emptySlot, "AmountText", "");
            var btn = emptySlot.transform.Find("TakeButton")?.GetComponent<Button>();
            if (btn != null) btn.interactable = false;
            return;
        }

        // 为每种输出物生成一个槽位行
        foreach (var kvp in allSlots)
        {
            string resourceId = kvp.Key;
            float  amount     = kvp.Value;

            var slot = Instantiate(outputSlotPrefab, outputSlotContainer);
            outputSlotObjects.Add(slot);

            SetText(slot, "ResourceNameText",
                ResourceManager.Instance.GetDisplayName(resourceId));
            SetText(slot, "AmountText", $"{Mathf.FloorToInt(amount)}");

            // 单槽取走按钮
            var capturedId = resourceId;
            var btn = slot.transform.Find("TakeButton")?.GetComponent<Button>();
            if (btn != null)
            {
                btn.interactable = amount > 0;
                btn.onClick.AddListener(() =>
                {
                    building.PullFromOutputBuffer(capturedId,
                        building.GetOutputBufferAmount(capturedId));
                    RefreshOutputSlots();
                });
            }
        }

        // 一键取走按钮：有产物时才可用
        if (collectAllButton != null)
            collectAllButton.interactable = allSlots.Count > 0;
    }

    // ─────────────────────────────────────────────────────
    //  开关机
    // ─────────────────────────────────────────────────────
    void OnPowerToggleChanged(bool isOn) => building.SetPower(isOn);

    // ─────────────────────────────────────────────────────
    //  事件回调
    // ─────────────────────────────────────────────────────
    void OnStateChanged(ProcessingBuilding.MachineState state)
    {
        RefreshStateDisplay(state);
        if (powerToggle != null)
            powerToggle.SetIsOnWithoutNotify(state != ProcessingBuilding.MachineState.Off);
    }

    void OnRecipeChanged(RecipeData recipe)
    {
        if (progressBar != null) progressBar.value = 0;
        if (progressText != null) progressText.text = "";
        RefreshOutputSlots(); // 切换配方后清空输出槽显示
    }

    void OnProgressChanged(float progress)
    {
        if (progressBar != null) progressBar.value = progress;
        if (progressText != null && building.CurrentRecipe != null)
        {
            float elapsed = progress * building.CurrentRecipe.processingTime;
            progressText.text = $"{elapsed:F1}s / {building.CurrentRecipe.processingTime:F1}s";
        }
        RefreshInputSlots();
    }

    void OnProcessingComplete(List<RecipeData.ItemStack> results)
    {
        RefreshOutputSlots(); // 加工完成，刷新输出槽
        RefreshInputSlots();
    }

    void OnBufferChanged() => RefreshOutputSlots();

    // ─────────────────────────────────────────────────────
    //  整体刷新
    // ─────────────────────────────────────────────────────
    void RefreshAll()
    {
        RefreshStateDisplay(building.State);
        RefreshOutputSlots();
        if (progressBar != null) progressBar.value = 0;
    }

    void RefreshStateDisplay(ProcessingBuilding.MachineState state)
    {
        string label = state switch
        {
            ProcessingBuilding.MachineState.Off        => "已关机",
            ProcessingBuilding.MachineState.Idle       => "空闲中",
            ProcessingBuilding.MachineState.Processing => "加工中",
            ProcessingBuilding.MachineState.OutputFull => "输出已满",
            _ => ""
        };
        Color color = state switch
        {
            ProcessingBuilding.MachineState.Off        => ColorOff,
            ProcessingBuilding.MachineState.Idle       => ColorIdle,
            ProcessingBuilding.MachineState.Processing => ColorProcessing,
            ProcessingBuilding.MachineState.OutputFull => ColorOutputFull,
            _ => ColorOff
        };
        if (stateText != null)      stateText.text  = label;
        if (stateIndicator != null) stateIndicator.color = color;
        if (powerStatusText != null) powerStatusText.text = label;
    }

    // ─────────────────────────────────────────────────────
    //  工具方法
    // ─────────────────────────────────────────────────────
    void SetText(GameObject parent, string childName, string text)
    {
        var child = parent.transform.Find(childName);
        if (child == null) return;
        var tmp = child.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.text = text;
    }
}

// Assets/_StarForge/Scripts/Systems/Automation/ProcessingBuilding.cs
// 职责：所有加工建筑的通用运行逻辑
//
// ── 输出缓存槽位设计 ─────────────────────────────────────
// OutputBuffer 是 Dictionary<string, float>
// 每种产物（主产物+每种副产物）各占一个独立 key：
//
//   OutputBuffer["ingot_iron"]           = 4    ← 主产物槽
//   OutputBuffer["res_stone"]            = 2    ← 副产物槽①
//   OutputBuffer["powder_washed_iron"]   = 1    ← 副产物槽②
//
// 玩家打开机器UI可以看到所有槽位，分别取走或一键全取。
// outputBufferCapacity 是所有槽位的总容量上限。
//
// ── 设计模式：状态机（State Machine）────────────────────
// Off → Idle → Processing → OutputFull
//
// ── 设计模式：策略模式（Strategy Pattern）───────────────
// RecipeData 是策略，切换配方 = 切换策略，机器代码零改动

using System.Collections.Generic;
using UnityEngine;

public class ProcessingBuilding : MonoBehaviour
{
    public enum MachineState { Off, Idle, Processing, OutputFull }

    [Header("建筑配置")]
    public RecipeData.MachineType machineType;

    [Header("缓存配置")]
    public float inputBufferCapacity  = 50f;    // 每种输入材料的缓存上限
    public float outputBufferCapacity = 50f;    // 所有输出槽的总容量上限

    [Header("电力配置")]
    public bool  requiresPower    = true;
    public float powerConsumption = 1f;

    // ── 运行时状态 ────────────────────────────────────────
    public MachineState State         { get; private set; } = MachineState.Off;
    public RecipeData   CurrentRecipe { get; private set; }
    public float Progress => processingTimer / (CurrentRecipe?.processingTime ?? 1f);

    // 输入缓存：resourceId → 当前存量
    public Dictionary<string, float> InputBuffer  { get; private set; } = new();
    // 输出缓存：resourceId → 当前存量（主产物 + 副产物各占独立槽位）
    public Dictionary<string, float> OutputBuffer { get; private set; } = new();

    // ── 事件 ──────────────────────────────────────────────
    public event System.Action<MachineState>               OnStateChanged;
    public event System.Action<RecipeData>                 OnRecipeChanged;
    public event System.Action<float>                      OnProgressChanged;
    public event System.Action<List<RecipeData.ItemStack>> OnProcessingComplete;
    public event System.Action                             OnBufferChanged;

    private float processingTimer = 0f;
    private System.Random rng = new System.Random();

    // ─────────────────────────────────────────────────────
    //  Update：状态机主循环
    // ─────────────────────────────────────────────────────
    void Update()
    {
        switch (State)
        {
            case MachineState.Off:        break;
            case MachineState.Idle:       TryStartProcessing(); break;
            case MachineState.Processing: TickProcessing();     break;
            case MachineState.OutputFull: CheckOutputCleared(); break;
        }
    }

    void TryStartProcessing()
    {
        if (CurrentRecipe == null) return;
        if (requiresPower && !PowerManager.Instance.IsPowered()) return;

        // 检查输入缓存材料是否足够
        foreach (var input in CurrentRecipe.inputs)
        {
            InputBuffer.TryGetValue(input.resourceId, out float have);
            if (have < input.amount) return;
        }

        // 检查输出缓存总量是否有空间放下主产物
        // 副产物是概率触发，不提前预留空间（和 Factorio 逻辑一致）
        if (GetOutputTotal() + CurrentRecipe.output.amount > outputBufferCapacity)
        {
            TransitionTo(MachineState.OutputFull);
            return;
        }

        TransitionTo(MachineState.Processing);
    }

    void TickProcessing()
    {
        processingTimer += Time.deltaTime;
        OnProgressChanged?.Invoke(Progress);
        if (processingTimer >= CurrentRecipe.processingTime)
            FinishProcessing();
    }

    void FinishProcessing()
    {
        var results = new List<RecipeData.ItemStack>();

        // 1. 从输入缓存扣除材料
        foreach (var input in CurrentRecipe.inputs)
        {
            InputBuffer[input.resourceId] =
                Mathf.Max(0, InputBuffer.GetValueOrDefault(input.resourceId) - input.amount);
        }

        // 2. 主产物进输出缓存（独立槽位）
        AddToOutputBuffer(CurrentRecipe.output.resourceId, CurrentRecipe.output.amount);
        results.Add(new RecipeData.ItemStack
        {
            resourceId = CurrentRecipe.output.resourceId,
            amount     = CurrentRecipe.output.amount
        });

        // 3. 副产物进输出缓存（各自独立槽位，概率触发）
        //    使用 System.Random 而非 Unity Random，性能更好且线程安全
        foreach (var bp in CurrentRecipe.byproducts)
        {
            if (rng.NextDouble() >= bp.probability) continue;

            // 检查输出缓存总量是否还有空间
            if (GetOutputTotal() + bp.amount <= outputBufferCapacity)
            {
                AddToOutputBuffer(bp.resourceId, bp.amount);
                results.Add(new RecipeData.ItemStack
                {
                    resourceId = bp.resourceId,
                    amount     = bp.amount
                });
            }
            else
            {
                // 输出缓存已满，副产物丢弃（和 Factorio 满了溢出的逻辑一致）
                Debug.Log($"[ProcessingBuilding] 输出缓存已满，副产物 {bp.resourceId} 丢弃");
            }
        }

        OnProcessingComplete?.Invoke(results);
        OnBufferChanged?.Invoke();

        // 检查是否进入满状态
        if (GetOutputTotal() >= outputBufferCapacity)
            TransitionTo(MachineState.OutputFull);
        else
            TransitionTo(MachineState.Idle);
    }

    void CheckOutputCleared()
    {
        if (GetOutputTotal() < outputBufferCapacity)
            TransitionTo(MachineState.Idle);
    }

    // ─────────────────────────────────────────────────────
    //  输出缓存内部操作
    // ─────────────────────────────────────────────────────

    void AddToOutputBuffer(string resourceId, float amount)
    {
        OutputBuffer.TryGetValue(resourceId, out float cur);
        OutputBuffer[resourceId] = cur + amount;
    }

    /// <summary>所有输出槽的总存量</summary>
    float GetOutputTotal()
    {
        float total = 0;
        foreach (var v in OutputBuffer.Values) total += v;
        return total;
    }

    // ─────────────────────────────────────────────────────
    //  状态转换
    // ─────────────────────────────────────────────────────
    void TransitionTo(MachineState next)
    {
        State = next;
        OnStateChanged?.Invoke(next);
        if (next is MachineState.Processing or MachineState.Idle)
            processingTimer = 0f;
    }

    // ─────────────────────────────────────────────────────
    //  公开 API
    // ─────────────────────────────────────────────────────

    /// <summary>开关机 Toggle</summary>
    public void SetPower(bool on)
    {
        if (on  && State == MachineState.Off) TransitionTo(MachineState.Idle);
        if (!on && State != MachineState.Off) TransitionTo(MachineState.Off);
    }

    /// <summary>设置配方，切换时清空输入缓存防残留</summary>
    public bool SetRecipe(RecipeData recipe)
    {
        if (recipe == null) return false;
        if (!recipe.IsCompatibleWith(machineType)) return false;
        if (!recipe.IsUnlocked()) return false;

        CurrentRecipe = recipe;
        processingTimer = 0f;
        InputBuffer.Clear();
        OnRecipeChanged?.Invoke(recipe);

        if (State == MachineState.Processing)
            TransitionTo(MachineState.Idle);
        return true;
    }

    /// <summary>
    /// 玩家/物流向输入缓存投料。
    /// 从玩家背包扣除，放入输入缓存。
    /// 返回实际投入量。
    /// </summary>
    public float PushToInputBuffer(string resourceId, float amount)
    {
        InputBuffer.TryGetValue(resourceId, out float cur);
        float canAccept = Mathf.Max(0, inputBufferCapacity - cur);
        float actual    = Mathf.Min(amount, canAccept);
        if (actual <= 0) return 0;

        if (!InventoryManager.Instance.Spend(resourceId, actual)) return 0;

        InputBuffer[resourceId] = cur + actual;
        OnBufferChanged?.Invoke();
        return actual;
    }

    /// <summary>
    /// 玩家从指定输出槽取货，进背包。
    /// 返回实际取出量。
    /// </summary>
    public float PullFromOutputBuffer(string resourceId, float amount)
    {
        OutputBuffer.TryGetValue(resourceId, out float cur);
        float actual = Mathf.Min(amount, cur);
        if (actual <= 0) return 0;

        OutputBuffer[resourceId] = cur - actual;
        // 槽位为空时清理 key，保持字典整洁
        if (OutputBuffer[resourceId] <= 0)
            OutputBuffer.Remove(resourceId);

        InventoryManager.Instance.Add(resourceId, actual);
        OnBufferChanged?.Invoke();

        if (State == MachineState.OutputFull)
            TransitionTo(MachineState.Idle);
        return actual;
    }

    /// <summary>一键取走所有输出槽（主产物+所有副产物）</summary>
    public void CollectAllOutput()
    {
        // 复制 key 列表，避免遍历时修改字典
        foreach (var key in new List<string>(OutputBuffer.Keys))
            PullFromOutputBuffer(key, OutputBuffer.GetValueOrDefault(key));
    }

    // ── 查询 API ──────────────────────────────────────────
    public float GetInputBufferAmount(string id)
    { InputBuffer.TryGetValue(id, out float v); return v; }

    public float GetOutputBufferAmount(string id)
    { OutputBuffer.TryGetValue(id, out float v); return v; }

    /// <summary>获取所有输出槽（供 MachineUI 遍历显示）</summary>
    public Dictionary<string, float> GetAllOutputSlots() => OutputBuffer;
}

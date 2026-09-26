using System;
using UnityEngine;

/// <summary>
/// 可被金币调整的玩家属性类型。
/// </summary>
public enum PlayerStatType
{
    /// <summary>
    /// 生命值属性。
    /// </summary>
    Health,

    /// <summary>
    /// 魔法值属性。
    /// </summary>
    Mana,

    /// <summary>
    /// 体力值属性。
    /// </summary>
    Stamina
}

/// <summary>
/// 玩家属性的内存快照，用于原子交易失败时回滚。
/// </summary>
public sealed class PlayerStatsSnapshot
{
    /// <summary>
    /// 最大生命值。
    /// </summary>
    public int maxHealth;

    /// <summary>
    /// 当前生命值。
    /// </summary>
    public float currentHealth;

    /// <summary>
    /// 最大魔法值。
    /// </summary>
    public int maxMana;

    /// <summary>
    /// 当前魔法值。
    /// </summary>
    public float currentMana;

    /// <summary>
    /// 最大体力值。
    /// </summary>
    public int maxStamina;

    /// <summary>
    /// 当前体力值。
    /// </summary>
    public float currentStamina;
}

/// <summary>
/// 保存并运行时管理玩家的生命值、魔法值和体力值。
/// </summary>
[DefaultExecutionOrder(-90)]
public class PlayerStatsRuntime : MonoBehaviour
{
    /// <summary>
    /// 玩家属性发生变化时通知 HUD（游戏主界面）和 PackagePanelUI（背包面板界面）。
    /// </summary>
    public event Action OnStatsChanged;

    /// <summary>
    /// 玩家成功消耗体力时通知体力控制器开始计算恢复延迟。
    /// </summary>
    public event Action<float> OnStaminaConsumed;

    /// <summary>
    /// 属性允许达到的最小值。
    /// </summary>
    public const int MinimumMaximumValue = 10;

    /// <summary>
    /// 属性允许达到的最大值。
    /// </summary>
    public const int MaximumMaximumValue = 999;

    /// <summary>
    /// 最大生命值。
    /// </summary>
    [SerializeField] private int maxHealth = 100;

    /// <summary>
    /// 当前生命值。
    /// </summary>
    [SerializeField] private float currentHealth = 100f;

    /// <summary>
    /// 最大魔法值。
    /// </summary>
    [SerializeField] private int maxMana = 100;

    /// <summary>
    /// 当前魔法值。
    /// </summary>
    [SerializeField] private float currentMana = 100f;

    /// <summary>
    /// 最大体力值。
    /// </summary>
    [SerializeField] private int maxStamina = 100;

    /// <summary>
    /// 当前体力值。
    /// </summary>
    [SerializeField] private float currentStamina = 100f;

    /// <summary>
    /// 当前最大生命值。
    /// </summary>
    public int MaxHealth => maxHealth;

    /// <summary>
    /// 当前生命值。
    /// </summary>
    public float CurrentHealth => currentHealth;

    /// <summary>
    /// 当前最大魔法值。
    /// </summary>
    public int MaxMana => maxMana;

    /// <summary>
    /// 当前魔法值。
    /// </summary>
    public float CurrentMana => currentMana;

    /// <summary>
    /// 当前最大体力值。
    /// </summary>
    public int MaxStamina => maxStamina;

    /// <summary>
    /// 当前体力值。
    /// </summary>
    public float CurrentStamina => currentStamina;

    /// <summary>
    /// 当前对象是否已经从存档初始化。
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// 从统一存档加载玩家属性。
    /// </summary>
    private void Awake()
    {
        InitializeFromSave();
    }

    /// <summary>
    /// 供其他运行时系统确保属性已经完成初始化。
    /// </summary>
    public void EnsureInitializedForRuntime()
    {
        InitializeFromSave();
    }

    /// <summary>
    /// 获取指定属性的最大值。
    /// </summary>
    /// <param name="statType">要查询的属性类型。</param>
    /// <returns>属性最大值。</returns>
    public int GetMaximum(PlayerStatType statType)
    {
        switch (statType)
        {
            case PlayerStatType.Health:
                return maxHealth;
            case PlayerStatType.Mana:
                return maxMana;
            default:
                return maxStamina;
        }
    }

    /// <summary>
    /// 获取指定属性的当前值。
    /// </summary>
    /// <param name="statType">要查询的属性类型。</param>
    /// <returns>属性当前值。</returns>
    public float GetCurrent(PlayerStatType statType)
    {
        switch (statType)
        {
            case PlayerStatType.Health:
                return currentHealth;
            case PlayerStatType.Mana:
                return currentMana;
            default:
                return currentStamina;
        }
    }

    /// <summary>
    /// 尝试消耗体力；体力不足时不会修改数据。
    /// </summary>
    /// <param name="amount">要消耗的体力数量。</param>
    /// <returns>消耗成功返回 true。</returns>
    public bool TryConsumeStamina(float amount)
    {
        EnsureInitializedForRuntime();

        if (amount <= 0f || currentStamina + 0.0001f < amount)
            return false;

        currentStamina = Mathf.Clamp(currentStamina - amount, 0f, maxStamina);
        OnStatsChanged?.Invoke();
        OnStaminaConsumed?.Invoke(amount);
        return true;
    }

    /// <summary>
    /// 恢复指定数量的体力。
    /// </summary>
    /// <param name="amount">要恢复的体力数量。</param>
    public void RecoverStamina(float amount)
    {
        EnsureInitializedForRuntime();

        if (amount <= 0f || currentStamina >= maxStamina)
            return;

        float previousValue = currentStamina;
        currentStamina = Mathf.Clamp(currentStamina + amount, 0f, maxStamina);

        if (!Mathf.Approximately(previousValue, currentStamina))
            OnStatsChanged?.Invoke();
    }

    /// <summary>
    /// 通过统一接口设置当前生命值。
    /// </summary>
    /// <param name="value">新的当前生命值。</param>
    public void SetCurrentHealth(float value)
    {
        SetCurrentValue(PlayerStatType.Health, value, true);
    }

    /// <summary>
    /// 通过统一接口设置当前魔法值。
    /// </summary>
    /// <param name="value">新的当前魔法值。</param>
    public void SetCurrentMana(float value)
    {
        SetCurrentValue(PlayerStatType.Mana, value, true);
    }

    /// <summary>
    /// 通过统一接口设置当前体力值。
    /// </summary>
    /// <param name="value">新的当前体力值。</param>
    public void SetCurrentStamina(float value)
    {
        SetCurrentValue(PlayerStatType.Stamina, value, true);
    }

    /// <summary>
    /// 捕获当前属性快照。
    /// </summary>
    /// <returns>当前属性快照。</returns>
    public PlayerStatsSnapshot CaptureSnapshot()
    {
        EnsureInitializedForRuntime();

        return new PlayerStatsSnapshot
        {
            maxHealth = maxHealth,
            currentHealth = currentHealth,
            maxMana = maxMana,
            currentMana = currentMana,
            maxStamina = maxStamina,
            currentStamina = currentStamina
        };
    }

    /// <summary>
    /// 恢复属性快照。
    /// </summary>
    /// <param name="snapshot">要恢复的属性快照。</param>
    /// <param name="notify">是否通知界面刷新。</param>
    public void RestoreSnapshot(PlayerStatsSnapshot snapshot, bool notify)
    {
        if (snapshot == null)
            return;

        maxHealth = Mathf.Clamp(snapshot.maxHealth, MinimumMaximumValue, MaximumMaximumValue);
        maxMana = Mathf.Clamp(snapshot.maxMana, MinimumMaximumValue, MaximumMaximumValue);
        maxStamina = Mathf.Clamp(snapshot.maxStamina, MinimumMaximumValue, MaximumMaximumValue);
        currentHealth = Mathf.Clamp(snapshot.currentHealth, 0f, maxHealth);
        currentMana = Mathf.Clamp(snapshot.currentMana, 0f, maxMana);
        currentStamina = Mathf.Clamp(snapshot.currentStamina, 0f, maxStamina);

        if (notify)
            OnStatsChanged?.Invoke();
    }

    /// <summary>
    /// 通知所有监听者玩家属性已经发生变化。
    /// </summary>
    public void SendStatsChanged()
    {
        OnStatsChanged?.Invoke();
    }

    /// <summary>
    /// 写入候选存档，供 PlayerProgressionService（玩家成长服务）执行原子保存。
    /// </summary>
    /// <param name="data">候选存档。</param>
    internal void WriteToSaveData(GameSaveData data)
    {
        if (data == null)
            return;

        EnsureInitializedForRuntime();

        if (data.playerStats == null)
            data.playerStats = new PlayerStatsSaveData();

        data.playerStats.maxHealth = maxHealth;
        data.playerStats.currentHealth = currentHealth;
        data.playerStats.maxMana = maxMana;
        data.playerStats.currentMana = currentMana;
        data.playerStats.maxStamina = maxStamina;
        data.playerStats.currentStamina = currentStamina;
    }

    /// <summary>
    /// 在不提前触发事件的情况下增加一个最大属性点。
    /// </summary>
    /// <param name="statType">要增加的属性类型。</param>
    /// <param name="notify">是否立即通知界面刷新。</param>
    /// <returns>修改成功返回 true。</returns>
    internal bool TryIncreaseMaximum(PlayerStatType statType, bool notify)
    {
        EnsureInitializedForRuntime();

        int oldMaximum = GetMaximum(statType);

        if (oldMaximum >= MaximumMaximumValue)
            return false;

        bool wasFull = GetCurrent(statType) >= oldMaximum - 0.0001f;
        SetMaximum(statType, oldMaximum + 1);

        if (wasFull)
            SetCurrentValue(statType, GetMaximum(statType), false);
        else
            ClampCurrentValue(statType, false);

        if (notify)
            OnStatsChanged?.Invoke();

        return true;
    }

    /// <summary>
    /// 在不提前触发事件的情况下减少一个最大属性点。
    /// </summary>
    /// <param name="statType">要减少的属性类型。</param>
    /// <param name="notify">是否立即通知界面刷新。</param>
    /// <returns>修改成功返回 true。</returns>
    internal bool TryDecreaseMaximum(PlayerStatType statType, bool notify)
    {
        EnsureInitializedForRuntime();

        int oldMaximum = GetMaximum(statType);

        if (oldMaximum <= MinimumMaximumValue)
            return false;

        SetMaximum(statType, oldMaximum - 1);
        ClampCurrentValue(statType, false);

        if (notify)
            OnStatsChanged?.Invoke();

        return true;
    }

    /// <summary>
    /// 初始化并读取统一存档中的属性。
    /// </summary>
    private void InitializeFromSave()
    {
        if (IsInitialized)
            return;

        GameSaveData data = GameSaveService.GetOrCreateData();

        if (data.playerStats == null)
            data.playerStats = new PlayerStatsSaveData();

        maxHealth = Mathf.Clamp(data.playerStats.maxHealth, MinimumMaximumValue, MaximumMaximumValue);
        maxMana = Mathf.Clamp(data.playerStats.maxMana, MinimumMaximumValue, MaximumMaximumValue);
        maxStamina = Mathf.Clamp(data.playerStats.maxStamina, MinimumMaximumValue, MaximumMaximumValue);
        currentHealth = Mathf.Clamp(data.playerStats.currentHealth, 0f, maxHealth);
        currentMana = Mathf.Clamp(data.playerStats.currentMana, 0f, maxMana);
        currentStamina = Mathf.Clamp(data.playerStats.currentStamina, 0f, maxStamina);
        IsInitialized = true;
    }

    /// <summary>
    /// 设置指定属性的最大值。
    /// </summary>
    /// <param name="statType">属性类型。</param>
    /// <param name="value">新的最大值。</param>
    private void SetMaximum(PlayerStatType statType, int value)
    {
        value = Mathf.Clamp(value, MinimumMaximumValue, MaximumMaximumValue);

        switch (statType)
        {
            case PlayerStatType.Health:
                maxHealth = value;
                break;
            case PlayerStatType.Mana:
                maxMana = value;
                break;
            default:
                maxStamina = value;
                break;
        }
    }

    /// <summary>
    /// 设置指定属性的当前值。
    /// </summary>
    /// <param name="statType">属性类型。</param>
    /// <param name="value">新的当前值。</param>
    /// <param name="notify">是否通知界面刷新。</param>
    private void SetCurrentValue(PlayerStatType statType, float value, bool notify)
    {
        EnsureInitializedForRuntime();
        value = Mathf.Max(0f, value);

        switch (statType)
        {
            case PlayerStatType.Health:
                currentHealth = Mathf.Clamp(value, 0f, maxHealth);
                break;
            case PlayerStatType.Mana:
                currentMana = Mathf.Clamp(value, 0f, maxMana);
                break;
            default:
                currentStamina = Mathf.Clamp(value, 0f, maxStamina);
                break;
        }

        if (notify)
            OnStatsChanged?.Invoke();
    }

    /// <summary>
    /// 把当前值限制在对应最大值以内。
    /// </summary>
    /// <param name="statType">属性类型。</param>
    /// <param name="notify">是否通知界面刷新。</param>
    private void ClampCurrentValue(PlayerStatType statType, bool notify)
    {
        SetCurrentValue(statType, GetCurrent(statType), notify);
    }

    /// <summary>
    /// 在 Inspector（检视面板）修改数值时保证属性上限合法。
    /// </summary>
    private void OnValidate()
    {
        maxHealth = Mathf.Clamp(maxHealth, MinimumMaximumValue, MaximumMaximumValue);
        maxMana = Mathf.Clamp(maxMana, MinimumMaximumValue, MaximumMaximumValue);
        maxStamina = Mathf.Clamp(maxStamina, MinimumMaximumValue, MaximumMaximumValue);
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        currentMana = Mathf.Clamp(currentMana, 0f, maxMana);
        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
    }
}

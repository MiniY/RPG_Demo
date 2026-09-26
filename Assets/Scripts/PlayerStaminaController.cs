using UnityEngine;

/// <summary>
/// 管理玩家冲刺时的体力消耗，以及停止消耗后的延迟恢复。
/// </summary>
[DefaultExecutionOrder(-40)]
public class PlayerStaminaController : MonoBehaviour
{
    /// <summary>
    /// 冲刺状态下每秒消耗的体力数量。
    /// </summary>
    [SerializeField, Min(0f)] private float sprintDrainPerSecond = 2f;

    /// <summary>
    /// 停止冲刺和攻击后每秒恢复的体力数量。
    /// </summary>
    [SerializeField, Min(0f)] private float staminaRecoveryPerSecond = 5f;

    /// <summary>
    /// 最近一次消耗体力后，开始恢复前需要等待的秒数。
    /// </summary>
    [SerializeField, Min(0f)] private float staminaRecoveryDelay = 1f;

    /// <summary>
    /// 是否要求玩家正在移动时才消耗冲刺体力。
    /// </summary>
    [SerializeField] private bool requirePlayerMoving = true;

    /// <summary>
    /// 玩家属性运行时数据。
    /// </summary>
    private PlayerStatsRuntime playerStatsRuntime;

    /// <summary>
    /// 玩家行为脚本。
    /// </summary>
    private PlayerAction playerAction;

    /// <summary>
    /// 游戏输入管理器。
    /// </summary>
    private GameInput gameInput;

    /// <summary>
    /// 最近一次体力消耗发生的时间。
    /// </summary>
    private float lastStaminaUseTime = float.NegativeInfinity;

    /// <summary>
    /// 当前是否已经监听属性数据的体力消耗事件。
    /// </summary>
    private bool isSubscribedToStats;

    /// <summary>
    /// 当前是否真的正在执行冲刺体力消耗。
    /// </summary>
    public bool IsSprinting { get; private set; }

    /// <summary>
    /// 初始化玩家组件引用。
    /// </summary>
    private void Awake()
    {
        playerAction = GetComponent<PlayerAction>();
        ResolveDependencies();
    }

    /// <summary>
    /// 启用时监听统一属性数据的体力消耗事件。
    /// </summary>
    private void OnEnable()
    {
        ResolveDependencies();
        SubscribeToStatsIfNeeded();
    }

    /// <summary>
    /// 禁用时解除体力消耗事件监听。
    /// </summary>
    private void OnDisable()
    {
        UnsubscribeFromStats();
    }

    /// <summary>
    /// 每帧执行冲刺体力消耗和恢复。
    /// </summary>
    private void Update()
    {
        ResolveDependencies();

        if (playerStatsRuntime == null)
            return;

        SubscribeToStatsIfNeeded();

        bool shouldSprint = CanConsumeSprintStamina();

        if (shouldSprint)
        {
            ConsumeSprintStamina();
            return;
        }

        IsSprinting = false;
        RecoverStaminaIfReady();
    }

    /// <summary>
    /// 查找属性数据、玩家行为和输入管理器。
    /// </summary>
    private void ResolveDependencies()
    {
        if (playerAction == null)
            playerAction = GetComponent<PlayerAction>();

        if (gameInput == null)
            gameInput = GameInput.Instance;

        if (playerStatsRuntime == null && GameSession.Instance != null)
            playerStatsRuntime = GameSession.Instance.PlayerStats;
    }

    /// <summary>
    /// 在属性数据准备好后监听体力消耗事件。
    /// </summary>
    private void SubscribeToStatsIfNeeded()
    {
        if (playerStatsRuntime == null || isSubscribedToStats)
            return;

        playerStatsRuntime.OnStaminaConsumed += HandleStaminaConsumed;
        isSubscribedToStats = true;
    }

    /// <summary>
    /// 解除属性数据的体力消耗事件监听。
    /// </summary>
    private void UnsubscribeFromStats()
    {
        if (playerStatsRuntime != null && isSubscribedToStats)
            playerStatsRuntime.OnStaminaConsumed -= HandleStaminaConsumed;

        isSubscribedToStats = false;
    }

    /// <summary>
    /// 判断当前是否满足冲刺体力消耗条件。
    /// </summary>
    /// <returns>满足条件返回 true。</returns>
    private bool CanConsumeSprintStamina()
    {
        if (gameInput == null || !gameInput.IsGameplayInputEnabled || !gameInput.IsControlActive)
            return false;

        if (requirePlayerMoving && (playerAction == null || !playerAction.IsMoving))
            return false;

        if (playerStatsRuntime.CurrentStamina <= 0.0001f)
        {
            gameInput.CancelControlState();
            return false;
        }

        return sprintDrainPerSecond > 0f;
    }

    /// <summary>
    /// 按每秒速率换算本帧需要消耗的体力。
    /// </summary>
    private void ConsumeSprintStamina()
    {
        float amount = sprintDrainPerSecond * Time.deltaTime;
        amount = Mathf.Min(amount, playerStatsRuntime.CurrentStamina);

        if (amount <= 0f || !playerStatsRuntime.TryConsumeStamina(amount))
        {
            IsSprinting = false;
            gameInput.CancelControlState();
            return;
        }

        IsSprinting = true;
    }

    /// <summary>
    /// 在满足延迟后恢复体力。
    /// </summary>
    private void RecoverStaminaIfReady()
    {
        if (Time.time - lastStaminaUseTime < staminaRecoveryDelay)
            return;

        playerStatsRuntime.RecoverStamina(staminaRecoveryPerSecond * Time.deltaTime);
    }

    /// <summary>
    /// 记录一次体力消耗，重新计算恢复延迟。
    /// </summary>
    /// <param name="amount">本次消耗的体力数量。</param>
    private void HandleStaminaConsumed(float amount)
    {
        if (amount <= 0f)
            return;

        lastStaminaUseTime = Time.time;
    }

    /// <summary>
    /// 在 Inspector（检视面板）中保证体力参数合法。
    /// </summary>
    private void OnValidate()
    {
        sprintDrainPerSecond = Mathf.Max(0f, sprintDrainPerSecond);
        staminaRecoveryPerSecond = Mathf.Max(0f, staminaRecoveryPerSecond);
        staminaRecoveryDelay = Mathf.Max(0f, staminaRecoveryDelay);
    }
}

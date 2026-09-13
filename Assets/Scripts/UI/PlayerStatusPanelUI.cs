using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 管理玩家状态面板的界面显示。
/// 当前先测试体力条，之后可以继续加入生命值条和魔法值条。
/// </summary>
public class PlayerStatusPanelUI : MonoBehaviour
{
    [Header("体力条显示")]

    /// <summary>
    /// 体力条的填充图片。
    /// </summary>
    [SerializeField] private Image staminaFill;

    [Header("体力测试参数")]

    /// <summary>
    /// 体力的最大值。
    /// </summary>
    [SerializeField, Min(1f)] private float maxStamina = 100f;

    /// <summary>
    /// 加速状态下每秒消耗的体力值。
    /// </summary>
    [SerializeField, Min(0f)] private float staminaDrainPerSecond = 2f;

    /// <summary>
    /// 未加速时每秒恢复的体力值。
    /// </summary>
    [SerializeField, Min(0f)] private float staminaRecoveryPerSecond = 5f;

    /// <summary>
    /// 是否只有玩家正在移动时才消耗体力。
    /// </summary>
    [SerializeField] private bool requirePlayerMoving = true;

    [Header("玩家引用")]

    /// <summary>
    /// 玩家行为组件，用来判断玩家是否正在移动。
    /// </summary>
    [SerializeField] private PlayerAction playerAction;

    /// <summary>
    /// 输入管理器，用来读取左 Shift 的加速状态。
    /// </summary>
    private GameInput gameInput;

    /// <summary>
    /// 当前体力值。
    /// </summary>
    private float currentStamina;

    /// <summary>
    /// 体力是否已经耗尽，耗尽后需要停止冲刺条件才能恢复。
    /// </summary>
    private bool isStaminaDepleted;

    /// <summary>
    /// 当前体力值，只允许其他脚本读取，不能直接修改。
    /// </summary>
    public float CurrentStamina => currentStamina;

    /// <summary>
    /// 最大体力值，只允许其他脚本读取，不能直接修改。
    /// </summary>
    public float MaxStamina => maxStamina;

    /// <summary>
    /// 当前是否处于加速状态。
    /// </summary>
    public bool IsSprinting { get; private set; }

    /// <summary>
    /// 初始化体力数值、组件引用和体力条显示设置。
    /// </summary>
    private void Awake()
    {
        maxStamina = Mathf.Max(1f, maxStamina);
        currentStamina = maxStamina;

        FindReferences();
        ConfigureStaminaFill();
        RefreshStaminaView();
    }

    /// <summary>
    /// 每帧更新加速状态、体力数值和体力条显示。
    /// </summary>
    private void Update()
    {
        FindGameInput();
        UpdateStaminaValue();
        RefreshStaminaView();
    }

    /// <summary>
    /// 自动查找玩家行为组件和输入管理器。
    /// 手动拖拽引用后，脚本不会重复查找对应组件。
    /// </summary>
    private void FindReferences()
    {
        if (playerAction == null)
            playerAction = FindObjectOfType<PlayerAction>();

        FindGameInput();
    }

    /// <summary>
    /// 查找场景中的输入管理器单例。
    /// </summary>
    private void FindGameInput()
    {
        if (gameInput == null)
            gameInput = GameInput.Instance;
    }

    /// <summary>
    /// 把体力填充图片配置为水平填充，并从左侧开始显示。
    /// 体力降低时，右侧会逐渐消失。
    /// </summary>
    private void ConfigureStaminaFill()
    {
        if (staminaFill == null)
            return;

        staminaFill.type = Image.Type.Filled;
        staminaFill.fillMethod = Image.FillMethod.Horizontal;
        staminaFill.fillOrigin = (int)Image.OriginHorizontal.Left;
    }

    /// <summary>
    /// 根据左 Shift 的状态更新当前体力值。
    /// 当前项目中左 Shift 是切换键：按一次开启，再按一次关闭。
    /// </summary>
    private void UpdateStaminaValue()
    {
        // 左 Shift 是否处于开启状态。
        bool controlActive = gameInput != null && gameInput.IsControlActive;

        // 玩家是否正在移动。
        bool isMoving = playerAction == null || playerAction.IsMoving;

        // 是否满足消耗体力的条件。
        bool shouldDrainStamina = controlActive && (!requirePlayerMoving || isMoving);

        // 停止加速或停止移动后，解除体力耗尽锁定并允许恢复。
        if (!shouldDrainStamina)
            isStaminaDepleted = false;

        IsSprinting = shouldDrainStamina && !isStaminaDepleted && currentStamina > 0f;

        if (IsSprinting)
        {
            // 每秒消耗 2 点，而不是每帧固定消耗 2 点。
            currentStamina -= staminaDrainPerSecond * Time.deltaTime;

            if (currentStamina <= 0f)
            {
                currentStamina = 0f;
                isStaminaDepleted = true;
            }
        }
        else if (!shouldDrainStamina)
        {
            // 加速停止后恢复体力，恢复速度可以在 Inspector 中调整。
            currentStamina += staminaRecoveryPerSecond * Time.deltaTime;
        }

        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
    }

    /// <summary>
    /// 把当前体力值换算成 0 到 1 的比例，并更新体力条。
    /// </summary>
    private void RefreshStaminaView()
    {
        if (staminaFill == null)
            return;

        staminaFill.fillAmount = currentStamina / maxStamina;
    }

    /// <summary>
    /// 设置当前体力值，供以后受到消耗或恢复效果时调用。
    /// </summary>
    /// <param name="newStamina">新的体力值。</param>
    public void SetStamina(float newStamina)
    {
        currentStamina = Mathf.Clamp(newStamina, 0f, maxStamina);
        isStaminaDepleted = currentStamina <= 0f;
        RefreshStaminaView();
    }

    /// <summary>
    /// 设置最大体力值，并同步修正当前体力值。
    /// </summary>
    /// <param name="newMaxStamina">新的最大体力值。</param>
    public void SetMaxStamina(float newMaxStamina)
    {
        maxStamina = Mathf.Max(1f, newMaxStamina);
        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
        isStaminaDepleted = currentStamina <= 0f;
        RefreshStaminaView();
    }

    /// <summary>
    /// 在 Inspector 修改参数时，保证体力参数保持合法。
    /// </summary>
    private void OnValidate()
    {
        maxStamina = Mathf.Max(1f, maxStamina);
        staminaDrainPerSecond = Mathf.Max(0f, staminaDrainPerSecond);
        staminaRecoveryPerSecond = Mathf.Max(0f, staminaRecoveryPerSecond);
    }
}

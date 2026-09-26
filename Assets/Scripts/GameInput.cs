using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 统一管理玩家的移动、交互、背包和战斗输入。
/// </summary>
public class GameInput : MonoBehaviour
{
    /// <summary>
    /// 游戏输入管理器的单例实例。
    /// </summary>
    public static GameInput Instance { get; private set; }

    /// <summary>
    /// 左 Ctrl 控制状态发生变化时发出的事件。
    /// </summary>
    public event Action<bool> OnControlToggled;

    /// <summary>
    /// Tab 背包开关按键被按下时发出的事件。
    /// </summary>
    public event Action OnPackagePressed;

    /// <summary>
    /// F 交互按键被按下时发出的事件。
    /// </summary>
    public event Action OnInteractPressed;

    /// <summary>
    /// Battle（战斗）按键被按下时发出的事件。
    /// </summary>
    public event Action OnBattlePressed;

    /// <summary>
    /// 左 Ctrl 当前是否处于开启状态。
    /// </summary>
    public bool IsControlActive { get; private set; }

    /// <summary>
    /// 当前是否允许玩家使用游戏玩法输入。
    /// </summary>
    public bool IsGameplayInputEnabled { get; private set; } = true;

    /// <summary>
    /// 当前的移动输入方向。
    /// </summary>
    public Vector2 MoveInput { get; private set; }

    /// <summary>
    /// Unity Input System（Unity 输入系统）生成的输入控制对象。
    /// </summary>
    private GameControls gameControls;

    /// <summary>
    /// 初始化单例和输入动作监听。
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        gameControls = new GameControls();
        gameControls.Player.Interact.performed += Interact_performed;
        gameControls.Player.Battle.performed += Battle_performed;
    }

    /// <summary>
    /// 启用玩家输入动作地图。
    /// </summary>
    private void OnEnable()
    {
        gameControls?.Player.Enable();
    }

    /// <summary>
    /// 禁用玩家输入动作地图。
    /// </summary>
    private void OnDisable()
    {
        gameControls?.Player.Disable();
    }

    /// <summary>
    /// 取消输入事件监听并清理输入控制对象。
    /// </summary>
    private void OnDestroy()
    {
        if (gameControls != null)
        {
            gameControls.Player.Interact.performed -= Interact_performed;
            gameControls.Player.Battle.performed -= Battle_performed;
            gameControls.Dispose();
        }

        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// 每帧读取移动输入；背包打开时强制清零移动输入。
    /// </summary>
    private void Update()
    {
        if (gameControls == null || !IsGameplayInputEnabled)
        {
            MoveInput = Vector2.zero;
            return;
        }

        MoveInput = Vector2.ClampMagnitude(gameControls.Player.Move.ReadValue<Vector2>(), 1f);
    }

    /// <summary>
    /// 设置是否允许玩家使用移动、控制切换、交互和战斗等玩法输入。
    /// </summary>
    /// <param name="isEnabled">是否允许玩法输入。</param>
    public void SetGameplayInputEnabled(bool isEnabled)
    {
        IsGameplayInputEnabled = isEnabled;
        MoveInput = Vector2.zero;

        if (!isEnabled)
            CancelControlState();
    }

    /// <summary>
    /// 取消左 Ctrl 的切换状态，并通知玩家行为脚本停止加速。
    /// </summary>
    public void CancelControlState()
    {
        if (!IsControlActive)
            return;

        IsControlActive = false;
        OnControlToggled?.Invoke(false);
    }

    /// <summary>
    /// 处理 Interact（交互动作）中的左 Ctrl、Tab 和 F 输入。
    /// </summary>
    /// <param name="context">输入系统传入的按键事件数据。</param>
    private void Interact_performed(InputAction.CallbackContext context)
    {
        // Tab 属于背包界面控制键，即使背包打开，也必须继续响应。
        if (IsPackageKey(context))
        {
            OnPackagePressed?.Invoke();
            return;
        }

        // 背包打开后，其他玩法输入全部忽略。
        if (!IsGameplayInputEnabled)
            return;

        if (IsInteractKey(context))
        {
            OnInteractPressed?.Invoke();
            return;
        }

        IsControlActive = !IsControlActive;
        OnControlToggled?.Invoke(IsControlActive);
    }

    /// <summary>
    /// 判断当前输入是否来自 Tab 背包按键。
    /// </summary>
    /// <param name="context">输入系统传入的按键事件数据。</param>
    /// <returns>来自 Tab 时返回 true，否则返回 false。</returns>
    private bool IsPackageKey(InputAction.CallbackContext context)
    {
        if (Keyboard.current != null && context.control == Keyboard.current.tabKey)
            return true;

        return context.control != null && context.control.name == "tab";
    }

    /// <summary>
    /// 判断当前输入是否来自 F 交互按键。
    /// </summary>
    /// <param name="context">输入系统传入的按键事件数据。</param>
    /// <returns>来自 F 时返回 true，否则返回 false。</returns>
    private bool IsInteractKey(InputAction.CallbackContext context)
    {
        if (Keyboard.current != null && context.control == Keyboard.current.fKey)
            return true;

        return context.control != null && context.control.name == "f";
    }

    /// <summary>
    /// 处理 Battle（战斗动作）输入；背包打开时忽略战斗。
    /// </summary>
    /// <param name="context">输入系统传入的按键事件数据。</param>
    private void Battle_performed(InputAction.CallbackContext context)
    {
        if (!IsGameplayInputEnabled)
            return;

        OnBattlePressed?.Invoke();
    }
}

using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 统一管理玩家的移动、控制切换和攻击输入。
/// </summary>
public class GameInput : MonoBehaviour
{//单例模式
    public static GameInput Instance { get; private set; }

    // 把输入事件统一从这里发出去，其他脚本不用直接碰 Input System。
    public event Action<bool> OnControlToggled; // 左 Shift 的开关状态变化事件。
    public event Action OnAttackPressed;
    public bool IsControlActive { get; private set; } // 左 Shift 当前是否处于开启状态。
    public Vector2 MoveInput { get; private set; }

    /// <summary>
    /// Unity Input System（Unity 输入系统）生成的输入控制对象。
    /// </summary>
    private GameControls gameControls;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        gameControls = new GameControls();
        // Button 类型的 performed 只在按键触发时执行，适合攻击、冲刺这类一次性输入。
        gameControls.Player.Controls.performed += Controls_performed;
        gameControls.Player.Attack.performed += Attack_performed;
    }

    private void OnEnable()
    {
        gameControls?.Player.Enable();
    }

    private void OnDisable()
    {
        gameControls?.Player.Disable();
    }

    private void OnDestroy()
    {
        if (gameControls != null)
        {
            gameControls.Player.Controls.performed -= Controls_performed;
            gameControls.Player.Attack.performed -= Attack_performed;
            gameControls.Dispose();
        }

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (gameControls == null)
        {
            MoveInput = Vector2.zero;
            return;
        }

        // 移动输入是持续状态，所以每帧读取一次。
        MoveInput = Vector2.ClampMagnitude(gameControls.Player.Move.ReadValue<Vector2>(), 1f);
    }

    private void Controls_performed(InputAction.CallbackContext context)
    {
        // 左 Shift 每按一次就切换一次状态。
        IsControlActive = !IsControlActive;
        OnControlToggled?.Invoke(IsControlActive);
    }

    private void Attack_performed(InputAction.CallbackContext context)
    {
        OnAttackPressed?.Invoke();
    }
}

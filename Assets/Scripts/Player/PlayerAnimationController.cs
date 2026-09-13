using UnityEngine;

// 控制玩家动画表现，不负责玩家行为逻辑。
public class PlayerAnimationController : MonoBehaviour
{
    [SerializeField] private PlayerAction playerAction; // 玩家行为组件。
    [SerializeField] private Animator animator; // 玩家动画控制器。

    // Animator 参数名集中放这里，避免字符串散落在各处。
    private const string ISWALKING = "IsWalking";
    private const string ATTACK = "Attack";
    private const string ATTACKDIR = "AttackDir";
    private Vector3 defaultScale; // Visual 初始缩放。

    // 初始化组件引用。
    private void Awake()
    {
        if (playerAction == null)
            playerAction = GetComponentInParent<PlayerAction>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        defaultScale = transform.localScale;
    }

    // 启用时监听玩家攻击事件。
    private void OnEnable()
    {
        if (playerAction != null)
            playerAction.OnAttackPerformed += HandleAttackPerformed;
    }

    // 禁用时取消监听玩家攻击事件。
    private void OnDisable()
    {
        if (playerAction != null)
            playerAction.OnAttackPerformed -= HandleAttackPerformed;
    }

    // 每帧刷新移动动画和朝向。
    private void Update()
    {
        if (playerAction == null || animator == null)
            return;

        // 移动动画每帧根据行为状态刷新。
        animator.SetBool(ISWALKING, playerAction.IsMoving);
        ApplyFacingScale();
    }

    // 播放玩家攻击动画。
    private void HandleAttackPerformed(int attackDirectionIndex)
    {
        if (playerAction == null || animator == null)
            return;

        ApplyFacingScale();
        animator.SetInteger(ATTACKDIR, attackDirectionIndex);
        animator.SetTrigger(ATTACK);
    }

    // 根据玩家横向朝向翻转 Visual。
    private void ApplyFacingScale()
    {
        // 只翻转 Visual，不翻转玩家根物体，避免碰撞体和相机目标跟着反向。
        Vector3 scale = defaultScale;
        scale.x = Mathf.Abs(defaultScale.x) * playerAction.FacingX;
        transform.localScale = scale;
    }
}

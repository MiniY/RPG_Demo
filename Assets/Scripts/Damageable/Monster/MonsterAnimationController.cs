using UnityEngine;

// 控制怪物 Visual 子物体的动画表现，不负责行为决策、移动或伤害。
public class MonsterAnimationController : MonoBehaviour
{
    [Header("组件引用")]
    [SerializeField] private MonsterMovementController movementController; // 父物体上的怪物移动模块。
    [SerializeField] private MonsterAttackController attackController; // 父物体上的怪物攻击模块。
    [SerializeField] private Animator animator; // Visual 子物体上的动画控制器。
    [SerializeField] private SpriteRenderer spriteRenderer; // Visual 子物体上的精灵渲染器。

    [Header("Animator 参数")]
    [SerializeField] private string isMovingParameterName = "IsMoving"; // 控制 Idle 和 Move 切换的布尔参数名。
    [SerializeField] private string attackParameterName = "Attack"; // 开始攻击时使用的触发参数名。
    [SerializeField] private string attackDirectionParameterName = "AttackDirection"; // 选择四方向攻击的整数参数名。

    [Header("朝向表现")]
    [SerializeField] private bool movementSpriteFacesLeft = true; // Idle 和 Walking 的原始水平精灵是否默认朝左。
    [SerializeField] private bool horizontalAttackSpriteFacesLeft = false; // 水平攻击原始精灵是否朝左；Torch_Blue 的 Attack_Right 素材朝右。

    private int isMovingParameterHash; // IsMoving 参数的哈希值。
    private int attackParameterHash; // Attack 参数的哈希值。
    private int attackDirectionParameterHash; // AttackDirection 参数的哈希值。
    private bool hasIsMovingParameter; // Animator 是否包含可用的 IsMoving 布尔参数。
    private bool hasAttackParameter; // Animator 是否包含可用的 Attack 触发参数。
    private bool hasAttackDirectionParameter; // Animator 是否包含可用的 AttackDirection 整数参数。

    // 初始化父物体行为引用和当前 Visual 上的 Animator。
    private void Awake()
    {
        if (movementController == null)
            movementController = GetComponentInParent<MonsterMovementController>();

        if (attackController == null)
            attackController = GetComponentInParent<MonsterAttackController>();

        if (animator == null)
            animator = GetComponent<Animator>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        CacheAnimatorParameters();
    }

    // 启用时监听父物体的移动表现事件，并同步当前动画状态。
    private void OnEnable()
    {
        if (movementController != null)
        {
            movementController.OnMovementChanged += HandleMovementChanged;
            movementController.OnFacingDirectionChanged += HandleFacingDirectionChanged;
            ApplyMovementAnimation(movementController.IsMoving);
            ApplyMovementFacing(movementController.FacingDirection);
        }

        if (attackController != null)
        {
            attackController.OnAttackStarted -= HandleAttackStarted;
            attackController.OnAttackStarted += HandleAttackStarted;
        }
    }

    // 禁用时取消监听，避免对象池复用后重复订阅。
    private void OnDisable()
    {
        if (movementController != null)
        {
            movementController.OnMovementChanged -= HandleMovementChanged;
            movementController.OnFacingDirectionChanged -= HandleFacingDirectionChanged;
        }

        if (attackController != null)
            attackController.OnAttackStarted -= HandleAttackStarted;
    }

    // 缓存 Animator 参数，避免每次移动状态改变时重复遍历。
    private void CacheAnimatorParameters()
    {
        hasIsMovingParameter = false;
        hasAttackParameter = false;
        hasAttackDirectionParameter = false;

        if (animator == null)
            return;

        isMovingParameterHash = Animator.StringToHash(isMovingParameterName);
        attackParameterHash = Animator.StringToHash(attackParameterName);
        attackDirectionParameterHash = Animator.StringToHash(attackDirectionParameterName);

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == isMovingParameterHash &&
                parameter.type == AnimatorControllerParameterType.Bool)
                hasIsMovingParameter = true;

            if (parameter.nameHash == attackParameterHash &&
                parameter.type == AnimatorControllerParameterType.Trigger)
                hasAttackParameter = true;

            if (parameter.nameHash == attackDirectionParameterHash &&
                parameter.type == AnimatorControllerParameterType.Int)
                hasAttackDirectionParameter = true;
        }
    }

    // 接收父物体移动状态变化，并转换为动画参数。
    private void HandleMovementChanged(bool isMoving)
    {
        ApplyMovementAnimation(isMoving);
    }

    // 接收父物体朝向变化，并更新水平精灵翻转。
    private void HandleFacingDirectionChanged(Vector2 facingDirection)
    {
        ApplyMovementFacing(facingDirection);
    }

    // 接收父物体攻击事件，并播放锁定方向对应的攻击动画。
    private void HandleAttackStarted(Vector2 attackDirection)
    {
        PlayAttack(attackDirection);
    }

    // 只在参数真实存在时设置移动动画，避免 Animator 报错。
    private void ApplyMovementAnimation(bool isMoving)
    {
        if (animator == null || !hasIsMovingParameter)
            return;

        animator.SetBool(isMovingParameterHash, isMoving);
    }

    // 根据目标方向选择并触发一次四方向攻击动画。
    public bool PlayAttack(Vector2 attackDirection)
    {
        if (animator == null || !hasAttackParameter || !hasAttackDirectionParameter)
            return false;

        if (attackDirection.sqrMagnitude <= 0.0001f && movementController != null)
            attackDirection = movementController.FacingDirection;

        int directionIndex = GetDirectionIndex(attackDirection);
        ApplyAttackFacing(directionIndex);

        animator.SetInteger(attackDirectionParameterHash, directionIndex);
        animator.ResetTrigger(attackParameterHash);
        animator.SetTrigger(attackParameterHash);
        return true;
    }

    // 水平移动时根据原始素材朝向翻转精灵，垂直移动时保留上一次水平朝向。
    private void ApplyMovementFacing(Vector2 facingDirection)
    {
        if (spriteRenderer == null || Mathf.Abs(facingDirection.x) <= 0.01f)
            return;

        bool facesRight = facingDirection.x > 0f;
        spriteRenderer.flipX = movementSpriteFacesLeft ? facesRight : !facesRight;
    }

    // 攻击时应用与四方向动画对应的精灵翻转。
    private void ApplyAttackFacing(int directionIndex)
    {
        if (spriteRenderer == null)
            return;

        const int upDirection = 0;
        const int downDirection = 1;
        const int leftDirection = 2;
        const int rightDirection = 3;

        switch (directionIndex)
        {
            case rightDirection:
                spriteRenderer.flipX = horizontalAttackSpriteFacesLeft;
                break;
            case leftDirection:
                spriteRenderer.flipX = !horizontalAttackSpriteFacesLeft;
                break;
            case upDirection:
            case downDirection:
                spriteRenderer.flipX = false;
                break;
        }
    }

    // 把方向向量转换成 Up=0、Down=1、Left=2、Right=3 的动画编号。
    private int GetDirectionIndex(Vector2 direction)
    {
        if (Mathf.Abs(direction.y) > Mathf.Abs(direction.x))
            return direction.y > 0f ? 0 : 1;

        return direction.x < 0f ? 2 : 3;
    }
}

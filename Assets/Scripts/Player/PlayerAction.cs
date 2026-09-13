using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

// 控制玩家的移动、朝向和攻击行为。
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerAction : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f; // 玩家移动速度。
    [SerializeField] private float speedBoostAmount = 2f; // 左 Shift 开启时额外增加的速度。
    [FormerlySerializedAs("monsterSearchRange")]
    [SerializeField] private float damageableSearchRange = 2f; // 攻击时搜索可破坏对象的半径。
    [SerializeField] private float attackDamage = 20f; // 每次攻击造成的伤害。
    [SerializeField, Min(0f)] private float attackHitDelay = 0.24f; // 攻击动画开始后延迟多久才真正造成伤害。
    [SerializeField] private bool showAttackDebugLog = false; // 是否显示攻击调试信息。

    private Rigidbody2D rb; // 玩家刚体组件。
    private GameInput gameInput; // 输入管理器。
    private BaseDamageable preparedAttackTarget; // 本次攻击准备命中的目标。
    private Vector2 moveInput; // 当前移动输入。
    private Vector2 facingDirection = Vector2.down; // 玩家当前面对方向。
    private float facingX = 1f; // 玩家横向朝向，1 向右，-1 向左。
    private int attackDirectionIndex = 1; // 攻击方向编号：Up = 0，Down = 1，Left = 2，Right = 3。
    private bool isSpeedBoostActive; // 左 Shift 是否处于加速状态。

    public event Action<int> OnAttackPerformed; // 玩家执行攻击时通知动画系统。

    public Vector2 MoveInput => moveInput; // 对外提供当前移动输入。
    public Vector2 FacingDirection => facingDirection; // 对外提供玩家面对方向。
    public float FacingX => facingX; // 对外提供横向朝向。
    public bool IsMoving => moveInput.sqrMagnitude > 0.001f; // 对外提供是否正在移动。
    public int AttackDirectionIndex => attackDirectionIndex; // 对外提供当前攻击方向编号。

    // 初始化玩家组件引用。
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        attackDirectionIndex = 1;
    }

    // 玩家启用时尝试绑定输入事件。
    private void OnEnable()
    {
        TryBindInput();
    }

    // 玩家禁用时解绑输入事件，避免重复监听。
    private void OnDisable()
    {
        UnbindInput();
        StopAllCoroutines();
    }

    // 每帧读取输入，并在输入管理器延迟创建时补绑事件。
    private void Update()
    {
        TryBindInput();
        ReadMoveInput();
    }

    // 固定帧移动玩家刚体。
    private void FixedUpdate()
    {
        Move();
    }

    // 从 GameInput 读取移动输入。
    private void ReadMoveInput()
    {
        if (GameInput.Instance == null)
        {
            moveInput = Vector2.zero;
            return;
        }

        // GameInput 已经负责读取新 Input System
        moveInput = GameInput.Instance.MoveInput;
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);

        if (moveInput.sqrMagnitude <= 0.001f)
            return;

        if (moveInput.x > 0.001f)
            facingX = 1f;
        else if (moveInput.x < -0.001f)
            facingX = -1f;

        // 角色使用四方向动画，记录最接近的主方向
        if (Mathf.Abs(moveInput.x) > Mathf.Abs(moveInput.y))
        {
            facingDirection = new Vector2(Mathf.Sign(moveInput.x), 0f);
        }
        else
        {
            facingDirection = new Vector2(0f, Mathf.Sign(moveInput.y));
        }

        // 平时移动时缓存攻击方向，站立攻击时也能沿最后面对方向出招。
        if (Mathf.Abs(moveInput.y) > Mathf.Abs(moveInput.x))
        {
            attackDirectionIndex = moveInput.y > 0f ? 0 : 1;
        }
        else
        {
            attackDirectionIndex = moveInput.x < 0f ? 2 : 3;
        }
    }

    // 根据当前输入移动玩家。
    private void Move()
    {
        float appliedMoveSpeed = moveSpeed + (isSpeedBoostActive ? speedBoostAmount : 0f);
        Vector2 nextPosition = rb.position + moveInput * appliedMoveSpeed * Time.fixedDeltaTime;

        rb.MovePosition(nextPosition);
    }

    // 准备攻击方向，并记录本次攻击目标。
    public int PrepareAttackDirection()
    {
        // 只有真正攻击时才搜索可破坏对象，平时不做额外检测。
        preparedAttackTarget = FindNearestDamageableInRange();

        if (preparedAttackTarget != null)
        {
            Vector2 directionToDamageable = preparedAttackTarget.transform.position - transform.position;
            attackDirectionIndex = GetDirectionIndex(directionToDamageable);
            UpdateFacingByDirectionIndex(attackDirectionIndex);

            if (showAttackDebugLog)
                Debug.Log($"Attack target: {preparedAttackTarget.name}, direction: {attackDirectionIndex}");
        }
        else if (showAttackDebugLog)
        {
            Debug.Log($"No damageable target in range. Use facing direction: {attackDirectionIndex}");
        }

        return attackDirectionIndex;
    }

    // 尝试绑定攻击输入事件。
    private void TryBindInput()
    {
        if (gameInput != null)
            return;

        if (GameInput.Instance == null)
            return;

        gameInput = GameInput.Instance;
        // 先同步当前左 Shift 状态，再监听后续切换。
        ApplyControlState(gameInput.IsControlActive);
        gameInput.OnControlToggled += HandleControlToggled;
        gameInput.OnAttackPressed += HandleAttackPressed;
    }

    // 解绑攻击输入事件。
    private void UnbindInput()
    {
        if (gameInput == null)
            return;

        gameInput.OnControlToggled -= HandleControlToggled;
        gameInput.OnAttackPressed -= HandleAttackPressed;
        gameInput = null;
    }

    // 处理左 Shift 的切换事件。
    private void HandleControlToggled(bool isActive)
    {
        ApplyControlState(isActive);
    }

    // 应用左 Shift 的当前状态。
    private void ApplyControlState(bool isActive)
    {
        isSpeedBoostActive = isActive;
    }

    // 处理玩家按下攻击键。
    private void HandleAttackPressed()
    {
        int preparedDirectionIndex = PrepareAttackDirection();
        BaseDamageable attackTarget = preparedAttackTarget;

        OnAttackPerformed?.Invoke(preparedDirectionIndex);
        StartCoroutine(DealDamageAfterDelay(attackTarget));
        preparedAttackTarget = null;
    }

    // 等待攻击命中延迟后再结算伤害。
    private IEnumerator DealDamageAfterDelay(BaseDamageable attackTarget)
    {
        if (attackHitDelay > 0f)
            yield return new WaitForSeconds(attackHitDelay);

        DealDamageToTarget(attackTarget);
    }

    // 对指定可破坏对象造成伤害。
    private void DealDamageToTarget(BaseDamageable attackTarget)
    {
        if (attackTarget == null || !attackTarget.gameObject.activeInHierarchy)
            return;

        if (attackTarget.IsDefeated)
        {
            if (showAttackDebugLog)
                Debug.LogWarning($"Attack target {attackTarget.name} is already defeated.");

            return;
        }

        attackTarget.TakeDamage(attackDamage, transform);
    }

    // 在攻击范围内寻找最近的可破坏对象。
    private BaseDamageable FindNearestDamageableInRange()
    {
        BaseDamageable[] damageables = UnityEngine.Object.FindObjectsOfType<BaseDamageable>();
        BaseDamageable nearestDamageable = null;
        float searchRangeSqr = damageableSearchRange * damageableSearchRange;
        float nearestDistanceSqr = searchRangeSqr;

        // 使用平方距离比较，结果一样，但省掉开方计算。
        foreach (BaseDamageable damageable in damageables)
        {
            if (damageable == null || !damageable.gameObject.activeInHierarchy)
                continue;

            Vector2 offset = damageable.transform.position - transform.position;
            float distanceSqr = offset.sqrMagnitude;

            if (distanceSqr > nearestDistanceSqr)
                continue;

            nearestDistanceSqr = distanceSqr;
            nearestDamageable = damageable;
        }

        return nearestDamageable;
    }

    // 根据方向向量换算四方向动画编号。
    private int GetDirectionIndex(Vector2 direction)
    {
        // 取偏移量更大的轴作为主攻击方向，避免斜向目标时方向摇摆。
        if (Mathf.Abs(direction.y) > Mathf.Abs(direction.x))
            return direction.y > 0f ? 0 : 1;

        return direction.x < 0f ? 2 : 3;
    }

    // 根据攻击方向更新玩家面对方向。
    private void UpdateFacingByDirectionIndex(int directionIndex)
    {
        switch (directionIndex)
        {
            case 0:
                facingDirection = Vector2.up;
                break;
            case 1:
                facingDirection = Vector2.down;
                break;
            case 2:
                facingDirection = Vector2.left;
                facingX = -1f;
                break;
            case 3:
                facingDirection = Vector2.right;
                facingX = 1f;
                break;
        }
    }

    // 在 Scene 视图里显示攻击搜索范围。
    private void OnDrawGizmosSelected()
    {
        // 选中玩家时显示自动搜索范围，方便在 Scene 里调参数。
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, damageableSearchRange);
    }

    // 在 Inspector 修改数值时保证参数合法。
    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        speedBoostAmount = Mathf.Max(0f, speedBoostAmount);
        damageableSearchRange = Mathf.Max(0f, damageableSearchRange);
        attackDamage = Mathf.Max(0f, attackDamage);
        attackHitDelay = Mathf.Max(0f, attackHitDelay);
    }
}

using System;
using UnityEngine;

[RequireComponent(typeof(MonsterMovementController))]
// 保存怪物近战攻击参数、发布攻击表现事件并执行最终伤害结算。
public class MonsterAttackController : MonoBehaviour
{
    [Header("近战攻击参数")]
    [SerializeField, Min(0f)] private float attackDamage = 10f; // 每次成功命中造成的伤害。
    [SerializeField, Min(0f)] private float attackRange = 1.2f; // 怪物能够开始并命中近战攻击的最大距离。
    [SerializeField, Min(0f)] private float attackWindupDuration = 0.25f; // 攻击动画开始到伤害结算之间的前摇时间。
    [SerializeField, Min(0f)] private float attackCooldownDuration = 0.35f; // 伤害结算后到下一次攻击之间的冷却时间。
    [SerializeField, Range(-1f, 1f)] private float minimumFacingDot = 0.7f; // 允许命中的最小朝向点积，数值越大正面范围越窄。

    public event Action<Vector2> OnAttackStarted; // 攻击开始时把锁定方向通知动画表现模块。

    public float AttackRange => attackRange; // 对外提供攻击范围。
    public float AttackWindupDuration => attackWindupDuration; // 对外提供攻击前摇时间。
    public float AttackCooldownDuration => attackCooldownDuration; // 对外提供攻击冷却时间。

    // 返回怪物指向目标碰撞体中心的方向向量。
    public Vector2 GetDirectionToTarget(Transform target)
    {
        if (target == null)
            return Vector2.zero;

        return GetTargetPosition(target) - (Vector2)transform.position;
    }

    // 判断目标当前是否位于近战攻击范围内。
    public bool IsTargetInRange(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy)
            return false;

        Vector2 closestTargetPoint = GetClosestTargetPoint(target); // 玩家碰撞体距离怪物最近的世界坐标。
        Vector2 offset = closestTargetPoint - (Vector2)transform.position; // 怪物到玩家实体边缘的偏移量。
        return offset.sqrMagnitude <= attackRange * attackRange;
    }

    // 发布一次攻击开始事件，不直接操作 Visual 子物体上的 Animator。
    public void BeginAttack(Vector2 attackDirection)
    {
        if (attackDirection.sqrMagnitude <= 0.0001f)
            return;

        OnAttackStarted?.Invoke(attackDirection.normalized);
    }

    // 在命中时刻重新检查距离和朝向，条件成立时对玩家造成伤害。
    public bool TryDealDamage(Transform target, Vector2 attackDirection)
    {
        if (!CanHitTarget(target, attackDirection))
            return false;

        PlayerDamageController damageController = ResolvePlayerDamageController(target); // 目标身上的玩家伤害接收器。

        if (damageController == null)
            return false;

        return damageController.TakeDamage(attackDamage, transform);
    }

    // 判断目标在命中时刻是否仍位于攻击范围和锁定方向的正面。
    private bool CanHitTarget(Transform target, Vector2 attackDirection)
    {
        if (!IsTargetInRange(target) || attackDirection.sqrMagnitude <= 0.0001f)
            return false;

        Vector2 directionToTarget = GetDirectionToTarget(target).normalized; // 命中时怪物指向玩家的实际方向。
        float facingDot = Vector2.Dot(attackDirection.normalized, directionToTarget); // 锁定方向与实际方向的点积。
        return facingDot >= minimumFacingDot;
    }

    // 从目标根物体或其层级中查找玩家伤害接收器。
    private PlayerDamageController ResolvePlayerDamageController(Transform target)
    {
        PlayerDamageController damageController = target.GetComponent<PlayerDamageController>(); // 目标根物体上的伤害接收器。

        if (damageController == null)
            damageController = target.GetComponentInParent<PlayerDamageController>();

        if (damageController == null)
            damageController = target.GetComponentInChildren<PlayerDamageController>();

        return damageController;
    }

    // 优先使用目标碰撞体中心，避免不同精灵轴心导致攻击距离判断偏移。
    private Vector2 GetTargetPosition(Transform target)
    {
        Collider2D targetCollider = ResolveTargetCollider(target); // 用于计算中心位置的目标碰撞体。

        return targetCollider != null
            ? (Vector2)targetCollider.bounds.center
            : (Vector2)target.position;
    }

    // 返回目标碰撞体上距离怪物最近的点，避免大型碰撞体造成斜向攻击死区。
    private Vector2 GetClosestTargetPoint(Transform target)
    {
        Collider2D targetCollider = ResolveTargetCollider(target); // 用于计算最近点的目标碰撞体。
        Vector2 monsterPosition = transform.position; // 怪物当前世界坐标。

        return targetCollider != null
            ? targetCollider.ClosestPoint(monsterPosition)
            : (Vector2)target.position;
    }

    // 查找目标根物体或子物体上的二维碰撞体。
    private Collider2D ResolveTargetCollider(Transform target)
    {
        Collider2D targetCollider = target.GetComponent<Collider2D>(); // 目标根物体上的二维碰撞体。

        if (targetCollider == null)
            targetCollider = target.GetComponentInChildren<Collider2D>();

        return targetCollider;
    }

    // 在 Scene 视图中显示怪物近战攻击范围。
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }

    // 在 Inspector 修改数值时保证攻击参数合法。
    private void OnValidate()
    {
        attackDamage = Mathf.Max(0f, attackDamage);
        attackRange = Mathf.Max(0f, attackRange);
        attackWindupDuration = Mathf.Max(0f, attackWindupDuration);
        attackCooldownDuration = Mathf.Max(0f, attackCooldownDuration);
        minimumFacingDot = Mathf.Clamp(minimumFacingDot, -1f, 1f);
    }
}

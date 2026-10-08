using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerAction))]
[RequireComponent(typeof(PlayerDamageController))]
// 监听玩家受伤事件并执行远离伤害来源的短距离击退，不负责扣除生命值或播放动画。
public class PlayerHurtController : MonoBehaviour, IMapTransitionResettable
{
    [Header("击退参数")]
    [SerializeField, Min(0f)] private float knockbackDistance = 0.65f; // 玩家每次受击向后移动的总距离。
    [SerializeField, Min(0f)] private float knockbackDuration = 0.14f; // 完成一次击退所需的时间。

    [Header("碰撞参数")]
    [SerializeField, Min(0f)] private float collisionSkinWidth = 0.02f; // 击退终点与墙体之间保留的距离。
    [SerializeField] private LayerMask obstacleLayers = 1; // 能够阻挡玩家击退的图层，默认检测 Default 图层。

    private readonly RaycastHit2D[] knockbackHits = new RaycastHit2D[8]; // 击退形状投射复用的命中结果数组。
    private Rigidbody2D body; // 玩家使用的二维刚体。
    private PlayerAction playerAction; // 玩家移动和输入控制器。
    private PlayerDamageController damageController; // 玩家伤害事件来源。
    private ContactFilter2D knockbackContactFilter; // 击退前形状投射使用的碰撞过滤条件。
    private Vector2 knockbackDirection; // 当前击退方向。
    private float remainingKnockbackDistance; // 当前击退尚未移动的距离。
    private float remainingKnockbackTime; // 当前击退剩余时间。
    private bool isKnockbackActive; // 当前是否正在执行击退。

    public bool IsKnockbackActive => isKnockbackActive; // 对外提供玩家当前是否正在被击退。

    // 初始化击退所需的组件引用和碰撞过滤条件。
    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        playerAction = GetComponent<PlayerAction>();
        damageController = GetComponent<PlayerDamageController>();
        ConfigureContactFilter();
    }

    // 启用时监听玩家受伤事件。
    private void OnEnable()
    {
        if (damageController != null)
        {
            damageController.OnDamaged -= HandlePlayerDamaged;
            damageController.OnDamaged += HandlePlayerDamaged;
        }
    }

    // 禁用时取消监听并结束尚未完成的击退。
    private void OnDisable()
    {
        if (damageController != null)
            damageController.OnDamaged -= HandlePlayerDamaged;

        EndKnockback();
    }

    // 在固定时间步中执行一小段击退位移。
    private void FixedUpdate()
    {
        ApplyKnockbackStep();
    }

    // 收到玩家受伤事件时，从伤害来源的反方向开始一次新击退。
    private void HandlePlayerDamaged(PlayerDamageController player, DamageInfo damageInfo)
    {
        if (player != damageController || !damageInfo.SourcePosition.HasValue)
            return;

        Vector2 direction = body.position - (Vector2)damageInfo.SourcePosition.Value; // 伤害来源指向玩家的反方向。

        if (direction.sqrMagnitude <= 0.0001f)
            direction = playerAction.FacingDirection.sqrMagnitude > 0.0001f
                ? -playerAction.FacingDirection
                : Vector2.down;

        BeginKnockback(direction.normalized);
    }

    // 重置当前击退进度并暂时锁定玩家的移动与攻击输入。
    private void BeginKnockback(Vector2 direction)
    {
        if (body == null || playerAction == null || knockbackDistance <= 0f)
            return;

        knockbackDirection = direction;
        remainingKnockbackDistance = knockbackDistance;
        remainingKnockbackTime = knockbackDuration;
        isKnockbackActive = true;

        body.velocity = Vector2.zero;
        playerAction.SetControlLocked(true);
    }

    // 计算本固定帧的安全移动距离，并在完成或撞墙时结束击退。
    private void ApplyKnockbackStep()
    {
        if (!isKnockbackActive || body == null)
            return;

        float requestedMoveDistance; // 本固定帧计划移动的距离。

        if (knockbackDuration <= 0f)
        {
            requestedMoveDistance = remainingKnockbackDistance;
        }
        else
        {
            float knockbackSpeed = knockbackDistance / knockbackDuration; // 击退阶段的恒定移动速度。
            requestedMoveDistance = Mathf.Min(
                remainingKnockbackDistance,
                knockbackSpeed * Time.fixedDeltaTime);
        }

        float allowedMoveDistance = GetAllowedMoveDistance(
            knockbackDirection,
            requestedMoveDistance); // 碰撞检测后本帧实际允许的距离。

        if (allowedMoveDistance > 0.0001f)
        {
            body.MovePosition(body.position + knockbackDirection * allowedMoveDistance);
            remainingKnockbackDistance -= allowedMoveDistance;
        }

        remainingKnockbackTime -= Time.fixedDeltaTime;
        bool wasBlocked = allowedMoveDistance + 0.0001f < requestedMoveDistance; // 本帧是否提前撞到障碍物。

        if (wasBlocked ||
            remainingKnockbackDistance <= 0.0001f ||
            remainingKnockbackTime <= 0f)
        {
            EndKnockback();
        }
    }

    // 使用玩家刚体形状投射，避免击退过程穿过墙体。
    private float GetAllowedMoveDistance(Vector2 direction, float requestedMoveDistance)
    {
        if (requestedMoveDistance <= 0f)
            return 0f;

        float castDistance = requestedMoveDistance + collisionSkinWidth; // 包含碰撞安全边距的检测距离。
        int hitCount = body.Cast(direction, knockbackContactFilter, knockbackHits, castDistance); // 本次形状投射命中数量。
        float allowedMoveDistance = requestedMoveDistance; // 当前允许的最大移动距离。

        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            RaycastHit2D hit = knockbackHits[hitIndex]; // 当前检测到的障碍物。

            if (hit.collider == null)
                continue;

            // 忽略包围玩家起点的地图边界碰撞体，避免 Confiner 阻止所有击退。
            if (hit.distance <= 0.0001f && hit.collider.OverlapPoint(body.position))
                continue;

            float distanceBeforeObstacle = Mathf.Max(0f, hit.distance - collisionSkinWidth);
            allowedMoveDistance = Mathf.Min(allowedMoveDistance, distanceBeforeObstacle);
        }

        return allowedMoveDistance;
    }

    // 完成击退并把移动与攻击控制权交还给玩家。
    private void EndKnockback()
    {
        isKnockbackActive = false;
        remainingKnockbackDistance = 0f;
        remainingKnockbackTime = 0f;

        if (body != null)
            body.velocity = Vector2.zero;

        if (playerAction != null)
            playerAction.SetControlLocked(false);
    }

    /// <summary>Stops prior-map knockback without changing Player gameplay state.</summary>
    public void ResetMapTransitionState()
    {
        EndKnockback();
    }

    // 创建忽略触发器且只检测指定障碍图层的击退过滤条件。
    private void ConfigureContactFilter()
    {
        knockbackContactFilter = new ContactFilter2D
        {
            useTriggers = false
        };
        knockbackContactFilter.SetLayerMask(obstacleLayers);
    }

    // 在 Inspector 修改数值时保证击退参数合法。
    private void OnValidate()
    {
        knockbackDistance = Mathf.Max(0f, knockbackDistance);
        knockbackDuration = Mathf.Max(0f, knockbackDuration);
        collisionSkinWidth = Mathf.Max(0f, collisionSkinWidth);
        ConfigureContactFilter();
    }
}

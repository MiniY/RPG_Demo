using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BaseMonster))]
[RequireComponent(typeof(MonsterMovementController))]
[RequireComponent(typeof(MonsterPerceptionController))]
[RequireComponent(typeof(MonsterAttackController))]
// 管理怪物行为状态、出生点和当前目标，不直接执行移动或动画。
public class MonsterAIController : MonoBehaviour, IMapDependentReinitializable, IMonsterPlacementTarget
{
    [Header("巡逻参数")]
    [SerializeField, Min(0f)] private float patrolRadius = 2f; // 以出生点为圆心的巡逻半径。
    [SerializeField] private Vector2 patrolWaitRange = new Vector2(0.5f, 1.5f); // 到达巡逻点后的随机停留时间范围。

    [Header("索敌参数")]
    [SerializeField, Min(0f)] private float searchConfirmationDuration = 0.35f; // 玩家持续可见多久后才确认目标。

    [Header("脱战参数")]
    [SerializeField, Min(0f)] private float lostTargetDuration = 1.5f; // 追击时持续丢失目标多久后进入脱战返回。
    [SerializeField, Min(0f)] private float maxChaseDistanceFromHome = 6f; // 目标距离出生点超过该半径时立即脱战。
    [SerializeField, Min(0f)] private float returnStoppingDistance = 0.1f; // 距离出生点小于该值时视为已经归位。

    [Header("运行状态")]
    [SerializeField] private MonsterStateType currentStateType = MonsterStateType.Patrol; // 当前行为状态，仅用于运行时观察。
    [SerializeField] private bool isTargetConfirmed; // 当前目标是否已经通过索敌确认，仅用于运行时观察。

    private readonly Dictionary<MonsterStateType, BaseMonsterState> states =
        new Dictionary<MonsterStateType, BaseMonsterState>(); // 已经实现并注册的状态。

    private BaseMonster baseMonster; // 怪物的受伤与被击败数据入口。
    private MonsterMovementController movementController; // 怪物移动模块。
    private MonsterPerceptionController perceptionController; // 怪物距离和视线感知模块。
    private MonsterAttackController attackController; // 怪物近战攻击能力模块。
    private BaseMonsterState currentState; // 当前正在执行的状态实例。
    private Vector2 homePosition; // 本次启用时记录的出生位置。
    private Transform currentTarget; // 当前锁定的目标对象。
    private Transform visibleTarget; // 感知模块最近一次报告的可见目标。
    private bool isRunning; // AI 是否正在运行。

    public event Action<MonsterStateType> OnStateChanged; // 行为状态改变时发出的事件。
    public event Action<Transform> OnTargetConfirmed; // 候选目标通过持续可见确认时发出的事件。

    public MonsterStateType CurrentStateType => currentStateType; // 对外提供当前行为状态。
    public Transform CurrentTarget => currentTarget; // 对外提供当前锁定目标。
    public bool IsTargetConfirmed => isTargetConfirmed; // 对外提供当前目标是否已经确认。
    public Transform PlacementTransform => transform; // 向地图集成层提供现有怪物实例的位置入口。
    public GameObject ReinitializationTarget => gameObject; // 向地图重初始化服务提供显式目标。
    internal Vector2 HomePosition => homePosition; // 向状态实现提供本次出生位置。
    internal float PatrolRadius => patrolRadius; // 向巡逻状态提供巡逻半径。
    internal Vector2 PatrolWaitRange => patrolWaitRange; // 向巡逻状态提供停留时间范围。
    internal float SearchConfirmationDuration => searchConfirmationDuration; // 向索敌状态提供确认时间。
    internal float LostTargetDuration => lostTargetDuration; // 向追击状态提供丢失目标后的等待时间。
    internal float ReturnStoppingDistance => returnStoppingDistance; // 向返回状态提供归位停止距离。
    internal Transform VisibleTarget => visibleTarget; // 向行为状态提供感知模块发布的当前可见目标。
    internal bool IsCurrentTargetVisible =>
        currentTarget != null && visibleTarget == currentTarget; // 当前锁定目标是否仍被感知模块看见。

    // 获取依赖并注册当前已经实现的状态。
    private void Awake()
    {
        baseMonster = GetComponent<BaseMonster>();
        movementController = GetComponent<MonsterMovementController>();
        perceptionController = GetComponent<MonsterPerceptionController>();
        attackController = GetComponent<MonsterAttackController>();

        RegisterState(new MonsterPatrolState(this, movementController));
        RegisterState(new MonsterSearchState(this, movementController));
        RegisterState(new MonsterChaseState(this, movementController, attackController));
        RegisterState(new MonsterAttackState(this, movementController, attackController));
        RegisterState(new MonsterReturnState(this, movementController));
    }

    // 启用或从对象池取出时记录出生点并启动巡逻。
    private void OnEnable()
    {
        BindDamageableEvents();
        BindPerceptionEvents();

        ReinitializeForMap();
    }

    // 禁用或回收到对象池时解绑事件并停止 AI。
    private void OnDisable()
    {
        UnbindDamageableEvents();
        UnbindPerceptionEvents();
        StopAI();
    }

    // 每帧更新当前行为状态。
    private void Update()
    {
        if (!isRunning)
            return;

        currentState?.Tick();
    }

    // 在最终地图定位后显式刷新出生点、目标和巡逻状态。
    public void ReinitializeForMap()
    {
        currentState?.Exit();
        currentState = null;
        movementController?.Stop();
        homePosition = transform.position;
        currentTarget = null;
        visibleTarget = perceptionController != null
            ? perceptionController.CurrentVisibleTarget
            : null;
        isTargetConfirmed = false;
        isRunning = true;
        TryChangeState(MonsterStateType.Patrol);
    }

    // 注册一个可供状态机切换的行为状态。
    private void RegisterState(BaseMonsterState state)
    {
        if (state != null)
            states[state.StateType] = state;
    }

    // 尝试切换行为状态，尚未实现的状态不会被进入。
    internal bool TryChangeState(MonsterStateType nextStateType)
    {
        if (!isRunning)
            return false;

        if (currentState != null && currentState.StateType == nextStateType)
            return true;

        if (!states.TryGetValue(nextStateType, out BaseMonsterState nextState))
            return false;

        currentState?.Exit();
        currentState = nextState;
        currentStateType = nextStateType;
        currentState.Enter();
        OnStateChanged?.Invoke(currentStateType);
        return true;
    }

    // 设置一个新的候选目标，并重置之前的确认结果。
    internal void SetCurrentTarget(Transform target)
    {
        currentTarget = target;
        isTargetConfirmed = false;
    }

    // 清除已经失效或离开视线的候选目标。
    internal void ClearCurrentTarget()
    {
        currentTarget = null;
        isTargetConfirmed = false;
    }

    // 判断指定目标是否仍在以出生点为圆心的最大追击范围内。
    internal bool IsTargetInsideHomeChaseRange(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy)
            return false;

        Vector2 offsetFromHome = (Vector2)target.position - homePosition; // 目标相对怪物出生点的偏移量。
        return offsetFromHome.sqrMagnitude <= maxChaseDistanceFromHome * maxChaseDistanceFromHome;
    }

    // 把持续可见的候选目标标记为已确认，并通知后续行为模块。
    internal void ConfirmCurrentTarget()
    {
        if (currentTarget == null || isTargetConfirmed)
            return;

        isTargetConfirmed = true;
        OnTargetConfirmed?.Invoke(currentTarget);
    }

    // 监听通用受伤和被击败事件。
    private void BindDamageableEvents()
    {
        if (baseMonster == null)
            return;

        baseMonster.OnDamaged -= HandleMonsterDamaged;
        baseMonster.OnDefeated -= HandleMonsterDefeated;
        baseMonster.OnDamaged += HandleMonsterDamaged;
        baseMonster.OnDefeated += HandleMonsterDefeated;
    }

    // 取消监听通用受伤和被击败事件。
    private void UnbindDamageableEvents()
    {
        if (baseMonster == null)
            return;

        baseMonster.OnDamaged -= HandleMonsterDamaged;
        baseMonster.OnDefeated -= HandleMonsterDefeated;
    }

    // 监听感知模块发布的可见目标变化，不在 AI 控制器中执行物理检测。
    private void BindPerceptionEvents()
    {
        if (perceptionController == null)
            return;

        perceptionController.OnVisibleTargetChanged -= HandleVisibleTargetChanged;
        perceptionController.OnVisibleTargetChanged += HandleVisibleTargetChanged;
    }

    // 取消监听感知事件，避免对象池复用后重复订阅。
    private void UnbindPerceptionEvents()
    {
        if (perceptionController == null)
            return;

        perceptionController.OnVisibleTargetChanged -= HandleVisibleTargetChanged;
    }

    // 缓存感知模块提供的环境事实，由当前行为状态决定如何响应。
    private void HandleVisibleTargetChanged(Transform target)
    {
        visibleTarget = target;
    }

    // 怪物受到攻击时跳过普通视觉确认，立即确认攻击者并进入追击状态。
    private void HandleMonsterDamaged(BaseDamageable damageable, DamageInfo damageInfo)
    {
        if (baseMonster == null || damageable != baseMonster || baseMonster.IsDefeated)
            return;

        if (damageInfo.SourceTransform == null)
            return;

        SetCurrentTarget(damageInfo.SourceTransform);
        ConfirmCurrentTarget();
        TryChangeState(MonsterStateType.Chase);
    }

    // 怪物被击败时立即停止状态机和移动。
    private void HandleMonsterDefeated(BaseDamageable damageable)
    {
        if (baseMonster == null || damageable != baseMonster)
            return;

        StopAI();
    }

    // 停止当前状态、移动和目标追踪。
    private void StopAI()
    {
        isRunning = false;
        currentState?.Exit();
        currentState = null;
        currentTarget = null;
        visibleTarget = null;
        isTargetConfirmed = false;
        movementController?.Stop();
    }

    // 在 Scene 视图显示出生点、巡逻范围和最大追击范围。
    private void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying
            ? new Vector3(homePosition.x, homePosition.y, transform.position.z)
            : transform.position;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center, patrolRadius);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(center, maxChaseDistanceFromHome);
    }

    // 在 Inspector 修改数值时保证参数合法。
    private void OnValidate()
    {
        patrolRadius = Mathf.Max(0f, patrolRadius);
        patrolWaitRange.x = Mathf.Max(0f, patrolWaitRange.x);
        patrolWaitRange.y = Mathf.Max(patrolWaitRange.x, patrolWaitRange.y);
        searchConfirmationDuration = Mathf.Max(0f, searchConfirmationDuration);
        lostTargetDuration = Mathf.Max(0f, lostTargetDuration);
        maxChaseDistanceFromHome = Mathf.Max(0f, maxChaseDistanceFromHome);
        returnStoppingDistance = Mathf.Max(0f, returnStoppingDistance);
    }
}

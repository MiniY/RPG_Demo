using UnityEngine;

// 控制怪物近战攻击的前摇、命中和冷却，不直接操作动画或玩家属性。
public sealed class MonsterAttackState : BaseMonsterState
{
    // 定义一次攻击循环当前所处的阶段。
    private enum AttackPhase
    {
        Windup, // 攻击已经开始，等待命中时刻。
        Cooldown // 伤害已经结算，等待下一次攻击。
    }

    private readonly MonsterAIController aiController; // 提供当前目标和状态切换能力的 AI 控制器。
    private readonly MonsterMovementController movementController; // 停止移动并锁定攻击朝向的移动控制器。
    private readonly MonsterAttackController attackController; // 提供攻击参数、表现事件和伤害结算的攻击控制器。

    private AttackPhase currentPhase; // 当前攻击阶段。
    private float remainingPhaseTime; // 当前攻击阶段的剩余时间。
    private Vector2 lockedAttackDirection; // 本轮攻击前摇开始时锁定的方向。

    public override MonsterStateType StateType => MonsterStateType.Attack; // 当前状态固定为攻击。

    // 保存攻击状态需要使用的行为模块引用。
    public MonsterAttackState(
        MonsterAIController aiController,
        MonsterMovementController movementController,
        MonsterAttackController attackController)
    {
        this.aiController = aiController;
        this.movementController = movementController;
        this.attackController = attackController;
    }

    // 进入攻击状态时停止追击，并立即开始第一轮攻击前摇。
    public override void Enter()
    {
        movementController.Stop();
        StartAttackCycle();
    }

    // 前摇期间允许玩家躲避，命中后完成冷却，再决定连击或恢复追击。
    public override void Tick()
    {
        Transform leashTarget = aiController.CurrentTarget; // 本次用于检查出生点追击边界的目标。

        if (leashTarget != null && !aiController.IsTargetInsideHomeChaseRange(leashTarget))
        {
            movementController.Stop();
            aiController.ClearCurrentTarget();
            aiController.TryChangeState(MonsterStateType.Return);
            return;
        }

        Transform target = aiController.CurrentTarget; // 当前已经确认的攻击目标。

        if (!IsTargetValid(target))
        {
            aiController.TryChangeState(MonsterStateType.Chase);
            return;
        }

        remainingPhaseTime -= Time.deltaTime;

        if (remainingPhaseTime > 0f)
            return;

        if (currentPhase == AttackPhase.Windup)
        {
            // 命中时刻重新验证距离和锁定方向，玩家及时躲开时本次攻击不会造成伤害。
            attackController.TryDealDamage(target, lockedAttackDirection);
            currentPhase = AttackPhase.Cooldown;
            remainingPhaseTime = attackController.AttackCooldownDuration;
            return;
        }

        if (aiController.IsCurrentTargetVisible && attackController.IsTargetInRange(target))
        {
            StartAttackCycle();
            return;
        }

        aiController.TryChangeState(MonsterStateType.Chase);
    }

    // 离开攻击状态时清空本状态的临时攻击数据。
    public override void Exit()
    {
        movementController.Stop();
        remainingPhaseTime = 0f;
        lockedAttackDirection = Vector2.zero;
    }

    // 锁定本轮攻击方向，并通知动画表现模块播放对应方向的攻击动画。
    private void StartAttackCycle()
    {
        Transform target = aiController.CurrentTarget; // 本轮攻击开始时的目标。
        Vector2 directionToTarget = attackController.GetDirectionToTarget(target); // 本轮攻击开始时指向目标的方向。

        if (directionToTarget.sqrMagnitude <= 0.0001f)
            directionToTarget = movementController.FacingDirection;

        lockedAttackDirection = directionToTarget.normalized;
        movementController.FaceDirection(lockedAttackDirection);
        movementController.Stop();

        currentPhase = AttackPhase.Windup;
        remainingPhaseTime = attackController.AttackWindupDuration;
        attackController.BeginAttack(lockedAttackDirection);
    }

    // 判断目标是否仍可用于继续当前攻击状态。
    private bool IsTargetValid(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy || !aiController.IsTargetConfirmed)
            return false;

        if (currentPhase == AttackPhase.Cooldown)
            return true;

        return aiController.IsCurrentTargetVisible && attackController.IsTargetInRange(target);
    }
}

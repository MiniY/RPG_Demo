using UnityEngine;

// 控制怪物追击已确认目标，并处理丢失视线计时和越界脱战，不负责感知实现或动画播放。
public sealed class MonsterChaseState : BaseMonsterState
{
    private readonly MonsterAIController aiController; // 提供当前目标和追击参数的 AI 控制器。
    private readonly MonsterMovementController movementController; // 执行追击移动和朝向更新的移动控制器。
    private readonly MonsterAttackController attackController; // 判断目标是否已经进入近战攻击范围的攻击控制器。

    private float remainingLostTargetTime; // 当前目标持续不可见后，距离脱战还剩余的时间。

    public override MonsterStateType StateType => MonsterStateType.Chase; // 当前状态固定为追击。

    // 保存追击状态需要使用的行为模块引用。
    public MonsterChaseState(
        MonsterAIController aiController,
        MonsterMovementController movementController,
        MonsterAttackController attackController)
    {
        this.aiController = aiController;
        this.movementController = movementController;
        this.attackController = attackController;
    }

    // 进入追击状态时先清除索敌阶段遗留的移动目标。
    public override void Enter()
    {
        movementController.Stop();
        remainingLostTargetTime = aiController.LostTargetDuration;
    }

    // 每帧读取目标事实，可见时追击，短暂不可见时等待，超时或越界时脱战返回。
    public override void Tick()
    {
        Transform target = aiController.CurrentTarget; // 当前已经锁定的追击目标。

        if (target == null ||
            !target.gameObject.activeInHierarchy ||
            !aiController.IsTargetConfirmed)
        {
            movementController.Stop();
            aiController.ClearCurrentTarget();
            aiController.TryChangeState(MonsterStateType.Return);
            return;
        }

        if (!aiController.IsTargetInsideHomeChaseRange(target))
        {
            movementController.Stop();
            aiController.ClearCurrentTarget();
            aiController.TryChangeState(MonsterStateType.Return);
            return;
        }

        if (!aiController.IsCurrentTargetVisible)
        {
            movementController.Stop();

            remainingLostTargetTime -= Time.deltaTime;

            if (remainingLostTargetTime <= 0f)
            {
                aiController.ClearCurrentTarget();
                aiController.TryChangeState(MonsterStateType.Return);
            }

            return;
        }

        remainingLostTargetTime = aiController.LostTargetDuration;

        Vector2 offsetToTarget =
            target.position - aiController.transform.position; // 怪物指向目标的实时方向和距离。
        movementController.FaceDirection(offsetToTarget);

        if (attackController.IsTargetInRange(target))
        {
            movementController.Stop();
            aiController.TryChangeState(MonsterStateType.Attack);
            return;
        }

        // 每帧更新目标位置，确保追击移动中的玩家而不是最初发现点。
        movementController.SetDestination(target.position);
    }

    // 离开追击状态时停止移动，避免旧目的地影响下一个行为状态。
    public override void Exit()
    {
        movementController.Stop();
        remainingLostTargetTime = 0f;
    }
}

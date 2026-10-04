using UnityEngine;

// 控制怪物脱战后返回出生点，并在返回途中响应重新进入活动范围的可见目标。
public sealed class MonsterReturnState : BaseMonsterState
{
    private readonly MonsterAIController aiController; // 提供出生点、感知事实和状态切换能力的 AI 控制器。
    private readonly MonsterMovementController movementController; // 执行返回出生点移动的控制器。

    public override MonsterStateType StateType => MonsterStateType.Return; // 当前状态固定为脱战返回。

    // 保存返回状态需要使用的行为模块引用。
    public MonsterReturnState(
        MonsterAIController aiController,
        MonsterMovementController movementController)
    {
        this.aiController = aiController;
        this.movementController = movementController;
    }

    // 进入返回状态时清除旧目标，并把出生点设为移动目的地。
    public override void Enter()
    {
        aiController.ClearCurrentTarget();
        movementController.SetDestination(aiController.HomePosition);
    }

    // 返回途中优先响应活动范围内的可见目标，到达出生点后恢复巡逻。
    public override void Tick()
    {
        Transform visibleTarget = aiController.VisibleTarget; // 感知模块最近一次报告的可见目标。

        if (visibleTarget != null && aiController.IsTargetInsideHomeChaseRange(visibleTarget))
        {
            aiController.SetCurrentTarget(visibleTarget);
            aiController.ConfirmCurrentTarget();
            aiController.TryChangeState(MonsterStateType.Chase);
            return;
        }

        Vector2 offsetToHome = aiController.HomePosition - (Vector2)aiController.transform.position; // 怪物到出生点的偏移量。
        float stoppingDistance = aiController.ReturnStoppingDistance; // 可以视为已经归位的停止距离。

        if (offsetToHome.sqrMagnitude <= stoppingDistance * stoppingDistance)
        {
            movementController.Stop();
            aiController.TryChangeState(MonsterStateType.Patrol);
            return;
        }

        movementController.SetDestination(aiController.HomePosition);
    }

    // 离开返回状态时停止旧的返回移动，避免目的地影响下一个状态。
    public override void Exit()
    {
        movementController.Stop();
    }
}

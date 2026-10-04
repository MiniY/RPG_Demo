using UnityEngine;

// 控制怪物在出生点附近选择随机位置并间歇巡逻。
public sealed class MonsterPatrolState : BaseMonsterState
{
    private readonly MonsterAIController aiController; // 提供出生点和巡逻参数的 AI 控制器。
    private readonly MonsterMovementController movementController; // 执行实际移动的控制器。

    private bool isWaiting; // 怪物是否正在巡逻点停留。
    private float remainingWaitTime; // 当前巡逻点剩余停留时间。

    public override MonsterStateType StateType => MonsterStateType.Patrol; // 当前状态固定为巡逻。

    // 保存巡逻状态需要使用的模块引用。
    public MonsterPatrolState(
        MonsterAIController aiController,
        MonsterMovementController movementController)
    {
        this.aiController = aiController;
        this.movementController = movementController;
    }

    // 进入巡逻状态时立即选择第一个巡逻点。
    public override void Enter()
    {
        SelectNextDestination();
    }

    // 收到可见目标事实时进入索敌确认，否则继续巡逻和停留。
    public override void Tick()
    {
        Transform visibleTarget = aiController.VisibleTarget; // 感知模块最近一次报告的可见目标。

        if (visibleTarget != null && aiController.IsTargetInsideHomeChaseRange(visibleTarget))
        {
            aiController.SetCurrentTarget(visibleTarget);
            aiController.TryChangeState(MonsterStateType.Search);
            return;
        }

        if (!movementController.HasReachedDestination)
            return;

        if (!isWaiting)
        {
            BeginWaiting();
            return;
        }

        remainingWaitTime -= Time.deltaTime;

        if (remainingWaitTime <= 0f)
            SelectNextDestination();
    }

    // 离开巡逻状态时停止巡逻移动。
    public override void Exit()
    {
        movementController.Stop();
        isWaiting = false;
        remainingWaitTime = 0f;
    }

    // 开始在当前巡逻点停留。
    private void BeginWaiting()
    {
        movementController.Stop();
        isWaiting = true;
        remainingWaitTime = UnityEngine.Random.Range(
            aiController.PatrolWaitRange.x,
            aiController.PatrolWaitRange.y);
    }

    // 在固定巡逻半径内选择下一个随机目标点。
    private void SelectNextDestination()
    {
        Vector2 offset = UnityEngine.Random.insideUnitCircle * aiController.PatrolRadius;
        Vector2 destination = aiController.HomePosition + offset;

        isWaiting = false;
        remainingWaitTime = 0f;
        movementController.SetDestination(destination);
    }
}

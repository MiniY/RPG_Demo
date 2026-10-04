using UnityEngine;

// 控制怪物发现玩家后的短暂停顿和持续可见确认，不负责追击移动。
public sealed class MonsterSearchState : BaseMonsterState
{
    private readonly MonsterAIController aiController; // 提供当前目标和状态切换能力的 AI 控制器。
    private readonly MonsterMovementController movementController; // 进入索敌时停止怪物普通移动。

    private float remainingConfirmationTime; // 确认目标前还需要保持可见的时间。
    private bool hasConfirmedTarget; // 本次索敌是否已经完成确认。

    public override MonsterStateType StateType => MonsterStateType.Search; // 当前状态固定为索敌确认。

    // 保存索敌状态需要使用的行为模块引用。
    public MonsterSearchState(
        MonsterAIController aiController,
        MonsterMovementController movementController)
    {
        this.aiController = aiController;
        this.movementController = movementController;
    }

    // 进入索敌状态时停止巡逻，并从完整确认时间开始计时。
    public override void Enter()
    {
        movementController.Stop();
        remainingConfirmationTime = aiController.SearchConfirmationDuration;
        hasConfirmedTarget = false;
    }

    // 目标持续可见时累计确认时间，丢失视线时返回巡逻。
    public override void Tick()
    {
        Transform target = aiController.CurrentTarget; // 当前正在确认的候选目标。

        if (target == null ||
            !aiController.IsCurrentTargetVisible ||
            !aiController.IsTargetInsideHomeChaseRange(target))
        {
            aiController.ClearCurrentTarget();
            aiController.TryChangeState(MonsterStateType.Patrol);
            return;
        }

        if (hasConfirmedTarget)
            return;

        remainingConfirmationTime -= Time.deltaTime;

        if (remainingConfirmationTime > 0f)
            return;

        hasConfirmedTarget = true;
        aiController.ConfirmCurrentTarget();

        // 确认完成后把行为控制权交给追击状态。
        aiController.TryChangeState(MonsterStateType.Chase);
    }

    // 离开索敌状态时清空本状态自己的临时计时数据。
    public override void Exit()
    {
        remainingConfirmationTime = 0f;
        hasConfirmedTarget = false;
    }
}

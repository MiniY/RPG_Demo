// 定义怪物 AI 可以处于的五种行为状态。
public enum MonsterStateType
{
    Patrol, // 在出生点附近巡逻。
    Search, // 发现候选目标后进行索敌确认。
    Chase, // 追击已经确认的目标。
    Attack, // 在攻击范围内尝试攻击目标。
    Return // 脱战后返回出生点。
}

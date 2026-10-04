// 统一怪物行为状态的生命周期，由具体状态实现自己的行为。
public abstract class BaseMonsterState
{
    public abstract MonsterStateType StateType { get; } // 当前状态的类型。

    // 进入状态时执行一次，子类可以按需重写。
    public virtual void Enter()
    {
    }

    // 状态生效期间每帧执行，由每个子类提供具体实现。
    public abstract void Tick();

    // 离开状态时执行一次，子类可以按需重写。
    public virtual void Exit()
    {
    }
}

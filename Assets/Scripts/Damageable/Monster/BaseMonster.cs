using UnityEngine;

// 怪物的通用数据入口，生命值、奖励和对象池流程由 BaseDamageable 统一处理。
[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class BaseMonster : BaseDamageable
{
    // 怪物类型在日志中的显示名称。
    protected override string DamageableLabel => "怪物";
}

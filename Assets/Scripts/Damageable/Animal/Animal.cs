using UnityEngine;

// 动物的通用数据入口，具体行为和动画由独立控制器处理。
[DisallowMultipleComponent]
public class Animal : BaseDamageable
{
    // 动物类型在日志中的显示名称。
    protected override string DamageableLabel => "动物";
}

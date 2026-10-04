using UnityEngine;

// 保存一次伤害事件需要传递的通用信息。
public readonly struct DamageInfo
{
    public float Amount { get; } // 本次伤害数值。
    public Transform SourceTransform { get; } // 造成伤害的对象，为空时表示来源未知。
    public Vector3? SourcePosition { get; } // 伤害发生时记录的来源位置。

    // 创建一份伤害信息，并立即记录伤害来源当时的位置。
    public DamageInfo(float amount, Transform sourceTransform)
    {
        Amount = amount;
        SourceTransform = sourceTransform;
        SourcePosition = sourceTransform != null ? sourceTransform.position : null;
    }
}

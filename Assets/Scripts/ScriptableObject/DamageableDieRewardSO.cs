using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
// 单条独立掉落规则。
public class RewardDropRule
{
    [Tooltip("这条规则对应的奖励物品。")]
    public RewardSO reward; // 奖励数据。

    [Range(0f, 1f)]
    [Tooltip("这件奖励独立掉落的概率，1 表示 100%。")]
    public float dropChance = 1f; // 独立掉落概率。

    [Min(1)]
    [Tooltip("一次掉落的最小数量。")]
    public int minAmount = 1; // 最小掉落数量。

    [Min(1)]
    [Tooltip("一次掉落的最大数量。")]
    public int maxAmount = 1; // 最大掉落数量。
}

[CreateAssetMenu(fileName = "DamageableDieReward_", menuName = "RPG/Rewards/Damageable Drop Table")]
// 单个可破坏对象的死亡掉落表。
public class DamageableDieRewardSO : ScriptableObject
{
    [Tooltip("用于和可破坏对象匹配的唯一编号，例如 Sheep、Tree、Mushroom。")]
    public string damageableId; // 可破坏对象编号。

    [Tooltip("方便在 Inspector 中识别的名称。")]
    public string damageableName; // 可破坏对象显示名称。

    [Tooltip("这个对象被击败或被摧毁时要逐条判定的奖励规则。")]
    public List<RewardDropRule> dieReward = new List<RewardDropRule>(); // 奖励规则列表。

    // 在编辑器里自动修正奖励规则的数据。
    private void OnValidate()
    {
        if (dieReward == null)
            dieReward = new List<RewardDropRule>();

        foreach (RewardDropRule rule in dieReward)
        {
            if (rule == null)
                continue;

            rule.dropChance = Mathf.Clamp01(rule.dropChance);
            rule.minAmount = Mathf.Max(1, rule.minAmount);
            rule.maxAmount = Mathf.Max(rule.minAmount, rule.maxAmount);
        }
    }
}

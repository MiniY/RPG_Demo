using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DamageableDieRewardList", menuName = "RPG/Rewards/Damageable Drop List")]
// 所有可破坏对象的死亡掉落表集合。
public class DamageableDieRewardListSO : ScriptableObject
{
    [Tooltip("所有可破坏对象的死亡奖励表。")]
    [SerializeField] private List<DamageableDieRewardSO> damageableDieRewardSO =
        new List<DamageableDieRewardSO>(); // 可破坏对象奖励表列表。

    // 根据对象编号查找对应的掉落表。
    public bool TryGetRewardTable(string damageableId, out DamageableDieRewardSO rewardTable)
    {
        rewardTable = null;

        if (string.IsNullOrWhiteSpace(damageableId))
            return false;

        foreach (DamageableDieRewardSO table in damageableDieRewardSO)
        {
            if (table == null || table.damageableId != damageableId)
                continue;

            rewardTable = table;
            return true;
        }

        return false;
    }

    // 在编辑器里检查是否存在重复编号。
    private void OnValidate()
    {
        if (damageableDieRewardSO == null)
            damageableDieRewardSO = new List<DamageableDieRewardSO>();

        HashSet<string> ids = new HashSet<string>();

        foreach (DamageableDieRewardSO table in damageableDieRewardSO)
        {
            if (table == null || string.IsNullOrWhiteSpace(table.damageableId))
                continue;

            if (!ids.Add(table.damageableId))
                Debug.LogWarning($"奖励表中存在重复的 damageableId：{table.damageableId}", this);
        }
    }
}

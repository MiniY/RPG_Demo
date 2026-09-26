using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 奖励注册表资产，把存档中的 RewardId（奖励唯一 ID）解析为实际 RewardSO（奖励数据资产）。
/// </summary>
[CreateAssetMenu(fileName = "RewardRegistry", menuName = "RPG/Reward Registry")]
public sealed class RewardRegistrySO : ScriptableObject
{
    // 注册表是存档 ID 与 Unity 资产之间的唯一解析入口。
    /// <summary>
    /// 当前游戏中允许被存档和读取的奖励数据资产。
    /// </summary>
    [SerializeField] private List<RewardSO> rewards = new List<RewardSO>(); // 奖励资产列表。

    /// <summary>
    /// 对外提供只读奖励列表。
    /// </summary>
    public IReadOnlyList<RewardSO> Rewards => rewards;

    /// <summary>
    /// 根据稳定 ID 查找奖励数据资产。
    /// </summary>
    /// <param name="rewardId">奖励唯一 ID。</param>
    /// <returns>找到时返回奖励资产，否则返回 null。</returns>
    public RewardSO FindById(string rewardId)
    {
        if (string.IsNullOrWhiteSpace(rewardId) || rewards == null)
            return null;

        for (int i = 0; i < rewards.Count; i++)
        {
            RewardSO reward = rewards[i];

            if (reward != null && reward.RewardId == rewardId)
                return reward;
        }

        return null;
    }

    /// <summary>
    /// 尝试根据稳定 ID 解析奖励数据资产。
    /// </summary>
    /// <param name="rewardId">奖励唯一 ID。</param>
    /// <param name="reward">解析到的奖励资产。</param>
    /// <returns>解析成功返回 true。</returns>
    public bool TryGetReward(string rewardId, out RewardSO reward)
    {
        reward = FindById(rewardId);
        return reward != null;
    }

    /// <summary>
    /// 在 Inspector（检视面板）中检查空引用和重复 RewardId（奖励唯一 ID）。
    /// </summary>
    private void OnValidate()
    {
        if (rewards == null)
            rewards = new List<RewardSO>();

        HashSet<string> rewardIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < rewards.Count; i++)
        {
            RewardSO reward = rewards[i];

            if (reward == null)
                continue;

            reward.EnsureRewardId();

            if (!rewardIds.Add(reward.RewardId))
                Debug.LogWarning($"奖励注册表中存在重复的 RewardId：{reward.RewardId}", this);
        }
    }
}

using System;
using UnityEngine;

/// <summary>
/// 保存一种奖励的静态资料，不保存玩家当前拥有的数量。
/// </summary>
[CreateAssetMenu(fileName = "Reward_", menuName = "RPG/Reward")]
public class RewardSO : ScriptableObject
{
    // 存档使用稳定字符串 ID，避免依赖 Unity 资产对象引用。
    /// <summary>
    /// 奖励数据资产的稳定唯一 ID，存档只保存这个字符串而不是 Unity 对象引用。
    /// </summary>
    [SerializeField, Tooltip("奖励的稳定唯一 ID，创建后不要随意修改。")]
    private string rewardId; // 奖励唯一 ID。

    /// <summary>
    /// 奖励在界面中显示的名称。
    /// </summary>
    [Tooltip("奖励在游戏中显示的名称。")]
    public string rewardName; // 奖励名称。

    /// <summary>
    /// 背包格子和悬浮提示中显示的奖励图标。
    /// </summary>
    [Tooltip("背包格子和悬浮提示中显示的奖励图标。")]
    public Sprite itemIcon; // 奖励图标。

    /// <summary>
    /// 奖励的文字说明，用于悬浮提示等界面。
    /// </summary>
    [TextArea(2, 5)]
    [Tooltip("奖励的文字说明，用于悬浮提示等界面。")]
    public string description; // 奖励说明。

    /// <summary>
    /// 拾取数量倍率，适合金币袋这类“1 个掉落物代表多个资源”的奖励。
    /// </summary>
    [SerializeField, Min(1)] private int pickupAmountMultiplier = 1; // 拾取数量倍率。

    /// <summary>
    /// 奖励掉落到地面时使用的预制体。
    /// </summary>
    [Tooltip("奖励掉落到地面时使用的预制体。")]
    public GameObject rewardPrefab; // 奖励地面预制体。

    /// <summary>
    /// 对外提供奖励稳定唯一 ID，只允许读取。
    /// </summary>
    public string RewardId => rewardId;

    /// <summary>
    /// 确保奖励数据资产拥有稳定 ID。
    /// </summary>
    internal void EnsureRewardId()
    {
        if (string.IsNullOrWhiteSpace(rewardId))
            rewardId = Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// 根据奖励配置换算最终加入背包的数量。
    /// </summary>
    /// <param name="sourceAmount">掉落或预制体传入的原始数量。</param>
    /// <returns>最终加入玩家背包的数量。</returns>
    public int GetPickupAmount(int sourceAmount)
    {
        long safeSourceAmount = Math.Max(1L, (long)sourceAmount); // 安全的原始数量。
        long safeMultiplier = Math.Max(1L, (long)pickupAmountMultiplier); // 安全的拾取倍率。
        long multipliedAmount = safeSourceAmount * safeMultiplier; // 使用 long 避免 int 溢出。

        return (int)Math.Min((long)GameSaveService.MaxQuantity, multipliedAmount);
    }

    /// <summary>
    /// 在 Inspector 修改数值时保证拾取数量倍率合法。
    /// </summary>
    private void OnValidate()
    {
        EnsureRewardId();
        pickupAmountMultiplier = Mathf.Max(1, pickupAmountMultiplier);
    }
}

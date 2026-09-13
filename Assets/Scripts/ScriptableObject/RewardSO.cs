using UnityEngine;

/// <summary>
/// 保存一种奖励的静态资料，不保存玩家当前拥有的数量。
/// </summary>
[CreateAssetMenu(fileName = "Reward_", menuName = "RPG/Reward")]
public class RewardSO : ScriptableObject
{
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
    /// 奖励掉落到地面时使用的预制体。
    /// </summary>
    [Tooltip("奖励掉落到地面时使用的预制体。")]
    public GameObject rewardPrefab; // 奖励地面预制体。
}

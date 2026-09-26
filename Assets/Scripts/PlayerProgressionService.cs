using UnityEngine;

/// <summary>
/// 处理金币与玩家属性之间的原子交易。
/// </summary>
[DefaultExecutionOrder(-80)]
public class PlayerProgressionService : MonoBehaviour
{
    /// <summary>
    /// 每次增加或减少一个属性点所需要或返还的金币数量。
    /// </summary>
    [SerializeField, Min(1)] private int goldCostPerPoint = 10;

    /// <summary>
    /// 玩家背包系统。
    /// </summary>
    private PlayerInventory playerInventory;

    /// <summary>
    /// 玩家属性运行时数据。
    /// </summary>
    private PlayerStatsRuntime playerStats;

    /// <summary>
    /// 初始化成长服务使用的属性数据。
    /// </summary>
    /// <param name="statsRuntime">玩家属性运行时数据。</param>
    public void Initialize(PlayerStatsRuntime statsRuntime)
    {
        playerStats = statsRuntime;
        ResolveDependencies();
    }

    /// <summary>
    /// 重新查找背包系统，适应玩家对象重新生成或切换场景。
    /// </summary>
    public void ResolveDependencies()
    {
        if (playerStats == null && GameSession.Instance != null)
            playerStats = GameSession.Instance.PlayerStats;

        if (playerInventory != null && playerInventory.gameObject.scene.IsValid())
            return;

#pragma warning disable CS0618
        playerInventory = FindObjectOfType<PlayerInventory>();
#pragma warning restore CS0618
    }

    /// <summary>
    /// 尝试增加一个最大属性点，并扣除十枚金币。
    /// </summary>
    /// <param name="statType">要增加的属性类型。</param>
    /// <param name="failureMessage">失败时返回给 UI（用户界面）的提示。</param>
    /// <returns>交易成功返回 true。</returns>
    public bool TryIncrease(PlayerStatType statType, out string failureMessage)
    {
        return TryModifyMaximum(statType, 1, out failureMessage);
    }

    /// <summary>
    /// 尝试减少一个最大属性点，并返还十枚金币。
    /// </summary>
    /// <param name="statType">要减少的属性类型。</param>
    /// <param name="failureMessage">失败时返回给 UI（用户界面）的提示。</param>
    /// <returns>交易成功返回 true。</returns>
    public bool TryDecrease(PlayerStatType statType, out string failureMessage)
    {
        return TryModifyMaximum(statType, -1, out failureMessage);
    }

    /// <summary>
    /// 获取每个属性点对应的金币数量。
    /// </summary>
    public int GoldCostPerPoint => goldCostPerPoint;

    /// <summary>
    /// 执行一次属性和金币的原子交易。
    /// </summary>
    /// <param name="statType">要修改的属性类型。</param>
    /// <param name="direction">增加传入 1，减少传入 -1。</param>
    /// <param name="failureMessage">失败原因。</param>
    /// <returns>交易成功返回 true。</returns>
    private bool TryModifyMaximum(
        PlayerStatType statType,
        int direction,
        out string failureMessage)
    {
        failureMessage = string.Empty;
        ResolveDependencies();

        if (playerStats == null || playerInventory == null)
        {
            failureMessage = "玩家成长数据尚未初始化。";
            return false;
        }

        playerStats.EnsureInitializedForRuntime();
        playerInventory.EnsureInitializedForRuntime();
        RewardSO goldReward = playerInventory.GoldReward;

        if (goldReward == null)
        {
            failureMessage = "金币奖励数据尚未绑定。";
            return false;
        }

        int currentMaximum = playerStats.GetMaximum(statType);

        if (direction > 0)
        {
            if (currentMaximum >= PlayerStatsRuntime.MaximumMaximumValue)
            {
                failureMessage = GetStatLabel(statType) + "已经达到最大值 999。";
                return false;
            }

            if (playerInventory.GetItemAmountLong(goldReward) < goldCostPerPoint)
            {
                failureMessage = "金币不足，需要 " + goldCostPerPoint + " 金币。";
                return false;
            }
        }
        else
        {
            if (currentMaximum <= PlayerStatsRuntime.MinimumMaximumValue)
            {
                failureMessage = GetStatLabel(statType) + "不能低于 10。";
                return false;
            }
        }

        PlayerInventorySnapshot inventorySnapshot = playerInventory.CaptureSnapshot();
        PlayerStatsSnapshot statsSnapshot = playerStats.CaptureSnapshot();

        bool statsChanged = direction > 0
            ? playerStats.TryIncreaseMaximum(statType, false)
            : playerStats.TryDecreaseMaximum(statType, false);

        if (!statsChanged)
        {
            failureMessage = "属性修改失败。";
            return false;
        }

        bool goldChanged = direction > 0
            ? playerInventory.TryRemoveItemWithoutPersistence(goldReward, goldCostPerPoint)
            : playerInventory.TryAddItemWithoutPersistence(goldReward, goldCostPerPoint);

        if (!goldChanged)
        {
            playerStats.RestoreSnapshot(statsSnapshot, false);
            failureMessage = direction > 0 ? "金币不足。" : "金币数量已经达到存档上限。";
            return false;
        }

        GameSaveData candidate = GameSaveService.Clone(GameSaveService.GetOrCreateData());
        playerInventory.WriteToSaveData(candidate);
        playerStats.WriteToSaveData(candidate);

        if (!GameSaveService.TrySave(candidate))
        {
            playerInventory.RestoreSnapshot(inventorySnapshot, false);
            playerStats.RestoreSnapshot(statsSnapshot, false);
            failureMessage = "保存失败，属性和金币没有改变。";
            return false;
        }

        playerInventory.NotifyInventoryChanged();
        playerStats.SendStatsChanged();
        return true;
    }

    /// <summary>
    /// 获取属性的中文显示名称。
    /// </summary>
    /// <param name="statType">属性类型。</param>
    /// <returns>属性中文名称。</returns>
    private static string GetStatLabel(PlayerStatType statType)
    {
        switch (statType)
        {
            case PlayerStatType.Health:
                return "生命值";
            case PlayerStatType.Mana:
                return "魔法值";
            default:
                return "体力值";
        }
    }

    /// <summary>
    /// 在 Inspector（检视面板）中保证金币消耗合法。
    /// </summary>
    private void OnValidate()
    {
        goldCostPerPoint = Mathf.Max(1, goldCostPerPoint);
    }
}

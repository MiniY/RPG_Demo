using TMPro;
using UnityEngine;

/// <summary>
/// 控制 HUD 上的金币数量显示，并预留货币红点能力。
/// </summary>
public class CurrencyHudUI : BaseRedDot
{
    /// <summary>
    /// 用来显示金币数量的文本组件。
    /// </summary>
    [SerializeField] private TMP_Text goldAmountText; // 金币数量文本。

    /// <summary>
    /// 玩家背包系统，用来读取金币数量。
    /// </summary>
    [SerializeField] private PlayerInventory playerInventory; // 玩家背包系统。

    /// <summary>
    /// 金币奖励数据，用来查询玩家拥有的金币数量。
    /// </summary>
    [SerializeField] private RewardSO goldReward; // 金币奖励数据。

    /// <summary>
    /// 初始化并自动补全常用引用。
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        ResolveReferences();
    }

    /// <summary>
    /// 启用时监听背包数据变化并刷新金币数量。
    /// </summary>
    private void OnEnable()
    {
        ResolveReferences();

        if (playerInventory != null)
            playerInventory.OnInventoryChanged += RefreshGoldAmount;

        RefreshGoldAmount();
    }

    /// <summary>
    /// 禁用时取消监听背包数据变化。
    /// </summary>
    private void OnDisable()
    {
        if (playerInventory != null)
            playerInventory.OnInventoryChanged -= RefreshGoldAmount;
    }

    /// <summary>
    /// 自动查找没有手动绑定的引用。
    /// </summary>
    private void ResolveReferences()
    {
        if (goldAmountText == null)
            goldAmountText = GetComponentInChildren<TMP_Text>();

        if (playerInventory == null || !playerInventory.gameObject.scene.IsValid())
            playerInventory = FindObjectOfType<PlayerInventory>();

        if (playerInventory != null)
        {
            playerInventory.EnsureInitializedForRuntime();

            goldReward = playerInventory.GoldReward;
        }
    }

    /// <summary>
    /// 刷新 HUD 上显示的金币数量。
    /// </summary>
    private void RefreshGoldAmount()
    {
        int goldAmount = GetGoldAmount(); // 当前金币数量。

        if (goldAmountText != null)
            goldAmountText.text = goldAmount.ToString();

        SetRedDotVisible(false);
    }

    /// <summary>
    /// 获取玩家当前拥有的金币数量。
    /// </summary>
    /// <returns>玩家当前金币数量。</returns>
    private int GetGoldAmount()
    {
        if (playerInventory == null || goldReward == null)
            return 0;

        return playerInventory.GetItemAmount(goldReward);
    }
}

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 控制 HUD 上的背包按钮点击和背包红点提示。
/// </summary>
public class PackageHudButtonUI : BaseRedDot
{
    /// <summary>
    /// HUD 上的背包按钮。
    /// </summary>
    [SerializeField] private Button packageButton; // 背包按钮。

    /// <summary>
    /// 背包面板控制器，用来打开或关闭背包面板。
    /// </summary>
    [SerializeField] private PackagePanelController packagePanelController; // 背包面板控制器。

    /// <summary>
    /// 玩家背包系统，用来读取金币数量。
    /// </summary>
    [SerializeField] private PlayerInventory playerInventory; // 玩家背包系统。

    /// <summary>
    /// 金币奖励数据，用来判断玩家拥有的金币数量。
    /// </summary>
    [SerializeField] private RewardSO goldReward; // 金币奖励数据。

    /// <summary>
    /// 显示背包红点所需的金币数量。
    /// </summary>
    [SerializeField, Min(1)] private int requiredGoldForRedDot = 10; // 红点金币需求。

    /// <summary>
    /// 当前红点提示是否已经被玩家查看过。
    /// </summary>
    private bool redDotDismissed; // 红点是否已读。

    /// <summary>
    /// 背包打开时红点是否正在显示。
    /// </summary>
    private bool redDotWasVisibleWhenPanelOpened; // 打开背包时红点是否可见。

    /// <summary>
    /// 上一次记录的金币数量，用来判断是否获得了新的金币。
    /// </summary>
    private int lastGoldAmount = -1; // 上一次金币数量。

    /// <summary>
    /// 初始化并自动补全常用引用。
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        ResolveReferences();
    }

    /// <summary>
    /// 启用时监听按钮点击、背包数量变化和背包面板开关。
    /// </summary>
    private void OnEnable()
    {
        ResolveReferences();
        BindEvents();
        RefreshRedDot();
    }

    /// <summary>
    /// 禁用时取消监听，避免重复绑定事件。
    /// </summary>
    private void OnDisable()
    {
        UnbindEvents();
    }

    /// <summary>
    /// 自动查找没有手动绑定的引用。
    /// </summary>
    private void ResolveReferences()
    {
        if (packageButton == null)
            packageButton = GetComponent<Button>();

        if (packagePanelController == null)
            packagePanelController = FindObjectOfType<PackagePanelController>();

        if (playerInventory == null)
            playerInventory = FindObjectOfType<PlayerInventory>();
    }

    /// <summary>
    /// 绑定按钮和数据事件。
    /// </summary>
    private void BindEvents()
    {
        if (packageButton != null)
            packageButton.onClick.AddListener(HandlePackageButtonClicked);

        if (playerInventory != null)
            playerInventory.OnInventoryChanged += HandleInventoryChanged;

        if (packagePanelController != null)
            packagePanelController.OnPackagePanelVisibilityChanged += HandlePackagePanelVisibilityChanged;
    }

    /// <summary>
    /// 解绑按钮和数据事件。
    /// </summary>
    private void UnbindEvents()
    {
        if (packageButton != null)
            packageButton.onClick.RemoveListener(HandlePackageButtonClicked);

        if (playerInventory != null)
            playerInventory.OnInventoryChanged -= HandleInventoryChanged;

        if (packagePanelController != null)
            packagePanelController.OnPackagePanelVisibilityChanged -= HandlePackagePanelVisibilityChanged;
    }

    /// <summary>
    /// 点击 HUD 背包按钮时，执行和 Tab 相同的背包开关逻辑。
    /// </summary>
    private void HandlePackageButtonClicked()
    {
        if (packagePanelController != null)
            packagePanelController.TogglePackagePanel();
    }

    /// <summary>
    /// 背包数据变化时刷新红点显示。
    /// </summary>
    private void HandleInventoryChanged()
    {
        RefreshRedDot();
    }

    /// <summary>
    /// 背包面板打开或关闭时更新红点已读状态。
    /// </summary>
    /// <param name="isVisible">背包面板是否正在显示。</param>
    private void HandlePackagePanelVisibilityChanged(bool isVisible)
    {
        if (isVisible)
        {
            redDotWasVisibleWhenPanelOpened = IsRedDotVisible;
            return;
        }

        if (!redDotWasVisibleWhenPanelOpened)
            return;

        redDotDismissed = true;
        redDotWasVisibleWhenPanelOpened = false;
        SetRedDotVisible(false);
    }

    /// <summary>
    /// 根据金币数量和红点已读状态刷新背包红点。
    /// </summary>
    private void RefreshRedDot()
    {
        int goldAmount = GetGoldAmount(); // 当前金币数量。

        if (goldAmount < requiredGoldForRedDot)
        {
            redDotDismissed = false;
        }
        else if (lastGoldAmount >= 0 && goldAmount > lastGoldAmount)
        {
            redDotDismissed = false;
        }

        lastGoldAmount = goldAmount;
        SetRedDotVisible(goldAmount >= requiredGoldForRedDot && !redDotDismissed);
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
using System;
using UnityEngine;

/// <summary>
/// 商店面板控制器，负责打开、关闭商店 UI，并在商店打开时锁定玩家玩法输入。
/// </summary>
public class ShopPanelController : MonoBehaviour
{
    private const string GameplayGateOwner = "ui.shop";
    /// <summary>
    /// 商店面板显示状态发生变化时发出的事件。
    /// </summary>
    public event Action<bool> OnShopPanelVisibilityChanged;

    /// <summary>
    /// 商店面板根物体，打开或关闭时会控制这个对象的显示状态。
    /// </summary>
    [SerializeField] private GameObject shopPanelRoot; // 商店面板根物体。

    /// <summary>
    /// 游戏开始时商店面板是否默认打开。
    /// </summary>
    [SerializeField] private bool openAtStart = false; // 是否开始时打开。

    /// <summary>
    /// 商店商品网格，用于切换到被交互商人的目录。
    /// </summary>
    [SerializeField] private ShopGridUI shopGridUI; // 商品网格界面。

    /// <summary>
    /// 当前已经绑定的游戏输入管理器。
    /// </summary>
    private GameInput gameInput; // 游戏输入管理器。
    private TowerRunGateLease gameplayGateLease;

    /// <summary>
    /// 当前商店面板是否正在显示。
    /// </summary>
    public bool IsShopPanelVisible => IsShopPanelOpen();

    /// <summary>
    /// 初始化商店面板显示状态。
    /// </summary>
    private void Awake()
    {
        if (shopPanelRoot == null)
            shopPanelRoot = gameObject;

        if (shopGridUI == null)
            shopGridUI = GetComponent<ShopGridUI>();

        SetShopPanelVisible(openAtStart);
    }

    /// <summary>
    /// 启用时尝试绑定输入管理器。
    /// </summary>
    private void OnEnable()
    {
        MerchantInteractionController.OnAnyMerchantInteracted += HandleMerchantInteracted;
        TryBindInput();
    }

    /// <summary>
    /// 每帧尝试补绑定输入管理器。
    /// </summary>
    private void Update()
    {
        TryBindInput();
    }

    /// <summary>
    /// 禁用时解绑输入管理器。
    /// </summary>
    private void OnDisable()
    {
        MerchantInteractionController.OnAnyMerchantInteracted -= HandleMerchantInteracted;
        UnbindInput();
        ReleaseGameplayGate();
    }

    /// <summary>
    /// 响应商人交互事件并打开商店面板。
    /// </summary>
    /// <param name="merchant">本次被交互的商人。</param>
    private void HandleMerchantInteracted(MerchantInteractionController merchant)
    {
        if (merchant == null || IsShopPanelOpen() ||
            TowerRunGateHub.IsGameplayBlockedByOtherThan(GameplayGateOwner))
            return;

        if (merchant.ShopCatalog != null && shopGridUI != null)
            shopGridUI.SetShopCatalog(merchant.ShopCatalog);

        OpenShopPanel();
    }

    /// <summary>
    /// 打开商店面板。
    /// </summary>
    public void OpenShopPanel()
    {
        SetShopPanelVisible(true);
    }

    /// <summary>
    /// 关闭商店面板。
    /// </summary>
    public void CloseShopPanel()
    {
        SetShopPanelVisible(false);
    }

    /// <summary>
    /// 切换商店面板显示状态。
    /// </summary>
    public void ToggleShopPanel()
    {
        SetShopPanelVisible(!IsShopPanelOpen());
    }

    /// <summary>
    /// 设置商店面板显示状态，并同步玩家玩法输入锁定。
    /// </summary>
    /// <param name="isVisible">是否显示商店面板。</param>
    private void SetShopPanelVisible(bool isVisible)
    {
        bool wasVisible = IsShopPanelOpen(); // 修改前的显示状态。

        if (shopPanelRoot != null)
            shopPanelRoot.SetActive(isVisible);

        if (isVisible)
            AcquireGameplayGate();
        else
            ReleaseGameplayGate();

        if (wasVisible != isVisible)
            OnShopPanelVisibilityChanged?.Invoke(isVisible);
    }

    /// <summary>
    /// 判断商店面板当前是否打开。
    /// </summary>
    /// <returns>商店面板打开时返回 true，否则返回 false。</returns>
    private bool IsShopPanelOpen()
    {
        return shopPanelRoot != null && shopPanelRoot.activeSelf;
    }

    /// <summary>
    /// 尝试绑定 GameInput（游戏输入）。
    /// </summary>
    private void TryBindInput()
    {
        if (gameInput != null || GameInput.Instance == null)
            return;

        gameInput = GameInput.Instance;

        if (IsShopPanelOpen())
            AcquireGameplayGate();
    }

    /// <summary>
    /// 解绑 GameInput（游戏输入）。
    /// </summary>
    private void UnbindInput()
    {
        if (gameInput == null)
            return;

        gameInput = null;
    }

    private void AcquireGameplayGate()
    {
        if (gameplayGateLease == null)
            gameplayGateLease = TowerRunGateHub.AcquireGameplay(GameplayGateOwner);
    }

    private void ReleaseGameplayGate()
    {
        gameplayGateLease?.Dispose();
        gameplayGateLease = null;
    }
}

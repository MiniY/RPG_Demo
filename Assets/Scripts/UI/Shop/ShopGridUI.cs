using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 商店网格 UI，负责按商品分类生成商品格子，并协调购买结果、警告和购买飞行动画。
/// </summary>
public class ShopGridUI : MonoBehaviour
{
    /// <summary>
    /// 当前商店使用的商品目录数据。
    /// </summary>
    [SerializeField] private ShopCatalogSO shopCatalog; // 商店目录数据。

    /// <summary>
    /// 商店交易控制器，用来执行金币购买和库存减少。
    /// </summary>
    [SerializeField] private ShopTradeController tradeController; // 商店交易控制器。

    /// <summary>
    /// 当前显示的商品分类。
    /// </summary>
    [SerializeField] private ShopProductCategoryType currentCategory = ShopProductCategoryType.Food; // 当前商品分类。

    /// <summary>
    /// UI 启用时是否自动显示默认分类。
    /// </summary>
    [SerializeField] private bool refreshOnEnable = true; // 启用时是否刷新。

    /// <summary>
    /// 显示商店名称的文本。
    /// </summary>
    [SerializeField] private TMP_Text shopNameText; // 商店名称文本。

    /// <summary>
    /// 显示当前分类名称的文本。
    /// </summary>
    [SerializeField] private TMP_Text categoryNameText; // 分类名称文本。

    /// <summary>
    /// 承载警告背景和警告文本的完整 GameObject（游戏物体）。
    /// </summary>
    [SerializeField] private GameObject warningRoot; // 警告根对象。

    /// <summary>
    /// 显示购买失败或售罄提示内容的 TMP_Text（TextMeshPro 文本）。
    /// </summary>
    [SerializeField] private TMP_Text warningText; // 警告文本。

    /// <summary>
    /// 每条警告保持显示的秒数，使用不受 Time.timeScale（时间缩放）影响的真实时间。
    /// </summary>
    [SerializeField, Min(0.1f)] private float warningDisplayDuration = 3f; // 警告显示时长。

    /// <summary>
    /// 用来生成商品格子的预制体。
    /// </summary>
    [SerializeField] private ShopSlotUI slotPrefab; // 商品格子预制体。

    /// <summary>
    /// 承载商品格子的父物体，通常绑定 ShopScrollView/ShopViewport/ShopContent。
    /// </summary>
    [SerializeField] private Transform contentRoot; // 商品内容根节点。

    /// <summary>
    /// 购买成功后播放的商品飞入背包动画。
    /// </summary>
    [SerializeField] private ShopPurchaseFlyEffect purchaseFlyEffect; // 购买飞行动画。

    /// <summary>
    /// 当前网格所属的 ShopPanelController（商店面板控制器），用于监听商店关闭事件。
    /// </summary>
    [SerializeField] private ShopPanelController shopPanelController; // 商店面板控制器。

    /// <summary>
    /// 当前已经生成的商品格子列表。
    /// </summary>
    private readonly List<ShopSlotUI> generatedSlots = new List<ShopSlotUI>(); // 已生成商品格子。

    /// <summary>
    /// 当前是否已经订阅交易控制器状态变化事件。
    /// </summary>
    private bool hasSubscribedTradeController; // 是否已订阅交易控制器。

    /// <summary>
    /// 当前是否已经订阅商店面板显示状态变化事件。
    /// </summary>
    private bool hasSubscribedShopPanelController; // 是否已订阅商店面板控制器。

    /// <summary>
    /// 当前负责延迟隐藏警告的 Coroutine（协程）。
    /// </summary>
    private Coroutine hideWarningCoroutine; // 警告隐藏协程。

    /// <summary>
    /// 脚本加载时补全缺失引用。
    /// </summary>
    private void Awake()
    {
        if (contentRoot == null)
            contentRoot = transform;

        ResolveWarningReferences();
        HideWarningImmediate();
        ResolveTradeController();
        ResolveShopPanelController();
        ApplyCatalogToTradeController();
    }

    /// <summary>
    /// UI 启用时订阅事件并按当前分类刷新商品列表。
    /// </summary>
    private void OnEnable()
    {
        ResolveTradeController();
        ResolveShopPanelController();
        ApplyCatalogToTradeController();
        SubscribeTradeController();
        SubscribeShopPanelController();

        if (refreshOnEnable)
            Refresh();
    }

    /// <summary>
    /// UI 禁用时解绑事件并清理生成的商品格子。
    /// </summary>
    private void OnDisable()
    {
        UnsubscribeShopPanelController();
        UnsubscribeTradeController();
        ResetGridInteractionState();
        ClearSlots();
    }

    /// <summary>
    /// 设置当前商店目录，并刷新商品列表。
    /// </summary>
    /// <param name="catalog">新的商店目录数据。</param>
    public void SetShopCatalog(ShopCatalogSO catalog)
    {
        shopCatalog = catalog;
        ApplyCatalogToTradeController();
        Refresh();
    }

    /// <summary>
    /// 设置当前商品分类，并刷新商品列表。
    /// </summary>
    /// <param name="category">要显示的商品分类。</param>
    public void SetCategory(ShopProductCategoryType category)
    {
        currentCategory = category;
        Refresh();
    }

    /// <summary>
    /// 显示 Food（食物）分类，供 Button（按钮）事件绑定。
    /// </summary>
    public void ShowFoodCategory()
    {
        SetCategory(ShopProductCategoryType.Food);
    }

    /// <summary>
    /// 显示 Weapon（武器）分类，供 Button（按钮）事件绑定。
    /// </summary>
    public void ShowWeaponCategory()
    {
        SetCategory(ShopProductCategoryType.Weapon);
    }

    /// <summary>
    /// 显示 Skill（技能）分类，供 Button（按钮）事件绑定。
    /// </summary>
    public void ShowSkillCategory()
    {
        SetCategory(ShopProductCategoryType.Skill);
    }

    /// <summary>
    /// 显示 Material（材料）分类，供 Button（按钮）事件绑定。
    /// </summary>
    public void ShowMaterialCategory()
    {
        SetCategory(ShopProductCategoryType.Material);
    }

    /// <summary>
    /// 显示 Other（其他）分类，供 Button（按钮）事件绑定。
    /// </summary>
    public void ShowOtherCategory()
    {
        SetCategory(ShopProductCategoryType.Other);
    }

    /// <summary>
    /// 处理 FoodToggle（食物切换按钮）的值变化，供 Toggle（切换控件）事件绑定。
    /// </summary>
    /// <param name="isOn">当前 Toggle（切换控件）是否被选中。</param>
    public void HandleFoodCategoryToggleChanged(bool isOn)
    {
        if (isOn)
            ShowFoodCategory();
    }

    /// <summary>
    /// 处理 WeaponToggle（武器切换按钮）的值变化，供 Toggle（切换控件）事件绑定。
    /// </summary>
    /// <param name="isOn">当前 Toggle（切换控件）是否被选中。</param>
    public void HandleWeaponCategoryToggleChanged(bool isOn)
    {
        if (isOn)
            ShowWeaponCategory();
    }

    /// <summary>
    /// 处理 SkillToggle（技能切换按钮）的值变化，供 Toggle（切换控件）事件绑定。
    /// </summary>
    /// <param name="isOn">当前 Toggle（切换控件）是否被选中。</param>
    public void HandleSkillCategoryToggleChanged(bool isOn)
    {
        if (isOn)
            ShowSkillCategory();
    }

    /// <summary>
    /// 处理 MaterialToggle（材料切换按钮）的值变化，供 Toggle（切换控件）事件绑定。
    /// </summary>
    /// <param name="isOn">当前 Toggle（切换控件）是否被选中。</param>
    public void HandleMaterialCategoryToggleChanged(bool isOn)
    {
        if (isOn)
            ShowMaterialCategory();
    }

    /// <summary>
    /// 处理 OtherToggle（其他切换按钮）的值变化，供 Toggle（切换控件）事件绑定。
    /// </summary>
    /// <param name="isOn">当前 Toggle（切换控件）是否被选中。</param>
    public void HandleOtherCategoryToggleChanged(bool isOn)
    {
        if (isOn)
            ShowOtherCategory();
    }

    /// <summary>
    /// 保留旧按钮绑定兼容：把原 CurrencyTradeButton（货币交易按钮）临时视为 Food（食物）分类。
    /// </summary>
    /// <param name="isOn">当前 Toggle（切换控件）是否被选中。</param>
    public void HandleCurrencyTradeToggleChanged(bool isOn)
    {
        HandleFoodCategoryToggleChanged(isOn);
    }

    /// <summary>
    /// 保留旧按钮绑定兼容：把原 BarterTradeButton（以物易物按钮）临时视为 Weapon（武器）分类。
    /// </summary>
    /// <param name="isOn">当前 Toggle（切换控件）是否被选中。</param>
    public void HandleBarterTradeToggleChanged(bool isOn)
    {
        HandleWeaponCategoryToggleChanged(isOn);
    }

    /// <summary>
    /// 根据当前分类刷新商品列表。
    /// </summary>
    public void Refresh()
    {
        ClearSlots();
        RefreshHeaderTexts();
        ShowWarning(string.Empty);

        if (shopCatalog == null || slotPrefab == null || contentRoot == null)
        {
            ShowWarning("商店 UI 配置不完整");
            return;
        }

        IReadOnlyList<ShopCatalogProduct> products = shopCatalog.GetProducts(currentCategory); // 当前分类商品列表。

        for (int i = 0; i < products.Count; i++)
        {
            ShopCatalogProduct product = products[i]; // 当前要生成的商品。

            if (product == null || !product.IsValid)
                continue;

            ShopSlotUI slot = Instantiate(slotPrefab, contentRoot); // 新生成的商品格子。
            slot.SetData(product, tradeController);
            slot.OnPurchaseRequested += HandlePurchaseRequested;
            generatedSlots.Add(slot);
        }

        if (generatedSlots.Count == 0)
            ShowWarning("这个分类暂无商品");
    }

    /// <summary>
    /// 处理商品格子发出的购买请求。
    /// </summary>
    /// <param name="slot">请求购买的商品格子。</param>
    private void HandlePurchaseRequested(ShopSlotUI slot)
    {
        TryPurchase(slot);
    }

    /// <summary>
    /// 处理交易状态变化，并刷新所有商品库存显示。
    /// </summary>
    private void HandleTradeStateChanged()
    {
        RefreshGeneratedSlotStockStates();
    }

    /// <summary>
    /// 响应商店面板显示状态变化，并在关闭商店时保存库存、重置交互状态。
    /// </summary>
    /// <param name="isVisible">商店面板当前是否可见。</param>
    private void HandleShopPanelVisibilityChanged(bool isVisible)
    {
        if (!isVisible)
        {
            if (tradeController != null)
                tradeController.SaveRuntimeStocks();

            ResetGridInteractionState();
        }
    }

    /// <summary>
    /// 立即收起所有商品格子的描述覆盖层，并清除警告文本。
    /// </summary>
    public void ResetGridInteractionState()
    {
        for (int i = 0; i < generatedSlots.Count; i++)
        {
            ShopSlotUI slot = generatedSlots[i]; // 当前要重置的商品格子。

            if (slot != null)
                slot.ResetInteractionState();
        }

        ShowWarning(string.Empty);
    }

    /// <summary>
    /// 尝试购买指定商品格子的商品。
    /// </summary>
    /// <param name="slot">目标商品格子。</param>
    private void TryPurchase(ShopSlotUI slot)
    {
        if (slot == null || slot.CurrentProduct == null)
            return;

        ResolveTradeController();

        if (tradeController == null)
        {
            ShowWarning("商店缺少交易控制器");
            return;
        }

        ShopPurchaseResult result = tradeController.TryPurchase(slot.CurrentProduct); // 本次购买结果。

        if (!result.succeeded)
        {
            ShowWarning(result.message);
            slot.RefreshStockState();
            return;
        }

        ShowWarning(result.message);
        slot.RefreshStockState();
        PlayPurchaseFlyEffect(slot);
    }

    /// <summary>
    /// 播放商品飞向背包图标的购买反馈动画。
    /// </summary>
    /// <param name="slot">购买成功的商品格子。</param>
    private void PlayPurchaseFlyEffect(ShopSlotUI slot)
    {
        if (purchaseFlyEffect == null || slot == null || slot.CurrentProduct == null)
            return;

        purchaseFlyEffect.Play(slot.CurrentProduct.Icon, slot.RectTransform);
    }

    /// <summary>
    /// 刷新商店名称和分类名称文本。
    /// </summary>
    private void RefreshHeaderTexts()
    {
        if (shopNameText != null)
            shopNameText.text = shopCatalog == null ? "Shop" : shopCatalog.ShopDisplayName;

        if (categoryNameText != null)
            categoryNameText.text = shopCatalog == null ? currentCategory.ToString() : shopCatalog.GetCategoryDisplayName(currentCategory);
    }

    /// <summary>
    /// 显示警告面板，并在指定时长后自动隐藏。
    /// </summary>
    /// <param name="message">要显示的警告内容，空字符串表示隐藏。</param>
    private void ShowWarning(string message)
    {
        ResolveWarningReferences();
        CancelWarningHideCoroutine();

        if (string.IsNullOrWhiteSpace(message))
        {
            HideWarningImmediate();
            return;
        }

        if (warningText == null || warningRoot == null)
            return;

        warningText.text = message;
        warningText.gameObject.SetActive(true);
        warningRoot.SetActive(true);
        hideWarningCoroutine = StartCoroutine(HideWarningAfterDelay());
    }

    /// <summary>
    /// 等待警告显示时长结束，然后隐藏完整警告面板。
    /// </summary>
    /// <returns>供 Unity 驱动的协程枚举器。</returns>
    private IEnumerator HideWarningAfterDelay()
    {
        yield return new WaitForSecondsRealtime(warningDisplayDuration);
        hideWarningCoroutine = null;
        HideWarningImmediate();
    }

    /// <summary>
    /// 立即隐藏完整警告面板并清空旧提示内容。
    /// </summary>
    private void HideWarningImmediate()
    {
        if (warningText != null)
            warningText.text = string.Empty;

        if (warningRoot != null)
            warningRoot.SetActive(false);
    }

    /// <summary>
    /// 取消上一条警告尚未结束的自动隐藏协程。
    /// </summary>
    private void CancelWarningHideCoroutine()
    {
        if (hideWarningCoroutine == null)
            return;

        StopCoroutine(hideWarningCoroutine);
        hideWarningCoroutine = null;
    }

    /// <summary>
    /// 补全警告根对象和警告文本引用，兼容当前 WarningText/Text（警告面板/文本）层级。
    /// </summary>
    private void ResolveWarningReferences()
    {
        if (warningText == null && warningRoot != null)
            warningText = warningRoot.GetComponentInChildren<TMP_Text>(true);

        if (warningRoot != null || warningText == null)
            return;

        Transform textTransform = warningText.transform; // 警告文本的变换组件。
        Transform parentTransform = textTransform.parent; // 警告文本的直接父物体。

        if (textTransform.name == "WarningText")
            warningRoot = textTransform.gameObject;
        else if (parentTransform != null && parentTransform.name == "WarningText")
            warningRoot = parentTransform.gameObject;
        else
            warningRoot = textTransform.gameObject;
    }

    /// <summary>
    /// 刷新所有已生成商品格子的库存状态。
    /// </summary>
    private void RefreshGeneratedSlotStockStates()
    {
        for (int i = 0; i < generatedSlots.Count; i++)
        {
            ShopSlotUI slot = generatedSlots[i]; // 当前商品格子。

            if (slot != null)
                slot.RefreshStockState();
        }
    }

    /// <summary>
    /// 清理已经生成的商品格子。
    /// </summary>
    private void ClearSlots()
    {
        for (int i = generatedSlots.Count - 1; i >= 0; i--)
        {
            ShopSlotUI slot = generatedSlots[i]; // 当前要清理的商品格子。

            if (slot == null)
                continue;

            slot.OnPurchaseRequested -= HandlePurchaseRequested;
            Destroy(slot.gameObject);
        }

        generatedSlots.Clear();
    }

    /// <summary>
    /// 自动寻找商店交易控制器。
    /// </summary>
    private void ResolveTradeController()
    {
        if (tradeController != null)
            return;

#pragma warning disable CS0618
        tradeController = FindObjectOfType<ShopTradeController>();
#pragma warning restore CS0618
    }

    /// <summary>
    /// 自动寻找当前网格使用的 ShopPanelController（商店面板控制器）。
    /// </summary>
    private void ResolveShopPanelController()
    {
        if (shopPanelController != null)
            return;

        shopPanelController = GetComponent<ShopPanelController>();

        if (shopPanelController != null)
            return;

#pragma warning disable CS0618
        shopPanelController = FindObjectOfType<ShopPanelController>();
#pragma warning restore CS0618
    }

    /// <summary>
    /// 把当前商店目录同步给交易控制器。
    /// </summary>
    private void ApplyCatalogToTradeController()
    {
        if (tradeController != null && shopCatalog != null)
            tradeController.SetShopCatalog(shopCatalog);
    }

    /// <summary>
    /// 订阅交易控制器状态变化事件。
    /// </summary>
    private void SubscribeTradeController()
    {
        if (hasSubscribedTradeController || tradeController == null)
            return;

        tradeController.OnTradeStateChanged += HandleTradeStateChanged;
        hasSubscribedTradeController = true;
    }

    /// <summary>
    /// 取消订阅交易控制器状态变化事件。
    /// </summary>
    private void UnsubscribeTradeController()
    {
        if (!hasSubscribedTradeController || tradeController == null)
            return;

        tradeController.OnTradeStateChanged -= HandleTradeStateChanged;
        hasSubscribedTradeController = false;
    }

    /// <summary>
    /// 订阅商店面板显示状态变化事件。
    /// </summary>
    private void SubscribeShopPanelController()
    {
        if (hasSubscribedShopPanelController || shopPanelController == null)
            return;

        shopPanelController.OnShopPanelVisibilityChanged += HandleShopPanelVisibilityChanged;
        hasSubscribedShopPanelController = true;
    }

    /// <summary>
    /// 取消订阅商店面板显示状态变化事件。
    /// </summary>
    private void UnsubscribeShopPanelController()
    {
        if (!hasSubscribedShopPanelController || shopPanelController == null)
            return;

        shopPanelController.OnShopPanelVisibilityChanged -= HandleShopPanelVisibilityChanged;
        hasSubscribedShopPanelController = false;
    }
}

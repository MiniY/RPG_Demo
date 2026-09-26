using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 商店商品格子 UI，负责显示商品信息、库存、悬浮说明并发出购买请求。
/// </summary>
public class ShopSlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    /// <summary>
    /// 当前商品请求购买时发出的事件。
    /// </summary>
    public event Action<ShopSlotUI> OnPurchaseRequested;

    /// <summary>
    /// 显示商品图标的 Image（图片）组件。
    /// </summary>
    [SerializeField] private Image itemIcon; // 商品图标。

    /// <summary>
    /// 显示商品名称的 TMP_Text（TextMeshPro 文本）组件。
    /// </summary>
    [SerializeField] private TMP_Text itemNameText; // 商品名称文本。

    /// <summary>
    /// 显示商品金币单价的 TMP_Text（TextMeshPro 文本）组件。
    /// </summary>
    [SerializeField] private TMP_Text priceText; // 商品单价文本。

    /// <summary>
    /// 覆盖在商品格子上的描述层，用高度动画实现从上往下覆盖。
    /// </summary>
    [SerializeField] private RectTransform descriptionOverlay; // 描述覆盖层。

    /// <summary>
    /// 描述覆盖层的 CanvasGroup（画布组），用于透明度和射线控制。
    /// </summary>
    [SerializeField] private CanvasGroup descriptionCanvasGroup; // 描述覆盖层画布组。

    /// <summary>
    /// 显示商品说明的 TMP_Text（TextMeshPro 文本）组件。
    /// </summary>
    [SerializeField] private TMP_Text descriptionText; // 商品说明文本。

    /// <summary>
    /// 显示当前商人库存数量的 TMP_Text（TextMeshPro 文本）组件。
    /// </summary>
    [FormerlySerializedAs("stockText")]
    [SerializeField] private TMP_Text stockNumberText; // 库存数字文本。

    /// <summary>
    /// 表示当前商品售罄的视觉对象。
    /// </summary>
    [SerializeField] private GameObject soldOutObject; // 售罄状态对象。

    /// <summary>
    /// 覆盖层展开和收起的动画速度。
    /// </summary>
    [SerializeField, Min(0.1f)] private float overlayAnimationSpeed = 12f; // 覆盖层动画速度。

    /// <summary>
    /// 当前格子显示的商店商品。
    /// </summary>
    private ShopCatalogProduct currentProduct; // 当前商品。

    /// <summary>
    /// 当前格子使用的商店交易控制器。
    /// </summary>
    private ShopTradeController tradeController; // 商店交易控制器。

    /// <summary>
    /// 覆盖层当前展开进度，范围 0 到 1。
    /// </summary>
    private float overlayProgress; // 覆盖层当前进度。

    /// <summary>
    /// 覆盖层目标展开进度，范围 0 到 1。
    /// </summary>
    private float targetOverlayProgress; // 覆盖层目标进度。

    /// <summary>
    /// 覆盖层完整高度。
    /// </summary>
    private float overlayFullHeight; // 覆盖层完整高度。

    /// <summary>
    /// 当前格子的 RectTransform（矩形变换）。
    /// </summary>
    public RectTransform RectTransform => transform as RectTransform;

    /// <summary>
    /// 当前格子显示的商品，只允许外部读取。
    /// </summary>
    public ShopCatalogProduct CurrentProduct => currentProduct;

    /// <summary>
    /// 初始化引用和覆盖层状态。
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
        CacheOverlayHeight();
        ResetInteractionState();
    }

    /// <summary>
    /// 禁用时立即重置悬浮说明状态。
    /// </summary>
    private void OnDisable()
    {
        ResetInteractionState();
    }

    /// <summary>
    /// 每帧更新覆盖层展开动画。
    /// </summary>
    private void Update()
    {
        UpdateOverlayAnimation();
    }

    /// <summary>
    /// 在 Inspector 添加脚本时自动绑定常见子物体引用。
    /// </summary>
    private void Reset()
    {
        ResolveReferences();
    }

    /// <summary>
    /// 设置当前商品格子的显示数据。
    /// </summary>
    /// <param name="product">要显示的商店商品。</param>
    /// <param name="controller">商店交易控制器。</param>
    public void SetData(ShopCatalogProduct product, ShopTradeController controller)
    {
        currentProduct = product;
        tradeController = controller;
        ResetInteractionState();
        RefreshVisuals();
    }

    /// <summary>
    /// 刷新商品库存状态。
    /// </summary>
    public void RefreshStockState()
    {
        bool isSoldOut = IsSoldOut(); // 当前是否售罄。

        if (stockNumberText != null)
            stockNumberText.text = GetStockNumberText();

        if (soldOutObject != null)
            soldOutObject.SetActive(isSoldOut);
    }

    /// <summary>
    /// 立即收起格子的描述覆盖层，不播放收起动画。
    /// </summary>
    public void ResetInteractionState()
    {
        SetOverlayImmediate(false);
    }

    /// <summary>
    /// 请求购买当前商品。
    /// </summary>
    public void RequestPurchase()
    {
        if (currentProduct == null || !currentProduct.IsValid)
            return;

        OnPurchaseRequested?.Invoke(this);
    }

    /// <summary>
    /// 鼠标点击商品格子时直接请求购买。
    /// </summary>
    /// <param name="eventData">鼠标点击事件数据。</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (currentProduct == null || !currentProduct.IsValid)
            return;

        RequestPurchase();
    }

    /// <summary>
    /// 鼠标进入商品格子时展开描述覆盖层。
    /// </summary>
    /// <param name="eventData">鼠标悬浮事件数据。</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentProduct == null || !currentProduct.IsValid)
            return;

        SetOverlayTarget(true);
    }

    /// <summary>
    /// 鼠标离开商品格子时收起描述覆盖层。
    /// </summary>
    /// <param name="eventData">鼠标离开事件数据。</param>
    public void OnPointerExit(PointerEventData eventData)
    {
        SetOverlayTarget(false);
    }

    /// <summary>
    /// 根据当前商品刷新图标、名称、单价、说明和库存。
    /// </summary>
    private void RefreshVisuals()
    {
        if (itemIcon != null)
        {
            itemIcon.sprite = currentProduct == null ? null : currentProduct.Icon;
            itemIcon.enabled = itemIcon.sprite != null;
        }

        if (itemNameText != null)
            itemNameText.text = currentProduct == null ? "Empty" : currentProduct.DisplayName;

        if (priceText != null)
            priceText.text = currentProduct == null ? "--" : currentProduct.UnitPrice.ToString();

        if (descriptionText != null)
            descriptionText.text = currentProduct == null ? string.Empty : currentProduct.Description;

        RefreshStockState();
    }

    /// <summary>
    /// 判断当前商品是否售罄。
    /// </summary>
    /// <returns>售罄返回 true，否则返回 false。</returns>
    private bool IsSoldOut()
    {
        if (currentProduct == null || currentProduct.InfiniteStock)
            return false;

        int stock = tradeController == null ? currentProduct.InitialStock : tradeController.GetCurrentStock(currentProduct); // 当前库存。
        return stock <= 0;
    }

    /// <summary>
    /// 获取当前商品库存的数字部分。
    /// </summary>
    /// <returns>有限库存返回非负数字，无限库存返回无穷符号。</returns>
    private string GetStockNumberText()
    {
        if (currentProduct == null)
            return "0";

        if (currentProduct.InfiniteStock)
            return "∞";

        int stock = tradeController == null
            ? currentProduct.InitialStock
            : tradeController.GetCurrentStock(currentProduct);

        return Mathf.Max(0, stock).ToString();
    }

    /// <summary>
    /// 设置覆盖层目标显示状态。
    /// </summary>
    /// <param name="isVisible">是否显示覆盖层。</param>
    private void SetOverlayTarget(bool isVisible)
    {
        targetOverlayProgress = isVisible ? 1f : 0f;

        if (descriptionOverlay != null && isVisible)
            descriptionOverlay.gameObject.SetActive(true);
    }

    /// <summary>
    /// 立即设置覆盖层显示状态，不播放动画。
    /// </summary>
    /// <param name="isVisible">是否显示覆盖层。</param>
    private void SetOverlayImmediate(bool isVisible)
    {
        overlayProgress = isVisible ? 1f : 0f;
        targetOverlayProgress = overlayProgress;
        ApplyOverlayProgress();

        if (descriptionOverlay != null)
            descriptionOverlay.gameObject.SetActive(isVisible);
    }

    /// <summary>
    /// 更新覆盖层高度和透明度动画。
    /// </summary>
    private void UpdateOverlayAnimation()
    {
        if (descriptionOverlay == null)
            return;

        overlayProgress = Mathf.MoveTowards(overlayProgress, targetOverlayProgress, Time.unscaledDeltaTime * overlayAnimationSpeed);
        ApplyOverlayProgress();

        if (overlayProgress <= 0.001f && targetOverlayProgress <= 0f)
            descriptionOverlay.gameObject.SetActive(false);
    }

    /// <summary>
    /// 应用覆盖层展开进度到 RectTransform（矩形变换）和 CanvasGroup（画布组）。
    /// </summary>
    private void ApplyOverlayProgress()
    {
        if (descriptionOverlay != null)
        {
            Vector2 size = descriptionOverlay.sizeDelta; // 当前覆盖层尺寸。
            size.y = overlayFullHeight * overlayProgress;
            descriptionOverlay.sizeDelta = size;
        }

        if (descriptionCanvasGroup != null)
        {
            descriptionCanvasGroup.alpha = overlayProgress;
            descriptionCanvasGroup.interactable = overlayProgress > 0.99f;
            descriptionCanvasGroup.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// 缓存覆盖层完整高度。
    /// </summary>
    private void CacheOverlayHeight()
    {
        if (descriptionOverlay == null)
            return;

        overlayFullHeight = descriptionOverlay.rect.height > 0f ? descriptionOverlay.rect.height : Mathf.Abs(descriptionOverlay.sizeDelta.y);

        if (overlayFullHeight <= 0f && RectTransform != null)
            overlayFullHeight = RectTransform.rect.height;

        if (overlayFullHeight <= 0f)
            overlayFullHeight = 190f;

        descriptionOverlay.pivot = new Vector2(descriptionOverlay.pivot.x, 1f);
    }

    /// <summary>
    /// 自动绑定常见命名的子物体引用。
    /// </summary>
    private void ResolveReferences()
    {
        if (itemIcon == null)
            itemIcon = FindChildComponent<Image>("ItemIcon");

        if (itemNameText == null)
            itemNameText = FindChildComponent<TMP_Text>("ItemNameText");

        if (itemNameText == null)
            itemNameText = FindChildComponent<TMP_Text>("NameText");

        if (itemNameText == null)
            itemNameText = FindChildComponent<TMP_Text>("ShopSlotNameText");

        if (priceText == null)
            priceText = FindChildComponent<TMP_Text>("PriceText");

        if (descriptionOverlay == null)
            descriptionOverlay = FindChildComponent<RectTransform>("DescriptionOverlay");

        if (descriptionCanvasGroup == null && descriptionOverlay != null)
        {
            descriptionCanvasGroup = descriptionOverlay.GetComponent<CanvasGroup>();

            if (descriptionCanvasGroup == null)
                descriptionCanvasGroup = descriptionOverlay.gameObject.AddComponent<CanvasGroup>();
        }

        if (descriptionText == null)
            descriptionText = FindChildComponent<TMP_Text>("DescriptionText");

        if (stockNumberText == null)
            stockNumberText = FindChildComponent<TMP_Text>("StockNumber");

        if (soldOutObject == null)
            soldOutObject = FindChildGameObject("SoldOut");
    }

    /// <summary>
    /// 按子物体名称查找指定类型组件。
    /// </summary>
    /// <typeparam name="T">要查找的组件类型。</typeparam>
    /// <param name="childName">子物体名称。</param>
    /// <returns>找到时返回组件，找不到时返回 null。</returns>
    private T FindChildComponent<T>(string childName) where T : Component
    {
        T[] childComponents = GetComponentsInChildren<T>(true); // 包括禁用对象在内的所有子组件。

        foreach (T childComponent in childComponents)
        {
            if (childComponent != null && childComponent.name == childName)
                return childComponent;
        }

        return null;
    }

    /// <summary>
    /// 按子物体名称查找 GameObject（游戏对象）。
    /// </summary>
    /// <param name="childName">子物体名称。</param>
    /// <returns>找到时返回游戏对象，找不到时返回 null。</returns>
    private GameObject FindChildGameObject(string childName)
    {
        Transform childTransform = FindChildTransform(childName); // 目标子物体。
        return childTransform == null ? null : childTransform.gameObject;
    }

    /// <summary>
    /// 在所有层级中按名称查找子物体，并包含当前处于禁用状态的对象。
    /// </summary>
    /// <param name="childName">要查找的子物体名称。</param>
    /// <returns>找到时返回 Transform（变换组件），找不到时返回 null。</returns>
    private Transform FindChildTransform(string childName)
    {
        Transform[] childTransforms = GetComponentsInChildren<Transform>(true); // 所有层级的子物体。

        foreach (Transform childTransform in childTransforms)
        {
            if (childTransform != null && childTransform != transform && childTransform.name == childName)
                return childTransform;
        }

        return null;
    }
}

using TMPro;
using UnityEngine;

/// <summary>商店独立详情视图，仅持有商品引用和临时选择，不持有库存副本。</summary>
public sealed class ShopDetailsUI : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text factsText;
    [SerializeField] private UnityEngine.UI.Image icon;
    [SerializeField] private GameObject presentationRoot;
    [SerializeField] private bool compactScroll;
    [SerializeField] private UnityEngine.UI.ScrollRect descriptionScroll;
    private ShopCatalogProduct renderedProduct;
    private ShopSlotUI pinnedSlot;
    private ShopSlotUI hoveredSlot;
    private ShopTradeController trade;

    public ShopCatalogProduct CurrentProduct => DisplayedSlot == null ? null : DisplayedSlot.CurrentProduct;
    private ShopSlotUI DisplayedSlot => hoveredSlot != null ? hoveredSlot : pinnedSlot;

    public void Show(ShopSlotUI slot, ShopTradeController controller, bool pin)
    {
        if (slot == null || slot.CurrentProduct == null) return;
        trade = controller;
        if (pin)
        {
            if (pinnedSlot != null) pinnedSlot.SetSelected(false);
            pinnedSlot = slot;
            pinnedSlot.SetSelected(true);
        }
        hoveredSlot = slot;
        Refresh();
    }

    public void EndHover(ShopSlotUI slot)
    {
        if (hoveredSlot == slot) hoveredSlot = null;
        Refresh();
    }

    public void SuspendHover() { hoveredSlot = null; Refresh(); }

    public void Clear()
    {
        if (pinnedSlot != null) pinnedSlot.SetSelected(false);
        pinnedSlot = null;
        hoveredSlot = null;
        trade = null;
        Refresh();
    }

    public void Refresh()
    {
        ShopCatalogProduct product = CurrentProduct;
        if (presentationRoot != null) presentationRoot.SetActive(product != null);
        if (renderedProduct != product && descriptionScroll != null)
        {
            descriptionScroll.StopMovement();
            descriptionScroll.content.anchoredPosition = Vector2.zero;
            descriptionScroll.verticalNormalizedPosition = 1f;
        }
        renderedProduct = product;
        ShopUiText.SetFittedName(titleText, product == null ? "物品详情" : ShopUiText.Name(product));
        if (descriptionText != null) descriptionText.text = product == null
            ? "悬停查看说明，点击商品购买。" : ShopUiText.Description(product);
        if (factsText != null) factsText.text = product == null ? string.Empty :
            (compactScroll
                ? $"{ShopUiText.Price(product)}\n{ShopUiText.Stock(product, trade)}\n每次购买：{product.PurchaseAmount}"
                : $"{ShopUiText.Price(product)}    {ShopUiText.Stock(product, trade)}    每次购买：{product.PurchaseAmount}");
        if (icon != null)
        {
            icon.sprite = product == null ? null : product.Icon;
            icon.enabled = icon.sprite != null;
        }
    }

    private void OnDisable() => Clear();
}

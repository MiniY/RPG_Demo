using System;

/// <summary>One user command crosses into the existing transaction owner exactly once.</summary>
public sealed class ShopPresenter
{
    private readonly ShopTradeController trade;
    private readonly Action<ShopSlotUI, ShopPurchaseResult> renderResult;
    public int CommandsSubmitted { get; private set; }
    public ShopPresenter(ShopTradeController trade, Action<ShopSlotUI, ShopPurchaseResult> renderResult)
    { this.trade = trade; this.renderResult = renderResult; }
    public void Purchase(ShopSlotUI slot)
    {
        if (slot == null || slot.CurrentProduct == null || trade == null) return;
        CommandsSubmitted++;
        ShopPurchaseResult result = trade.TryPurchase(slot.CurrentProduct);
        renderResult(slot, result);
    }
}

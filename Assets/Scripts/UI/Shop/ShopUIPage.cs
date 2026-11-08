using UnityEngine;

public sealed class ShopUIPage : UIPage<ShopOpenArgs>
{
    [SerializeField] private ShopPanelController controller;
    [SerializeField] private ShopGridUI grid;
    [SerializeField] private ShopMerchantPresentationUI presentation;
    public MerchantInteractionController CurrentMerchant { get; private set; }
    public override void ValidateBindings()
    {
        base.ValidateBindings();
        if (controller == null || grid == null || presentation == null)
            throw new System.InvalidOperationException("Shop page bindings are incomplete");
    }
    public override bool CanOpen(ShopOpenArgs args) => args.Merchant != null && args.Merchant.isActiveAndEnabled &&
        args.Merchant.ShopCatalog != null && presentation != null && presentation.CanPresent(args.Merchant);
    public override void Open(ShopOpenArgs args)
    {
        CurrentMerchant = args.Merchant;
        grid.SetShopCatalog(args.Merchant.ShopCatalog);
        controller.NotifyVisibility(true);
    }
    public override void Close(UICloseReason reason)
    {
        try { controller.NotifyVisibility(false); }
        finally { CurrentMerchant = null; }
    }
    public override void Suspend() => grid.SuspendInteraction();
}

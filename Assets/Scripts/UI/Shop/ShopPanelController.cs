using System;
using UnityEngine;

/// <summary>Merchant/button adapter. It supplies context; UIManager owns the window state.</summary>
public class ShopPanelController : MonoBehaviour
{
    public event Action<bool> OnShopPanelVisibilityChanged;
    [SerializeField] private GameObject shopPanelRoot;
    [SerializeField] private bool openAtStart;
    [SerializeField] private ShopGridUI shopGridUI;
    private UIRoot root;
    private ShopUIPage page;
    public GameObject PanelRoot => shopPanelRoot;
    public MerchantInteractionController CurrentMerchant => page == null ? null : page.CurrentMerchant;
    public bool IsShopPanelVisible => root != null && root.Manager.TryFind(UIRoutes.Shop, root.Scope, out _);

    public void Bind(UIRoot value)
    {
        root = value;
        page = root.FindPage<ShopUIPage>(UIRoutes.Shop.Id);
    }
    internal void NotifyVisibility(bool visible) => OnShopPanelVisibilityChanged?.Invoke(visible);
    private void OnEnable() => MerchantInteractionController.OnAnyMerchantInteracted += HandleMerchantInteracted;
    private void OnDisable() => MerchantInteractionController.OnAnyMerchantInteracted -= HandleMerchantInteracted;
    private void Start() { if (openAtStart) OpenShopPanel(); }
    private void HandleMerchantInteracted(MerchantInteractionController merchant)
    {
        if (root == null || IsShopPanelVisible || !root.CanUseWorkspace(UIRoutes.Shop.Id)) return;
        root.Manager.Open(UIRoutes.Shop, new ShopOpenArgs(merchant), root.Scope);
    }
    public void OpenShopPanel()
    {
        if (root != null && root.CanUseWorkspace(UIRoutes.Shop.Id) && CurrentMerchant != null)
            root.Manager.Open(UIRoutes.Shop, new ShopOpenArgs(CurrentMerchant), root.Scope);
    }
    public void CloseShopPanel()
    {
        if (root != null && root.Manager.TryFind(UIRoutes.Shop, root.Scope, out UIHandle handle)) root.Manager.Close(handle);
    }
    public void ToggleShopPanel() { if (IsShopPanelVisible) CloseShopPanel(); else OpenShopPanel(); }
    public void ValidateContext()
    {
        if (IsShopPanelVisible && (CurrentMerchant == null || !CurrentMerchant.isActiveAndEnabled))
            if (root.Manager.TryFind(UIRoutes.Shop, root.Scope, out UIHandle handle)) root.Manager.Close(handle, UICloseReason.ContextInvalid);
    }
}

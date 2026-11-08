using System;
using UnityEngine;

/// <summary>Compatibility entry for existing buttons; UIManager owns visibility and leases.</summary>
public class PackagePanelController : MonoBehaviour
{
    public event Action<bool> OnPackagePanelVisibilityChanged;
    [SerializeField] private GameObject packagePanel;
    [SerializeField] private bool openAtStart;
    private UIRoot root;
    private GameInput gameInput;
    public bool IsPackagePanelVisible => root != null && root.Manager.TryFind(UIRoutes.Package, root.Scope, out _);

    public void Bind(UIRoot value)
    {
        root = value;
    }
    internal void NotifyVisibility(bool visible) => OnPackagePanelVisibilityChanged?.Invoke(visible);
    private void Start()
    {
        gameInput = GameInput.Instance;
        if (gameInput != null) gameInput.OnPackagePressed += HandlePackagePressed;
        if (openAtStart) OpenPackagePanel();
    }
    private void OnDestroy()
    { if (gameInput != null) gameInput.OnPackagePressed -= HandlePackagePressed; }
    private void HandlePackagePressed()
    {
        if (root == null || !root.CanUseWorkspace(UIRoutes.Package.Id)) return;
        TogglePackagePanel();
    }
    public void OpenPackagePanel()
    {
        if (root != null && root.CanUseWorkspace(UIRoutes.Package.Id))
            root.Manager.Open(UIRoutes.Package, new UIEmptyArgs(), root.Scope);
    }
    public void ClosePackagePanel()
    {
        if (root != null && root.Manager.TryFind(UIRoutes.Package, root.Scope, out UIHandle handle)) root.Manager.Close(handle);
    }
    public void TogglePackagePanel()
    { if (IsPackagePanelVisible) ClosePackagePanel(); else OpenPackagePanel(); }
}

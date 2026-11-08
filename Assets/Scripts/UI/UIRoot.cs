using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Scene composition root. References are authored in SampleScene, never discovered by page name.</summary>
[DefaultExecutionOrder(-300)]
public sealed class UIRoot : MonoBehaviour
{
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private GameObject hudRoot;
    [SerializeField] private PackagePanelController package;
    [SerializeField] private ShopPanelController shop;
    [SerializeField] private RectTransform settingsAction;
    [SerializeField] private UnityEngine.UI.Button settingsButton;
    [SerializeField] private RectTransform shopEffects;
    [SerializeField] private EventSystem eventSystem;
    [SerializeField] private UIPageCatalog pageCatalog;
    [SerializeField] private UIPage[] pages;
    [SerializeField] private UILayerBindings layerBindings;
    private UIInputRouter input;
    private TowerRunCoordinator coordinator;
    private bool tearingDown;
    public UIManager Manager { get; private set; }
    public UILayerController Layers { get; private set; }
    public UIPageCatalog PageCatalog => pageCatalog;
    public System.Collections.Generic.IReadOnlyList<UIPage> Pages => pages;
    public UILayerBindings LayerBindings => layerBindings;
    public UIScope Scope => Manager.Scope;
    public bool IsPlaying => coordinator != null && coordinator.State == TowerRunFlowState.Playing;
    public bool CanUseWorkspace(string owner) => IsPlaying && !TowerRunGateHub.IsGameplayBlockedByOtherThan(owner);

    private void Awake()
    {
        ValidateConfiguration();
        Layers = new UILayerController(rootCanvas, hudRoot, layerBindings);
        input = new UIInputRouter(eventSystem);
        Manager = new UIManager(new UIScope(gameObject.scene.handle), Layers, input);
        foreach (UIPage page in pages) page.Bind(this);
        if (settingsAction != null)
        {
            // Keep the complete existing decoration/button group in the same screen position.
            settingsAction.SetParent(Layers.SystemActions, true);
            settingsAction.localScale = Vector3.one;
        }
        var accidentalPackageBinding = settingsButton.GetComponent<PackageHudButtonUI>();
        if (accidentalPackageBinding != null) accidentalPackageBinding.enabled = false;
        settingsButton.onClick.AddListener(OpenSettings);
        package.Bind(this); shop.Bind(this);
        foreach (UIPage page in pages)
        {
            UIPageDefinition definition = pageCatalog.CreateDefinition(page);
            if (page.IsTemplate) page.gameObject.SetActive(false);
            else page.Register(Manager, definition);
        }
        if (shopEffects != null)
        {
            shopEffects.SetParent(shop.PanelRoot.transform, false);
            UILayerController.Stretch(shopEffects);
        }
    }
    public TPage FindPage<TPage>(string id) where TPage : UIPage
    {
        if (pages != null)
            foreach (UIPage page in pages) if (page != null && page.PageId == id && page is TPage typed) return typed;
        throw new System.InvalidOperationException("Missing or incorrectly typed scene UI page: " + id);
    }
    public void ValidateConfiguration()
    {
        if (rootCanvas == null || eventSystem == null || package == null || shop == null || settingsButton == null)
            throw new System.InvalidOperationException("UIRoot requires explicit scene bindings");
        if (pageCatalog == null || pages == null || layerBindings == null)
            throw new System.InvalidOperationException("UIRoot requires an authored page catalog, pages and layers");
        pageCatalog.Validate(); layerBindings.Validate(rootCanvas);
        var systemViews = GetComponent<TowerRunUiViews>();
        if (systemViews == null) throw new System.InvalidOperationException("UIRoot requires authored system-view bindings");
        systemViews.ValidateBindings();
        var ids = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        foreach (UIPage page in pages)
        {
            if (page == null || !page.transform.IsChildOf(transform) || !ids.Add(page.PageId))
                throw new System.InvalidOperationException("Missing, duplicate or foreign scene UI page");
            page.ValidateBindings(); pageCatalog.CreateDefinition(page);
        }
        if (ids.Count != pageCatalog.Pages.Count) throw new System.InvalidOperationException("Catalog and scene pages must match exactly");
        FindPage<PackageUIPage>(UIRoutes.Package.Id); FindPage<ShopUIPage>(UIRoutes.Shop.Id);
        FindPage<SettingsUIPage>(UIRoutes.Settings.Id); FindPage<FlowUIPage>(UIRoutes.Flow.Id);
    }
    public void BindFlow(TowerRunCoordinator value)
    {
        if (coordinator != null) coordinator.StateChanged -= HandleFlowChanged;
        coordinator = value;
        if (coordinator != null) coordinator.StateChanged += HandleFlowChanged;
        HandleFlowChanged();
    }
    private void HandleFlowChanged()
    {
        if (!IsPlaying) Manager.CloseWorkspaces(UICloseReason.ContextInvalid);
        if (settingsAction != null) settingsAction.gameObject.SetActive(IsPlaying);
    }
    private void OpenSettings()
    {
        if (IsPlaying) Manager.Open(UIRoutes.Settings, new UIEmptyArgs(), Scope);
    }
    private void Update()
    {
        if (Manager == null) return;
        if (input.PollBack(Manager) == UIBackResult.Unhandled && IsPlaying) OpenSettings();
        shop.ValidateContext();
    }
    [ContextMenu("UI/输出运行快照")]
    public void DumpState() => Debug.Log(Manager == null ? "UI 尚未初始化，请进入运行模式。" : Manager.DebugSnapshot(), this);
    private void OnDisable()
    {
        if (Manager != null && !tearingDown) Manager.CloseScope(Scope, UICloseReason.SceneUnloaded);
    }
    private void OnDestroy()
    {
        tearingDown = true;
        if (coordinator != null) coordinator.StateChanged -= HandleFlowChanged;
        if (settingsButton != null) settingsButton.onClick.RemoveListener(OpenSettings);
        Manager?.Dispose();
    }
}

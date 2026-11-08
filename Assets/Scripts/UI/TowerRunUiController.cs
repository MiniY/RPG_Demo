using UnityEngine;

/// <summary>Compatibility/bootstrap facade; page logic lives in the flow/settings/save presenters.</summary>
[DisallowMultipleComponent]
public sealed class TowerRunUiController : MonoBehaviour
{
    private TowerRunUiViews views;
    private TowerRunFlowPresenter flow;
    private TowerRunSettingsPresenter settings;
    private TowerRunSaveSlotsPresenter saves;
    private TowerRunCoordinator coordinator;
    private UIRoot root;
    // Existing fixed acceptance probes reflect this field; it aliases the actual view button only.
    private UnityEngine.UI.Button primaryButton;
    public UnityEngine.UI.Button PrimaryButton => primaryButton;
    public static TowerRunUiController EnsureForScene(TowerRunCoordinator coordinator)
    {
        var root=UnityEngine.Object.FindObjectOfType<UIRoot>();
        if(root==null)return null; // Headless/auxiliary scenes retain their existing no-UI behavior.
        var facade=root.GetComponent<TowerRunUiController>();
        if(facade==null)facade=root.gameObject.AddComponent<TowerRunUiController>();
        facade.Initialize(coordinator);return facade;
    }
    public void Initialize(TowerRunCoordinator value)
    {
        if(coordinator==value && flow!=null){flow.Refresh();return;}
        if(coordinator!=null)throw new System.InvalidOperationException("UI scope must be recreated when the coordinator changes");
        coordinator=value;root=GetComponent<UIRoot>();
        views=GetComponent<TowerRunUiViews>();
        if(views==null)throw new System.InvalidOperationException("Authored system UI bindings are required");
        saves=new TowerRunSaveSlotsPresenter(value,views,root);
        settings=new TowerRunSettingsPresenter(views,saves);
        views.ShowAudioPage=settings.ShowAudioPage;views.ShowSavePage=settings.ShowSavePage;
        views.BeginSaveRequest=saves.BeginSaveRequest;views.PlayPreviewSound=settings.PlayPreviewSound;
        views.CloseSettings=CloseSettings;views.HandleVolumeChanged=settings.HandleVolumeChanged;views.HandleMuteChanged=settings.HandleMuteChanged;
        views.BindActions();primaryButton=views.primaryButton;settings.LoadAudioPreferences();
        root.FindPage<SettingsUIPage>(UIRoutes.Settings.Id).BindPresenter(settings);
        root.BindFlow(value);
        flow=new TowerRunFlowPresenter(value,views,root,saves.RefreshSaveSlots);
    }
    public void OpenSettings(){if(root!=null)root.Manager.Open(UIRoutes.Settings,new UIEmptyArgs(),root.Scope);}
    public void CloseSettings(){if(root!=null && root.Manager.TryFind(UIRoutes.Settings,root.Scope,out UIHandle handle))root.Manager.Close(handle);}
    private void OnDestroy(){if(views!=null)views.UnbindActions();flow?.Dispose();settings?.Dispose();}
}

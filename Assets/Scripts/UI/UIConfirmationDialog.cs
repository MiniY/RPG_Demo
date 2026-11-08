using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>A request owns one cloned dialog; all outcomes complete through UIManager after cleanup.</summary>
public sealed class UIConfirmationDialog : UIPage<UIEmptyArgs>
{
    [SerializeField] private UnityEngine.UI.Button confirmButton;
    [SerializeField] private UnityEngine.UI.Button cancelButton;
    [SerializeField] private UnityEngine.UI.Text details;
    private UIRoot root;
    public override bool IsTemplate => true;
    private UIPageDefinition definition;
    public override void ValidateBindings()
    {
        base.ValidateBindings();
        if (confirmButton == null || cancelButton == null || details == null || confirmButton == cancelButton ||
            !confirmButton.transform.IsChildOf(transform) || !cancelButton.transform.IsChildOf(transform) || !details.transform.IsChildOf(transform))
            throw new System.InvalidOperationException("Confirmation dialog bindings are incomplete or belong to another page");
    }
    public void SetBindings(UnityEngine.UI.Button confirm, UnityEngine.UI.Button cancel, UnityEngine.UI.Text text)
    { confirmButton = confirm; cancelButton = cancel; details = text; }
    public static Task<UIConfirmResult> ShowAsync(UIRoot root, UIHandle owner, GameObject template,
        string message, CancellationToken cancellationToken = default)
    {
        if (root == null || root.PageCatalog == null || template == null)
            throw new System.InvalidOperationException("Confirmation requires an authored root, catalog and template");
        var source = template.GetComponent<UIConfirmationDialog>();
        if (source == null) throw new System.InvalidOperationException("Confirmation template has no UIPage component");
        source.ValidateBindings();
        root.PageCatalog.CreateDefinition(source);
        if (root.Manager == null) throw new System.InvalidOperationException("Confirmation requires an initialized UIManager");
        var clone = Instantiate(template, root.Layers.Host(UILayer.Modal), false);
        try
        {
            clone.name = "ConfirmationDialog";
            var dialog = clone.GetComponent<UIConfirmationDialog>();
            dialog.root = root; dialog.Bind(root);
            dialog.details.text = message;
            dialog.definition = root.PageCatalog.CreateDefinition(dialog);
            return root.Manager.ConfirmAsync(dialog.definition, dialog, owner, cancellationToken);
        }
        catch
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(clone); else Destroy(clone);
#else
            Destroy(clone);
#endif
            throw;
        }
    }
    public override bool CanOpen(UIEmptyArgs args) => confirmButton != null && cancelButton != null && details != null;
    public override void Initialize()
    {
        confirmButton.gameObject.SetActive(true); cancelButton.gameObject.SetActive(true);
        confirmButton.onClick.RemoveAllListeners(); cancelButton.onClick.RemoveAllListeners();
        confirmButton.onClick.AddListener(() => Complete(UIConfirmResult.Confirmed));
        cancelButton.onClick.AddListener(() => Complete(UIConfirmResult.Cancelled));
    }
    private void Complete(UIConfirmResult result)
    {
        if (root.Manager.TryFind(new UIRoute<UIEmptyArgs>(definition.Id), root.Scope, out UIHandle handle))
            root.Manager.CompleteConfirmation(handle, result);
    }
    public override void Open(UIEmptyArgs args) { }
    public override void Suspend() { }
    public override void Resume() { }
    public override void Close(UICloseReason reason) { }
    public override void Dispose() { confirmButton.onClick.RemoveAllListeners(); cancelButton.onClick.RemoveAllListeners(); }
}

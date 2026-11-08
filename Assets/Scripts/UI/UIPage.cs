using UnityEngine;

/// <summary>A scene-visible page identity and lifecycle. Runtime state remains in UIManager.</summary>
public abstract class UIPage : MonoBehaviour
{
    [SerializeField] private string pageId;
    [SerializeField] private UnityEngine.UI.Selectable defaultFocus;
    private UIRoot root;
    public string PageId => pageId;
    public UnityEngine.UI.Selectable DefaultFocus => defaultFocus;
    public virtual bool IsTemplate => false;
    public UIRoot OwningRoot => root;
    protected UIRoot Root => root;
    internal void Bind(UIRoot owner)
    {
        if (root != null && root != owner) throw new System.InvalidOperationException("Page already belongs to another UI root: " + PageId);
        root = owner;
    }
    public virtual void ValidateBindings()
    {
        if (string.IsNullOrWhiteSpace(pageId)) throw new System.InvalidOperationException("UIPage requires an ID: " + name);
        if (defaultFocus != null && !defaultFocus.transform.IsChildOf(transform))
            throw new System.InvalidOperationException("Default focus must belong to its page: " + PageId);
    }
    internal abstract void Register(UIManager manager, UIPageDefinition definition);
}

public abstract class UIPage<TArgs> : UIPage, IUIView<TArgs>
{
    internal override void Register(UIManager manager, UIPageDefinition definition) =>
        manager.Register(new UIRoute<TArgs>(PageId), definition, this);
    public virtual bool CanOpen(TArgs args) => true;
    public virtual void Initialize() { }
    public virtual void Open(TArgs args) { }
    public virtual void Suspend() { }
    public virtual void Resume() { }
    public virtual void Close(UICloseReason reason) { }
    public virtual void Dispose() { }
}

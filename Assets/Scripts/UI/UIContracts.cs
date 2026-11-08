using System;
using UnityEngine;

public enum UILayer { HUD = 0, Window = 200, System = 400, Modal = 600, Blocking = 800 }
public enum UIBackPolicy { Close, Reject }
public enum UIOpenStatus { Opened, AlreadyOpen, Rejected, Busy, Failed }
public enum UICloseStatus { Closed, AlreadyClosed, StaleHandle, Rejected, Busy, Failed }
public enum UIBackResult { Handled, Rejected, Unhandled }
public enum UICloseReason { User, Replaced, OwnerClosed, SceneUnloaded, ContextInvalid, Confirmed, Cancelled, Failed }
public enum UIConfirmResult { Confirmed, Cancelled, OwnerClosed, SceneUnloaded, RequestCancelled, Failed }
public enum UILifecycle { Created, Open, Closing, Closed, Disposed }

public readonly struct UIScope : IEquatable<UIScope>
{
    public readonly int Id;
    public UIScope(int id) { Id = id; }
    public bool Equals(UIScope other) => Id == other.Id;
    public override bool Equals(object obj) => obj is UIScope other && Equals(other);
    public override int GetHashCode() => Id;
}

public readonly struct UIHandle : IEquatable<UIHandle>
{
    public readonly int Instance;
    public readonly int Generation;
    public readonly UIScope Scope;
    public bool IsValid => Instance > 0 && Generation > 0;
    public UIHandle(int instance, int generation, UIScope scope)
    { Instance = instance; Generation = generation; Scope = scope; }
    public bool Equals(UIHandle other) => Instance == other.Instance && Generation == other.Generation && Scope.Equals(other.Scope);
    public override bool Equals(object obj) => obj is UIHandle other && Equals(other);
    public override int GetHashCode() => Instance * 397 ^ Generation ^ Scope.GetHashCode();
    public override string ToString() => $"{Scope.Id}:{Instance}:{Generation}";
}

public sealed class UIRoute<TArgs>
{
    public string Id { get; }
    public UIRoute(string id)
    { if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException(nameof(id)); Id = id; }
}

public readonly struct UIEmptyArgs { }
public readonly struct ShopOpenArgs
{
    public readonly MerchantInteractionController Merchant;
    public ShopOpenArgs(MerchantInteractionController merchant) { Merchant = merchant; }
}

public static class UIRoutes
{
    public static readonly UIRoute<UIEmptyArgs> Package = new UIRoute<UIEmptyArgs>("ui.package");
    public static readonly UIRoute<ShopOpenArgs> Shop = new UIRoute<ShopOpenArgs>("ui.shop");
    public static readonly UIRoute<UIEmptyArgs> Settings = new UIRoute<UIEmptyArgs>("ui.settings");
    public static readonly UIRoute<UIEmptyArgs> Flow = new UIRoute<UIEmptyArgs>("ui.flow");
    public static readonly UIRoute<UIEmptyArgs> Blocking = new UIRoute<UIEmptyArgs>("ui.blocking");
}

[Serializable]
public sealed class UIPageDefinition
{
    public string Id;
    public UILayer Layer;
    public string Workspace;
    public UIBackPolicy BackPolicy;
    public bool BlockGameplay;
    public bool PauseWorld;
    public bool Cache = true;
    public GameObject SceneInstance;
    public GameObject Prefab;
    public UnityEngine.UI.Selectable DefaultFocus;
    public float DimAlpha;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || (SceneInstance == null) == (Prefab == null))
            throw new InvalidOperationException("A UI definition requires a unique ID and exactly one source: " + Id);
        if (!Enum.IsDefined(typeof(UILayer), Layer) || DimAlpha < 0 || DimAlpha > 1)
            throw new InvalidOperationException("Invalid UI layer or dimmer: " + Id);
    }
}

public readonly struct UIOpenResult
{
    public readonly UIOpenStatus Status;
    public readonly UIHandle Handle;
    public readonly string Message;
    public bool Succeeded => Status == UIOpenStatus.Opened || Status == UIOpenStatus.AlreadyOpen;
    public UIOpenResult(UIOpenStatus status, UIHandle handle = default, string message = null)
    { Status = status; Handle = handle; Message = message; }
}

public interface IUIView<TArgs>
{
    bool CanOpen(TArgs args);
    void Initialize();
    void Open(TArgs args);
    void Suspend();
    void Resume();
    void Close(UICloseReason reason);
    void Dispose();
}

/// <summary>Composition adapter for existing views; registration owns these callbacks, never the data they display.</summary>
public sealed class UIView<TArgs> : IUIView<TArgs>
{
    public Func<TArgs, bool> ValidateContext;
    public Action InitializeView, SuspendView, ResumeView, DisposeView;
    public Action<TArgs> OpenView;
    public Action<UICloseReason> CloseView;
    public bool CanOpen(TArgs args) => ValidateContext == null || ValidateContext(args);
    public void Initialize() => InitializeView?.Invoke();
    public void Open(TArgs args) => OpenView?.Invoke(args);
    public void Suspend() => SuspendView?.Invoke();
    public void Resume() => ResumeView?.Invoke();
    public void Close(UICloseReason reason) => CloseView?.Invoke(reason);
    public void Dispose() => DisposeView?.Invoke();
}

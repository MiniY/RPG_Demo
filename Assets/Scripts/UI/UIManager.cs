using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>Owns UI transactions. Gameplay, inventory, trade and persistence retain their existing owners.</summary>
public sealed class UIManager : IDisposable
{
    internal sealed class Entry
    {
        internal UIPageDefinition Definition;
        internal GameObject Root;
        internal Type ArgsType;
        internal Func<object, bool> CanOpen;
        internal Action Initialize, Suspend, Resume, Dispose;
        internal Action CreateInstance;
        internal Action<object> Open;
        internal Action<UICloseReason> Close;
        internal object Context;
        internal UIHandle Handle, Owner;
        internal TowerRunGateLease Gameplay, Pause;
        internal UILifecycle State;
        internal bool Initialized, Covered, PendingCompletion, IsConfirmation;
        internal GameObject LastFocus;
        internal TaskCompletionSource<UIConfirmResult> Completion;
        internal CancellationTokenRegistration Cancellation;
        internal UIConfirmResult? Result;
        internal int Order;
        internal bool IsOpen => State == UILifecycle.Open;
    }

    private readonly Dictionary<string, Entry> registry = new Dictionary<string, Entry>(StringComparer.Ordinal);
    private readonly Dictionary<int, Entry> instances = new Dictionary<int, Entry>();
    private readonly UILayerController layers;
    private readonly UIInputRouter input;
    private readonly SynchronizationContext mainContext;
    private readonly int threadId;
    private int generation, instanceId, order, modalId;
    private bool busy, disposed;
    private bool disposeRequested;
    private UICloseReason? pendingScopeClose;
    private readonly Queue<Action> completions = new Queue<Action>();
    private readonly Queue<string> transitions = new Queue<string>();
    public UIScope Scope { get; }
    public UINavigation Navigation { get; } = new UINavigation();
    public event Action Changed;
    public string LastFailure { get; private set; }
    internal IEnumerable<Entry> OpenEntries => instances.Values.Where(e => e.IsOpen);
    internal Entry Top => OpenEntries.OrderByDescending(e => (int)e.Definition.Layer).ThenByDescending(e => e.Order).FirstOrDefault();
    public int OpenCount => instances.Count;
    public int RegisteredCount => registry.Count;
    public int PendingConfirmations => instances.Values.Count(e => e.Completion != null);

    public UIManager(UIScope scope, UILayerController layers, UIInputRouter input)
    {
        Scope = scope; this.layers = layers; this.input = input;
        threadId = Thread.CurrentThread.ManagedThreadId; mainContext = SynchronizationContext.Current;
    }

    public void Register<TArgs>(UIRoute<TArgs> route, UIPageDefinition definition, IUIView<TArgs> view)
    {
        if (definition.Prefab != null) throw new InvalidOperationException("Prefab pages require a factory bound to the created instance");
        Register(route, definition, _ => view);
    }

    public void Register<TArgs>(UIRoute<TArgs> route, UIPageDefinition definition, Func<GameObject, IUIView<TArgs>> viewFactory)
    {
        CheckThread();
        if (disposed || busy || registry.ContainsKey(route.Id) || definition.Id != route.Id)
            throw new InvalidOperationException("Duplicate, busy or invalid UI registration: " + route.Id);
        definition.Validate();
        if (definition.SceneInstance != null && registry.Values.Any(e => e.Root == definition.SceneInstance))
            throw new InvalidOperationException("The same UI root cannot have two state owners");
        var entry = new Entry { Definition = definition, ArgsType = typeof(TArgs), State = UILifecycle.Created };
        entry.CreateInstance = () =>
        {
            GameObject root = definition.SceneInstance;
            if (root == null) root = layers.InstantiateInactive(definition.Prefab);
            try
            {
                IUIView<TArgs> view = viewFactory(root);
                if (view == null) throw new InvalidOperationException("UI view factory returned null: " + definition.Id);
                entry.Root = root; entry.CanOpen = args => view.CanOpen((TArgs)args);
                entry.Initialize = view.Initialize; entry.Open = args => view.Open((TArgs)args);
                entry.Suspend = view.Suspend; entry.Resume = view.Resume; entry.Close = view.Close; entry.Dispose = view.Dispose;
                entry.Initialized = false;
                layers.Attach(definition, root); root.SetActive(false);
            }
            catch { if (definition.SceneInstance == null) DestroyRoot(root); throw; }
        };
        entry.CreateInstance();
        registry.Add(route.Id, entry);
    }

    public UIOpenResult Open<TArgs>(UIRoute<TArgs> route, TArgs args, UIScope scope,
        UIHandle owner = default, bool replaceWorkspace = false)
    {
        CheckThread();
        if (disposed || !scope.Equals(Scope)) return new UIOpenResult(UIOpenStatus.Rejected, message: "Scope is unavailable");
        if (busy) return new UIOpenResult(UIOpenStatus.Busy);
        if (!registry.TryGetValue(route.Id, out Entry entry) || entry.ArgsType != typeof(TArgs))
            return new UIOpenResult(UIOpenStatus.Failed, message: "Route is not registered with these arguments");
        if (owner.IsValid && !TryEntry(owner, out _)) return new UIOpenResult(UIOpenStatus.Rejected, message: "Owner is unavailable");
        if (Top != null && (Top.Definition.Layer == UILayer.Blocking ||
            (entry.Definition.Layer == UILayer.Window && Top.Definition.Layer >= UILayer.System)))
            return new UIOpenResult(UIOpenStatus.Rejected, message: "Covered by a system operation");
        busy = true;
        Entry replaced = null;
        bool createdForOpen = false;
        try
        {
            replaced = OpenEntries.FirstOrDefault(e => e != entry && !string.IsNullOrEmpty(entry.Definition.Workspace) &&
                e.Definition.Workspace == entry.Definition.Workspace);
            if (replaced != null && !replaceWorkspace) return new UIOpenResult(UIOpenStatus.Rejected, message: "Workspace is occupied");
            if (entry.Root == null && entry.Definition.Prefab != null) { entry.CreateInstance(); createdForOpen = true; }
            bool accepted;
            try { accepted = entry.Root != null && entry.CanOpen(args); }
            catch (Exception validationError)
            {
                LastFailure = validationError.ToString();
                if (createdForOpen) ReleasePrefabInstance(entry);
                return new UIOpenResult(UIOpenStatus.Failed, entry.IsOpen ? entry.Handle : default, validationError.Message);
            }
            if (!accepted)
            {
                if (createdForOpen) ReleasePrefabInstance(entry);
                return new UIOpenResult(UIOpenStatus.Rejected, message: "Context is invalid");
            }
            if (entry.IsOpen)
            {
                if (!Equals(entry.Context, args))
                {
                    if (!replaceWorkspace || string.IsNullOrEmpty(entry.Definition.Workspace))
                        return new UIOpenResult(UIOpenStatus.Rejected, entry.Handle, "Explicit workspace replacement is required");
                    return ReplaceContext(entry, args, owner);
                }
                BringToFrontCore(entry);
                Reconcile();
                return new UIOpenResult(UIOpenStatus.AlreadyOpen, entry.Handle);
            }
            if (!entry.Initialized) { entry.Initialize(); entry.Initialized = true; }
            entry.Handle = new UIHandle(++instanceId, ++generation, scope); entry.Owner = owner; entry.Context = args;
            entry.Order = ++order;
            if (entry.Definition.BlockGameplay) entry.Gameplay = TowerRunGateHub.AcquireGameplay(entry.Definition.Id);
            if (entry.Definition.PauseWorld) entry.Pause = TowerRunGateHub.AcquireWorldPause(entry.Definition.Id);
            entry.State = UILifecycle.Open;
            instances.Add(entry.Handle.Instance, entry); Navigation.Enter(entry.Handle, entry.Definition.Layer);
            entry.Open(args); entry.Root.SetActive(true);
            RecordTransition("Open " + entry.Definition.Id + " " + entry.Handle);
            // Keep the previous workspace alive until the replacement has initialized and activated successfully.
            if (replaced != null) CloseCore(replaced, UICloseReason.Replaced);
            Reconcile();
            return new UIOpenResult(UIOpenStatus.Opened, entry.Handle);
        }
        catch (Exception error)
        {
            LastFailure = error.ToString();
            if (entry.IsOpen) CloseCore(entry, UICloseReason.Failed);
            else
            {
                Release(entry);
                if (entry.Definition.Prefab != null) ReleasePrefabInstance(entry);
            }
            Reconcile();
            return new UIOpenResult(UIOpenStatus.Failed, message: error.Message);
        }
        finally { EndTransaction(); }
    }

    private UIOpenResult ReplaceContext(Entry entry, object args, UIHandle owner)
    {
        object previous = entry.Context;
        UIHandle previousHandle = entry.Handle;
        entry.Root.SetActive(false);
        try
        {
            // Retain registration, navigation and leases until the new presentation is ready.
            entry.Close(UICloseReason.Replaced);
            entry.Open(args);
        }
        catch (Exception error)
        {
            LastFailure = error.ToString();
            try { entry.Close(UICloseReason.Failed); entry.Open(previous); entry.Root.SetActive(true); }
            catch (Exception rollback)
            {
                LastFailure += "\nContext rollback failed: " + rollback;
                CloseCore(entry, UICloseReason.Failed);
            }
            Reconcile();
            return new UIOpenResult(UIOpenStatus.Failed, entry.IsOpen ? previousHandle : default, error.Message);
        }
        foreach (Entry child in OpenEntries.Where(e => e.Owner.Equals(previousHandle)).ToArray())
            CloseCore(child, UICloseReason.OwnerClosed);
        instances.Remove(previousHandle.Instance); Navigation.Remove(previousHandle);
        entry.Handle = new UIHandle(++instanceId, ++generation, Scope);
        entry.Owner = owner; entry.Context = args; entry.LastFocus = null; entry.Order = ++order;
        instances.Add(entry.Handle.Instance, entry); Navigation.Enter(entry.Handle, entry.Definition.Layer);
        entry.Root.SetActive(true);
        RecordTransition("ReplaceContext " + entry.Definition.Id + " " + previousHandle + " -> " + entry.Handle);
        Reconcile();
        return new UIOpenResult(UIOpenStatus.Opened, entry.Handle);
    }

    public UICloseStatus Close(UIHandle handle, UICloseReason reason = UICloseReason.User)
    {
        CheckThread();
        if (busy) return UICloseStatus.Busy;
        if (!TryEntry(handle, out Entry entry))
            return !handle.IsValid || registry.Values.Any(e => e.Handle.Equals(handle) && e.State == UILifecycle.Closed)
                ? UICloseStatus.AlreadyClosed : UICloseStatus.StaleHandle;
        if (reason == UICloseReason.User && entry.Definition.BackPolicy == UIBackPolicy.Reject) return UICloseStatus.Rejected;
        busy = true;
        try { bool succeeded = CloseCore(entry, reason); Reconcile(); return succeeded ? UICloseStatus.Closed : UICloseStatus.Failed; }
        finally { EndTransaction(); }
    }

    public UIBackResult Back()
    {
        CheckThread();
        if (busy) return UIBackResult.Rejected;
        Entry top = Top;
        if (top == null) return UIBackResult.Unhandled;
        if (top.Definition.BackPolicy == UIBackPolicy.Reject) return UIBackResult.Rejected;
        return Close(top.Handle, UICloseReason.Cancelled) == UICloseStatus.Busy ? UIBackResult.Rejected : UIBackResult.Handled;
    }

    public bool TryFind<TArgs>(UIRoute<TArgs> route, UIScope scope, out UIHandle handle)
    {
        handle = default;
        if (!scope.Equals(Scope) || !registry.TryGetValue(route.Id, out Entry entry) || entry.ArgsType != typeof(TArgs) || !entry.IsOpen) return false;
        handle = entry.Handle; return true;
    }
    public bool IsFocused(UIHandle handle) => Top != null && Top.Handle.Equals(handle);
    internal void ConfigureClosedLayer<TArgs>(UIRoute<TArgs> route, UILayer layer)
    {
        CheckThread();
        Entry entry = registry[route.Id];
        if (busy || entry.IsOpen) throw new InvalidOperationException("Layer changes require a closed view");
        entry.Definition.Layer = layer;
        layers.Attach(entry.Definition, entry.Root);
    }
    public bool BringToFront(UIHandle handle)
    {
        CheckThread();
        if (busy || !TryEntry(handle, out Entry entry) || entry.Definition.Layer != UILayer.Window ||
            (Top != null && Top.Definition.Layer > UILayer.Window)) return false;
        BringToFrontCore(entry); Reconcile(); return true;
    }
    private void BringToFrontCore(Entry entry)
    { entry.Order = ++order; Navigation.BringToFront(entry.Handle, entry.Definition.Layer); }

    /// <summary>Takes ownership of the request instance, including every rejection before registration.</summary>
    public Task<UIConfirmResult> ConfirmAsync(UIPageDefinition definition, IUIView<UIEmptyArgs> view,
        UIHandle owner, CancellationToken cancellationToken = default)
    {
        CheckThread();
        UIConfirmResult? rejection = cancellationToken.IsCancellationRequested ? UIConfirmResult.RequestCancelled :
            disposed || !TryEntry(owner, out _) ? UIConfirmResult.OwnerClosed : busy ? UIConfirmResult.Failed : (UIConfirmResult?)null;
        if (rejection.HasValue)
        {
            DisposeUnregisteredConfirmation(definition, view);
            return Task.FromResult(rejection.Value);
        }
        string id = "ui.confirm." + (++modalId);
        definition.Id = id; definition.Layer = UILayer.Modal; definition.Cache = false;
        var route = new UIRoute<UIEmptyArgs>(id);
        try { Register(route, definition, view); }
        catch (Exception error)
        {
            LastFailure = error.ToString();
            DisposeUnregisteredConfirmation(definition, view);
            return Task.FromResult(UIConfirmResult.Failed);
        }
        Entry entry = registry[id];
        entry.IsConfirmation = true;
        var completion = new TaskCompletionSource<UIConfirmResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        entry.Completion = completion;
        // The completion exists before activation: lifecycle callbacks may close the owner during Open.
        UIOpenResult result = Open(route, new UIEmptyArgs(), Scope, owner);
        if (!result.Succeeded || !entry.IsOpen)
        {
            if (registry.ContainsKey(id)) DisposeRegistration(id);
            completion.TrySetResult(UIConfirmResult.Failed);
            return completion.Task;
        }
        if (cancellationToken.CanBeCanceled)
        {
            var registration = cancellationToken.Register(() =>
            {
                Action cancel = () => CompleteConfirmation(result.Handle, UIConfirmResult.RequestCancelled);
                if (Thread.CurrentThread.ManagedThreadId == threadId) cancel();
                else if (mainContext != null) mainContext.Post(_ => cancel(), null);
            });
            // Register can invoke synchronously for a token cancelled during Open.
            if (entry.IsOpen) entry.Cancellation = registration; else registration.Dispose();
        }
        return completion.Task;
    }
    private void DisposeUnregisteredConfirmation(UIPageDefinition definition, IUIView<UIEmptyArgs> view)
    {
        try { view.Dispose(); } catch (Exception error) { LastFailure = error.ToString(); }
        if (definition.SceneInstance != null) DestroyRoot(definition.SceneInstance);
    }
    public bool CompleteConfirmation(UIHandle handle, UIConfirmResult result)
    {
        CheckThread();
        if (!TryEntry(handle, out Entry entry) || entry.Completion == null) return false;
        entry.Result = result;
        if (busy) { entry.PendingCompletion = true; return true; }
        entry.PendingCompletion = false;
        Close(handle, result == UIConfirmResult.Confirmed ? UICloseReason.Confirmed : UICloseReason.Cancelled);
        return true;
    }

    public void CloseScope(UIScope scope, UICloseReason reason = UICloseReason.SceneUnloaded)
    {
        CheckThread();
        if (!scope.Equals(Scope)) return;
        if (busy) { pendingScopeClose = reason; return; }
        busy = true;
        try { foreach (Entry entry in OpenEntries.OrderByDescending(e => e.Order).ToArray()) CloseCore(entry, reason); Reconcile(); }
        finally { EndTransaction(); }
    }
    public void CloseWorkspaces(UICloseReason reason)
    {
        CheckThread();
        if (busy) return;
        busy = true;
        try { foreach (Entry entry in OpenEntries.Where(e => e.Definition.Layer == UILayer.Window || e.Definition.Layer == UILayer.System).ToArray()) CloseCore(entry, reason); Reconcile(); }
        finally { EndTransaction(); }
    }

    private bool CloseCore(Entry entry, UICloseReason reason)
    {
        if (!entry.IsOpen) return true;
        entry.State = UILifecycle.Closing;
        RecordTransition("Close " + entry.Definition.Id + " " + entry.Handle + " " + reason);
        foreach (Entry child in OpenEntries.Where(e => e.Owner.Equals(entry.Handle)).ToArray())
            CloseCore(child, reason == UICloseReason.SceneUnloaded ? reason : UICloseReason.OwnerClosed);
        bool succeeded = true;
        try { entry.Close(reason); }
        catch (Exception error) { LastFailure = error.ToString(); succeeded = false; }
        finally
        {
            if (entry.Root != null) entry.Root.SetActive(false);
            instances.Remove(entry.Handle.Instance); Navigation.Remove(entry.Handle); Release(entry);
            entry.State = UILifecycle.Closed; entry.Covered = false; entry.Context = null;
            if (entry.Completion != null)
            {
                var completion = entry.Completion;
                var outcome = !succeeded || reason == UICloseReason.Failed ? UIConfirmResult.Failed : (reason == UICloseReason.SceneUnloaded ? UIConfirmResult.SceneUnloaded :
                    reason == UICloseReason.OwnerClosed || reason == UICloseReason.ContextInvalid ? UIConfirmResult.OwnerClosed :
                    entry.Result ?? UIConfirmResult.Cancelled);
                entry.Cancellation.Dispose(); entry.Completion = null;
                completions.Enqueue(() => completion.TrySetResult(outcome));
            }
            if (!entry.Definition.Cache)
            {
                if (entry.Definition.Prefab != null && !entry.IsConfirmation) ReleasePrefabInstance(entry);
                else DisposeRegistration(entry.Definition.Id);
            }
        }
        return succeeded;
    }
    private void DisposeRegistration(string id)
    {
        if (!registry.TryGetValue(id, out Entry entry)) return;
        try { entry.Dispose?.Invoke(); } catch (Exception error) { LastFailure = error.ToString(); }
        entry.State = UILifecycle.Disposed; registry.Remove(id);
        if (entry.Root != null && (entry.Definition.Prefab != null || !entry.Definition.Cache)) DestroyRoot(entry.Root);
    }
    private void ReleasePrefabInstance(Entry entry)
    {
        try { entry.Dispose?.Invoke(); } catch (Exception error) { LastFailure = error.ToString(); }
        if (entry.Root != null) DestroyRoot(entry.Root);
        entry.Root = null; entry.Initialized = false;
        entry.Initialize = entry.Suspend = entry.Resume = entry.Dispose = null;
        entry.Open = null; entry.Close = null; entry.CanOpen = null;
    }
    private static void DestroyRoot(GameObject root)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) { UnityEngine.Object.DestroyImmediate(root); return; }
#endif
        UnityEngine.Object.Destroy(root);
    }
    private static void Release(Entry entry)
    { entry.Gameplay?.Dispose(); entry.Gameplay = null; entry.Pause?.Dispose(); entry.Pause = null; }
    private bool TryEntry(UIHandle handle, out Entry entry)
    { return instances.TryGetValue(handle.Instance, out entry) && entry.Handle.Equals(handle) && entry.IsOpen; }
    private void Reconcile()
    {
        Entry top = Top;
        foreach (Entry entry in OpenEntries.ToArray())
        {
            bool covered = entry != top;
            if (covered != entry.Covered)
            {
                entry.Covered = covered;
                try { if (covered) entry.Suspend(); else entry.Resume(); }
                catch (Exception error) { LastFailure = error.ToString(); }
            }
        }
        layers?.Apply(OpenEntries, top); input?.Apply(OpenEntries, top);
    }
    private void EndTransaction()
    {
        busy = false;
        if (disposeRequested) { disposeRequested = false; Dispose(); }
        else if (pendingScopeClose.HasValue)
        { var reason = pendingScopeClose.Value; pendingScopeClose = null; CloseScope(Scope, reason); }
        foreach (Entry entry in OpenEntries.Where(e => e.PendingCompletion).ToArray())
            CompleteConfirmation(entry.Handle, entry.Result.Value);
        try { Changed?.Invoke(); } catch (Exception error) { LastFailure = error.ToString(); }
        while (completions.Count > 0) completions.Dequeue()();
    }
    private void CheckThread()
    { if (Thread.CurrentThread.ManagedThreadId != threadId) throw new InvalidOperationException("UI operations require the Unity main thread"); }
    private void RecordTransition(string description)
    {
        if (transitions.Count == 24) transitions.Dequeue();
        transitions.Enqueue(description);
    }
    public string DebugSnapshot()
    {
        return JsonUtility.ToJson(new UIDebugState { scope = Scope.Id, pendingConfirmations = PendingConfirmations,
            registered = RegisteredCount, recentTransitions = transitions.ToArray(),
            gameplayOwners = TowerRunGateHub.CaptureGameplayOwners().Select(p => p.Key + ":" + p.Value).ToArray(),
            worldPauseOwners = TowerRunGateHub.CaptureWorldPauseOwners().Select(p => p.Key + ":" + p.Value).ToArray(),
            dimAlpha = layers == null ? 0 : layers.DimAlpha, blockerActive = layers != null && layers.BlockerActive,
            failure = LastFailure, backReceiver = Top?.Definition.Id,
            entries = OpenEntries.OrderBy(e => e.Order).Select(e => new UIDebugEntry { id = e.Definition.Id,
                handle = e.Handle.ToString(), owner = e.Owner.ToString(), layer = (int)e.Definition.Layer,
                order = e.Order, lifecycle = e.State.ToString(), covered = e.Covered, focused = e == Top,
                gameplayOwner = e.Gameplay == null ? null : e.Definition.Id, pauseOwner = e.Pause == null ? null : e.Definition.Id,
                canvasOrder = e.Root == null ? -1 : e.Root.GetComponentInParent<Canvas>().sortingOrder,
                canvasOverridesSorting = e.Root != null && e.Root.GetComponentInParent<Canvas>().overrideSorting,
                effectiveCanvasOrder = layers == null ? (int)e.Definition.Layer : layers.Host(e.Definition.Layer).GetComponent<Canvas>().sortingOrder }).ToArray() }, true);
    }
    public void Dispose()
    {
        if (disposed) return;
        if (busy) { disposeRequested = true; return; }
        CloseScope(Scope); disposed = true;
        foreach (string id in registry.Keys.ToArray()) DisposeRegistration(id);
        Navigation.Clear();
    }
    [Serializable] private sealed class UIDebugState { public int scope, pendingConfirmations, registered; public float dimAlpha; public bool blockerActive; public string backReceiver, failure; public string[] recentTransitions, gameplayOwners, worldPauseOwners; public UIDebugEntry[] entries; }
    [Serializable] private sealed class UIDebugEntry { public string id, handle, owner, lifecycle, gameplayOwner, pauseOwner; public int layer, order, canvasOrder, effectiveCanvasOrder; public bool covered, focused, canvasOverridesSorting; }
}

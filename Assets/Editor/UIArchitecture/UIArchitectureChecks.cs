#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

/// <summary>Contract checks exercise UIManager through its public operations, with real layer canvases.</summary>
public static class UIArchitectureChecks
{
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    public static void RunCase(string name)
    {
        GameObject canvasObject = new GameObject("UIContractCanvas", typeof(RectTransform), typeof(Canvas));
        var layers = new UILayerController(canvasObject.GetComponent<Canvas>(), null);
        var manager = new UIManager(new UIScope(731), layers, null);
        var scope = manager.Scope;
        var a = new UIRoute<int>("test.workspace.a");
        var b = new UIRoute<int>("test.workspace.b");
        var settings = new UIRoute<int>("test.settings");
        int opens = 0, closes = 0, suspended = 0, resumed = 0;
        var ar = new GameObject("WorkspaceA", typeof(RectTransform));
        var br = new GameObject("WorkspaceB", typeof(RectTransform));
        var sr = new GameObject("Settings", typeof(RectTransform));
        var av = new UIView<int> { OpenView = _ => opens++, CloseView = _ => closes++, SuspendView = () => suspended++, ResumeView = () => resumed++ };
        manager.Register(a, new UIPageDefinition { Id = a.Id, SceneInstance = ar, Layer = UILayer.Window,
            Workspace = "primary", BlockGameplay = true, DimAlpha = .35f }, av);
        manager.Register(b, new UIPageDefinition { Id = b.Id, SceneInstance = br, Layer = UILayer.Window,
            Workspace = "primary", BlockGameplay = true, DimAlpha = .35f }, new UIView<int> {
                ValidateContext = n => n > 0, OpenView = n => { if (n == 99) throw new Exception("Injected opening failure"); } });
        manager.Register(settings, new UIPageDefinition { Id = settings.Id, SceneInstance = sr,
            Layer = UILayer.System, BlockGameplay = true, PauseWorld = true, DimAlpha = .5f }, new UIView<int>());
        try
        {
            var first = manager.Open(a, 1, scope);
            Require(first.Succeeded, "First workspace failed");
            switch (name)
            {
                case "TransparentOverlayStillBlocksInput":
                    var transparent = new UIRoute<int>("test.transparent.overlay");
                    manager.Register(transparent, new UIPageDefinition { Id = transparent.Id,
                        SceneInstance = new GameObject("TransparentOverlay", typeof(RectTransform)), Layer = UILayer.Modal, DimAlpha = 0 }, new UIView<int>());
                    var transparentOpen = manager.Open(transparent, 1, scope, first.Handle);
                    var blocker = canvasObject.GetComponentsInChildren<UnityEngine.UI.Image>().Single(image => image.name == "ManagedScrim");
                    Require(transparentOpen.Succeeded && layers.BlockerActive && layers.DimAlpha == 0 && blocker.raycastTarget &&
                        !blocker.canvasRenderer.cullTransparentMesh && !UILayerController.Group(ar).interactable && !UILayerController.Group(ar).blocksRaycasts,
                        "Zero visual dim disabled the actual input blocker or lower-page coverage");
                    manager.Close(transparentOpen.Handle);
                    Require(manager.IsFocused(first.Handle) && Mathf.Approximately(layers.DimAlpha, .35f), "Closing transparent overlay lost the originating window");
                    break;
                case "ConfirmationCloseFailureCannotConfirmBusiness":
                    var throwingDialog = new GameObject("ThrowingCloseModal", typeof(RectTransform));
                    var throwingDefinition = new UIPageDefinition { SceneInstance = throwingDialog };
                    var failingConfirmation = manager.ConfirmAsync(throwingDefinition,
                        new UIView<UIEmptyArgs> { CloseView = _ => throw new InvalidOperationException("Modal close failure") }, first.Handle);
                    manager.TryFind(new UIRoute<UIEmptyArgs>(throwingDefinition.Id), scope, out UIHandle failingHandle);
                    manager.CompleteConfirmation(failingHandle, UIConfirmResult.Confirmed);
                    Require(failingConfirmation.IsCompleted && failingConfirmation.Result == UIConfirmResult.Failed &&
                        manager.PendingConfirmations == 0 && throwingDialog == null && manager.IsFocused(first.Handle),
                        "A failed modal cleanup was returned as business confirmation");
                    var openingDefinition = new UIPageDefinition { SceneInstance = new GameObject("ThrowingOpenModal", typeof(RectTransform)) };
                    var openingConfirmation = manager.ConfirmAsync(openingDefinition, new UIView<UIEmptyArgs> { OpenView = _ => {
                        manager.TryFind(new UIRoute<UIEmptyArgs>(openingDefinition.Id), scope, out UIHandle openingHandle);
                        manager.CompleteConfirmation(openingHandle, UIConfirmResult.Confirmed);
                        throw new InvalidOperationException("Confirmed inside a failed Open");
                    } }, first.Handle);
                    Require(openingConfirmation.IsCompleted && openingConfirmation.Result == UIConfirmResult.Failed &&
                        manager.PendingConfirmations == 0 && manager.IsFocused(first.Handle),
                        "Failed Open was returned as confirmation merely because a callback queued Confirmed");
                    var unloadingDefinition = new UIPageDefinition { SceneInstance = new GameObject("UnloadingOpenModal", typeof(RectTransform)) };
                    var unloadingConfirmation = manager.ConfirmAsync(unloadingDefinition, new UIView<UIEmptyArgs> { OpenView = _ => {
                        manager.TryFind(new UIRoute<UIEmptyArgs>(unloadingDefinition.Id), scope, out UIHandle unloadingHandle);
                        manager.CompleteConfirmation(unloadingHandle, UIConfirmResult.Confirmed);
                        manager.CloseScope(scope);
                    } }, first.Handle);
                    Require(unloadingConfirmation.IsCompleted && unloadingConfirmation.Result == UIConfirmResult.SceneUnloaded &&
                        manager.OpenCount == 0, "Scene teardown was returned as a business confirmation");
                    break;
                case "ExplicitContextReplacementAndRollback":
                    int context = 1;
                    av.ValidateContext = n => n == 98 ? throw new InvalidOperationException("Validation failure") : n > 0;
                    av.OpenView = n => { context = n; if (n == 99) throw new InvalidOperationException("Context bind failure"); };
                    Require(manager.Open(a, -1, scope, replaceWorkspace: true).Status == UIOpenStatus.Rejected && context == 1,
                        "Invalid replacement changed the previous context");
                    Require(manager.Open(a, 2, scope).Status == UIOpenStatus.Rejected && context == 1,
                        "Implicit context replacement was accepted");
                    Require(manager.Open(a, 98, scope, replaceWorkspace: true).Status == UIOpenStatus.Failed && context == 1 &&
                        manager.IsFocused(first.Handle) && ar.activeSelf && TowerRunGateHub.IsGameplayBlocked,
                        "Throwing validation closed an unchanged old context");
                    Require(manager.Open(a, 99, scope, replaceWorkspace: true).Status == UIOpenStatus.Failed && context == 1 &&
                        manager.TryFind(a, scope, out UIHandle retained) && retained.Equals(first.Handle) && ar.activeSelf,
                        "Context failure did not restore the old window and handle");
                    var changed = manager.Open(a, 2, scope, replaceWorkspace: true);
                    Require(changed.Succeeded && context == 2 && manager.OpenCount == 1 && manager.IsFocused(changed.Handle) &&
                        manager.Close(first.Handle) == UICloseStatus.StaleHandle && TowerRunGateHub.IsGameplayBlocked,
                        "Successful context replacement retained stale identity or lost its lease");
                    break;
                case "IdempotencyAndStaleHandles":
                    Require(!manager.TryFind(new UIRoute<string>(a.Id), scope, out _), "A differently typed route found an unrelated instance");
                    Require(manager.Open(a, 1, scope).Status == UIOpenStatus.AlreadyOpen && opens == 1, "Repeated open repeated side effects");
                    Require(manager.Close(first.Handle) == UICloseStatus.Closed, "Close failed");
                    Require(manager.Close(first.Handle) == UICloseStatus.AlreadyClosed, "Repeated close is not idempotent");
                    var next = manager.Open(a, 1, scope);
                    Require(manager.Close(first.Handle) == UICloseStatus.StaleHandle && manager.IsFocused(next.Handle), "Stale handle closed new instance");
                    break;
                case "WorkspaceReplacementRollsBack":
                    Require(manager.Open(b, -1, scope, replaceWorkspace: true).Status == UIOpenStatus.Rejected, "Invalid replacement accepted");
                    Require(manager.Open(b, 99, scope, replaceWorkspace: true).Status == UIOpenStatus.Failed && ar.activeSelf && manager.IsFocused(first.Handle), "Failed replacement lost old workspace");
                    Require(manager.Open(b, 2, scope, replaceWorkspace: true).Succeeded && !ar.activeSelf && br.activeSelf, "Explicit replacement failed");
                    break;
                case "LayersDimmerAndCoverage":
                    var upper = manager.Open(settings, 1, scope);
                    Require(upper.Succeeded && suspended == 1 && ar.activeSelf, "Covered window was closed");
                    Require(!ar.GetComponent<CanvasGroup>().interactable && sr.GetComponent<CanvasGroup>().interactable, "Lower controls accept input");
                    Require(!manager.BringToFront(first.Handle), "Window escaped system layer");
                    Require(Mathf.Approximately(layers.DimAlpha, .5f) && layers.BlockerActive, "Dimmer is not a single top policy");
                    var scrim = layers.Host(UILayer.System).Find("ManagedScrim");
                    Require(scrim != null && scrim.GetSiblingIndex() < sr.transform.GetSiblingIndex(), "Scrim covers the active view");
                    Require(manager.Back() == UIBackResult.Handled && manager.IsFocused(first.Handle) && resumed == 1, "Back did not restore the window");
                    break;
                case "RejectedBackIsConsumed":
                    var blocked = new UIRoute<int>("test.blocking");
                    var gr = new GameObject("Blocking", typeof(RectTransform));
                    manager.Register(blocked, new UIPageDefinition { Id = blocked.Id, SceneInstance = gr, Layer = UILayer.Blocking,
                        BackPolicy = UIBackPolicy.Reject }, new UIView<int>());
                    manager.Open(blocked, 1, scope);
                    Require(manager.Back() == UIBackResult.Rejected && ar.activeSelf, "Rejected Back reached lower window");
                    Require(manager.Open(settings, 1, scope).Status == UIOpenStatus.Rejected, "Blocking accepted ordinary open");
                    break;
                case "NestedConfirmAndOwnerCleanup":
                    var d1 = new UIPageDefinition { SceneInstance = new GameObject("Modal1", typeof(RectTransform)), DimAlpha = .55f };
                    var t1 = manager.ConfirmAsync(d1, new UIView<UIEmptyArgs>(), first.Handle);
                    manager.TryFind(new UIRoute<UIEmptyArgs>(d1.Id), scope, out UIHandle h1);
                    var d2 = new UIPageDefinition { SceneInstance = new GameObject("Modal2", typeof(RectTransform)), DimAlpha = .55f };
                    var t2 = manager.ConfirmAsync(d2, new UIView<UIEmptyArgs>(), h1);
                    Require(manager.PendingConfirmations == 2, "Nested modal missing");
                    manager.Back();
                    Require(t2.IsCompleted && t2.Result == UIConfirmResult.Cancelled && !t1.IsCompleted, "One Back consumed two modals");
                    manager.Close(first.Handle);
                    Require(t1.IsCompleted && t1.Result == UIConfirmResult.OwnerClosed && manager.PendingConfirmations == 0, "Owner cleanup left pending dialog");
                    break;
                case "CancellationAndSceneUnload":
                    using (var cancellation = new CancellationTokenSource())
                    {
                        var d = new UIPageDefinition { SceneInstance = new GameObject("CancelledModal", typeof(RectTransform)) };
                        var t = manager.ConfirmAsync(d, new UIView<UIEmptyArgs>(), first.Handle, cancellation.Token);
                        cancellation.Cancel();
                        Require(t.IsCompleted && t.Result == UIConfirmResult.RequestCancelled, "Cancellation left pending await");
                    }
                    var d3 = new UIPageDefinition { SceneInstance = new GameObject("SceneModal", typeof(RectTransform)) };
                    var t3 = manager.ConfirmAsync(d3, new UIView<UIEmptyArgs>(), first.Handle);
                    manager.CloseScope(scope);
                    Require(t3.IsCompleted && t3.Result == UIConfirmResult.SceneUnloaded && manager.OpenCount == 0, "Scene unload leaked state");
                    break;
                case "ConfirmationRejectionsDestroyRequestInstances":
                    using (var cancelled = new CancellationTokenSource())
                    {
                        cancelled.Cancel();
                        var rejectedRoot = new GameObject("PreCancelledModal", typeof(RectTransform));
                        var rejected = manager.ConfirmAsync(new UIPageDefinition { SceneInstance = rejectedRoot },
                            new UIView<UIEmptyArgs>(), first.Handle, cancelled.Token);
                        Require(rejected.Result == UIConfirmResult.RequestCancelled && rejectedRoot == null, "Pre-cancelled request leaked its instance");
                    }
                    var invalidRoot = new GameObject("InvalidOwnerModal", typeof(RectTransform));
                    var invalid = manager.ConfirmAsync(new UIPageDefinition { SceneInstance = invalidRoot }, new UIView<UIEmptyArgs>(), default);
                    Require(invalid.Result == UIConfirmResult.OwnerClosed && invalidRoot == null, "Invalid owner leaked its instance");
                    GameObject busyRoot = null;
                    System.Threading.Tasks.Task<UIConfirmResult> busyResult = null;
                    av.SuspendView = () => {
                        busyRoot = new GameObject("BusyModal", typeof(RectTransform));
                        busyResult = manager.ConfirmAsync(new UIPageDefinition { SceneInstance = busyRoot }, new UIView<UIEmptyArgs>(), first.Handle);
                    };
                    manager.Open(settings, 1, scope);
                    Require(busyResult != null && busyResult.IsCompleted && busyResult.Result == UIConfirmResult.Failed && busyRoot == null,
                        "Busy request leaked its instance or swallowed cancellation");
                    Require(manager.PendingConfirmations == 0, "Rejected request remained registered");
                    break;
                case "CancellationDuringNestedOpen":
                    using (var cancellation = new CancellationTokenSource())
                    {
                        var lower = new UIPageDefinition { SceneInstance = new GameObject("LowerModal", typeof(RectTransform)) };
                        var lowerTask = manager.ConfirmAsync(lower, new UIView<UIEmptyArgs> { SuspendView = cancellation.Cancel }, first.Handle, cancellation.Token);
                        manager.TryFind(new UIRoute<UIEmptyArgs>(lower.Id), scope, out UIHandle lowerHandle);
                        var nestedDefinition = new UIPageDefinition { SceneInstance = new GameObject("UpperModal", typeof(RectTransform)) };
                        var upperTask = manager.ConfirmAsync(nestedDefinition, new UIView<UIEmptyArgs>(), lowerHandle);
                        Require(lowerTask.IsCompleted && lowerTask.Result == UIConfirmResult.RequestCancelled, "Busy cancellation was lost");
                        Require(upperTask.IsCompleted && upperTask.Result == UIConfirmResult.OwnerClosed, "Nested open lost its owner without finishing");
                        Require(manager.PendingConfirmations == 0 && manager.IsFocused(first.Handle), "Cancelled modal left orphan state");
                    }
                    break;
                case "LeaseOwnershipAndRepeatedCycles":
                    using (var other = TowerRunGateHub.AcquireGameplay("test.other.owner"))
                    {
                        manager.Open(settings, 1, scope); manager.Back();
                        Require(TowerRunGateHub.IsGameplayBlocked, "Settings released another owner's lease");
                        manager.Close(first.Handle);
                        Require(TowerRunGateHub.IsGameplayBlocked, "Window released another owner's lease");
                    }
                    Require(!TowerRunGateHub.IsGameplayBlocked, "Owned leases leaked");
                    for (int i = 0; i < 50; i++) { var r = manager.Open(a, i, scope); manager.Close(r.Handle); }
                    Require(manager.OpenCount == 0 && opens == closes && !TowerRunGateHub.IsGameplayBlocked, "Repeated cycles accumulate state");
                    break;
                case "ReentrancyAndCloseFailure":
                    av.CloseView = _ => { closes++; throw new Exception("Injected close failure"); };
                    Require(manager.Close(first.Handle) == UICloseStatus.Failed && manager.OpenCount == 0 && !TowerRunGateHub.IsGameplayBlocked, "Close exception leaked resources");
                    av.CloseView = _ => closes++;
                    av.OpenView = _ => Require(manager.Open(settings, 1, scope).Status == UIOpenStatus.Busy, "Reentrant open was not rejected");
                    Require(manager.Open(a, 1, scope).Succeeded, "Manager cannot recover after close failure");
                    break;
                case "PrefabCachePoliciesAndInactiveCreation":
                    var prefab = new GameObject("PrefabTemplate", typeof(RectTransform));
                    prefab.transform.SetParent(canvasObject.transform, false);
                    var transient = new UIRoute<int>("test.transient.prefab");
                    int createdViews = 0, initializedViews = 0, disposedViews = 0;
                    var roots = new List<GameObject>();
                    manager.Register(transient, new UIPageDefinition { Id = transient.Id, Prefab = prefab, Cache = false, Layer = UILayer.System },
                        instance => {
                            Require(!instance.activeInHierarchy, "Prefab activated before registration");
                            createdViews++; roots.Add(instance);
                            return new UIView<int> { InitializeView = () => initializedViews++, DisposeView = () => disposedViews++ };
                        });
                    var transientFirst = manager.Open(transient, 1, scope);
                    manager.Close(transientFirst.Handle);
                    Require(roots[0] == null && disposedViews == 1, "Released prefab instance remained alive");
                    var transientSecond = manager.Open(transient, 2, scope);
                    Require(transientSecond.Succeeded && createdViews == 2 && initializedViews == 2, "Released prefab route cannot reopen with a fresh view");
                    Require(manager.Close(transientFirst.Handle) == UICloseStatus.StaleHandle && manager.IsFocused(transientSecond.Handle), "Old prefab handle closed its replacement");
                    manager.Close(transientSecond.Handle);
                    var occupied = new UIRoute<int>("test.occupied.prefab");
                    int occupiedCreates = 0;
                    manager.Register(occupied, new UIPageDefinition { Id = occupied.Id, Prefab = prefab, Cache = false,
                        Layer = UILayer.Window, Workspace = "primary" }, instance => { occupiedCreates++; return new UIView<int>(); });
                    var occupiedFirst = manager.Open(occupied, 1, scope, replaceWorkspace: true);
                    manager.Close(occupiedFirst.Handle);
                    manager.Open(a, 1, scope);
                    Require(manager.Open(occupied, 2, scope).Status == UIOpenStatus.Rejected && occupiedCreates == 1,
                        "Rejected workspace request created an unneeded prefab instance");
                    var cached = new UIRoute<int>("test.cached.prefab");
                    int cacheCreates = 0, cacheInitializes = 0;
                    manager.Register(cached, new UIPageDefinition { Id = cached.Id, Prefab = prefab, Cache = true, Layer = UILayer.System },
                        instance => { cacheCreates++; return new UIView<int> { InitializeView = () => cacheInitializes++ }; });
                    var cacheFirst = manager.Open(cached, 1, scope); manager.Close(cacheFirst.Handle);
                    var cacheSecond = manager.Open(cached, 2, scope);
                    Require(cacheSecond.Succeeded && cacheCreates == 1 && cacheInitializes == 1, "Cached prefab was recreated or initialized twice");
                    break;
                default: throw new ArgumentException(name);
            }
        }
        finally { manager.Dispose(); UnityEngine.Object.DestroyImmediate(canvasObject); }
    }

    public static void RunBatch()
    {
        string output = Environment.GetEnvironmentVariable("RPG_UI_CHECK_OUTPUT");
        var names = new[] { "IdempotencyAndStaleHandles", "WorkspaceReplacementRollsBack", "LayersDimmerAndCoverage", "RejectedBackIsConsumed",
            "NestedConfirmAndOwnerCleanup", "CancellationAndSceneUnload", "ConfirmationRejectionsDestroyRequestInstances",
            "CancellationDuringNestedOpen", "LeaseOwnershipAndRepeatedCycles", "ReentrancyAndCloseFailure", "PrefabCachePoliciesAndInactiveCreation", "ExplicitContextReplacementAndRollback", "ConfirmationCloseFailureCannotConfirmBusiness", "TransparentOverlayStillBlocksInput" };
        var results = new List<string>();
        try
        {
            foreach (string name in names) { RunCase(name); results.Add(name); }
            File.WriteAllText(output, JsonUtility.ToJson(new CheckReport { passed = results.ToArray() }, true));
            UnityEditor.EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText(output, JsonUtility.ToJson(new CheckReport { passed = results.ToArray(), error = error.ToString() }, true));
            UnityEditor.EditorApplication.Exit(2);
        }
    }
    [Serializable] private sealed class CheckReport { public string[] passed; public string error; }
}
#endif

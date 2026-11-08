#if UNITY_EDITOR || RPG_SHOP_INTEGRATION_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed partial class MapVNextI1Acceptance
{
    [Serializable] private sealed class UIArchitectureEvidence
    {
        public string status = "RUNNING", buildId, sourceFingerprint, slotRoot, inputScope;
        public int width, height;
        public List<string> checks = new List<string>();
        public List<UIFrameEvidence> frames = new List<UIFrameEvidence>();
    }
    [Serializable] private sealed class UIFrameEvidence
    { public string file, state; public int frame, width, height; public float time; }
    private UIArchitectureEvidence uiEvidence;
    private sealed class UIScrollFixture : IDisposable
    {
        private readonly ScrollRect scroll;
        private readonly LayoutElement sizing;
        private readonly bool created;
        private readonly float previousMinimum, previousPosition;
        internal UIScrollFixture(ScrollRect scroll)
        {
            this.scroll = scroll;
            previousPosition = scroll.verticalNormalizedPosition;
            sizing = scroll.content.GetComponent<LayoutElement>();
            if (sizing == null) { sizing = scroll.content.gameObject.AddComponent<LayoutElement>(); created = true; }
            previousMinimum = sizing.minHeight;
            sizing.minHeight = Mathf.Max(1, scroll.viewport.rect.height * 2);
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = .37f;
            Canvas.ForceUpdateCanvases();
        }
        public void Dispose()
        {
            sizing.minHeight = previousMinimum;
            if (created) UnityEngine.Object.Destroy(sizing);
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = previousPosition;
        }
    }
    private void PersistUI() => File.WriteAllText(Path.Combine(output, "ui-architecture.json"), JsonUtility.ToJson(uiEvidence, true));
    private void CheckUI(bool condition, string message)
    { Require(condition, message); uiEvidence.checks.Add(message); PersistUI(); }
    private void CheckVisibleBounds(GameObject view, string label)
    {
        Canvas.ForceUpdateCanvases();
        var corners = new Vector3[4];
        int inspected = 0;
        foreach (Graphic graphic in view.GetComponentsInChildren<Graphic>())
        {
            // Retired transparent backgrounds still have RectTransforms, but draw no visible content.
            if (!graphic.enabled || graphic.canvasRenderer.cull || graphic.color.a <= 0.001f ||
                graphic.canvasRenderer.GetInheritedAlpha() <= 0.001f) continue;
            inspected++;
            graphic.rectTransform.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(null, corner);
                Require(point.x >= -2 && point.y >= -2 && point.x <= Screen.width + 2 && point.y <= Screen.height + 2,
                    "VisibleGraphicOutsideScreen:" + label + ":" + graphic.name + ":" + point);
            }
        }
        CheckUI(inspected > 0, label + " has visible graphics and they fit the screen bounds");
    }
    private static Button ActiveUIButton(string name)
        => FindObjectsOfType<Button>().Single(b => b.name == name && b.IsInteractable());
    private static IEnumerator WaitForUILayout()
    {
        // Player screenshots resume at end-of-frame. Let both layout and RectMask2D culling finish before the next hit test.
        yield return null;
        if (!Application.isBatchMode) yield return new WaitForEndOfFrame();
        yield return null;
        Canvas.ForceUpdateCanvases();
    }
    private void ClickUI(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)button.transform;
        Vector2 position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        Require(position.x >= 0 && position.y >= 0 && position.x < Screen.width && position.y < Screen.height,
            "UIButtonOutsideScreen:" + button.name + ":" + position);
        var pointer = new PointerEventData(EventSystem.current) { position = position, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button,
            "UIButtonOccluded:" + button.name + ":" + (hits.Count == 0 ? "no hit" : hits[0].gameObject.name));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }
    private IEnumerator UIFrame(string name, UIRoot root)
    {
        if (Application.isBatchMode) yield break;
        Canvas.ForceUpdateCanvases();
        yield return new WaitForEndOfFrame();
        string file = "UI/" + name + ".png";
        Directory.CreateDirectory(Path.Combine(output, "UI"));
        Texture2D picture = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(output, file), picture.EncodeToPNG()); Destroy(picture);
        uiEvidence.frames.Add(new UIFrameEvidence { file = file, frame = Time.frameCount, time = Time.realtimeSinceStartup,
            width = Screen.width, height = Screen.height, state = root.Manager.DebugSnapshot() });
        PersistUI();
    }
    public IEnumerator RunUIArchitectureEditorTest(string directory)
    {
        yield return RunShopEditorTestCore(directory, true);
        BindInput();
        try { yield return ProveUIArchitecture(); }
        finally
        {
            try { session?.ReturnToStartupChoice(); }
            finally { if (keyboard != null) CleanupInput(); }
        }
    }
    private IEnumerator ProveUIArchitecture()
    {
        BindShopPointer();
        var root = FindObjectOfType<UIRoot>();
        var views = FindObjectOfType<TowerRunUiViews>();
        var shop = FindObjectOfType<ShopPanelController>(true);
        var grid = FindObjectOfType<ShopGridUI>(true);
        var package = FindObjectOfType<PackagePanelController>(true);
        var settingsButton = PrivateField<Button>(root, "settingsButton");
        var merchant = FindObjectsOfType<MerchantInteractionController>().First(m => m.BoundPlayer == body.transform);
        uiEvidence = new UIArchitectureEvidence { buildId = MapHostBuildStamp.BuildId,
            sourceFingerprint = Environment.GetEnvironmentVariable("RPG_I1_SOURCE_FINGERPRINT"),
            slotRoot = TowerRunRuntimeEnvironment.SlotRoot, width = Screen.width, height = Screen.height,
            inputScope = "Explicit fixture positioning via original SetupFoot; queued keyboard -> GameInput/F; real EventSystem raycast -> pointer click. Not a physical mouse/keyboard or human acceptance claim." };
        PersistUI();
        CheckUI(session.State == TowerRunFlowState.Playing && root.Manager.OpenCount == 0, "Playing starts with no managed window");
        yield return SetupFoot((Vector2)merchant.transform.position + Vector2.down * .85f, "UI_ARCHITECTURE_PROXIMITY_FIXTURE");
        yield return new WaitForFixedUpdate(); yield return ShopKey(Key.F);
        CheckUI(shop.IsShopPanelVisible && shop.CurrentMerchant == merchant, "Original F interaction opens the managed shop");
        CheckUI(!TowerRunGateHub.IsWorldPaused && !GameInput.Instance.IsGameplayInputEnabled, "Shop blocks gameplay without pausing the world");
        yield return WaitForUILayout();
        CheckVisibleBounds(PrivateField<GameObject>(root, "hudRoot"), "HUD");
        CheckVisibleBounds(root.Layers.SystemActions.gameObject, "SystemActions");
        CheckVisibleBounds(shop.PanelRoot, "Shop");
        yield return VerifyTransparentBlocker(root);
        var food = FindObjectsOfType<Toggle>().Single(t => t.name == "FoodTab"); food.isOn = true;
        yield return UIFrame("01-shop-food", root);
        var category = FindObjectsOfType<Toggle>().Single(t => t.name == "WeaponTab"); category.isOn = true;
        EventSystem.current.SetSelectedGameObject(category.gameObject);
        var scroll = PrivateField<Transform>(grid, "contentRoot").GetComponentInParent<ScrollRect>();
        using (var scrolling = new UIScrollFixture(scroll))
        {
        float position = scroll.verticalNormalizedPosition;
        CheckUI(position > .1f && position < .9f && PrivateField<ShopProductCategoryType>(grid, "currentCategory") == ShopProductCategoryType.Weapon,
            "Non-default category and non-edge scroll fixture are established; added blank UI content space only, catalog/trade unchanged");
        var currentMerchant = shop.CurrentMerchant;
        yield return UIFrame("01-shop", root);
        ClickUI(settingsButton); yield return null;
        CheckUI(root.Manager.TryFind(UIRoutes.Settings, root.Scope, out UIHandle settingsHandle), "System action remains reachable above the shop");
        CheckUI(shop.IsShopPanelVisible && !shop.PanelRoot.GetComponent<CanvasGroup>().interactable, "Covered shop remains open and cannot receive input");
        CheckUI(Mathf.Approximately(root.Layers.DimAlpha, .5f), "Settings uses exactly its single dimmer policy");
        CheckUI(FindObjectsOfType<Image>(true).Where(i => i.name == "GreyBg").All(i => i.color.a == 0 && !i.raycastTarget), "Legacy full-screen dimmers and raycast targets are retired");
        CheckUI(TowerRunGateHub.IsGameplayBlocked && TowerRunGateHub.IsWorldPaused, "Settings owns pause while the shop keeps its gameplay lease");
        yield return UIFrame("02-settings-over-shop", root);
        ClickUI(ActiveUIButton("SaveTab")); yield return WaitForUILayout();
        CheckUI(views.savePage.activeInHierarchy && !views.audioPage.activeInHierarchy, "Save tab activates the real save page before pointer input");
        yield return UIFrame("02b-save-page", root);
        ClickUI(ActiveUIButton("BeginSaveRequest")); yield return WaitForUILayout();
        ClickUI(ActiveUIButton("SaveSlot1")); yield return null;
        CheckUI(root.Manager.PendingConfirmations == 1 && Mathf.Approximately(root.Layers.DimAlpha, .55f), "Overwrite modal owns the only dimmer");
        yield return UIFrame("03-overwrite-modal", root);
        var modal = FindObjectsOfType<UIConfirmationDialog>().Single();
        var modalHandle = root.Manager.Top.Handle;
        var nested = UIConfirmationDialog.ShowAsync(root, modalHandle, views.overwriteDialog,
            "嵌套弹窗验证：本弹窗不写入存档。\nEsc 应只关闭这一层，再回到覆盖确认。");
        yield return null;
        CheckUI(root.Manager.PendingConfirmations == 2, "Real nested dialog is owned by the overwrite confirmation");
        yield return UIFrame("03b-nested-modal", root);
        yield return ShopKey(Key.Escape);
        CheckUI(nested.IsCompleted && nested.Result == UIConfirmResult.Cancelled && root.Manager.PendingConfirmations == 1 && modal != null,
            "One Escape cancels only the upper real dialog");
        yield return ShopKey(Key.Escape);
        CheckUI(root.Manager.PendingConfirmations == 0 && root.Manager.TryFind(UIRoutes.Settings, root.Scope, out _), "First Escape cancels only the modal");
        yield return ShopKey(Key.Escape);
        CheckUI(shop.IsShopPanelVisible && shop.CurrentMerchant == currentMerchant && category.isOn &&
            PrivateField<ShopProductCategoryType>(grid, "currentCategory") == ShopProductCategoryType.Weapon &&
            Mathf.Approximately(scroll.verticalNormalizedPosition, position), "Returning preserves merchant category and scroll position");
        CheckUI(EventSystem.current.currentSelectedGameObject == category.gameObject, "Returning restores the valid originating selection");
        CheckUI(!TowerRunGateHub.IsWorldPaused && TowerRunGateHub.IsGameplayBlocked, "Closing settings releases only its own leases");
        yield return UIFrame("04-shop-restored", root);
        }
        food.isOn = true;
        yield return ShopKey(Key.Escape);
        CheckUI(!shop.IsShopPanelVisible && GameInput.Instance.IsGameplayInputEnabled, "Closing shop restores gameplay");
        yield return VerifyUISavePaths(root, settingsButton, views);
        yield return ShopKey(Key.Tab);
        var packageGrid = FindObjectOfType<PackageGridUI>(true);
        var packageSlot = FindObjectsOfType<PackageSlotUI>().First();
        packageSlot.OnPointerClick(new PointerEventData(EventSystem.current));
        var selected = PrivateField<PackageSlotUI>(packageGrid, "currentSelectedSlot");
        yield return UIFrame("05-package", root);
        CheckVisibleBounds(PrivateField<GameObject>(package, "packagePanel"), "Package");
        ClickUI(settingsButton); yield return null;
        CheckUI(PrivateField<PackageSlotUI>(packageGrid, "currentTooltipSlot") == null, "Covering the package clears only hover tooltip");
        yield return UIFrame("06-settings-over-package", root);
        yield return ShopKey(Key.Escape);
        CheckUI(package.IsPackagePanelVisible && PrivateField<PackageSlotUI>(packageGrid, "currentSelectedSlot") == selected,
            "Returning preserves package selection");
        yield return ShopKey(Key.Tab);
        int cameras = FindObjectsOfType<Camera>(true).Length;
        int textures = Resources.FindObjectsOfTypeAll<RenderTexture>().Length;
        for (int i = 0; i < 10; i++)
        {
            yield return ShopKey(Key.Tab);
            CheckUI(package.IsPackagePanelVisible && root.Manager.OpenCount == 1, "Repeated cycle " + i + " actually opens the package");
            yield return ShopKey(Key.Tab);
            CheckUI(!package.IsPackagePanelVisible && root.Manager.OpenCount == 0, "Repeated cycle " + i + " actually closes the package");
        }
        CheckUI(root.Manager.OpenCount == 0 && root.Manager.PendingConfirmations == 0 &&
            FindObjectsOfType<Camera>(true).Length == cameras && Resources.FindObjectsOfTypeAll<RenderTexture>().Length == textures &&
            FindObjectsOfType<ShopPurchaseFlyEffect>(true).All(f => f.ActiveIconCount == 0), "Repeated cycles leave no accumulated entries waits RTs cameras or temporary effects");
        // Source invalidation exercises a real interaction component; restore the fixture even on failure.
        yield return SetupFoot((Vector2)merchant.transform.position + Vector2.down * .85f, "UI_OWNER_INVALIDATION_FIXTURE");
        yield return new WaitForFixedUpdate(); yield return ShopKey(Key.F);
        CheckUI(shop.IsShopPanelVisible && shop.CurrentMerchant == merchant, "F actually reopens the correct shop before source invalidation");
        try
        {
            merchant.enabled = false; yield return null; yield return null;
            CheckUI(!shop.IsShopPanelVisible && shop.CurrentMerchant == null &&
                shop.GetComponent<ShopMerchantPresentationUI>().SourceMerchant == null && !TowerRunGateHub.IsGameplayBlocked,
                "Invalid merchant context closes its owned window and releases cached presentation context");
        }
        finally { merchant.enabled = true; }
        yield return UIFrame("07-clean-hud", root);
        yield return SetupFoot((Vector2)merchant.transform.position + Vector2.down * .85f, "UI_RECORDING_FIXTURE");
        yield return new WaitForFixedUpdate(); yield return ShopKey(Key.F);
        CheckUI(shop.IsShopPanelVisible && shop.CurrentMerchant == merchant, "Recording and death cleanup begin with the actual shop open");
        if (!Application.isBatchMode)
            for (int frame = 0; frame < 60; frame++)
            {
                if (frame == 15) ClickUI(settingsButton);
                if (frame == 40) root.Manager.Back();
                yield return null;
                yield return UIFrame("Sequence-" + frame.ToString("D3"), root);
            }
        // Exercise the real defeat event while a workspace, settings and descendant modal are alive.
        if (!root.Manager.TryFind(UIRoutes.Settings, root.Scope, out var deathOwner))
        { ClickUI(settingsButton); root.Manager.TryFind(UIRoutes.Settings, root.Scope, out deathOwner); }
        CheckUI(shop.IsShopPanelVisible && deathOwner.IsValid, "Death fixture contains the retained workspace and settings owner");
        var deathWait = UIConfirmationDialog.ShowAsync(root, deathOwner, views.overwriteDialog, "场景流程清理验证，不写存档。");
        CheckUI(root.Manager.PendingConfirmations == 1 && !deathWait.IsCompleted, "Death fixture contains an actually pending descendant modal");
        CheckUI(body.GetComponent<PlayerDamageController>().TakeDamage(float.MaxValue, null), "Real player damage triggers the defeat event");
        yield return null;
        CheckUI(session.State == TowerRunFlowState.DeathChoice && !shop.IsShopPanelVisible &&
            !package.IsPackagePanelVisible && root.Manager.PendingConfirmations == 0 && deathWait.IsCompleted &&
            deathWait.Result == UIConfirmResult.OwnerClosed && root.Manager.Top.Definition.Id == UIRoutes.Flow.Id,
            "Death flow closes workspaces settings and descendant confirmations through original coordinator");
        CheckUI(EventSystem.current.currentSelectedGameObject == FindObjectOfType<TowerRunUiController>().PrimaryButton.gameObject,
            "Death flow focuses the active primary action");
        yield return UIFrame("08-death-flow", root);
        session.BeginRestoreSlot(3, "deliberately-empty-private-slot"); yield return null;
        CheckUI(session.State == TowerRunFlowState.Failure && root.Manager.Back() == UIBackResult.Rejected &&
            EventSystem.current.currentSelectedGameObject != null &&
            EventSystem.current.currentSelectedGameObject.GetComponent<Selectable>().IsInteractable(),
            "Original empty-slot restore produces a forced failure page with legal fallback focus and consumed Back");
        yield return UIFrame("09-failure-flow", root);
        session.ReturnToStartupChoice(); yield return null;
        CheckUI(session.State == TowerRunFlowState.StartupChoice && shop.CurrentMerchant == null &&
            root.Manager.PendingConfirmations == 0, "Returning to startup never restores a retired merchant context");
        yield return UIFrame("10-startup-flow", root);
        uiEvidence.status = "AUTOMATED_UI_LAYER_AND_RETURN_VERIFIED_HUMAN_PENDING"; PersistUI();
    }

    private IEnumerator VerifyTransparentBlocker(UIRoot root)
    {
        root.Manager.TryFind(UIRoutes.Shop, root.Scope, out UIHandle origin);
        int registered = root.Manager.RegisteredCount;
        var route = new UIRoute<UIEmptyArgs>("diagnostic.transparent.blocker");
        var temporary = new GameObject("TransparentBlockerFixture", typeof(RectTransform));
        root.Manager.Register(route, new UIPageDefinition { Id = route.Id, SceneInstance = temporary,
            Layer = UILayer.Modal, DimAlpha = 0, Cache = false }, new UIView<UIEmptyArgs>());
        var opened = root.Manager.Open(route, new UIEmptyArgs(), root.Scope, origin);
        CheckUI(opened.Succeeded && root.Layers.BlockerActive && root.Layers.DimAlpha == 0, "Transparent overlay retains its real full-screen blocker");
        yield return WaitForUILayout();
        var pointer = new PointerEventData(EventSystem.current) { position = new Vector2(8, 8) };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        CheckUI(hits.Count > 0 && hits[0].gameObject.name == "ManagedScrim", "Zero-alpha blocker is the actual first EventSystem raycast hit outside modal content");
        root.Manager.Close(opened.Handle); yield return null;
        CheckUI(root.Manager.IsFocused(origin) && root.Manager.RegisteredCount == registered && temporary == null,
            "Transparent fixture closes with no temporary instance and restores the same originating handle");
    }

    private IEnumerator VerifyUISavePaths(UIRoot root, Button settingsButton, TowerRunUiViews views)
    {
        string path = Path.Combine(TowerRunRuntimeEnvironment.SlotRoot, "tower-run-slot-1.json");
        string before = MapHostCompatibility.Sha(File.ReadAllBytes(path));
        ClickUI(settingsButton); yield return null;
        ClickUI(ActiveUIButton("SaveTab")); yield return WaitForUILayout();
        ClickUI(ActiveUIButton("BeginSaveRequest")); yield return WaitForUILayout();
        ClickUI(ActiveUIButton("SaveSlot1"));
        yield return null;
        ClickUI(ActiveUIButton("ConfirmOverwrite")); yield return null; yield return null;
        CheckUI(root.Manager.PendingConfirmations == 0 && views.beginSaveButton.gameObject.activeSelf &&
            session.ReadSlot(1).state == TowerRunSlotState.Valid && MapHostCompatibility.Sha(File.ReadAllBytes(path)) != before &&
            views.saveMessage.text.Contains("成功"), "Confirmed overwrite commits once through the original private slot store");
        string saved = MapHostCompatibility.Sha(File.ReadAllBytes(path));
        ClickUI(ActiveUIButton("BeginSaveRequest")); yield return WaitForUILayout();
        ClickUI(ActiveUIButton("SaveSlot1")); yield return null;
        string priorMessage = views.saveMessage.text;
        // Permit the authority to read its original record; deny native replacement for this private test only.
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            ClickUI(ActiveUIButton("ConfirmOverwrite")); yield return null; yield return null;
            CheckUI(root.Manager.PendingConfirmations == 0 && !views.beginSaveButton.gameObject.activeSelf &&
                views.saveMessage.text != priorMessage && !views.saveMessage.text.Contains("成功") &&
                MapHostCompatibility.Sha(File.ReadAllBytes(path)) == saved,
                "A real private-file replacement failure keeps the old record and leaves no pending confirmation: " + views.saveMessage.text);
            yield return UIFrame("04b-save-io-failure", root);
        }
        yield return ShopKey(Key.Escape);
    }
}
#endif

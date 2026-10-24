using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class TowerRunFlowPlayModeTests
{
    private string testRoot;
    private Keyboard testKeyboard;
    private InputSettings.BackgroundBehavior previousBackgroundBehavior;
    private InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior;

    [SetUp]
    public void SetUp()
    {
        previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
        previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        testRoot = Path.Combine(
            Path.GetTempPath(),
            "RPG_Demo-TowerRunFlowTests",
            System.Guid.NewGuid().ToString("N"));
        TowerRunRuntimeEnvironment.ConfigureTestIsolation(testRoot);
        testKeyboard = InputSystem.AddDevice<Keyboard>("TowerRunFlowTestKeyboard");
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (testKeyboard != null && testKeyboard.added)
                InputSystem.RemoveDevice(testKeyboard);

            TowerRunRuntimeEnvironment.ResetOverrides();

            if (Directory.Exists(testRoot))
                Directory.Delete(testRoot, true);
        }
        finally
        {
            InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
        }
    }

    [UnityTest]
    public IEnumerator TutorialToFloorTwoRequiresTargetsAndActualInteractInput()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        yield return null;

        MonoBehaviour coordinator = FindBehaviour("TowerRunCoordinator");
        MonoBehaviour floor = FindBehaviour("TowerFloorRuntimeController");
        Assert.That(coordinator, Is.Not.Null);
        Assert.That(floor, Is.Not.Null);
        AssertIsolatedSlotRoot(coordinator);
        Invoke(coordinator, "ReturnToStartupChoice");
        Invoke(coordinator, "BeginNewRun");
        yield return null;

        GameObject runtimeRoot = FindRootObject(SceneManager.GetActiveScene(), "RandomMapRuntime");
        MapRuntimeBootstrap bootstrap = runtimeRoot.GetComponent<MapRuntimeBootstrap>();
        MapGenerationController controller =
            runtimeRoot.GetComponentInChildren<MapGenerationController>(true);
        MapMinimapController minimap =
            runtimeRoot.GetComponentInChildren<MapMinimapController>(true);

        AssertPlaying(coordinator, "新手关卡");
        AssertFreshSpawnExploration(minimap);
        Assert.That(GetProperty(floor, "State").ToString(), Is.EqualTo("Locked"));
        Assert.That((int)GetProperty(floor, "TargetCount"), Is.GreaterThan(0));
        Assert.That(minimap.IsExitAvailable, Is.False);
        Assert.That(GetPortal(floor).activeSelf, Is.False);

        DefeatFirstRole(bootstrap.Context, DestructiblePlacementService.DestructibleRole);
        yield return null;
        Assert.That(GetProperty(floor, "State").ToString(), Is.EqualTo("Locked"),
            "A non-target Destructible must not complete a combat floor.");
        Assert.That(minimap.IsExitAvailable, Is.False);

        GameObject tutorialTarget = DefeatAllRoleTargets(
            bootstrap.Context,
            MonsterPlacementService.MonsterRole);
        yield return null;
        Assert.That(GetProperty(floor, "State").ToString(), Is.EqualTo("Available"));
        Assert.That(minimap.IsExitAvailable, Is.True);
        GameObject tutorialPortal = GetPortal(floor);
        Assert.That(tutorialPortal.activeInHierarchy, Is.True);

        System.Guid tutorialGeneration = bootstrap.Context.ActiveGenerationId.Value;
        controller.Player.position = tutorialPortal.transform.position;
        MonoBehaviour runtimeInventory = FindBehaviour("PlayerInventory");
        Assert.That(runtimeInventory, Is.Not.Null);
        SetPrivateField(coordinator, "inventory", null);
        LogAssert.Expect(LogType.Error, new Regex("换层请求未被接受"));
        PressInteract();
        yield return null;
        AssertPlaying(coordinator, "新手关卡");
        Assert.That(GetProperty(floor, "State").ToString(), Is.EqualTo("Available"));
        Assert.That(tutorialPortal.activeInHierarchy, Is.True,
            "An exit request must remain unaccepted when carry capture cannot complete.");
        SetPrivateField(coordinator, "inventory", runtimeInventory);

        PressInteract();
        yield return null;

        AssertPlaying(coordinator, "第 1 层");
        AssertFreshSpawnExploration(minimap);
        Assert.That(bootstrap.Context.ActiveGenerationId, Is.Not.EqualTo(tutorialGeneration));
        Assert.That(GetProperty(floor, "State").ToString(), Is.EqualTo("Locked"));
        Assert.That(minimap.IsExitAvailable, Is.False);
        Assert.That(minimap.TrySetExitAvailable(tutorialGeneration, true), Is.False,
            "A stale generation callback must not unlock the current minimap exit.");

        PressInteract();
        yield return null;
        AssertPlaying(coordinator, "第 1 层");

        yield return new WaitForSecondsRealtime(0.65f);
        foreach (MapObjectRegistryEntry entry in bootstrap.Context.ActiveRegistry.Entries)
        {
            if (entry.Role != MonsterPlacementService.MonsterRole)
                continue;
            GameObject currentTarget = ResolveGameObject(entry.Instance);
            Assert.That(currentTarget?.activeInHierarchy, Is.True,
                "A previous generation must not retire a current Monster.");
            Assert.That(GetBoolProperty(currentTarget, "BaseDamageable", "IsDefeated"), Is.False);
        }

        DefeatAllRoleTargets(bootstrap.Context, MonsterPlacementService.MonsterRole);
        yield return null;
        GameObject firstFloorPortal = GetPortal(floor);
        Assert.That(GetProperty(floor, "State").ToString(), Is.EqualTo("Available"));
        Assert.That(minimap.IsExitAvailable, Is.True);
        controller.Player.position = firstFloorPortal.transform.position;
        PressInteract();
        yield return null;

        AssertPlaying(coordinator, "第 2 层");
        AssertFreshSpawnExploration(minimap);
        Assert.That(GetProperty(floor, "State").ToString(), Is.EqualTo("Locked"));
        Assert.That(minimap.IsExitAvailable, Is.False);
    }

    [UnityTest]
    public IEnumerator SettingsSkinUsesSceneSpritesAndKeepsOverwriteModalIsolated()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        yield return null;

        MonoBehaviour coordinator = FindBehaviour("TowerRunCoordinator");
        MonoBehaviour runUi = FindBehaviour("TowerRunUiController");
        MonoBehaviour skin = FindBehaviour("TowerRunUiSkin");
        Assert.That(skin, Is.Not.Null,
            $"Scene={runUi.gameObject.scene.path}, UI object={runUi.gameObject.name}");
        Assert.That(skin.gameObject, Is.SameAs(runUi.gameObject));
        Assert.That((Sprite)GetProperty(skin, "WoodFrame"), Is.Not.Null);
        Sprite carvedPaper = (Sprite)GetProperty(skin, "CarvedPaper");
        Sprite carvedCard = (Sprite)GetProperty(skin, "CarvedCard");
        Sprite blueButton = (Sprite)GetProperty(skin, "BlueButton");
        Assert.That(carvedPaper, Is.Not.Null);
        Assert.That(carvedCard, Is.Not.Null);
        Assert.That((Sprite)GetProperty(skin, "TitleBanner"), Is.Not.Null);
        Assert.That((Sprite)GetProperty(skin, "BlueRibbon"), Is.Not.Null);
        Assert.That(blueButton, Is.Not.Null);
        Assert.That((Sprite)GetProperty(skin, "RedButton"), Is.Not.Null);
        Assert.That((Sprite)GetProperty(skin, "DisabledButton"), Is.Not.Null);
        Assert.That(carvedPaper.border, Is.Not.EqualTo(Vector4.zero));
        Assert.That(carvedCard.border, Is.Not.EqualTo(Vector4.zero));
        Assert.That(blueButton.border, Is.Not.EqualTo(Vector4.zero));
        Assert.That(blueButton.texture.filterMode, Is.EqualTo(FilterMode.Point));

        Invoke(coordinator, "ReturnToStartupChoice");
        Invoke(coordinator, "BeginNewRun");
        yield return null;
        Invoke(runUi, "OpenSettings");
        Assert.That(GetPrivateField<GameObject>(runUi, "settingsOverlay").activeSelf, Is.True);
        RectTransform frame = GetPrivateField<RectTransform>(runUi, "settingsFrame");
        RectTransform canvas = runUi.GetComponent<RectTransform>();
        Canvas.ForceUpdateCanvases();
        Assert.That(frame.rect.width, Is.LessThanOrEqualTo(canvas.rect.width));
        Assert.That(frame.rect.height, Is.LessThanOrEqualTo(canvas.rect.height));
        Assert.That(frame.GetComponent<Image>().type, Is.EqualTo(Image.Type.Sliced));
        RectTransform tabs = (RectTransform)frame.Find(
            "SettingsCarvedPaper/SettingsLayout/SettingsTabs");
        Assert.That(tabs, Is.Not.Null);
        Assert.That(tabs.rect.height, Is.LessThanOrEqualTo(64f),
            "Tabs must retain their compact hit region instead of consuming the save-page height.");

        InvokePrivate(runUi, "ShowSavePage");
        Canvas.ForceUpdateCanvases();
        Assert.That(GetPrivateField<GameObject>(runUi, "savePage").activeSelf, Is.True);
        Assert.That(GetPrivateField<GameObject>(runUi, "audioPage").activeSelf, Is.False);
        RectTransform lastSlot = (RectTransform)frame.Find(
            "SettingsCarvedPaper/SettingsLayout/SavePage/SaveViewport/SaveContent/SaveSlot3");
        Assert.That(lastSlot.GetComponent<Image>().sprite, Is.SameAs(carvedCard));
        RectTransform close = (RectTransform)frame.Find(
            "SettingsCarvedPaper/SettingsLayout/CloseSettings");
        Vector3[] slotCorners = new Vector3[4];
        Vector3[] closeCorners = new Vector3[4];
        lastSlot.GetWorldCorners(slotCorners);
        close.GetWorldCorners(closeCorners);
        Assert.That(slotCorners[0].y, Is.GreaterThan(closeCorners[1].y),
            "All three slot cards must remain above the Close button.");
        ScrollRect saveScroll = GetPrivateField<GameObject>(runUi, "savePage").GetComponent<ScrollRect>();
        Assert.That(saveScroll, Is.Not.Null);
        Assert.That(saveScroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
        Text message = GetPrivateField<Text>(runUi, "saveMessage");
        message.text = new string('长', 300);
        Canvas.ForceUpdateCanvases();
        Assert.That(saveScroll.content.rect.height, Is.GreaterThan(saveScroll.viewport.rect.height),
            "Long save messages must scroll inside the page instead of covering cards or Close.");
        InvokePrivate(runUi, "ShowSavePage");
        InvokePrivate(runUi, "BeginSaveRequest");
        InvokePrivate(runUi, "SelectSaveSlot", 1);
        TowerRunSlotReadResult saved = (TowerRunSlotReadResult)Invoke(coordinator, "ReadSlot", 1);
        Assert.That(saved.state, Is.EqualTo(TowerRunSlotState.Valid), saved.message);
        string savedAtUtc = saved.summary.savedAtUtc;
        string persistedBeforeCancel = JsonUtility.ToJson(saved.record);
        Assert.That(System.DateTime.TryParseExact(
                savedAtUtc,
                "O",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out System.DateTime parsedSavedAt),
            Is.True,
            "The formal slot must keep its persisted ISO 8601 UTC timestamp.");
        string expectedLocalTime = parsedSavedAt.ToLocalTime().ToString(
            "yyyy-MM-dd HH:mm",
            System.Globalization.CultureInfo.InvariantCulture);
        MethodInfo formatSavedAt = runUi.GetType().GetMethod(
            "FormatSavedAtLocal",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(formatSavedAt, Is.Not.Null);
        Assert.That(formatSavedAt.Invoke(null, new object[] { "not-an-iso-time" }),
            Is.EqualTo("保存时间未知"),
            "An invalid timestamp must degrade safely without exposing malformed storage data.");
        string slotDisplay = GetPrivateField<Text[]>(runUi, "saveSlotTexts")[0].text;
        InvokePrivate(runUi, "BeginSaveRequest");
        InvokePrivate(runUi, "SelectSaveSlot", 1);
        GameObject overwrite = GetPrivateField<GameObject>(runUi, "overwriteDialog");
        Assert.That(overwrite.activeSelf, Is.True);
        Assert.That(GetPrivateField<GameObject>(runUi, "settingsOverlay").activeSelf, Is.True);
        CanvasGroup frameInteraction = frame.GetComponent<CanvasGroup>();
        Button audioTab = GetPrivateField<Button>(runUi, "audioTabButton");
        Button saveTab = GetPrivateField<Button>(runUi, "saveTabButton");
        Button closeButton = close.GetComponent<Button>();
        string overwriteDisplay = GetPrivateField<Text>(runUi, "overwriteDetails").text;
        Image overwriteMask = overwrite.GetComponent<Image>();
        bool modalIsAboveFrame = overwrite.transform.GetSiblingIndex() > frame.GetSiblingIndex();
        bool backgroundControlsBlocked = frameInteraction != null &&
                                         !frameInteraction.interactable &&
                                         !audioTab.IsInteractable() &&
                                         !saveTab.IsInteractable() &&
                                         !closeButton.IsInteractable();
        InvokePrivate(runUi, "CancelOverwrite");
        TowerRunSlotReadResult afterCancel =
            (TowerRunSlotReadResult)Invoke(coordinator, "ReadSlot", 1);
        string persistedAfterCancel = JsonUtility.ToJson(afterCancel.record);

        Assert.That(slotDisplay, Does.Contain(expectedLocalTime),
            "Slot cards must show the player's local time to the minute.");
        Assert.That(slotDisplay, Does.Not.Contain(savedAtUtc),
            "Slot cards must not expose the raw persisted ISO timestamp.");
        Assert.That(overwriteDisplay, Does.Contain(expectedLocalTime),
            "Overwrite details must use the same local-time presentation as the slot card.");
        Assert.That(overwriteDisplay, Does.Not.Contain(savedAtUtc),
            "Overwrite details must not expose the raw persisted ISO timestamp.");
        Assert.That(frameInteraction, Is.Not.Null,
            "The settings frame needs a CanvasGroup so modal interaction can be isolated.");
        Assert.That(backgroundControlsBlocked, Is.True,
            "All settings controls behind the overwrite modal must be non-interactable.");
        Assert.That(overwriteMask.raycastTarget, Is.True,
            "The modal mask must intercept pointer input.");
        Assert.That(overwriteMask.color.a, Is.GreaterThanOrEqualTo(0.78f),
            "The modal mask must suppress background text bleed-through.");
        Assert.That(modalIsAboveFrame, Is.True,
            "The overwrite modal must render above the settings frame.");
        Assert.That(overwrite.activeSelf, Is.False);
        Assert.That(frameInteraction != null && frameInteraction.interactable, Is.True,
            "Cancelling overwrite must restore settings interaction.");
        Assert.That(audioTab.IsInteractable(), Is.True);
        Assert.That(saveTab.IsInteractable(), Is.True);
        Assert.That(closeButton.IsInteractable(), Is.True);
        Assert.That(persistedAfterCancel, Is.EqualTo(persistedBeforeCancel),
            "Cancelling overwrite must not modify the formal slot record.");
        Assert.That(afterCancel.summary.savedAtUtc, Is.EqualTo(savedAtUtc),
            "UI localisation must not mutate the persisted UTC timestamp.");
        Invoke(runUi, "CloseSettings");
    }

    [UnityTest]
    public IEnumerator SettingsRemainReadableAndContainedAt1024By576()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        yield return null;

        MonoBehaviour coordinator = FindBehaviour("TowerRunCoordinator");
        MonoBehaviour runUi = FindBehaviour("TowerRunUiController");
        Invoke(coordinator, "ReturnToStartupChoice");
        Invoke(coordinator, "BeginNewRun");
        yield return null;
        Invoke(runUi, "OpenSettings");
        Canvas.ForceUpdateCanvases();

        const float minimumWindowScale = 1024f / 1920f;
        const float minimumReadablePixelSize = 12.75f;
        RectTransform canvas = runUi.GetComponent<RectTransform>();
        RectTransform frame = GetPrivateField<RectTransform>(runUi, "settingsFrame");
        RectTransform banner = (RectTransform)frame.Find(
            "SettingsCarvedPaper/SettingsLayout/SettingsHeader/TitleBanner");
        Slider slider = GetPrivateField<Slider>(runUi, "volumeSlider");
        Toggle mute = GetPrivateField<Toggle>(runUi, "muteToggle");
        Button preview = frame.Find(
            "SettingsCarvedPaper/SettingsLayout/AudioPage/AudioContent/Preview").GetComponent<Button>();
        Button close = frame.Find(
            "SettingsCarvedPaper/SettingsLayout/CloseSettings").GetComponent<Button>();
        Text volumeLabel = frame.Find(
            "SettingsCarvedPaper/SettingsLayout/AudioPage/AudioContent/VolumeLabel").GetComponent<Text>();
        Text muteLabel = mute.transform.Find("Label").GetComponent<Text>();
        Text previewLabel = preview.GetComponentInChildren<Text>();

        InvokePrivate(runUi, "ShowSavePage");
        Canvas.ForceUpdateCanvases();
        Text saveMessage = GetPrivateField<Text>(runUi, "saveMessage");
        Text[] slotTexts = GetPrivateField<Text[]>(runUi, "saveSlotTexts");
        RectTransform thirdSlot = (RectTransform)frame.Find(
            "SettingsCarvedPaper/SettingsLayout/SavePage/SaveViewport/SaveContent/SaveSlot3");

        Assert.That(saveMessage.text, Does.Contain("\n"),
            "The minimum-width save instructions need an intentional line break instead of an orphaned punctuation line.");

        InvokePrivate(runUi, "BeginSaveRequest");
        InvokePrivate(runUi, "SelectSaveSlot", 1);
        InvokePrivate(runUi, "BeginSaveRequest");
        InvokePrivate(runUi, "SelectSaveSlot", 1);
        Canvas.ForceUpdateCanvases();
        GameObject overwrite = GetPrivateField<GameObject>(runUi, "overwriteDialog");
        RectTransform dialogFrame = (RectTransform)overwrite.transform.Find("OverwriteWoodFrame");
        Text dialogTitle = overwrite.transform.Find(
            "OverwriteWoodFrame/OverwriteCarvedPaper/SettingsLayout/OverwriteTitle").GetComponent<Text>();
        Text dialogDetails = GetPrivateField<Text>(runUi, "overwriteDetails");
        Button dialogCancel = GetPrivateField<Button>(runUi, "overwriteCancelButton");
        Button dialogConfirm = GetPrivateField<Button>(runUi, "overwriteConfirmButton");

        AssertRectContained(canvas, frame,
            "The settings frame must remain inside a 1024x576 target window.");
        AssertRectContained(canvas, dialogFrame,
            "The overwrite dialog must remain inside a 1024x576 target window.");
        Assert.That(thirdSlot.rect.height, Is.GreaterThan(0f),
            "The third formal slot must remain visible.");
        Assert.That(banner.rect.width, Is.LessThanOrEqualTo(96f));
        Assert.That(banner.rect.height, Is.LessThanOrEqualTo(92f));
        Assert.That(((RectTransform)preview.transform).rect.width, Is.LessThanOrEqualTo(320f),
            "The preview action should stay compact instead of stretching across the page.");
        Assert.That(slider.handleRect.Find("PixelFill"), Is.Not.Null,
            "The slider handle needs a pixel-style inner fill.");
        Assert.That(mute.transform.Find("Background/PixelFill"), Is.Not.Null,
            "The mute box needs the same pixel-style inner fill language.");
        Assert.That(volumeLabel.fontSize * minimumWindowScale,
            Is.GreaterThanOrEqualTo(minimumReadablePixelSize));
        Assert.That(muteLabel.fontSize * minimumWindowScale,
            Is.GreaterThanOrEqualTo(minimumReadablePixelSize));
        Assert.That(previewLabel.fontSize * minimumWindowScale,
            Is.GreaterThanOrEqualTo(minimumReadablePixelSize));
        Assert.That(saveMessage.fontSize * minimumWindowScale,
            Is.GreaterThanOrEqualTo(minimumReadablePixelSize));
        Assert.That(slotTexts[0].fontSize * minimumWindowScale,
            Is.GreaterThanOrEqualTo(minimumReadablePixelSize));
        Assert.That(close.GetComponentInChildren<Text>().fontSize * minimumWindowScale,
            Is.GreaterThanOrEqualTo(minimumReadablePixelSize));
        Assert.That(dialogTitle.fontSize * minimumWindowScale,
            Is.GreaterThanOrEqualTo(minimumReadablePixelSize));
        Assert.That(dialogDetails.fontSize * minimumWindowScale,
            Is.GreaterThanOrEqualTo(minimumReadablePixelSize));
        Assert.That(((RectTransform)preview.transform).rect.height * minimumWindowScale,
            Is.GreaterThanOrEqualTo(32f));
        Assert.That(((RectTransform)dialogCancel.transform).rect.height * minimumWindowScale,
            Is.GreaterThanOrEqualTo(32f));
        Assert.That(((RectTransform)dialogConfirm.transform).rect.height * minimumWindowScale,
            Is.GreaterThanOrEqualTo(32f));

        InvokePrivate(runUi, "CancelOverwrite");
        Invoke(runUi, "CloseSettings");
    }

    [UnityTest]
    public IEnumerator SettingsTextUsesHighContrastInkAndMinimumWeightAt1024By576()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        yield return null;

        MonoBehaviour coordinator = FindBehaviour("TowerRunCoordinator");
        MonoBehaviour runUi = FindBehaviour("TowerRunUiController");
        Invoke(coordinator, "ReturnToStartupChoice");
        Invoke(coordinator, "BeginNewRun");
        yield return null;
        Invoke(runUi, "OpenSettings");
        InvokePrivate(runUi, "ShowAudioPage");
        Canvas.ForceUpdateCanvases();

        Color primaryInk = new Color32(0x30, 0x23, 0x1B, 0xFF);
        Color secondaryInk = new Color32(0x4A, 0x36, 0x29, 0xFF);
        Color selectedTabInk = new Color32(0xFF, 0xF3, 0xDB, 0xFF);
        RectTransform frame = GetPrivateField<RectTransform>(runUi, "settingsFrame");
        Text settingsTitle = frame.Find(
            "SettingsCarvedPaper/SettingsLayout/SettingsHeader/SettingsTitle").GetComponent<Text>();
        Text audioTab = GetPrivateField<Button>(runUi, "audioTabButton").GetComponentInChildren<Text>();
        Text saveTab = GetPrivateField<Button>(runUi, "saveTabButton").GetComponentInChildren<Text>();
        Text volumeLabel = frame.Find(
            "SettingsCarvedPaper/SettingsLayout/AudioPage/AudioContent/VolumeLabel").GetComponent<Text>();
        Text muteLabel = GetPrivateField<Toggle>(runUi, "muteToggle").transform
            .Find("Label").GetComponent<Text>();
        Text previewLabel = frame.Find(
            "SettingsCarvedPaper/SettingsLayout/AudioPage/AudioContent/Preview")
            .GetComponent<Button>().GetComponentInChildren<Text>();
        Text closeLabel = frame.Find(
            "SettingsCarvedPaper/SettingsLayout/CloseSettings")
            .GetComponent<Button>().GetComponentInChildren<Text>();

        AssertReadableText(settingsTitle, 38, FontStyle.Bold, primaryInk, "Settings title");
        AssertReadableText(audioTab, 24, FontStyle.Bold, selectedTabInk, "Selected audio tab");
        AssertReadableText(saveTab, 24, FontStyle.Bold, primaryInk, "Unselected save tab");
        AssertReadableText(volumeLabel, 24, FontStyle.Bold, primaryInk, "Master-volume label");
        AssertReadableText(muteLabel, 24, FontStyle.Bold, primaryInk, "Mute label");
        AssertReadableText(previewLabel, 24, FontStyle.Bold, primaryInk, "Preview button");
        AssertReadableText(closeLabel, 24, FontStyle.Bold, primaryInk, "Close button");
        AssertSubtlePixelShadow(settingsTitle, "Settings title");
        AssertSubtlePixelShadow(audioTab, "Selected audio tab");

        InvokePrivate(runUi, "ShowSavePage");
        Canvas.ForceUpdateCanvases();
        Text saveMessage = GetPrivateField<Text>(runUi, "saveMessage");
        Text[] slotTexts = GetPrivateField<Text[]>(runUi, "saveSlotTexts");
        AssertReadableText(saveMessage, 24, FontStyle.Bold, primaryInk, "Save result/instruction");
        for (int i = 0; i < slotTexts.Length; i++)
        {
            AssertReadableText(slotTexts[i], 24, FontStyle.Bold, primaryInk,
                $"Formal slot {i + 1}");
        }

        InvokePrivate(runUi, "BeginSaveRequest");
        InvokePrivate(runUi, "SelectSaveSlot", 1);
        InvokePrivate(runUi, "BeginSaveRequest");
        InvokePrivate(runUi, "SelectSaveSlot", 1);
        Canvas.ForceUpdateCanvases();

        GameObject overwrite = GetPrivateField<GameObject>(runUi, "overwriteDialog");
        Text dialogTitle = overwrite.transform.Find(
            "OverwriteWoodFrame/OverwriteCarvedPaper/SettingsLayout/OverwriteTitle").GetComponent<Text>();
        Text dialogDetails = GetPrivateField<Text>(runUi, "overwriteDetails");
        Text dialogCancel = GetPrivateField<Button>(runUi, "overwriteCancelButton")
            .GetComponentInChildren<Text>();
        Text dialogConfirm = GetPrivateField<Button>(runUi, "overwriteConfirmButton")
            .GetComponentInChildren<Text>();
        AssertReadableText(dialogTitle, 34, FontStyle.Bold, primaryInk, "Overwrite title");
        AssertReadableText(dialogDetails, 24, FontStyle.Bold, secondaryInk, "Overwrite details");
        AssertReadableText(dialogCancel, 24, FontStyle.Bold, primaryInk, "Cancel overwrite");
        AssertReadableText(dialogConfirm, 24, FontStyle.Bold, primaryInk, "Confirm overwrite");
        AssertSubtlePixelShadow(dialogTitle, "Overwrite title");

        InvokePrivate(runUi, "CancelOverwrite");
        Invoke(runUi, "CloseSettings");
    }

    [UnityTest]
    public IEnumerator ShortSettingsButtonLabelsKeepOneOpticalCenterAcrossStates()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        yield return null;

        MonoBehaviour coordinator = FindBehaviour("TowerRunCoordinator");
        MonoBehaviour runUi = FindBehaviour("TowerRunUiController");
        Invoke(coordinator, "ReturnToStartupChoice");
        Invoke(coordinator, "BeginNewRun");
        yield return null;
        Invoke(runUi, "OpenSettings");
        InvokePrivate(runUi, "ShowAudioPage");
        Canvas.ForceUpdateCanvases();

        RectTransform frame = GetPrivateField<RectTransform>(runUi, "settingsFrame");
        Button audioTab = GetPrivateField<Button>(runUi, "audioTabButton");
        Button saveTab = GetPrivateField<Button>(runUi, "saveTabButton");
        Button preview = frame.Find(
            "SettingsCarvedPaper/SettingsLayout/AudioPage/AudioContent/Preview").GetComponent<Button>();
        Button close = frame.Find(
            "SettingsCarvedPaper/SettingsLayout/CloseSettings").GetComponent<Button>();
        Button beginSave = GetPrivateField<Button>(runUi, "beginSaveButton");

        AssertShortButtonLabelOpticalCenter(audioTab, "Audio tab");
        AssertShortButtonLabelOpticalCenter(saveTab, "Save tab");
        AssertShortButtonLabelOpticalCenter(preview, "Preview button");
        AssertShortButtonLabelOpticalCenter(close, "Close button");
        Assert.That(beginSave.GetComponentInChildren<Text>().rectTransform.anchoredPosition.y,
            Is.EqualTo(0f).Within(0.01f),
            "Long action labels must keep their existing layout.");

        Assert.That(EventSystem.current, Is.Not.Null);
        PointerEventData pointer = new PointerEventData(EventSystem.current);
        Button[] shortButtons = { audioTab, saveTab, preview, close };
        for (int i = 0; i < shortButtons.Length; i++)
        {
            Button button = shortButtons[i];
            Vector2 position = button.GetComponentInChildren<Text>().rectTransform.anchoredPosition;
            button.OnPointerEnter(pointer);
            Canvas.ForceUpdateCanvases();
            Assert.That(button.GetComponentInChildren<Text>().rectTransform.anchoredPosition,
                Is.EqualTo(position),
                $"Hovering {button.name} must tint the button without moving its label.");
            button.OnPointerExit(pointer);
        }

        Vector2 audioPosition = audioTab.GetComponentInChildren<Text>().rectTransform.anchoredPosition;
        Vector2 savePosition = saveTab.GetComponentInChildren<Text>().rectTransform.anchoredPosition;
        InvokePrivate(runUi, "ShowSavePage");
        Canvas.ForceUpdateCanvases();
        Assert.That(audioTab.GetComponentInChildren<Text>().rectTransform.anchoredPosition,
            Is.EqualTo(audioPosition),
            "Changing the selected tab must not move the audio label.");
        Assert.That(saveTab.GetComponentInChildren<Text>().rectTransform.anchoredPosition,
            Is.EqualTo(savePosition),
            "Changing the selected tab must not move the save label.");

        Invoke(runUi, "CloseSettings");
    }

    [UnityTest]
    public IEnumerator StartupAndTwoDeathRestoresUseFormalSlotAndReleaseModalOwners()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        yield return null;

        MonoBehaviour coordinator = FindBehaviour("TowerRunCoordinator");
        MonoBehaviour runUi = FindBehaviour("TowerRunUiController");
        MonoBehaviour gameInput = FindBehaviour("GameInput");
        Assert.That(coordinator, Is.Not.Null);
        Assert.That(runUi, Is.Not.Null);
        Assert.That(gameInput, Is.Not.Null);
        AssertIsolatedSlotRoot(coordinator);
        Invoke(coordinator, "ReturnToStartupChoice");
        Invoke(coordinator, "BeginNewRun");
        yield return null;
        AssertPlaying(coordinator, "新手关卡");

        MonoBehaviour package = FindBehaviour("PackagePanelController");
        Assert.That(package, Is.Not.Null);
        PressTab();
        yield return null;
        Assert.That((bool)GetProperty(package, "IsPackagePanelVisible"), Is.True);
        yield return PressEscape(runUi);
        Assert.That((bool)GetProperty(package, "IsPackagePanelVisible"), Is.False,
            "Esc must be consumed by the topmost package panel before settings can open.");
        Assert.That(GetPrivateField<GameObject>(runUi, "settingsOverlay").activeSelf, Is.False);

        yield return PressEscape(runUi);
        Assert.That(GetPrivateField<GameObject>(runUi, "settingsOverlay").activeSelf, Is.True,
            "Esc must open the shared settings window while gameplay is active.");
        PressTab();
        yield return null;
        Assert.That((bool)GetProperty(package, "IsPackagePanelVisible"), Is.False,
            "Tab must not pass through the settings gameplay gate.");
        yield return PressEscape(runUi);
        Assert.That(GetPrivateField<GameObject>(runUi, "settingsOverlay").activeSelf, Is.False);

        GameObject settingsButtonObject = FindSceneGameObject("SettingButton");
        Assert.That(settingsButtonObject, Is.Not.Null);
        Button settingsButton = settingsButtonObject.GetComponent<Button>();
        Assert.That(settingsButton, Is.Not.Null);
        settingsButton.onClick.Invoke();
        Assert.That(GetPrivateField<GameObject>(runUi, "settingsOverlay").activeSelf, Is.True,
            "The existing HUD settings button must open the same settings overlay.");
        Slider volumeSlider = GetPrivateField<Slider>(runUi, "volumeSlider");
        Toggle muteToggle = GetPrivateField<Toggle>(runUi, "muteToggle");
        volumeSlider.value = 0.37f;
        InvokePrivate(runUi, "PlayPreviewSound");
        AudioSource previewSource = GetPrivateField<AudioSource>(runUi, "previewSource");
        Assert.That(previewSource.isPlaying, Is.True);
        InvokePrivate(runUi, "PlayPreviewSound");
        Assert.That(runUi.GetComponents<AudioSource>(), Has.Length.EqualTo(1),
            "Repeated preview must reuse one AudioSource instead of stacking playback owners.");
        Assert.That(previewSource.isPlaying, Is.True);
        muteToggle.isOn = true;
        Invoke(runUi, "CloseSettings");

        Invoke(runUi, "OpenSettings");
        InvokePrivate(runUi, "ShowSavePage");
        InvokePrivate(runUi, "BeginSaveRequest");
        TowerRunCheckpointRecord requestedCandidate =
            GetPrivateField<TowerRunCheckpointRecord>(runUi, "requestedSaveCandidate");
        Assert.That(requestedCandidate, Is.Not.Null);
        Assert.That(requestedCandidate.savedAtUtc, Is.Empty,
            "A pending candidate must not impersonate a completed disk save.");
        InvokePrivate(runUi, "SelectSaveSlot", 1);
        TowerRunSlotReadResult saved =
            (TowerRunSlotReadResult)Invoke(coordinator, "ReadSlot", 1);
        Assert.That(saved.state, Is.EqualTo(TowerRunSlotState.Valid), saved.message);
        Invoke(runUi, "CloseSettings");

        yield return RecreateApplicationRuntime();
        coordinator = FindBehaviour("TowerRunCoordinator");
        runUi = FindBehaviour("TowerRunUiController");
        gameInput = FindBehaviour("GameInput");
        Assert.That(coordinator, Is.Not.Null);
        Assert.That(runUi, Is.Not.Null);
        Assert.That(gameInput, Is.Not.Null);
        AssertIsolatedSlotRoot(coordinator);
        Assert.That(GetProperty(coordinator, "State").ToString(), Is.EqualTo("StartupChoice"));
        Assert.That((float)GetPrivateField<Slider>(runUi, "volumeSlider").value,
            Is.EqualTo(0.37f).Within(0.001f));
        Assert.That(GetPrivateField<Toggle>(runUi, "muteToggle").isOn, Is.True,
            "Audio preferences must survive an equivalent independent runtime rebuild.");
        saved = (TowerRunSlotReadResult)Invoke(coordinator, "ReadSlot", 1);
        Assert.That(saved.state, Is.EqualTo(TowerRunSlotState.Valid), saved.message);
        Invoke(coordinator, "BeginRestoreSlot", 1, saved.confirmationToken);
        yield return null;
        AssertPlaying(coordinator, "新手关卡");

        Invoke(runUi, "OpenSettings");
        InvokePrivate(runUi, "ShowSavePage");
        InvokePrivate(runUi, "BeginSaveRequest");
        InvokePrivate(runUi, "SelectSaveSlot", 1);
        Assert.That(GetPrivateFieldValue<int>(runUi, "pendingOverwriteSlot"), Is.EqualTo(1));
        yield return PressEscape(runUi);
        Assert.That(GetPrivateField<GameObject>(runUi, "settingsOverlay").activeSelf, Is.True,
            "Esc during overwrite confirmation must return to slot selection, not close settings.");
        Assert.That(GetPrivateFieldValue<int>(runUi, "pendingOverwriteSlot"), Is.Zero);
        Invoke(runUi, "CloseSettings");

        GameObject runtimeRoot = FindRootObject(SceneManager.GetActiveScene(), "RandomMapRuntime");
        MapRuntimeBootstrap bootstrap = runtimeRoot.GetComponent<MapRuntimeBootstrap>();
        MapGenerationController controller =
            runtimeRoot.GetComponentInChildren<MapGenerationController>(true);
        MapMinimapController minimap =
            runtimeRoot.GetComponentInChildren<MapMinimapController>(true);
        AssertFreshSpawnExploration(minimap);
        GameObject pendingAttackTarget = FindFirstRoleTarget(
            bootstrap.Context,
            MonsterPlacementService.MonsterRole);
        controller.Player.position = pendingAttackTarget.transform.position;
        float targetHealthBeforeDeath =
            (float)GetComponentProperty(pendingAttackTarget, "BaseDamageable", "CurrentHealth");
        MonoBehaviour playerAction = FindBehaviour("PlayerAction");
        Assert.That(playerAction, Is.Not.Null);
        InvokePrivate(playerAction, "HandleBattlePressed");

        Invoke(runUi, "OpenSettings");
        GetPrivateField<Toggle>(runUi, "muteToggle").isOn = false;
        InvokePrivate(runUi, "PlayPreviewSound");
        Assert.That(Time.timeScale, Is.Zero);
        Assert.That((bool)GetProperty(gameInput, "IsGameplayInputEnabled"), Is.False);
        MapObjectRegistry firstDeathRegistry = bootstrap.Context.ActiveRegistry;
        DefeatPlayer();
        yield return null;
        Assert.That(GetProperty(coordinator, "State").ToString(), Is.EqualTo("DeathChoice"));
        Assert.That(GetPrivateField<GameObject>(runUi, "settingsOverlay").activeSelf, Is.False);
        Assert.That(GetPrivateField<AudioSource>(runUi, "previewSource").clip, Is.Null);
        Assert.That(Time.timeScale, Is.Zero,
            "Closing settings must release only its owner; the death pause owner remains.");
        yield return new WaitForSecondsRealtime(0.35f);
        Assert.That(
            (float)GetComponentProperty(pendingAttackTarget, "BaseDamageable", "CurrentHealth"),
            Is.EqualTo(targetHealthBeforeDeath),
            "A pending attack from the defeated life must be cancelled before its hit frame.");

        Invoke(coordinator, "BeginRestoreSlot", 1, saved.confirmationToken);
        yield return null;
        AssertPlaying(coordinator, "新手关卡");
        AssertFreshSpawnExploration(minimap);
        Assert.That(firstDeathRegistry.IsRetired, Is.True);
        AssertSingleRuntimeOwners();
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That((bool)GetProperty(gameInput, "IsGameplayInputEnabled"), Is.True);

        MapObjectRegistry secondDeathRegistry = bootstrap.Context.ActiveRegistry;
        DefeatPlayer();
        yield return null;
        Assert.That(GetProperty(coordinator, "State").ToString(), Is.EqualTo("DeathChoice"));
        Invoke(coordinator, "BeginRestoreSlot", 1, saved.confirmationToken);
        yield return null;
        AssertPlaying(coordinator, "新手关卡");
        AssertFreshSpawnExploration(minimap);
        Assert.That(secondDeathRegistry.IsRetired, Is.True);
        AssertSingleRuntimeOwners();
        Assert.That(Time.timeScale, Is.EqualTo(1f));
    }

    [UnityTest]
    public IEnumerator StartupRestoreRejectsSlotChangedAfterTheChoiceWasRendered()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        yield return null;

        MonoBehaviour coordinator = FindBehaviour("TowerRunCoordinator");
        MonoBehaviour runUi = FindBehaviour("TowerRunUiController");
        Assert.That(coordinator, Is.Not.Null);
        Assert.That(runUi, Is.Not.Null);
        AssertIsolatedSlotRoot(coordinator);

        Invoke(coordinator, "ReturnToStartupChoice");
        Invoke(coordinator, "BeginNewRun");
        yield return null;
        AssertPlaying(coordinator, "新手关卡");

        Invoke(runUi, "OpenSettings");
        InvokePrivate(runUi, "ShowSavePage");
        InvokePrivate(runUi, "BeginSaveRequest");
        InvokePrivate(runUi, "SelectSaveSlot", 1);
        TowerRunSlotReadResult displayed =
            (TowerRunSlotReadResult)Invoke(coordinator, "ReadSlot", 1);
        Assert.That(displayed.state, Is.EqualTo(TowerRunSlotState.Valid), displayed.message);
        Invoke(runUi, "CloseSettings");

        Invoke(coordinator, "ReturnToStartupChoice");
        yield return null;

        TowerRunCheckpointRecord concurrent = displayed.record.DeepClone();
        concurrent.runId = "concurrent-run";
        concurrent.savedAtUtc = System.DateTime.UtcNow.ToString("O");
        TowerRunSlotStore slotStore = GetPrivateField<TowerRunSlotStore>(coordinator, "slotStore");
        TowerRunSlotWriteResult concurrentWrite = slotStore.TryCommit(
            1,
            concurrent,
            true,
            displayed.confirmationToken);
        Assert.That(concurrentWrite.succeeded, Is.True, concurrentWrite.message);

        Button[] restoreButtons = GetPrivateField<Button[]>(runUi, "flowSlotButtons");
        restoreButtons[0].onClick.Invoke();
        yield return null;

        Assert.That(
            GetProperty(coordinator, "State").ToString(),
            Is.EqualTo("Failure"),
            "A restore choice must be rejected when the displayed slot record changed before click.");
        Assert.That(
            (string)GetProperty(coordinator, "FailureMessage"),
            Does.Contain("变化"));
        Assert.That(slotStore.ReadSlot(1).record.runId, Is.EqualTo("concurrent-run"));
    }

    [UnityTest]
    public IEnumerator NewRunAfterDeathDoesNotRevealPreviousRunLocation()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        yield return null;

        MonoBehaviour coordinator = FindBehaviour("TowerRunCoordinator");
        AssertIsolatedSlotRoot(coordinator);
        IEnumerator BeginNewRunWithOriginalRetry()
        {
            bool finiteRejection = false;
            void ObserveRejection(string message, string stack, LogType type)
            {
                if (type != LogType.Error || !message.StartsWith("Map generation failed [EcologyBudgetExhaustedOnFixedTerrain]:", System.StringComparison.Ordinal)) return;
                finiteRejection = true;
                LogAssert.Expect(LogType.Error, message);
                var staged = GetPrivateField<TowerRunCheckpointRecord>(coordinator, "stagedRecord");
                string output = System.Environment.GetEnvironmentVariable("RPG_DEMO_REMEDIATION_EVIDENCE");
                if (staged != null && !string.IsNullOrEmpty(output))
                    File.WriteAllText(Path.Combine(output, "DeathNewRun-Rejection-" + System.Guid.NewGuid().ToString("N") + ".json"), JsonUtility.ToJson(staged, true));
            }
            Application.logMessageReceived += ObserveRejection;
            try
            {
                Invoke(coordinator, "BeginNewRun"); yield return null;
                for (int retry = 0; retry < 3 && GetProperty(coordinator, "State").ToString() == "Failure"; retry++)
                {
                    Assert.That(finiteRejection, Is.True, "Only the declared finite terrain-quality rejection may use the original Retry action.");
                    Assert.That((bool)GetProperty(coordinator, "CanRetryFailedOperation"), Is.True);
                    finiteRejection = false;
                    Invoke(coordinator, "RetryFailedOperation"); yield return null;
                }
            }
            finally { Application.logMessageReceived -= ObserveRejection; }
        }
        Invoke(coordinator, "ReturnToStartupChoice");
        yield return BeginNewRunWithOriginalRetry();
        AssertPlaying(coordinator, "新手关卡");

        GameObject runtimeRoot = FindRootObject(SceneManager.GetActiveScene(), "RandomMapRuntime");
        MapGenerationController controller =
            runtimeRoot.GetComponentInChildren<MapGenerationController>(true);
        MapMinimapController minimap =
            runtimeRoot.GetComponentInChildren<MapMinimapController>(true);
        MapData previousMap = minimap.DisplayedMap;
        int revealRadius = GetPrivateFieldValue<int>(minimap, "explorationRadius");
        Vector2Int previousDeathCell = previousMap.Origin +
            new Vector2Int(previousMap.Width - 2, previousMap.Height - 2);
        Assert.That(Vector2Int.Distance(previousDeathCell, previousMap.SpawnCell),
            Is.GreaterThan(revealRadius + 1));

        controller.Player.position = minimap.TilemapRenderer.Coordinates.CellToWorld(
            previousMap, previousDeathCell);
        yield return null;
        Assert.That(minimap.ExplorationState.IsExplored(previousDeathCell), Is.True,
            "Exploration in the current Run must remain available before death.");

        DefeatPlayer();
        yield return null;
        Assert.That(GetProperty(coordinator, "State").ToString(), Is.EqualTo("DeathChoice"));
        yield return BeginNewRunWithOriginalRetry();
        AssertPlaying(coordinator, "新手关卡");

        MapData newMap = minimap.DisplayedMap;
        AssertFreshSpawnExploration(minimap);
        Assert.That(newMap.IsInside(previousDeathCell), Is.True,
            "The old location must lie inside the replacement map for this regression check.");
        Assert.That(Vector2Int.Distance(previousDeathCell, newMap.SpawnCell),
            Is.GreaterThan(revealRadius + 1),
            "The old location must be outside normal new-Run spawn vision.");
        Assert.That(minimap.ExplorationState.IsExplored(newMap.SpawnCell), Is.True,
            "The new Run must still reveal its own spawn vision.");
        Assert.That(minimap.ExplorationState.IsExplored(previousDeathCell), Is.False,
            "A new Run must not inherit the previous death location's exploration.");
        Vector2Int textureCell = previousDeathCell - newMap.Origin;
        Assert.That(minimap.FogTexture.GetPixel(textureCell.x, textureCell.y).a,
            Is.GreaterThan(0f),
            "The visible fog texture must also cover the previous Run's death location.");
    }

    [UnityTest]
    public IEnumerator FinalizerExceptionEntersRetryableFailureAndRetryKeepsFrozenCarry()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        yield return null;

        MonoBehaviour coordinator = FindBehaviour("TowerRunCoordinator");
        MonoBehaviour floor = FindBehaviour("TowerFloorRuntimeController");
        AssertIsolatedSlotRoot(coordinator);
        Invoke(coordinator, "ReturnToStartupChoice");
        Invoke(coordinator, "BeginNewRun");
        yield return null;
        AssertPlaying(coordinator, "新手关卡");

        MonoBehaviour playerDamage = FindBehaviour("PlayerDamageController");
        DamagePlayer(playerDamage, 7f);
        float frozenHealth = (float)GetProperty(playerDamage, "CurrentHealth");
        MonoBehaviour inventory = FindBehaviour("PlayerInventory");
        MonoBehaviour shop = FindBehaviour("ShopTradeController");
        Assert.That(inventory, Is.Not.Null);
        Assert.That(shop, Is.Not.Null);
        object goldReward = GetProperty(inventory, "GoldReward");
        Assert.That(goldReward, Is.Not.Null);
        Assert.That((bool)Invoke(inventory, "TryAddItem", goldReward, 10000L), Is.True);
        object finiteProduct = FindFiniteStockProduct(shop);
        int stockBeforePurchase = (int)Invoke(shop, "GetCurrentStock", finiteProduct);
        object purchase = Invoke(shop, "TryPurchase", finiteProduct);
        Assert.That(GetPublicFieldValue<bool>(purchase, "succeeded"), Is.True);
        int frozenStock = (int)Invoke(shop, "GetCurrentStock", finiteProduct);
        Assert.That(frozenStock, Is.EqualTo(stockBeforePurchase - 1));
        GameObject runtimeRoot = FindRootObject(SceneManager.GetActiveScene(), "RandomMapRuntime");
        MapRuntimeBootstrap bootstrap = runtimeRoot.GetComponent<MapRuntimeBootstrap>();
        MapGenerationController controller =
            runtimeRoot.GetComponentInChildren<MapGenerationController>(true);
        ThrowOnceTowerRunReadyFinalizer throwOnce =
            runtimeRoot.AddComponent<ThrowOnceTowerRunReadyFinalizer>();

        DefeatAllRoleTargets(bootstrap.Context, MonsterPlacementService.MonsterRole);
        yield return null;
        GameObject portal = GetPortal(floor);
        controller.Player.position = portal.transform.position;
        LogAssert.Expect(
            LogType.Exception,
            new Regex("Injected ready-finalizer exception"));
        PressInteract();
        yield return null;

        Assert.That(GetProperty(coordinator, "State").ToString(), Is.EqualTo("Failure"));
        Assert.That((bool)GetProperty(coordinator, "CanRetryFailedOperation"), Is.True);
        Assert.That(Time.timeScale, Is.Zero);
        TowerRunCheckpointRecord frozenCarry =
            GetPrivateField<TowerRunCheckpointRecord>(coordinator, "stagedRecord").DeepClone();
        Assert.That(frozenCarry.player.shops, Has.Some.Matches<TowerRunShopRecord>(shopRecord =>
            shopRecord.catalogId == (string)GetProperty(shop, "CatalogId") &&
            shopRecord.productStocks.Exists(stock =>
                stock.productId == (string)GetObjectProperty(finiteProduct, "ProductId") &&
                stock.quantity == frozenStock)));

        Assert.That((bool)Invoke(inventory, "TryAddItem", goldReward, 37L), Is.True);
        Invoke(shop, "ResetRuntimeStocks");
        Invoke(coordinator, "RetryFailedOperation");
        yield return null;

        AssertPlaying(coordinator, "第 1 层");
        Assert.That((float)GetProperty(playerDamage, "CurrentHealth"), Is.EqualTo(frozenHealth));
        object[] saveRequestArguments = { null, null };
        Assert.That((bool)Invoke(coordinator, "TryCreateSaveRequest", saveRequestArguments), Is.True);
        TowerRunCheckpointRecord restoredCandidate =
            (TowerRunCheckpointRecord)saveRequestArguments[0];
        Assert.That(
            JsonUtility.ToJson(restoredCandidate.player),
            Is.EqualTo(JsonUtility.ToJson(frozenCarry.player)),
            "Retry must restore the complete frozen inventory, gold and shop state without duplication.");
        Assert.That(throwOnce.InvocationCount, Is.EqualTo(2));
        Assert.That(Time.timeScale, Is.EqualTo(1f));
    }

    private void PressInteract()
    {
        EnsureTestKeyboardEnabled();
        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.F));
        InputSystem.Update();
        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
        InputSystem.Update();
    }

    private void PressTab()
    {
        EnsureTestKeyboardEnabled();
        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Tab));
        InputSystem.Update();
        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
        InputSystem.Update();
    }

    private IEnumerator PressEscape(MonoBehaviour runUi)
    {
        EnsureTestKeyboardEnabled();
        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Escape));
        InputSystem.Update();
        InvokePrivate(runUi, "Update");
        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
        InputSystem.Update();
        yield return null;
    }

    private void EnsureTestKeyboardEnabled()
    {
        Assert.That(testKeyboard, Is.Not.Null);
        Assert.That(testKeyboard.added, Is.True);

        if (!testKeyboard.enabled)
            InputSystem.EnableDevice(testKeyboard);

        Assert.That(testKeyboard.enabled, Is.True);
    }

    private IEnumerator RecreateApplicationRuntime()
    {
        MonoBehaviour session = FindBehaviour("GameSession");
        Assert.That(session, Is.Not.Null);
        System.Type sessionType = session.GetType();
        Object.Destroy(session.gameObject);
        yield return null;

        GameObject recreatedSession = new GameObject("GameSession-TestRecreated");
        recreatedSession.AddComponent(sessionType);
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        yield return null;
    }

    private void AssertIsolatedSlotRoot(MonoBehaviour coordinator)
    {
        Assert.That(coordinator, Is.Not.Null);
        string actualRoot = GetPrivateField<string>(coordinator, "slotStoreRoot");
        string expectedRoot = Path.Combine(testRoot, "tower-run-slots");
        Assert.That(actualRoot, Is.EqualTo(expectedRoot),
            "The coordinator must rebuild its slot store after test isolation is configured.");
    }

    private static void AssertSingleRuntimeOwners()
    {
        Assert.That(CountSceneBehaviours("PlayerAction"), Is.EqualTo(1));
        Assert.That(CountSceneBehaviours("PlayerDamageController"), Is.EqualTo(1));
        Assert.That(CountSceneBehaviours("TowerFloorRuntimeController"), Is.EqualTo(1));

        int portalCount = 0;
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null && objects[i].scene.IsValid() &&
                objects[i].name == "TowerFloorExitPortal")
            {
                portalCount++;
            }
        }

        Assert.That(portalCount, Is.EqualTo(1));
    }

    private static int CountSceneBehaviours(string typeName)
    {
        int count = 0;
        MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null && behaviours[i].gameObject.scene.IsValid() &&
                behaviours[i].GetType().Name == typeName)
            {
                count++;
            }
        }

        return count;
    }

    private static void DefeatPlayer()
    {
        MonoBehaviour damage = FindBehaviour("PlayerDamageController");
        Assert.That(damage, Is.Not.Null);
        DamagePlayer(damage, float.MaxValue);
    }

    private static void DamagePlayer(MonoBehaviour damage, float amount)
    {
        MethodInfo takeDamage = damage.GetType().GetMethod(
            "TakeDamage",
            new[] { typeof(float), typeof(Transform) });
        Assert.That(takeDamage, Is.Not.Null);
        takeDamage.Invoke(damage, new object[] { amount, null });
    }

    private static GameObject DefeatAllRoleTargets(MapRuntimeContext context, string role)
    {
        GameObject first = null;
        int defeated = 0;

        foreach (MapObjectRegistryEntry entry in
                 new System.Collections.Generic.List<MapObjectRegistryEntry>(context.ActiveRegistry.Entries))
        {
            if (entry.Role != role)
                continue;

            GameObject target = ResolveGameObject(entry.Instance);
            Assert.That(target, Is.Not.Null);
            first ??= target;
            Defeat(target);
            defeated++;
        }

        Assert.That(defeated, Is.GreaterThan(0));
        return first;
    }

    private static GameObject FindFirstRoleTarget(MapRuntimeContext context, string role)
    {
        foreach (MapObjectRegistryEntry entry in context.ActiveRegistry.Entries)
        {
            if (entry.Role != role)
                continue;

            GameObject target = ResolveGameObject(entry.Instance);
            Assert.That(target, Is.Not.Null);
            return target;
        }

        Assert.Fail($"No active registry entry was found for role {role}.");
        return null;
    }

    private static void DefeatFirstRole(MapRuntimeContext context, string role)
    {
        foreach (MapObjectRegistryEntry entry in context.ActiveRegistry.Entries)
        {
            if (entry.Role != role)
                continue;

            GameObject target = ResolveGameObject(entry.Instance);
            Assert.That(target, Is.Not.Null);
            Defeat(target);
            return;
        }

        Assert.Fail($"No active registry entry was found for role {role}.");
    }

    private static void Defeat(GameObject target)
    {
        Component damageable = target.GetComponent("BaseDamageable");
        Assert.That(damageable, Is.Not.Null);
        PropertyInfo maxHealth = damageable.GetType().GetProperty("MaxHealth");
        MethodInfo takeDamage = damageable.GetType().GetMethod("TakeDamage", new[] { typeof(float) });
        Assert.That(maxHealth, Is.Not.Null);
        Assert.That(takeDamage, Is.Not.Null);
        takeDamage.Invoke(damageable, new[] { maxHealth.GetValue(damageable) });
    }

    private static bool GetBoolProperty(GameObject target, string componentName, string propertyName)
    {
        Component component = target.GetComponent(componentName);
        PropertyInfo property = component?.GetType().GetProperty(propertyName);
        Assert.That(property, Is.Not.Null);
        return (bool)property.GetValue(component);
    }

    private static object GetComponentProperty(
        GameObject target,
        string componentName,
        string propertyName)
    {
        Component component = target.GetComponent(componentName);
        Assert.That(component, Is.Not.Null);
        return GetObjectProperty(component, propertyName);
    }

    private static object FindFiniteStockProduct(MonoBehaviour shop)
    {
        FieldInfo catalogField = shop.GetType().GetField(
            "shopCatalog",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(catalogField, Is.Not.Null);
        object catalog = catalogField.GetValue(shop);
        Assert.That(catalog, Is.Not.Null);
        System.Collections.IEnumerable categories =
            GetObjectProperty(catalog, "Categories") as System.Collections.IEnumerable;
        Assert.That(categories, Is.Not.Null);

        foreach (object category in categories)
        {
            System.Collections.IEnumerable products =
                GetObjectProperty(category, "Products") as System.Collections.IEnumerable;

            if (products == null)
                continue;

            foreach (object product in products)
            {
                if (product != null &&
                    !(bool)GetObjectProperty(product, "InfiniteStock") &&
                    (int)GetObjectProperty(product, "InitialStock") > 0)
                {
                    return product;
                }
            }
        }

        Assert.Fail("The formal scene must expose at least one finite-stock product.");
        return null;
    }

    private static object GetObjectProperty(object source, string propertyName)
    {
        Assert.That(source, Is.Not.Null);
        PropertyInfo property = source.GetType().GetProperty(propertyName);
        Assert.That(property, Is.Not.Null);
        return property.GetValue(source);
    }

    private static T GetPublicFieldValue<T>(object source, string fieldName)
    {
        Assert.That(source, Is.Not.Null);
        FieldInfo field = source.GetType().GetField(fieldName);
        Assert.That(field, Is.Not.Null);
        return (T)field.GetValue(source);
    }

    private static GameObject ResolveGameObject(Object instance)
    {
        if (instance is GameObject gameObject)
            return gameObject;

        return instance is Component component ? component.gameObject : null;
    }

    private static GameObject GetPortal(MonoBehaviour floor)
    {
        FieldInfo field = floor.GetType().GetField(
            "exitPortalInstance",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (GameObject)field.GetValue(floor);
    }

    private static void AssertPlaying(MonoBehaviour coordinator, string floorDisplayName)
    {
        Assert.That(GetProperty(coordinator, "State").ToString(), Is.EqualTo("Playing"));
        Assert.That((string)GetProperty(coordinator, "FloorDisplayName"), Is.EqualTo(floorDisplayName));
    }

    private static void AssertFreshSpawnExploration(MapMinimapController minimap)
    {
        MapData map = minimap.DisplayedMap;
        Assert.That(map, Is.Not.Null);
        Assert.That(minimap.ExplorationState, Is.Not.Null);
        MapExplorationState expected = new MapExplorationState(map);
        expected.RevealAround(
            map.SpawnCell,
            GetPrivateFieldValue<int>(minimap, "explorationRadius"));

        Assert.That(minimap.ExplorationState.ExploredCount,
            Is.EqualTo(expected.ExploredCount),
            "A new entrance world should only reveal the current spawn vision.");
        for (int x = map.Origin.x; x < map.Origin.x + map.Width; x++)
        {
            for (int y = map.Origin.y; y < map.Origin.y + map.Height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (minimap.ExplorationState.IsExplored(cell) != expected.IsExplored(cell))
                    Assert.Fail($"Unexpected exploration at {cell} after entrance initialization.");
            }
        }
    }

    private static void AssertRectContained(RectTransform outer, RectTransform inner, string message)
    {
        Vector3[] outerCorners = new Vector3[4];
        Vector3[] innerCorners = new Vector3[4];
        outer.GetWorldCorners(outerCorners);
        inner.GetWorldCorners(innerCorners);
        const float tolerance = 0.5f;

        Assert.That(innerCorners[0].x, Is.GreaterThanOrEqualTo(outerCorners[0].x - tolerance), message);
        Assert.That(innerCorners[0].y, Is.GreaterThanOrEqualTo(outerCorners[0].y - tolerance), message);
        Assert.That(innerCorners[2].x, Is.LessThanOrEqualTo(outerCorners[2].x + tolerance), message);
        Assert.That(innerCorners[2].y, Is.LessThanOrEqualTo(outerCorners[2].y + tolerance), message);
    }

    private static void AssertReadableText(
        Text text,
        int minimumFontSize,
        FontStyle expectedStyle,
        Color expectedColor,
        string label)
    {
        Assert.That(text, Is.Not.Null, $"{label} text must exist.");
        Assert.That(text.font, Is.Not.Null, $"{label} needs a font.");
        Assert.That(text.font.dynamic, Is.True,
            $"{label} relies on a dynamic font so Unity can render the requested bold weight.");
        Assert.That(text.fontSize, Is.GreaterThanOrEqualTo(minimumFontSize),
            $"{label} is too small for the 1024x576 reference scale.");
        Assert.That(text.fontStyle, Is.EqualTo(expectedStyle),
            $"{label} needs the requested visual weight.");
        Assert.That(text.color.a, Is.EqualTo(1f).Within(0.001f),
            $"{label} must remain fully opaque.");
        Assert.That(Vector4.Distance(text.color, expectedColor), Is.LessThan(0.01f),
            $"{label} must use the approved high-contrast ink role.");
    }

    private static void AssertSubtlePixelShadow(Text text, string label)
    {
        Shadow shadow = text.GetComponent<Shadow>();
        Assert.That(shadow, Is.Not.Null, $"{label} needs a restrained one-direction shadow.");
        Assert.That(Mathf.Abs(shadow.effectDistance.x), Is.InRange(0.5f, 1.5f));
        Assert.That(Mathf.Abs(shadow.effectDistance.y), Is.InRange(0.5f, 1.5f));
        Assert.That(shadow.effectColor.a, Is.InRange(0.2f, 0.5f),
            $"{label} shadow must remain subtle and pixel-like.");
    }

    private static void AssertShortButtonLabelOpticalCenter(Button button, string label)
    {
        const float expectedVisualOffsetY = 3f;
        Text text = button.GetComponentInChildren<Text>();
        RectTransform rect = text.rectTransform;

        Assert.That(text.alignment, Is.EqualTo(TextAnchor.MiddleCenter),
            $"{label} must remain geometrically centered before the optical correction.");
        Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
        Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
        Assert.That(rect.pivot, Is.EqualTo(new Vector2(0.5f, 0.5f)));
        Assert.That(rect.anchoredPosition.x, Is.EqualTo(0f).Within(0.01f));
        Assert.That(rect.anchoredPosition.y, Is.EqualTo(expectedVisualOffsetY).Within(0.01f),
            $"{label} needs the shared upward optical correction for the runtime font baseline.");
        Assert.That(rect.sizeDelta, Is.EqualTo(Vector2.zero),
            $"{label} must keep the full button label rect and avoid clipping after the correction.");
    }

    private static object GetProperty(MonoBehaviour behaviour, string propertyName)
    {
        PropertyInfo property = behaviour.GetType().GetProperty(propertyName);
        Assert.That(property, Is.Not.Null);
        return property.GetValue(behaviour);
    }

    private static object Invoke(MonoBehaviour behaviour, string methodName, params object[] arguments)
    {
        MethodInfo method = behaviour.GetType().GetMethod(methodName);
        Assert.That(method, Is.Not.Null);
        return method.Invoke(behaviour, arguments);
    }

    private static object InvokePrivate(
        MonoBehaviour behaviour,
        string methodName,
        params object[] arguments)
    {
        MethodInfo method = behaviour.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return method.Invoke(behaviour, arguments);
    }

    private static T GetPrivateField<T>(MonoBehaviour behaviour, string fieldName)
        where T : class
    {
        FieldInfo field = behaviour.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return field.GetValue(behaviour) as T;
    }

    private static T GetPrivateFieldValue<T>(MonoBehaviour behaviour, string fieldName)
        where T : struct
    {
        FieldInfo field = behaviour.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (T)field.GetValue(behaviour);
    }

    private static void SetPrivateField(MonoBehaviour behaviour, string fieldName, object value)
    {
        FieldInfo field = behaviour.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(behaviour, value);
    }


    private static MonoBehaviour FindBehaviour(string typeName)
    {
        MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null && behaviours[i].gameObject.scene.IsValid() &&
                behaviours[i].GetType().Name == typeName)
                return behaviours[i];
        }

        return null;
    }

    private static GameObject FindSceneGameObject(string name)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null && objects[i].scene.IsValid() && objects[i].name == name)
                return objects[i];
        }

        return null;
    }

    private static GameObject FindRootObject(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == name)
                return roots[i];
        }

        return null;
    }
}

public sealed class ThrowOnceTowerRunReadyFinalizer : MonoBehaviour, IMapRuntimeReadyFinalizer
{
    public int InvocationCount { get; private set; }

    public bool TryFinalizeBeforeReady(
        MapRuntimeContext context,
        System.Guid generationId,
        out string reason)
    {
        InvocationCount++;

        if (InvocationCount == 1)
            throw new System.InvalidOperationException("Injected ready-finalizer exception.");

        reason = string.Empty;
        return true;
    }
}

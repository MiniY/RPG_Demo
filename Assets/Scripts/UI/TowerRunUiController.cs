using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Builds the MVP startup/death/failure and settings UI inside the existing scene Canvas.
/// </summary>
[DisallowMultipleComponent]
public sealed class TowerRunUiController : MonoBehaviour
{
    private const string LegacyAcknowledgedKey = "tower-run.legacy-save-message-acknowledged.v1";
    private const string VolumeKey = "tower-run.audio.master-volume.v1";
    private const string MutedKey = "tower-run.audio.muted.v1";
    private const string SettingsGateOwner = "ui.settings";
    private const float ShortButtonLabelVisualOffsetY = 3f;

    private TowerRunCoordinator coordinator;
    private GameObject flowOverlay;
    private GameObject settingsOverlay;
    private GameObject audioPage;
    private GameObject savePage;
    private GameObject overwriteDialog;
    private RectTransform settingsFrame;
    private RectTransform overwriteDialogFrame;
    private CanvasGroup settingsFrameInteraction;
    private TowerRunUiSkin settingsSkin;
    private Button audioTabButton;
    private Button saveTabButton;
    private Text flowTitle;
    private Text flowMessage;
    private Text floorLabel;
    private Text saveMessage;
    private Text overwriteDetails;
    private Button primaryButton;
    private Button secondaryButton;
    private readonly Button[] flowSlotButtons = new Button[3];
    private readonly Text[] flowSlotTexts = new Text[3];
    private readonly Button[] saveSlotButtons = new Button[3];
    private readonly Text[] saveSlotTexts = new Text[3];
    private Button beginSaveButton;
    private Button overwriteConfirmButton;
    private Button overwriteCancelButton;
    private TowerRunCheckpointRecord requestedSaveCandidate;
    private int pendingOverwriteSlot;
    private string pendingOverwriteToken;
    private bool awaitingAbandonConfirmation;
    private Slider volumeSlider;
    private Toggle muteToggle;
    private AudioSource previewSource;
    private AudioClip previewClip;
    private TowerRunGateLease settingsGameplayGate;
    private TowerRunGateLease settingsPauseGate;

    public static TowerRunUiController EnsureForScene(TowerRunCoordinator coordinator)
    {
        Canvas canvas = null;
        Canvas[] canvases = FindObjectsOfType<Canvas>();

        // The minimap creates its own Canvas; settings must belong to the scene HUD Canvas.
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i].name == "Canvas" && canvases[i].GetComponent<TowerRunUiSkin>() != null)
            {
                canvas = canvases[i];
                break;
            }
        }

        if (canvas == null)
            canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
            return null;

        TowerRunUiController controller = canvas.GetComponent<TowerRunUiController>();

        if (controller == null)
            controller = canvas.gameObject.AddComponent<TowerRunUiController>();

        controller.Initialize(coordinator);
        return controller;
    }

    private void Awake()
    {
        BuildUi();
        HookExistingSettingsButton();
        LoadAudioPreferences();
    }

    private void OnDestroy()
    {
        if (coordinator != null)
            coordinator.StateChanged -= Refresh;

        ReleaseSettingsGates();
        StopPreviewSound();

        if (previewClip != null)
            Destroy(previewClip);
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame ||
            coordinator == null || coordinator.State != TowerRunFlowState.Playing)
        {
            return;
        }

        if (settingsOverlay.activeSelf)
        {
            if (pendingOverwriteSlot != 0)
            {
                CancelOverwrite();
                return;
            }

            CloseSettings();
            return;
        }

        PackagePanelController package = FindObjectOfType<PackagePanelController>();

        if (package != null && package.IsPackagePanelVisible)
        {
            package.ClosePackagePanel();
            return;
        }

        ShopPanelController shop = FindObjectOfType<ShopPanelController>();

        if (shop != null && shop.IsShopPanelVisible)
        {
            shop.CloseShopPanel();
            return;
        }

        OpenSettings();
    }

    public void Initialize(TowerRunCoordinator value)
    {
        if (coordinator == value)
        {
            Refresh();
            return;
        }

        if (coordinator != null)
            coordinator.StateChanged -= Refresh;

        coordinator = value;

        if (coordinator != null)
            coordinator.StateChanged += Refresh;

        Refresh();
    }

    public void OpenSettings()
    {
        if (coordinator == null || coordinator.State != TowerRunFlowState.Playing)
            return;

        settingsOverlay.SetActive(true);
        settingsGameplayGate ??= TowerRunGateHub.AcquireGameplay(SettingsGateOwner);
        settingsPauseGate ??= TowerRunGateHub.AcquireWorldPause(SettingsGateOwner);
        ShowAudioPage();
    }

    public void CloseSettings()
    {
        ResetSaveRequest();
        StopPreviewSound();
        settingsOverlay.SetActive(false);
        ReleaseSettingsGates();
    }

    private void Refresh()
    {
        if (coordinator == null || flowOverlay == null)
            return;

        floorLabel.text = coordinator.FloorDisplayName;
        TowerRunFlowState state = coordinator.State;
        bool showFlow = state != TowerRunFlowState.Playing;

        if (showFlow && settingsOverlay.activeSelf)
            CloseSettings();

        flowOverlay.SetActive(showFlow);

        if (!showFlow)
        {
            RefreshSaveSlots();
            return;
        }

        primaryButton.gameObject.SetActive(false);
        secondaryButton.gameObject.SetActive(false);

        for (int i = 0; i < flowSlotButtons.Length; i++)
            flowSlotButtons[i].gameObject.SetActive(false);

        switch (state)
        {
            case TowerRunFlowState.StartupChoice:
                ConfigureChoice("冒险", "开始新冒险，或选择一个有效正式槽位继续。", true);
                ShowLegacyMessageIfNeeded();
                break;
            case TowerRunFlowState.DeathChoice:
                ConfigureChoice("你已倒下", "选择一个正式槽位恢复，或开始新的冒险。", true);
                break;
            case TowerRunFlowState.Failure:
                flowTitle.text = "操作失败";
                flowMessage.text = coordinator.FailureMessage;
                ConfigureButton(
                    primaryButton,
                    "重试",
                    coordinator.RetryFailedOperation,
                    coordinator.CanRetryFailedOperation);
                awaitingAbandonConfirmation = false;
                ConfigureButton(secondaryButton, "返回启动选择", RequestReturnToStartup, true);
                break;
            default:
                flowTitle.text = "正在准备冒险";
                flowMessage.text = "数据、地图、玩家与依赖完成最终校验后才会开放玩法。";
                break;
        }
    }

    private void RequestReturnToStartup()
    {
        if (coordinator.OperationKind != TowerRunOperationKind.FloorTransition || awaitingAbandonConfirmation)
        {
            coordinator.ReturnToStartupChoice();
            return;
        }

        awaitingAbandonConfirmation = true;
        flowMessage.text = "返回启动选择将放弃当前未正式保存的运行进展。正式槽位不会改变。";
        ConfigureButton(secondaryButton, "确认放弃并返回", RequestReturnToStartup, true);
    }

    private void ConfigureChoice(string title, string message, bool allowNewRun)
    {
        flowTitle.text = title;
        flowMessage.text = message;
        ConfigureButton(primaryButton, "开始新冒险", coordinator.BeginNewRun, allowNewRun);

        for (int i = 0; i < flowSlotButtons.Length; i++)
        {
            int slotNumber = i + 1;
            TowerRunSlotReadResult read = coordinator.ReadSlot(slotNumber);
            flowSlotButtons[i].gameObject.SetActive(true);
            flowSlotButtons[i].interactable = read.state == TowerRunSlotState.Valid;
            flowSlotTexts[i].text = FormatSlot(slotNumber, read);
            flowSlotButtons[i].onClick.RemoveAllListeners();
            flowSlotButtons[i].onClick.AddListener(() => coordinator.BeginRestoreSlot(slotNumber));
        }
    }

    private void ShowLegacyMessageIfNeeded()
    {
        ITowerRunPreferenceStore preferences = TowerRunRuntimeEnvironment.Preferences;

        if (preferences.GetInt(LegacyAcknowledgedKey, 0) != 0)
            return;

        LegacySaveDetectionResult result = new LegacySaveDetector(
            new LegacySavePhysicalFileProbe(),
            TowerRunRuntimeEnvironment.LegacySavePath).Detect();

        if (result.state != LegacySaveState.Detected)
            return;

        flowMessage.text += "\n\n" + result.message;
        ConfigureButton(secondaryButton, "知道了", () =>
        {
            preferences.SetInt(LegacyAcknowledgedKey, 1);
            preferences.Save();
            Refresh();
        }, true);
    }

    private void ShowAudioPage()
    {
        audioPage.SetActive(true);
        savePage.SetActive(false);
        ResetSaveRequest();
        StyleSettingsTabs(true);
    }

    private void ShowSavePage()
    {
        StopPreviewSound();
        audioPage.SetActive(false);
        savePage.SetActive(true);
        ResetSaveRequest();
        StyleSettingsTabs(false);
        saveMessage.text = coordinator.HasEntranceCandidate
            ? "先请求“保存本层入口”，再选择正式槽位。\n进入本层后的变化不会写入。"
            : "当前没有可保存的入口候选。";
        beginSaveButton.interactable = coordinator.HasEntranceCandidate;
        StyleSaveButton(beginSaveButton, false);
        beginSaveButton.gameObject.SetActive(true);
        overwriteConfirmButton.gameObject.SetActive(false);
        overwriteCancelButton.gameObject.SetActive(false);
        RefreshSaveSlots();
    }

    private void BeginSaveRequest()
    {
        if (!coordinator.TryCreateSaveRequest(
                out TowerRunCheckpointRecord candidate,
                out string message))
        {
            saveMessage.text = message;
            return;
        }

        requestedSaveCandidate = candidate;
        beginSaveButton.gameObject.SetActive(false);
        saveMessage.text = "待保存：\n" + FormatCandidate(candidate) +
                           "\n请选择一个正式槽位。";
        RefreshSaveSlots();
    }

    private void RefreshSaveSlots()
    {
        if (coordinator == null || saveSlotButtons[0] == null)
            return;

        for (int i = 0; i < saveSlotButtons.Length; i++)
        {
            int slotNumber = i + 1;
            TowerRunSlotReadResult read = coordinator.ReadSlot(slotNumber);
            saveSlotTexts[i].text = FormatSlotCard(slotNumber, read);
            saveSlotButtons[i].interactable = requestedSaveCandidate != null;
            StyleSaveButton(saveSlotButtons[i], true);
            saveSlotButtons[i].onClick.RemoveAllListeners();
            saveSlotButtons[i].onClick.AddListener(() => SelectSaveSlot(slotNumber));
        }
    }

    private void SelectSaveSlot(int slotNumber)
    {
        if (requestedSaveCandidate == null)
            return;

        TowerRunSlotReadResult read = coordinator.ReadSlot(slotNumber);

        if (read.state == TowerRunSlotState.Empty)
        {
            CommitSlot(slotNumber, false);
            return;
        }

        pendingOverwriteSlot = slotNumber;
        pendingOverwriteToken = read.confirmationToken;
        overwriteDetails.text = $"现有记录\n{FormatSlotForDialog(slotNumber, read)}\n\n" +
                                $"即将覆盖为\n{FormatCandidateForDialog(requestedSaveCandidate)}";
        overwriteConfirmButton.gameObject.SetActive(true);
        overwriteCancelButton.gameObject.SetActive(true);

        for (int i = 0; i < saveSlotButtons.Length; i++)
            saveSlotButtons[i].interactable = false;

        SetOverwriteDialogActive(true);
    }

    private void ConfirmOverwrite()
    {
        if (pendingOverwriteSlot == 0)
            return;

        CommitSlot(pendingOverwriteSlot, true);
    }

    private void CancelOverwrite()
    {
        pendingOverwriteSlot = 0;
        pendingOverwriteToken = null;
        SetOverwriteDialogActive(false);
        overwriteConfirmButton.gameObject.SetActive(false);
        overwriteCancelButton.gameObject.SetActive(false);
        saveMessage.text = requestedSaveCandidate == null
            ? "当前没有待保存候选。"
            : "待保存：\n" + FormatCandidate(requestedSaveCandidate) +
              "\n请选择一个正式槽位。";
        RefreshSaveSlots();
    }

    private void CommitSlot(int slotNumber, bool overwriteConfirmed)
    {
        TowerRunSlotWriteResult result = coordinator.TrySaveEntranceCandidate(
            slotNumber,
            requestedSaveCandidate,
            overwriteConfirmed,
            overwriteConfirmed ? pendingOverwriteToken : null);
        saveMessage.text = result.message;
        pendingOverwriteSlot = 0;
        pendingOverwriteToken = null;
        SetOverwriteDialogActive(false);
        overwriteConfirmButton.gameObject.SetActive(false);
        overwriteCancelButton.gameObject.SetActive(false);

        if (result.succeeded)
        {
            requestedSaveCandidate = null;
            beginSaveButton.gameObject.SetActive(true);
            beginSaveButton.interactable = coordinator.HasEntranceCandidate;
            StyleSaveButton(beginSaveButton, false);
        }

        RefreshSaveSlots();
    }

    private void HookExistingSettingsButton()
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();

        for (int i = 0; i < objects.Length; i++)
        {
            GameObject candidate = objects[i];

            if (candidate == null || !candidate.scene.IsValid() || candidate.name != "SettingButton")
                continue;

            PackageHudButtonUI wrongBinding = candidate.GetComponent<PackageHudButtonUI>();

            if (wrongBinding != null)
                wrongBinding.enabled = false;

            Button button = candidate.GetComponent<Button>();

            if (button != null)
            {
                button.onClick.RemoveListener(OpenSettings);
                button.onClick.AddListener(OpenSettings);
            }
        }
    }

    private void LoadAudioPreferences()
    {
        ITowerRunPreferenceStore preferences = TowerRunRuntimeEnvironment.Preferences;
        float volume = Mathf.Clamp01(preferences.GetFloat(VolumeKey, 1f));
        bool muted = preferences.GetInt(MutedKey, 0) != 0;
        volumeSlider.SetValueWithoutNotify(volume);
        muteToggle.SetIsOnWithoutNotify(muted);
        ApplyAudio(volume, muted, persist: false);
    }

    private void HandleVolumeChanged(float value)
    {
        ApplyAudio(value, muteToggle.isOn, persist: true);
    }

    private void HandleMuteChanged(bool muted)
    {
        ApplyAudio(volumeSlider.value, muted, persist: true);
    }

    private static void ApplyAudio(float volume, bool muted, bool persist)
    {
        volume = Mathf.Clamp01(volume);
        AudioListener.volume = muted ? 0f : volume;

        if (!persist)
            return;

        ITowerRunPreferenceStore preferences = TowerRunRuntimeEnvironment.Preferences;
        preferences.SetFloat(VolumeKey, volume);
        preferences.SetInt(MutedKey, muted ? 1 : 0);
        preferences.Save();
    }

    private void PlayPreviewSound()
    {
        if (previewClip == null)
        {
            const int sampleRate = 44100;
            const float duration = 0.18f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float envelope = 1f - i / (float)sampleCount;
                samples[i] = Mathf.Sin(2f * Mathf.PI * 660f * i / sampleRate) * envelope * 0.25f;
            }

            previewClip = AudioClip.Create("TowerRunSettingsPreview", sampleCount, 1, sampleRate, false);
            previewClip.SetData(samples, 0);
        }

        previewSource.Stop();
        previewSource.clip = previewClip;
        previewSource.Play();
    }

    private void BuildUi()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        flowOverlay = CreateOverlay("TowerRunFlowOverlay", transform, new Color(0f, 0f, 0f, 0.82f));
        Transform flowContent = CreateVerticalContent("FlowContent", flowOverlay.transform);
        flowTitle = CreateText("Title", flowContent, font, 32, TextAnchor.MiddleCenter);
        flowMessage = CreateText("Message", flowContent, font, 18, TextAnchor.MiddleCenter);
        primaryButton = CreateButton("Primary", flowContent, font, out _);
        secondaryButton = CreateButton("Secondary", flowContent, font, out _);

        for (int i = 0; i < flowSlotButtons.Length; i++)
            flowSlotButtons[i] = CreateButton($"FlowSlot{i + 1}", flowContent, font, out flowSlotTexts[i]);

        settingsSkin = GetComponent<TowerRunUiSkin>();
        settingsOverlay = CreateOverlay("TowerRunSettingsOverlay", transform, new Color(0f, 0f, 0f, 0.72f));
        settingsFrame = CreateSettingsArt("SettingsWoodFrame", settingsOverlay.transform,
            settingsSkin != null ? settingsSkin.WoodFrame : null, new Color(0.27f, 0.18f, 0.17f));
        settingsFrameInteraction = settingsFrame.gameObject.AddComponent<CanvasGroup>();
        settingsFrame.anchorMin = settingsFrame.anchorMax = new Vector2(0.5f, 0.5f);
        settingsFrame.pivot = new Vector2(0.5f, 0.5f);
        settingsFrame.sizeDelta = new Vector2(840f, 820f);
        RectTransform paper = CreateSettingsArt("SettingsCarvedPaper", settingsFrame,
            settingsSkin != null ? settingsSkin.CarvedPaper : null, new Color(0.85f, 0.76f, 0.58f));
        StretchInside(paper, 20f, 24f);
        Transform settingsContent = CreateSettingsContent(paper);

        GameObject header = new GameObject("SettingsHeader", typeof(RectTransform), typeof(LayoutElement));
        header.transform.SetParent(settingsContent, false);
        LayoutElement headerLayout = header.GetComponent<LayoutElement>();
        headerLayout.minHeight = 96f;
        headerLayout.preferredHeight = 96f;
        headerLayout.flexibleHeight = 0f;
        RectTransform banner = CreateSettingsArt("TitleBanner", header.transform,
            settingsSkin != null ? settingsSkin.TitleBanner : null, Color.clear, false);
        banner.anchorMin = banner.anchorMax = new Vector2(0.5f, 0.5f);
        banner.sizeDelta = new Vector2(88f, 84f);
        banner.anchoredPosition = new Vector2(-92f, 0f);
        banner.GetComponent<Image>().preserveAspect = true;
        Text settingsTitle = CreateText("SettingsTitle", header.transform, font, 38, TextAnchor.MiddleCenter);
        settingsTitle.text = "设置";
        StyleSettingsText(settingsTitle, TowerRunUiSkin.Ink, 38, FontStyle.Bold, true);
        settingsTitle.rectTransform.anchorMin = settingsTitle.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        settingsTitle.rectTransform.sizeDelta = new Vector2(150f, 70f);
        settingsTitle.rectTransform.anchoredPosition = new Vector2(28f, 0f);

        Transform tabRow = CreateSettingsRow("SettingsTabs", settingsContent, 64f);
        audioTabButton = CreateSettingsButton(
            "AudioTab", tabRow, font, "音频", 54f, opticallyCenterShortLabel: true);
        audioTabButton.onClick.AddListener(ShowAudioPage);
        saveTabButton = CreateSettingsButton(
            "SaveTab", tabRow, font, "存档", 54f, opticallyCenterShortLabel: true);
        saveTabButton.onClick.AddListener(ShowSavePage);

        audioPage = CreateSettingsPage("AudioPage", settingsContent);
        Transform audioContent = CreateVerticalContent("AudioContent", audioPage.transform);
        VerticalLayoutGroup audioLayout = audioContent.GetComponent<VerticalLayoutGroup>();
        audioLayout.spacing = 14f;
        audioLayout.childAlignment = TextAnchor.MiddleCenter;
        audioLayout.childForceExpandWidth = false;
        Text volumeLabel = CreateText("VolumeLabel", audioContent, font, 24, TextAnchor.MiddleCenter);
        volumeLabel.text = "总音量";
        StyleSettingsText(volumeLabel, TowerRunUiSkin.Ink, 24, FontStyle.Bold);
        SetPreferredWidth(volumeLabel.GetComponent<LayoutElement>(), 590f);
        volumeSlider = CreateSlider("MasterVolume", audioContent);
        SetPreferredWidth(volumeSlider.GetComponent<LayoutElement>(), 590f);
        StyleAudioSlider(volumeSlider);
        volumeSlider.onValueChanged.AddListener(HandleVolumeChanged);
        muteToggle = CreateToggle("Mute", audioContent, font, "静音");
        SetPreferredWidth(muteToggle.GetComponent<LayoutElement>(), 590f);
        StyleAudioToggle(muteToggle);
        muteToggle.onValueChanged.AddListener(HandleMuteChanged);
        Button preview = CreateSettingsButton(
            "Preview", audioContent, font, "试听", 60f, opticallyCenterShortLabel: true);
        SetPreferredWidth(preview.GetComponent<LayoutElement>(), 300f);
        preview.onClick.AddListener(PlayPreviewSound);

        savePage = CreateSettingsPage("SavePage", settingsContent);
        GameObject saveViewport = new GameObject("SaveViewport", typeof(RectTransform), typeof(RectMask2D));
        saveViewport.transform.SetParent(savePage.transform, false);
        StretchInside((RectTransform)saveViewport.transform, 12f, 6f);
        Transform saveContent = CreateVerticalContent("SaveContent", saveViewport.transform);
        RectTransform saveContentRect = (RectTransform)saveContent;
        saveContentRect.anchorMin = saveContentRect.anchorMax = new Vector2(0.5f, 1f);
        saveContentRect.pivot = new Vector2(0.5f, 1f);
        saveContentRect.anchoredPosition = Vector2.zero;
        saveContentRect.sizeDelta = new Vector2(690f, 0f);
        ScrollRect saveScroll = savePage.AddComponent<ScrollRect>();
        saveScroll.viewport = (RectTransform)saveViewport.transform;
        saveScroll.content = saveContentRect;
        saveScroll.horizontal = false;
        saveScroll.vertical = true;
        saveScroll.movementType = ScrollRect.MovementType.Clamped;
        saveScroll.scrollSensitivity = 25f;
        saveMessage = CreateText("SaveMessage", saveContent, font, 24, TextAnchor.MiddleLeft);
        StyleSettingsText(saveMessage, TowerRunUiSkin.Ink, 24, FontStyle.Bold);
        saveMessage.GetComponent<LayoutElement>().minHeight = 88f;
        beginSaveButton = CreateSettingsButton("BeginSaveRequest", saveContent, font, "保存本层入口", 52f);
        beginSaveButton.onClick.AddListener(BeginSaveRequest);

        for (int i = 0; i < saveSlotButtons.Length; i++)
        {
            saveSlotButtons[i] = CreateSettingsButton($"SaveSlot{i + 1}", saveContent, font, "", 78f);
            saveSlotTexts[i] = saveSlotButtons[i].GetComponentInChildren<Text>();
            saveSlotTexts[i].alignment = TextAnchor.MiddleLeft;
            StyleSettingsText(saveSlotTexts[i], TowerRunUiSkin.Ink, 24, FontStyle.Bold, true);
            saveSlotTexts[i].rectTransform.offsetMin = new Vector2(34f, 4f);
            saveSlotTexts[i].rectTransform.offsetMax = new Vector2(-20f, -4f);
        }

        Button close = CreateSettingsButton(
            "CloseSettings", settingsContent, font, "关闭", 56f, opticallyCenterShortLabel: true);
        close.onClick.AddListener(CloseSettings);

        overwriteDialog = CreateOverlay("OverwriteDialogOverlay", settingsOverlay.transform,
            new Color(0f, 0f, 0f, 0.82f));
        overwriteDialogFrame = CreateSettingsArt("OverwriteWoodFrame", overwriteDialog.transform,
            settingsSkin != null ? settingsSkin.WoodFrame : null, new Color(0.27f, 0.18f, 0.17f));
        overwriteDialogFrame.anchorMin = overwriteDialogFrame.anchorMax = new Vector2(0.5f, 0.5f);
        overwriteDialogFrame.sizeDelta = new Vector2(720f, 480f);
        RectTransform dialogPaper = CreateSettingsArt("OverwriteCarvedPaper", overwriteDialogFrame,
            settingsSkin != null ? settingsSkin.CarvedPaper : null, new Color(0.85f, 0.76f, 0.58f));
        StretchInside(dialogPaper, 22f, 24f);
        Transform dialogContent = CreateSettingsContent(dialogPaper);
        Text dialogTitle = CreateText("OverwriteTitle", dialogContent, font, 34, TextAnchor.MiddleCenter);
        dialogTitle.text = "确认覆盖存档";
        StyleSettingsText(dialogTitle, TowerRunUiSkin.Ink, 34, FontStyle.Bold, true);
        dialogTitle.GetComponent<LayoutElement>().preferredHeight = 62f;
        overwriteDetails = CreateText("OverwriteDetails", dialogContent, font, 24, TextAnchor.MiddleLeft);
        StyleSettingsText(overwriteDetails, TowerRunUiSkin.MutedInk, 24, FontStyle.Bold);
        overwriteDetails.lineSpacing = 1.15f;
        overwriteDetails.GetComponent<LayoutElement>().flexibleHeight = 1f;
        Transform dialogButtons = CreateSettingsRow("OverwriteActions", dialogContent, 72f);
        overwriteCancelButton = CreateSettingsButton("CancelOverwrite", dialogButtons, font, "取消覆盖", 64f);
        overwriteCancelButton.onClick.AddListener(CancelOverwrite);
        overwriteConfirmButton = CreateSettingsButton("ConfirmOverwrite", dialogButtons, font, "确认覆盖", 64f,
            settingsSkin != null ? settingsSkin.RedButton : null);
        overwriteConfirmButton.onClick.AddListener(ConfirmOverwrite);
        ResizeSettingsFrame();

        GameObject floorObject = new GameObject("TowerFloorLabel", typeof(RectTransform), typeof(Text));
        floorObject.transform.SetParent(transform, false);
        RectTransform floorRect = (RectTransform)floorObject.transform;
        floorRect.anchorMin = new Vector2(0.5f, 1f);
        floorRect.anchorMax = new Vector2(0.5f, 1f);
        floorRect.pivot = new Vector2(0.5f, 1f);
        floorRect.anchoredPosition = new Vector2(0f, -20f);
        floorRect.sizeDelta = new Vector2(280f, 42f);
        floorLabel = floorObject.GetComponent<Text>();
        floorLabel.font = font;
        floorLabel.fontSize = 22;
        floorLabel.alignment = TextAnchor.MiddleCenter;
        floorLabel.color = Color.white;

        previewSource = gameObject.AddComponent<AudioSource>();
        previewSource.playOnAwake = false;
        previewSource.ignoreListenerPause = true;
        flowOverlay.SetActive(false);
        settingsOverlay.SetActive(false);
        savePage.SetActive(false);
        SetOverwriteDialogActive(false);
        overwriteConfirmButton.gameObject.SetActive(false);
        overwriteCancelButton.gameObject.SetActive(false);
    }

    private void OnRectTransformDimensionsChange()
    {
        ResizeSettingsFrame();
    }

    private void ResizeSettingsFrame()
    {
        if (settingsFrame == null)
            return;

        Rect canvasRect = ((RectTransform)transform).rect;
        settingsFrame.sizeDelta = new Vector2(
            Mathf.Min(840f, canvasRect.width - 32f),
            Mathf.Min(820f, canvasRect.height - 24f));

        if (overwriteDialogFrame != null)
        {
            overwriteDialogFrame.sizeDelta = new Vector2(
                Mathf.Min(720f, canvasRect.width - 64f),
                Mathf.Min(480f, canvasRect.height - 48f));
        }
    }

    private static RectTransform CreateSettingsArt(string name, Transform parent, Sprite sprite,
        Color fallback, bool sliced = true)
    {
        GameObject result = new GameObject(name, typeof(RectTransform), typeof(Image));
        result.transform.SetParent(parent, false);
        TowerRunUiSkin.ApplySprite(result.GetComponent<Image>(), sprite, fallback, sliced);
        return (RectTransform)result.transform;
    }

    private static void StretchInside(RectTransform rect, float horizontal, float vertical)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(horizontal, vertical);
        rect.offsetMax = new Vector2(-horizontal, -vertical);
    }

    private static Transform CreateSettingsContent(Transform parent)
    {
        GameObject result = new GameObject("SettingsLayout", typeof(RectTransform), typeof(VerticalLayoutGroup));
        result.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)result.transform;
        StretchInside(rect, 28f, 20f);
        VerticalLayoutGroup layout = result.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 4, 4);
        layout.spacing = 9f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return result.transform;
    }

    private static Transform CreateSettingsRow(string name, Transform parent, float height)
    {
        GameObject result = new GameObject(name,
            typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        result.transform.SetParent(parent, false);
        LayoutElement element = result.GetComponent<LayoutElement>();
        element.minHeight = height;
        element.preferredHeight = height;
        element.flexibleHeight = 0f;
        HorizontalLayoutGroup layout = result.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = true;
        return result.transform;
    }

    private static void SetPreferredWidth(LayoutElement element, float width)
    {
        element.minWidth = width;
        element.preferredWidth = width;
        element.flexibleWidth = 0f;
    }

    private static GameObject CreateSettingsPage(string name, Transform parent)
    {
        GameObject page = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        page.transform.SetParent(parent, false);
        Image image = page.GetComponent<Image>();
        image.color = new Color(1f, 0.98f, 0.88f, 0.3f);
        page.GetComponent<LayoutElement>().flexibleHeight = 1f;
        return page;
    }

    private Button CreateSettingsButton(string name, Transform parent, Font font, string caption,
        float height, Sprite sprite = null, bool opticallyCenterShortLabel = false)
    {
        Button button = CreateButton(name, parent, font, out Text label);
        label.text = caption;
        StyleSettingsText(label, TowerRunUiSkin.Ink, 24, FontStyle.Bold, true);

        if (opticallyCenterShortLabel)
            ApplyShortButtonLabelOpticalCenter(label);

        LayoutElement element = button.GetComponent<LayoutElement>();
        element.minHeight = Mathf.Max(60f, height);
        element.preferredHeight = Mathf.Max(60f, height);
        element.flexibleWidth = 1f;
        element.flexibleHeight = 0f;

        if (settingsSkin != null)
            settingsSkin.StyleButton(button, sprite);

        return button;
    }

    private static void ApplyShortButtonLabelOpticalCenter(Text label)
    {
        label.alignment = TextAnchor.MiddleCenter;
        RectTransform rect = label.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = new Vector2(0f, ShortButtonLabelVisualOffsetY);
    }

    private void StyleSettingsTabs(bool audioSelected)
    {
        if (settingsSkin == null)
            return;

        TowerRunUiSkin.ApplySprite(audioTabButton.GetComponent<Image>(),
            audioSelected ? settingsSkin.BlueRibbon : settingsSkin.BlueButton,
            new Color(0.21f, 0.44f, 0.59f));
        TowerRunUiSkin.ApplySprite(saveTabButton.GetComponent<Image>(),
            audioSelected ? settingsSkin.BlueButton : settingsSkin.BlueRibbon,
            new Color(0.21f, 0.44f, 0.59f));
        StyleSettingsText(audioTabButton.GetComponentInChildren<Text>(),
            audioSelected ? TowerRunUiSkin.LightInk : TowerRunUiSkin.Ink,
            24,
            FontStyle.Bold,
            true);
        StyleSettingsText(saveTabButton.GetComponentInChildren<Text>(),
            audioSelected ? TowerRunUiSkin.Ink : TowerRunUiSkin.LightInk,
            24,
            FontStyle.Bold,
            true);
    }

    private void StyleSaveButton(Button button, bool slotCard)
    {
        if (settingsSkin == null)
            return;

        TowerRunUiSkin.ApplySprite(button.GetComponent<Image>(),
            slotCard ? settingsSkin.CarvedCard :
                (button.interactable ? settingsSkin.BlueButton : settingsSkin.DisabledButton),
            slotCard ? new Color(0.85f, 0.76f, 0.58f) : new Color(0.21f, 0.44f, 0.59f));
        ColorBlock colors = button.colors;
        colors.disabledColor = Color.white;
        button.colors = colors;
    }

    private static void StyleAudioSlider(Slider slider)
    {
        Image background = slider.transform.Find("Background").GetComponent<Image>();
        background.color = new Color(0.31f, 0.24f, 0.22f, 1f);
        background.transform.Find("Fill").GetComponent<Image>().color =
            new Color(0.21f, 0.53f, 0.67f, 1f);
        slider.transform.Find("Handle").GetComponent<Image>().color =
            new Color(0.98f, 0.92f, 0.72f, 1f);
    }

    private static void StyleAudioToggle(Toggle toggle)
    {
        Image background = toggle.transform.Find("Background").GetComponent<Image>();
        background.color = TowerRunUiSkin.Ink;
        background.GetComponentInChildren<Text>().color = new Color(0.37f, 0.74f, 0.83f, 1f);
        StyleSettingsText(toggle.transform.Find("Label").GetComponent<Text>(),
            TowerRunUiSkin.Ink, 24, FontStyle.Bold);
    }

    private static GameObject CreateOverlay(string name, Transform parent, Color color)
    {
        GameObject result = new GameObject(name, typeof(RectTransform), typeof(Image));
        result.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)result.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        result.GetComponent<Image>().color = color;
        return result;
    }

    private static GameObject CreatePanel(string name, Transform parent, float minimumHeight)
    {
        GameObject result = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Image),
            typeof(LayoutElement));
        result.transform.SetParent(parent, false);
        result.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 0.95f);
        result.GetComponent<LayoutElement>().minHeight = minimumHeight;
        return result;
    }

    private static Transform CreateVerticalContent(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        result.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)result.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(620f, 0f);
        VerticalLayoutGroup layout = result.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(18, 18, 18, 18);
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = result.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return result.transform;
    }

    private static Text CreateText(string name, Transform parent, Font font, int size, TextAnchor anchor)
    {
        GameObject result = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        result.transform.SetParent(parent, false);
        Text text = result.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        result.GetComponent<LayoutElement>().minHeight = Mathf.Max(40f, size * 2f);
        return text;
    }

    private static void StyleSettingsText(
        Text text,
        Color color,
        int fontSize,
        FontStyle fontStyle,
        bool addShadow = false)
    {
        text.color = new Color(color.r, color.g, color.b, 1f);
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;

        if (!addShadow)
            return;

        Shadow shadow = text.GetComponent<Shadow>();
        if (shadow == null)
            shadow = text.gameObject.AddComponent<Shadow>();

        shadow.effectColor = TowerRunUiSkin.TextShadow;
        shadow.effectDistance = new Vector2(1f, -1f);
        shadow.useGraphicAlpha = true;
    }

    private static Button CreateButton(string name, Transform parent, Font font, out Text label)
    {
        GameObject result = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        result.transform.SetParent(parent, false);
        result.GetComponent<Image>().color = new Color(0.18f, 0.3f, 0.48f, 1f);
        result.GetComponent<LayoutElement>().minHeight = 48f;
        Button button = result.GetComponent<Button>();
        label = CreateText("Label", result.transform, font, 17, TextAnchor.MiddleCenter);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        return button;
    }

    private static Slider CreateSlider(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
        result.transform.SetParent(parent, false);
        LayoutElement sliderLayout = result.GetComponent<LayoutElement>();
        sliderLayout.minHeight = 60f;
        sliderLayout.preferredHeight = 60f;
        Slider slider = result.GetComponent<Slider>();
        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(result.transform, false);
        RectTransform backgroundRect = (RectTransform)background.transform;
        backgroundRect.anchorMin = new Vector2(0f, 0.39f);
        backgroundRect.anchorMax = new Vector2(1f, 0.61f);
        backgroundRect.offsetMin = new Vector2(12f, 0f);
        backgroundRect.offsetMax = new Vector2(-12f, 0f);
        background.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.24f, 1f);

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(background.transform, false);
        RectTransform fillRect = (RectTransform)fill.transform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fill.GetComponent<Image>().color = new Color(0.25f, 0.65f, 0.95f, 1f);

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(result.transform, false);
        RectTransform handleRect = (RectTransform)handle.transform;
        handleRect.sizeDelta = new Vector2(34f, 42f);
        handle.GetComponent<Image>().color = TowerRunUiSkin.Ink;
        GameObject handleFill = new GameObject("PixelFill", typeof(RectTransform), typeof(Image));
        handleFill.transform.SetParent(handle.transform, false);
        StretchInside((RectTransform)handleFill.transform, 5f, 5f);
        handleFill.GetComponent<Image>().color = new Color(0.98f, 0.92f, 0.72f, 1f);

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle.GetComponent<Image>();
        return slider;
    }

    private static Toggle CreateToggle(string name, Transform parent, Font font, string text)
    {
        GameObject result = new GameObject(name, typeof(RectTransform), typeof(Toggle), typeof(LayoutElement));
        result.transform.SetParent(parent, false);
        LayoutElement toggleLayout = result.GetComponent<LayoutElement>();
        toggleLayout.minHeight = 52f;
        toggleLayout.preferredHeight = 52f;
        Toggle toggle = result.GetComponent<Toggle>();
        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(result.transform, false);
        RectTransform backgroundRect = (RectTransform)background.transform;
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0f, 0.5f);
        backgroundRect.pivot = new Vector2(0f, 0.5f);
        backgroundRect.anchoredPosition = new Vector2(12f, 0f);
        backgroundRect.sizeDelta = new Vector2(36f, 36f);
        Image backgroundImage = background.GetComponent<Image>();
        backgroundImage.color = new Color(0.2f, 0.2f, 0.24f, 1f);
        GameObject boxFill = new GameObject("PixelFill", typeof(RectTransform), typeof(Image));
        boxFill.transform.SetParent(background.transform, false);
        StretchInside((RectTransform)boxFill.transform, 4f, 4f);
        boxFill.GetComponent<Image>().color = new Color(0.98f, 0.92f, 0.72f, 1f);
        Text checkmark = CreateText("Checkmark", background.transform, font, 22, TextAnchor.MiddleCenter);
        checkmark.text = "✓";
        checkmark.color = new Color(0.25f, 0.8f, 1f, 1f);
        RectTransform checkmarkRect = checkmark.rectTransform;
        checkmarkRect.anchorMin = Vector2.zero;
        checkmarkRect.anchorMax = Vector2.one;
        checkmarkRect.offsetMin = Vector2.zero;
        checkmarkRect.offsetMax = Vector2.zero;
        Text label = CreateText("Label", result.transform, font, 22, TextAnchor.MiddleLeft);
        label.text = text;
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(60f, 0f);
        labelRect.offsetMax = Vector2.zero;
        toggle.targetGraphic = backgroundImage;
        toggle.graphic = checkmark;
        return toggle;
    }

    private static void ConfigureButton(Button button, string text, UnityEngine.Events.UnityAction action, bool active)
    {
        button.gameObject.SetActive(active);
        Text label = button.GetComponentInChildren<Text>();

        if (label != null)
            label.text = text;

        button.onClick.RemoveAllListeners();

        if (action != null)
            button.onClick.AddListener(action);
    }

    private static string FormatSlot(int slotNumber, TowerRunSlotReadResult read)
    {
        if (read == null)
            return $"槽位 {slotNumber}：不可用";

        if (read.state != TowerRunSlotState.Valid || read.summary == null)
            return $"槽位 {slotNumber}：{read.state} - {read.message}";

        TowerRunSlotSummary summary = read.summary;
        return $"槽位 {slotNumber}：有效 / {summary.playerName} / {summary.floorDisplayName} / " +
               $"HP {summary.currentHealth:0}/{summary.maxHealth} / 保存于 {FormatSavedAtLocal(summary.savedAtUtc)}";
    }

    private static string FormatSlotCard(int slotNumber, TowerRunSlotReadResult read)
    {
        if (read == null)
            return $"槽位 {slotNumber}  ·  不可用\n无法读取此槽位";

        if (read.state != TowerRunSlotState.Valid || read.summary == null)
        {
            if (read.state == TowerRunSlotState.Empty)
                return $"槽位 {slotNumber}  ·  空槽位\n尚无正式存档";

            string state = read.state switch
            {
                TowerRunSlotState.Empty => "空槽位",
                TowerRunSlotState.Corrupt => "记录损坏",
                TowerRunSlotState.Incompatible => "版本不兼容",
                _ => "不可用"
            };
            return $"槽位 {slotNumber}  ·  {state}\n{Shorten(read.message, 48)}";
        }

        TowerRunSlotSummary summary = read.summary;
        return $"槽位 {slotNumber}  ·  {Shorten(summary.playerName, 20)}  ·  " +
               $"{Shorten(summary.floorDisplayName, 22)}\n" +
               $"HP {summary.currentHealth:0}/{summary.maxHealth}  ·  " +
               $"保存于 {FormatSavedAtLocal(summary.savedAtUtc)}";
    }

    private static string FormatSlotForDialog(int slotNumber, TowerRunSlotReadResult read)
    {
        if (read?.state != TowerRunSlotState.Valid || read.summary == null)
            return $"槽位 {slotNumber} · 当前记录不可用";

        TowerRunSlotSummary summary = read.summary;
        return $"槽位 {slotNumber} · {Shorten(summary.playerName, 20)} · " +
               $"{Shorten(summary.floorDisplayName, 22)}\n" +
               $"HP {summary.currentHealth:0}/{summary.maxHealth} · " +
               $"保存于 {FormatSavedAtLocal(summary.savedAtUtc)}";
    }

    private static string FormatSavedAtLocal(string savedAtUtc)
    {
        if (!DateTime.TryParseExact(
                savedAtUtc,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTime parsed))
        {
            return "保存时间未知";
        }

        return parsed.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private static string Shorten(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value;

        return value.Substring(0, maxLength - 1) + "…";
    }

    private static string FormatCandidate(TowerRunCheckpointRecord candidate)
    {
        if (candidate?.floor == null || candidate.player?.stats == null)
            return "候选不可用";

        return $"{candidate.playerName} / {candidate.floor.displayName} / " +
               $"HP {candidate.player.stats.currentHealth:0}/{candidate.player.stats.maxHealth} / 待保存";
    }

    private static string FormatCandidateForDialog(TowerRunCheckpointRecord candidate)
    {
        if (candidate?.floor == null || candidate.player?.stats == null)
            return "候选不可用";

        return $"{Shorten(candidate.playerName, 20)} · {Shorten(candidate.floor.displayName, 22)}\n" +
               $"HP {candidate.player.stats.currentHealth:0}/{candidate.player.stats.maxHealth} · 本层入口";
    }

    private void SetOverwriteDialogActive(bool active)
    {
        if (settingsFrameInteraction != null)
        {
            settingsFrameInteraction.interactable = !active;
            settingsFrameInteraction.blocksRaycasts = !active;
        }

        if (overwriteDialog == null)
            return;

        if (active)
            overwriteDialog.transform.SetAsLastSibling();

        overwriteDialog.SetActive(active);
    }

    private void ResetSaveRequest()
    {
        requestedSaveCandidate = null;
        pendingOverwriteSlot = 0;
        pendingOverwriteToken = null;

        if (overwriteConfirmButton != null)
            overwriteConfirmButton.gameObject.SetActive(false);

        if (overwriteCancelButton != null)
            overwriteCancelButton.gameObject.SetActive(false);

        SetOverwriteDialogActive(false);
    }

    private void StopPreviewSound()
    {
        if (previewSource == null)
            return;

        previewSource.Stop();
        previewSource.clip = null;
    }

    private void ReleaseSettingsGates()
    {
        settingsGameplayGate?.Dispose();
        settingsGameplayGate = null;
        settingsPauseGate?.Dispose();
        settingsPauseGate = null;
    }
}

using System;
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

    private TowerRunCoordinator coordinator;
    private GameObject flowOverlay;
    private GameObject settingsOverlay;
    private GameObject audioPage;
    private GameObject savePage;
    private Text flowTitle;
    private Text flowMessage;
    private Text floorLabel;
    private Text saveMessage;
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
        Canvas canvas = FindObjectOfType<Canvas>();

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
    }

    private void ShowSavePage()
    {
        StopPreviewSound();
        audioPage.SetActive(false);
        savePage.SetActive(true);
        ResetSaveRequest();
        saveMessage.text = coordinator.HasEntranceCandidate
            ? "先请求“保存本层入口”，再选择正式槽位。进入本层后的变化不会写入。"
            : "当前没有可保存的入口候选。";
        beginSaveButton.interactable = coordinator.HasEntranceCandidate;
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
            saveSlotTexts[i].text = FormatSlot(slotNumber, read);
            saveSlotButtons[i].interactable = requestedSaveCandidate != null;
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
        saveMessage.text = $"现有记录：\n{FormatSlot(slotNumber, read)}\n\n" +
                           $"待保存候选：\n{FormatCandidate(requestedSaveCandidate)}";
        overwriteConfirmButton.gameObject.SetActive(true);
        overwriteCancelButton.gameObject.SetActive(true);

        for (int i = 0; i < saveSlotButtons.Length; i++)
            saveSlotButtons[i].interactable = false;
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
        overwriteConfirmButton.gameObject.SetActive(false);
        overwriteCancelButton.gameObject.SetActive(false);

        if (result.succeeded)
        {
            requestedSaveCandidate = null;
            beginSaveButton.gameObject.SetActive(true);
            beginSaveButton.interactable = coordinator.HasEntranceCandidate;
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
        Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        flowOverlay = CreateOverlay("TowerRunFlowOverlay", transform, new Color(0f, 0f, 0f, 0.82f));
        Transform flowContent = CreateVerticalContent("FlowContent", flowOverlay.transform);
        flowTitle = CreateText("Title", flowContent, font, 32, TextAnchor.MiddleCenter);
        flowMessage = CreateText("Message", flowContent, font, 18, TextAnchor.MiddleCenter);
        primaryButton = CreateButton("Primary", flowContent, font, out _);
        secondaryButton = CreateButton("Secondary", flowContent, font, out _);

        for (int i = 0; i < flowSlotButtons.Length; i++)
            flowSlotButtons[i] = CreateButton($"FlowSlot{i + 1}", flowContent, font, out flowSlotTexts[i]);

        settingsOverlay = CreateOverlay("TowerRunSettingsOverlay", transform, new Color(0f, 0f, 0f, 0.75f));
        Transform settingsContent = CreateVerticalContent("SettingsContent", settingsOverlay.transform);
        CreateText("SettingsTitle", settingsContent, font, 28, TextAnchor.MiddleCenter).text = "设置";
        Button audioTab = CreateButton("AudioTab", settingsContent, font, out Text audioTabText);
        audioTabText.text = "音频";
        audioTab.onClick.AddListener(ShowAudioPage);
        Button saveTab = CreateButton("SaveTab", settingsContent, font, out Text saveTabText);
        saveTabText.text = "存档";
        saveTab.onClick.AddListener(ShowSavePage);

        audioPage = CreatePanel("AudioPage", settingsContent, 250f);
        Transform audioContent = CreateVerticalContent("AudioContent", audioPage.transform);
        CreateText("VolumeLabel", audioContent, font, 18, TextAnchor.MiddleCenter).text = "总音量";
        volumeSlider = CreateSlider("MasterVolume", audioContent);
        volumeSlider.onValueChanged.AddListener(HandleVolumeChanged);
        muteToggle = CreateToggle("Mute", audioContent, font, "静音");
        muteToggle.onValueChanged.AddListener(HandleMuteChanged);
        Button preview = CreateButton("Preview", audioContent, font, out Text previewText);
        previewText.text = "试听";
        preview.onClick.AddListener(PlayPreviewSound);

        savePage = CreatePanel("SavePage", settingsContent, 390f);
        Transform saveContent = CreateVerticalContent("SaveContent", savePage.transform);
        saveMessage = CreateText("SaveMessage", saveContent, font, 17, TextAnchor.MiddleCenter);
        beginSaveButton = CreateButton("BeginSaveRequest", saveContent, font, out Text beginSaveText);
        beginSaveText.text = "保存本层入口";
        beginSaveButton.onClick.AddListener(BeginSaveRequest);

        for (int i = 0; i < saveSlotButtons.Length; i++)
            saveSlotButtons[i] = CreateButton($"SaveSlot{i + 1}", saveContent, font, out saveSlotTexts[i]);

        overwriteConfirmButton = CreateButton("ConfirmOverwrite", saveContent, font, out Text overwriteText);
        overwriteText.text = "确认覆盖";
        overwriteConfirmButton.onClick.AddListener(ConfirmOverwrite);
        overwriteCancelButton = CreateButton("CancelOverwrite", saveContent, font, out Text cancelOverwriteText);
        cancelOverwriteText.text = "取消覆盖";
        overwriteCancelButton.onClick.AddListener(CancelOverwrite);
        Button close = CreateButton("CloseSettings", settingsContent, font, out Text closeText);
        closeText.text = "关闭";
        close.onClick.AddListener(CloseSettings);

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
        overwriteConfirmButton.gameObject.SetActive(false);
        overwriteCancelButton.gameObject.SetActive(false);
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
        result.GetComponent<LayoutElement>().minHeight = 34f;
        Slider slider = result.GetComponent<Slider>();
        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(result.transform, false);
        RectTransform backgroundRect = (RectTransform)background.transform;
        backgroundRect.anchorMin = new Vector2(0f, 0.35f);
        backgroundRect.anchorMax = new Vector2(1f, 0.65f);
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
        handleRect.sizeDelta = new Vector2(24f, 30f);
        handle.GetComponent<Image>().color = Color.white;

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
        result.GetComponent<LayoutElement>().minHeight = 40f;
        Toggle toggle = result.GetComponent<Toggle>();
        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(result.transform, false);
        RectTransform backgroundRect = (RectTransform)background.transform;
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0f, 0.5f);
        backgroundRect.pivot = new Vector2(0f, 0.5f);
        backgroundRect.anchoredPosition = new Vector2(12f, 0f);
        backgroundRect.sizeDelta = new Vector2(30f, 30f);
        Image backgroundImage = background.GetComponent<Image>();
        backgroundImage.color = new Color(0.2f, 0.2f, 0.24f, 1f);
        Text checkmark = CreateText("Checkmark", background.transform, font, 22, TextAnchor.MiddleCenter);
        checkmark.text = "✓";
        checkmark.color = new Color(0.25f, 0.8f, 1f, 1f);
        RectTransform checkmarkRect = checkmark.rectTransform;
        checkmarkRect.anchorMin = Vector2.zero;
        checkmarkRect.anchorMax = Vector2.one;
        checkmarkRect.offsetMin = Vector2.zero;
        checkmarkRect.offsetMax = Vector2.zero;
        Text label = CreateText("Label", result.transform, font, 17, TextAnchor.MiddleLeft);
        label.text = text;
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(54f, 0f);
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
               $"HP {summary.currentHealth:0}/{summary.maxHealth} / {summary.savedAtUtc}";
    }

    private static string FormatCandidate(TowerRunCheckpointRecord candidate)
    {
        if (candidate?.floor == null || candidate.player?.stats == null)
            return "候选不可用";

        return $"{candidate.playerName} / {candidate.floor.displayName} / " +
               $"HP {candidate.player.stats.currentHealth:0}/{candidate.player.stats.maxHealth} / 待保存";
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

using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Existing authored geometry and style, separated from flow, persistence and audio decisions.</summary>
public sealed class TowerRunUiViews : MonoBehaviour
{
    private const float ShortButtonLabelVisualOffsetY = 3f;
    [SerializeField] internal GameObject flowOverlay;
    [SerializeField] internal GameObject settingsOverlay;
    [SerializeField] internal GameObject audioPage;
    [SerializeField] internal GameObject savePage;
    [SerializeField] internal GameObject overwriteDialog;
    [SerializeField] internal RectTransform settingsFrame;
    [SerializeField] internal RectTransform overwriteDialogFrame;
    [SerializeField] internal CanvasGroup settingsFrameInteraction;
    [SerializeField] internal TowerRunUiSkin settingsSkin;
    [SerializeField] internal Button audioTabButton;
    [SerializeField] internal Button saveTabButton;
    [SerializeField] internal Text flowTitle;
    [SerializeField] internal Text flowMessage;
    [SerializeField] internal Text floorLabel;
    [SerializeField] internal Text saveMessage;
    [SerializeField] internal Text overwriteDetails;
    [SerializeField] internal Button primaryButton;
    [SerializeField] internal Button secondaryButton;
    [SerializeField] internal Button[] flowSlotButtons = new Button[3];
    [SerializeField] internal Text[] flowSlotTexts = new Text[3];
    [SerializeField] internal Button[] saveSlotButtons = new Button[3];
    [SerializeField] internal Text[] saveSlotTexts = new Text[3];
    [SerializeField] internal Button beginSaveButton;
    [SerializeField] internal Button overwriteConfirmButton;
    [SerializeField] internal Button overwriteCancelButton;
    [SerializeField] internal Slider volumeSlider;
    [SerializeField] internal Toggle muteToggle;
    [SerializeField] internal AudioSource previewSource;
    [SerializeField] private Button previewButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Vector2 settingsMaximumSize = new Vector2(840f, 820f);
    [SerializeField] private Vector2 confirmationMaximumSize = new Vector2(720f, 480f);
    internal UnityEngine.Events.UnityAction ShowAudioPage, ShowSavePage, BeginSaveRequest, PlayPreviewSound, CloseSettings;
    internal UnityEngine.Events.UnityAction CancelOverwrite = () => { }, ConfirmOverwrite = () => { };
    internal UnityEngine.Events.UnityAction<float> HandleVolumeChanged;
    internal UnityEngine.Events.UnityAction<bool> HandleMuteChanged;
    public void ValidateBindings()
    {
        if (flowOverlay == null || settingsOverlay == null || overwriteDialog == null || audioPage == null || savePage == null ||
            audioTabButton == null || saveTabButton == null || primaryButton == null || secondaryButton == null ||
            beginSaveButton == null || previewButton == null || closeButton == null || volumeSlider == null || muteToggle == null ||
            previewSource == null || flowTitle == null || flowMessage == null || floorLabel == null || saveMessage == null ||
            overwriteConfirmButton == null || overwriteCancelButton == null || overwriteDetails == null)
            throw new System.InvalidOperationException("Authored system UI has missing control bindings");
        if (flowSlotButtons.Length != 3 || flowSlotTexts.Length != 3 || saveSlotButtons.Length != 3 || saveSlotTexts.Length != 3)
            throw new System.InvalidOperationException("System UI requires its three existing slots");
        for (int i = 0; i < 3; i++)
            if (flowSlotButtons[i] == null || flowSlotTexts[i] == null || saveSlotButtons[i] == null || saveSlotTexts[i] == null)
                throw new System.InvalidOperationException("System UI slot bindings are incomplete");
    }
    internal void BindActions()
    {
        ValidateBindings(); UnbindActions();
        audioTabButton.onClick.AddListener(ShowAudioPage); saveTabButton.onClick.AddListener(ShowSavePage);
        beginSaveButton.onClick.AddListener(BeginSaveRequest); previewButton.onClick.AddListener(PlayPreviewSound);
        closeButton.onClick.AddListener(CloseSettings); volumeSlider.onValueChanged.AddListener(HandleVolumeChanged);
        muteToggle.onValueChanged.AddListener(HandleMuteChanged);
        ResizeSettingsFrame();
    }
    internal void UnbindActions()
    {
        if (audioTabButton != null) audioTabButton.onClick.RemoveListener(ShowAudioPage);
        if (saveTabButton != null) saveTabButton.onClick.RemoveListener(ShowSavePage);
        if (beginSaveButton != null) beginSaveButton.onClick.RemoveListener(BeginSaveRequest);
        if (previewButton != null) previewButton.onClick.RemoveListener(PlayPreviewSound);
        if (closeButton != null) closeButton.onClick.RemoveListener(CloseSettings);
        if (volumeSlider != null) volumeSlider.onValueChanged.RemoveListener(HandleVolumeChanged);
        if (muteToggle != null) muteToggle.onValueChanged.RemoveListener(HandleMuteChanged);
    }
#if UNITY_EDITOR
    public void BuildAuthoringLayout()
    {
        if (flowOverlay != null || settingsOverlay != null) throw new System.InvalidOperationException("System layout already exists; edit the authored objects");
        BuildUi();
    }
#endif
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
        saveTabButton = CreateSettingsButton(
            "SaveTab", tabRow, font, "存档", 54f, opticallyCenterShortLabel: true);

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
        muteToggle = CreateToggle("Mute", audioContent, font, "静音");
        SetPreferredWidth(muteToggle.GetComponent<LayoutElement>(), 590f);
        StyleAudioToggle(muteToggle);
        previewButton = CreateSettingsButton(
            "Preview", audioContent, font, "试听", 60f, opticallyCenterShortLabel: true);
        SetPreferredWidth(previewButton.GetComponent<LayoutElement>(), 300f);

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

        for (int i = 0; i < saveSlotButtons.Length; i++)
        {
            saveSlotButtons[i] = CreateSettingsButton($"SaveSlot{i + 1}", saveContent, font, "", 78f);
            saveSlotTexts[i] = saveSlotButtons[i].GetComponentInChildren<Text>();
            saveSlotTexts[i].alignment = TextAnchor.MiddleLeft;
            StyleSettingsText(saveSlotTexts[i], TowerRunUiSkin.Ink, 24, FontStyle.Bold, true);
            saveSlotTexts[i].rectTransform.offsetMin = new Vector2(34f, 4f);
            saveSlotTexts[i].rectTransform.offsetMax = new Vector2(-20f, -4f);
        }

        closeButton = CreateSettingsButton(
            "CloseSettings", settingsContent, font, "关闭", 56f, opticallyCenterShortLabel: true);

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
        overwriteConfirmButton = CreateSettingsButton("ConfirmOverwrite", dialogButtons, font, "确认覆盖", 64f,
            settingsSkin != null ? settingsSkin.RedButton : null);
        var template = overwriteDialog.AddComponent<UIConfirmationDialog>();
        template.SetBindings(overwriteConfirmButton, overwriteCancelButton, overwriteDetails);
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

    internal void OnRectTransformDimensionsChange()
    {
        ResizeSettingsFrame();
    }

    internal void ResizeSettingsFrame()
    {
        if (settingsFrame == null)
            return;

        Rect canvasRect = ((RectTransform)transform).rect;
        settingsFrame.sizeDelta = new Vector2(
            Mathf.Min(settingsMaximumSize.x, canvasRect.width - 32f),
            Mathf.Min(settingsMaximumSize.y, canvasRect.height - 24f));

        if (overwriteDialogFrame != null)
        {
            overwriteDialogFrame.sizeDelta = new Vector2(
                Mathf.Min(confirmationMaximumSize.x, canvasRect.width - 64f),
                Mathf.Min(confirmationMaximumSize.y, canvasRect.height - 48f));
        }
    }

    internal static RectTransform CreateSettingsArt(string name, Transform parent, Sprite sprite,
        Color fallback, bool sliced = true)
    {
        GameObject result = new GameObject(name, typeof(RectTransform), typeof(Image));
        result.transform.SetParent(parent, false);
        TowerRunUiSkin.ApplySprite(result.GetComponent<Image>(), sprite, fallback, sliced);
        return (RectTransform)result.transform;
    }

    internal static void StretchInside(RectTransform rect, float horizontal, float vertical)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(horizontal, vertical);
        rect.offsetMax = new Vector2(-horizontal, -vertical);
    }

    internal static Transform CreateSettingsContent(Transform parent)
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

    internal static Transform CreateSettingsRow(string name, Transform parent, float height)
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

    internal static void SetPreferredWidth(LayoutElement element, float width)
    {
        element.minWidth = width;
        element.preferredWidth = width;
        element.flexibleWidth = 0f;
    }

    internal static GameObject CreateSettingsPage(string name, Transform parent)
    {
        GameObject page = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        page.transform.SetParent(parent, false);
        Image image = page.GetComponent<Image>();
        image.color = new Color(1f, 0.98f, 0.88f, 0.3f);
        page.GetComponent<LayoutElement>().flexibleHeight = 1f;
        return page;
    }

    internal Button CreateSettingsButton(string name, Transform parent, Font font, string caption,
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

    internal static void ApplyShortButtonLabelOpticalCenter(Text label)
    {
        label.alignment = TextAnchor.MiddleCenter;
        RectTransform rect = label.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = new Vector2(0f, ShortButtonLabelVisualOffsetY);
    }

    internal void StyleSettingsTabs(bool audioSelected)
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

    internal void StyleSaveButton(Button button, bool slotCard)
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

    internal static void StyleAudioSlider(Slider slider)
    {
        Image background = slider.transform.Find("Background").GetComponent<Image>();
        background.color = new Color(0.31f, 0.24f, 0.22f, 1f);
        background.transform.Find("Fill").GetComponent<Image>().color =
            new Color(0.21f, 0.53f, 0.67f, 1f);
        slider.transform.Find("Handle").GetComponent<Image>().color =
            new Color(0.98f, 0.92f, 0.72f, 1f);
    }

    internal static void StyleAudioToggle(Toggle toggle)
    {
        Image background = toggle.transform.Find("Background").GetComponent<Image>();
        background.color = TowerRunUiSkin.Ink;
        background.GetComponentInChildren<Text>().color = new Color(0.37f, 0.74f, 0.83f, 1f);
        StyleSettingsText(toggle.transform.Find("Label").GetComponent<Text>(),
            TowerRunUiSkin.Ink, 24, FontStyle.Bold);
    }

    internal static GameObject CreateOverlay(string name, Transform parent, Color color)
    {
        GameObject result = new GameObject(name, typeof(RectTransform), typeof(Image));
        result.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)result.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        result.GetComponent<Image>().color = Color.clear;
        result.GetComponent<Image>().raycastTarget = false;
        return result;
    }

    internal static GameObject CreatePanel(string name, Transform parent, float minimumHeight)
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

    internal static Transform CreateVerticalContent(string name, Transform parent)
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

    internal static Text CreateText(string name, Transform parent, Font font, int size, TextAnchor anchor)
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

    internal static void StyleSettingsText(
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

    internal static Button CreateButton(string name, Transform parent, Font font, out Text label)
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

    internal static Slider CreateSlider(string name, Transform parent)
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

    internal static Toggle CreateToggle(string name, Transform parent, Font font, string text)
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

    internal static void ConfigureButton(Button button, string text, UnityEngine.Events.UnityAction action, bool active)
    {
        button.gameObject.SetActive(active);
        Text label = button.GetComponentInChildren<Text>();

        if (label != null)
            label.text = text;

        button.onClick.RemoveAllListeners();

        if (action != null)
            button.onClick.AddListener(action);
    }

    internal static string FormatSlot(int slotNumber, TowerRunSlotReadResult read)
    {
        if (read == null)
            return $"槽位 {slotNumber}：不可用";

        if (read.state != TowerRunSlotState.Valid || read.summary == null)
            return $"槽位 {slotNumber}：{read.state} - {read.message}";

        TowerRunSlotSummary summary = read.summary;
        return $"槽位 {slotNumber}：有效 / {summary.playerName} / {summary.floorDisplayName} / " +
               $"HP {summary.currentHealth:0}/{summary.maxHealth} / 保存于 {FormatSavedAtLocal(summary.savedAtUtc)}";
    }

    internal static string FormatSlotCard(int slotNumber, TowerRunSlotReadResult read)
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

    internal static string FormatSlotForDialog(int slotNumber, TowerRunSlotReadResult read)
    {
        if (read?.state != TowerRunSlotState.Valid || read.summary == null)
            return $"槽位 {slotNumber} · 当前记录不可用";

        TowerRunSlotSummary summary = read.summary;
        return $"槽位 {slotNumber} · {Shorten(summary.playerName, 20)} · " +
               $"{Shorten(summary.floorDisplayName, 22)}\n" +
               $"HP {summary.currentHealth:0}/{summary.maxHealth} · " +
               $"保存于 {FormatSavedAtLocal(summary.savedAtUtc)}";
    }

    internal static string FormatSavedAtLocal(string savedAtUtc)
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

    internal static string Shorten(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value;

        return value.Substring(0, maxLength - 1) + "…";
    }

    internal static string FormatCandidate(TowerRunCheckpointRecord candidate)
    {
        if (candidate?.floor == null || candidate.player?.stats == null)
            return "候选不可用";

        return $"{candidate.playerName} / {candidate.floor.displayName} / " +
               $"HP {candidate.player.stats.currentHealth:0}/{candidate.player.stats.maxHealth} / 待保存";
    }

    internal static string FormatCandidateForDialog(TowerRunCheckpointRecord candidate)
    {
        if (candidate?.floor == null || candidate.player?.stats == null)
            return "候选不可用";

        return $"{Shorten(candidate.playerName, 20)} · {Shorten(candidate.floor.displayName, 22)}\n" +
               $"HP {candidate.player.stats.currentHealth:0}/{candidate.player.stats.maxHealth} · 本层入口";
    }

    internal void SetOverwriteDialogActive(bool active)
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
}

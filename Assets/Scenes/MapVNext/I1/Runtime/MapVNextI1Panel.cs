using UnityEngine;

/// <summary>Small reproducibility input over the original coordinator; no generation or Ready ownership.</summary>
public sealed class MapVNextI1Panel : MonoBehaviour
{
    private GameObject controls;
    private UnityEngine.UI.InputField seed;
    private UnityEngine.UI.Text status;
    private TowerRunCoordinator Coordinator => GameSession.Instance == null ? null : GameSession.Instance.TowerRun;
    private void Start()
    {
        var canvasObject = new GameObject("I1ReviewControls", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 320;
        var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        status = Label(canvasObject.transform, "Build", 14, new Vector2(18, 12), new Vector2(1050, 25));
        controls = new GameObject("SeedControls", typeof(RectTransform), typeof(UnityEngine.UI.Image)); controls.transform.SetParent(canvasObject.transform, false);
        Rect(controls.GetComponent<RectTransform>(), new Vector2(18, 45), new Vector2(390, 100));
        controls.GetComponent<UnityEngine.UI.Image>().color = new Color(.04f, .055f, .08f, .95f);
        Label(controls.transform, "Title", 18, new Vector2(12, 64), new Vector2(365, 28)).text = "I1 review | start a reproducible seed";
        var inputObject = new GameObject("Seed", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.InputField)); inputObject.transform.SetParent(controls.transform, false);
        Rect(inputObject.GetComponent<RectTransform>(), new Vector2(12, 15), new Vector2(160, 40)); inputObject.GetComponent<UnityEngine.UI.Image>().color = new Color(.16f,.2f,.27f);
        seed = inputObject.GetComponent<UnityEngine.UI.InputField>(); seed.textComponent = Label(inputObject.transform, "Value", 22, new Vector2(10, 2), new Vector2(140, 36));
        seed.contentType = UnityEngine.UI.InputField.ContentType.IntegerNumber; seed.text = "21193";
        var buttonObject = new GameObject("StartSelectedSeed", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button)); buttonObject.transform.SetParent(controls.transform, false);
        Rect(buttonObject.GetComponent<RectTransform>(), new Vector2(186, 15), new Vector2(190, 40)); buttonObject.GetComponent<UnityEngine.UI.Image>().color = new Color(.28f,.2f,.47f);
        Label(buttonObject.transform, "Label", 18, new Vector2(8, 2), new Vector2(178, 36)).text = "Start selected seed";
        buttonObject.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() => { if (int.TryParse(seed.text, out int value)) Coordinator?.BeginNewRunWithSeed(value); });
    }
    private void Update()
    {
        if (controls == null || Coordinator == null) return;
        controls.SetActive(Coordinator.State == TowerRunFlowState.StartupChoice || Coordinator.State == TowerRunFlowState.DeathChoice);
        var map = GetComponent<MapVNextI1Bootstrap>().Controller.Orchestrator.Context;
        status.text = MapHostBuildStamp.BuildId + " | " + Coordinator.State + (map.ActiveMap == null ? "" : " | seed=" + map.ActiveMap.GenerationSeed) + " | Human review pending";
    }
    private static void Rect(RectTransform rect, Vector2 position, Vector2 size)
    { rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero; rect.anchoredPosition = position; rect.sizeDelta = size; }
    private static UnityEngine.UI.Text Label(Transform parent, string name, int size, Vector2 position, Vector2 bounds)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Text)); obj.transform.SetParent(parent, false);
        Rect(obj.GetComponent<RectTransform>(), position, bounds);
        var text = obj.GetComponent<UnityEngine.UI.Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = size; text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false; return text;
    }
}

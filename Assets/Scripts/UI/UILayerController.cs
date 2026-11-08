using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>The only writer of page sorting. One movable scrim supplies both visual dimming and a real raycast target.</summary>
public sealed class UILayerController
{
    private readonly Dictionary<UILayer, RectTransform> hosts = new Dictionary<UILayer, RectTransform>();
    private readonly UnityEngine.UI.Image scrim;
    private readonly CanvasGroup hud;
    private readonly CanvasGroup actions;
    private readonly RectTransform staging;
    public float DimAlpha => scrim == null ? 0 : scrim.color.a;
    public bool BlockerActive => scrim != null && scrim.gameObject.activeSelf;
    public RectTransform SystemActions { get; }
    public UILayerBindings Bindings { get; }

    public UILayerController(Canvas root, GameObject hudRoot) : this(root, hudRoot, CreateBindings(root)) { }
    public UILayerController(Canvas root, GameObject hudRoot, UILayerBindings bindings)
    {
        bindings.Validate(root); Bindings = bindings;
        hosts[UILayer.HUD] = (RectTransform)root.transform;
        BindLayer(root, UILayer.Window, bindings.Window); BindLayer(root, UILayer.System, bindings.System);
        BindLayer(root, UILayer.Modal, bindings.Modal); BindLayer(root, UILayer.Blocking, bindings.Blocking);
        SystemActions = bindings.SystemActions;
        staging = bindings.InactiveStaging;
        staging.gameObject.SetActive(false);
        actions = Group(SystemActions.gameObject);
        if (hudRoot != null) hud = Group(hudRoot);
        scrim = bindings.Scrim; scrim.raycastTarget = true;
        scrim.canvasRenderer.cullTransparentMesh = false;
        scrim.gameObject.SetActive(false);
    }
    private void BindLayer(Canvas root, UILayer layer, Canvas canvas)
    {
        canvas.overrideSorting = true; canvas.sortingOrder = (int)layer;
        canvas.sortingLayerID = root.sortingLayerID; canvas.targetDisplay = root.targetDisplay;
        hosts[layer] = (RectTransform)canvas.transform;
    }
    private static UILayerBindings CreateBindings(Canvas root)
    {
        // Used by isolated manager fixtures and the explicit Editor migration; production uses authored bindings.
        var result = new UILayerBindings();
        foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
        {
            if (layer == UILayer.HUD) continue;
            var go = new GameObject(layer + "Layer", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
            go.transform.SetParent(root.transform, false); Stretch((RectTransform)go.transform);
            var canvas = go.GetComponent<Canvas>();
            if (layer == UILayer.Window) result.Window = canvas;
            else if (layer == UILayer.System) result.System = canvas;
            else if (layer == UILayer.Modal) result.Modal = canvas;
            else result.Blocking = canvas;
        }
        result.SystemActions = CreateRect("SystemActionsRoot", result.System.transform);
        result.InactiveStaging = CreateRect("InactiveUIStaging", root.transform);
        result.Scrim = CreateRect("ManagedScrim", result.Window.transform).gameObject.AddComponent<UnityEngine.UI.Image>();
        return result;
    }
    public RectTransform Host(UILayer layer) => hosts[layer];
    internal GameObject InstantiateInactive(GameObject prefab)
    {
        GameObject instance = UnityEngine.Object.Instantiate(prefab, staging, false);
        instance.SetActive(false);
        return instance;
    }
    public void Attach(UIPageDefinition definition, GameObject root)
    {
        root.transform.SetParent(hosts[definition.Layer], false);
        Group(root);
        foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            canvas.overrideSorting = false;
        // Every graphic in a registered view uses its managed host's raycaster/order.
    }
    internal void Apply(IEnumerable<UIManager.Entry> entries, UIManager.Entry top)
    {
        var ordered = entries.OrderBy(e => (int)e.Definition.Layer).ThenBy(e => e.Order).ToArray();
        foreach (var entry in ordered)
        {
            entry.Root.transform.SetAsLastSibling();
            var group = Group(entry.Root); group.alpha = 1;
            group.interactable = entry == top; group.blocksRaycasts = entry == top;
        }
        bool blocking = top != null;
        if (hud != null) { hud.interactable = !blocking; hud.blocksRaycasts = !blocking; }
        bool allowActions = top == null || top.Definition.Layer == UILayer.Window;
        actions.interactable = allowActions; actions.blocksRaycasts = allowActions;
        actions.alpha = allowActions ? 1 : 0;
        scrim.gameObject.SetActive(top != null);
        if (scrim.gameObject.activeSelf)
        {
            scrim.transform.SetParent(hosts[top.Definition.Layer], false);
            Stretch((RectTransform)scrim.transform);
            int targetIndex = top.Root.transform.GetSiblingIndex();
            if (scrim.transform.GetSiblingIndex() < targetIndex) targetIndex--;
            scrim.transform.SetSiblingIndex(targetIndex);
            scrim.color = new Color(0, 0, 0, top.Definition.DimAlpha);
        }
    }
    public static CanvasGroup Group(GameObject root)
    {
        var group = root.GetComponent<CanvasGroup>();
        if (group == null) group = root.AddComponent<CanvasGroup>();
        group.ignoreParentGroups = false;
        return group;
    }
    public static RectTransform CreateRect(string name, Transform parent)
    { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); Stretch((RectTransform)go.transform); return (RectTransform)go.transform; }
    public static void Stretch(RectTransform rect)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one; }
}

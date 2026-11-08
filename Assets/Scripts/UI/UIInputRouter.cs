using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

public sealed class UIInputRouter
{
    private readonly EventSystem events;
    private UIManager.Entry focused;
    private UIHandle focusedHandle;
    private int lastCancelFrame = -1;
    public UIInputRouter(EventSystem events)
    {
        this.events = events;
        if (events == null) return;
        // Retire the old module, keeping the EventSystem and serialized button events intact.
        var legacy = events.GetComponent<StandaloneInputModule>();
        if (legacy != null) legacy.enabled = false;
        var module = events.GetComponent<InputSystemUIInputModule>();
        if (module == null) module = events.gameObject.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
        module.cancel = null; // Back has exactly one distributor below; Submit/Point/Navigate remain enabled.
        module.enabled = true;
    }
    public UIBackResult PollBack(UIManager manager)
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame || lastCancelFrame == Time.frameCount)
            return UIBackResult.Handled;
        lastCancelFrame = Time.frameCount;
        return manager.Back();
    }
    internal void Apply(IEnumerable<UIManager.Entry> entries, UIManager.Entry top)
    {
        if (events == null) return;
        UIHandle nextHandle = top == null ? default : top.Handle;
        if (focused == top && focusedHandle.Equals(nextHandle) &&
            (top == null || IsFocusable(events.currentSelectedGameObject, top.Root))) return;
        if (focused != null && focused.Root != null && focused.Handle.Equals(focusedHandle))
        {
            GameObject selected = events.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(focused.Root.transform)) focused.LastFocus = selected;
        }
        focused = top;
        focusedHandle = nextHandle;
        GameObject target = top?.LastFocus;
        if (!IsFocusable(target, top?.Root)) target = top?.Definition.DefaultFocus == null ? null : top.Definition.DefaultFocus.gameObject;
        if (!IsFocusable(target, top?.Root) && top?.Root != null)
            foreach (var selectable in top.Root.GetComponentsInChildren<UnityEngine.UI.Selectable>())
                if (selectable.IsInteractable()) { target = selectable.gameObject; break; }
        events.SetSelectedGameObject(IsFocusable(target, top?.Root) ? target : null);
    }
    private static bool IsFocusable(GameObject target, GameObject root)
    {
        if (target == null || root == null || !target.activeInHierarchy || !target.transform.IsChildOf(root.transform)) return false;
        var selectable = target.GetComponent<UnityEngine.UI.Selectable>();
        return selectable != null && selectable.IsActive() && selectable.IsInteractable();
    }
}

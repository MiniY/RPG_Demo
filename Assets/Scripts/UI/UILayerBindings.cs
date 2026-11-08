using System;
using UnityEngine;

[Serializable]
public sealed class UILayerBindings
{
    public Canvas Window;
    public Canvas System;
    public Canvas Modal;
    public Canvas Blocking;
    public RectTransform SystemActions;
    public RectTransform InactiveStaging;
    public UnityEngine.UI.Image Scrim;
    public void Validate(Canvas root)
    {
        if (root == null || Window == null || System == null || Modal == null || Blocking == null ||
            SystemActions == null || InactiveStaging == null || Scrim == null)
            throw new InvalidOperationException("UI layer bindings are incomplete");
        var unique = new System.Collections.Generic.HashSet<Canvas> { Window, System, Modal, Blocking };
        if (unique.Count != 4) throw new InvalidOperationException("Each UI layer requires its own Canvas");
        foreach (var canvas in unique)
            if (canvas == root || canvas.transform.parent != root.transform)
                throw new InvalidOperationException("UI layer must be a direct child of its root Canvas");
        if (SystemActions.parent != System.transform || InactiveStaging.parent != root.transform ||
            !Scrim.transform.IsChildOf(root.transform))
            throw new InvalidOperationException("UI layer auxiliary objects have invalid parents");
    }
}

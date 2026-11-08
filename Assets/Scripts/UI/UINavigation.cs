using System.Collections.Generic;
using System.Linq;

/// <summary>Display order and ownership are explicit; HUD never enters the return history.</summary>
public sealed class UINavigation
{
    private readonly List<UIHandle> pageHistory = new List<UIHandle>();
    private readonly List<UIHandle> windows = new List<UIHandle>();
    private readonly List<UIHandle> modals = new List<UIHandle>();
    public IReadOnlyList<UIHandle> PageHistory => pageHistory;
    public IReadOnlyList<UIHandle> Windows => windows;
    public IReadOnlyList<UIHandle> Modals => modals;

    internal void Enter(UIHandle handle, UILayer layer)
    {
        if (layer == UILayer.Modal) modals.Add(handle);
        else if (layer == UILayer.Window) windows.Add(handle);
        else if (layer == UILayer.System) pageHistory.Add(handle);
    }
    internal void Remove(UIHandle handle)
    { pageHistory.Remove(handle); windows.Remove(handle); modals.Remove(handle); }
    internal void BringToFront(UIHandle handle, UILayer layer)
    {
        // Focusing a window changes only its display order, never page navigation history.
        if (layer != UILayer.Window || !windows.Remove(handle)) return;
        windows.Add(handle);
    }
    internal void Clear() { pageHistory.Clear(); windows.Clear(); modals.Clear(); }
}

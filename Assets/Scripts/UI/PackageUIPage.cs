using UnityEngine;

public sealed class PackageUIPage : UIPage<UIEmptyArgs>
{
    [SerializeField] private PackagePanelController controller;
    public override void ValidateBindings()
    {
        base.ValidateBindings();
        if (controller == null) throw new System.InvalidOperationException("Package page requires its legacy button adapter");
    }
    public override void Open(UIEmptyArgs args) => controller.NotifyVisibility(true);
    public override void Close(UICloseReason reason) => controller.NotifyVisibility(false);
    public override void Suspend()
    {
        foreach (var grid in GetComponentsInChildren<PackageGridUI>(true)) grid.SuspendHover();
    }
}

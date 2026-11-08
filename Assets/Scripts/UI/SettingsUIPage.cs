public sealed class SettingsUIPage : UIPage<UIEmptyArgs>
{
    private TowerRunSettingsPresenter presenter;
    public void BindPresenter(TowerRunSettingsPresenter value) => presenter = value;
    public override bool CanOpen(UIEmptyArgs args) => presenter != null && Root != null && Root.IsPlaying;
    public override void Open(UIEmptyArgs args) => presenter.ShowAudioPage();
    public override void Close(UICloseReason reason) => presenter?.Close();
}

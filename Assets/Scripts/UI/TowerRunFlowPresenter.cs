using System;
using UnityEngine;

public sealed class TowerRunFlowPresenter : IDisposable
{
    private const string LegacyAcknowledgedKey = "tower-run.legacy-save-message-acknowledged.v1";
    private readonly TowerRunCoordinator coordinator;
    private readonly TowerRunUiViews view;
    private readonly UIRoot root;
    private readonly Action refreshSaveSlots;
    private bool awaitingAbandonConfirmation;
    public TowerRunFlowPresenter(TowerRunCoordinator coordinator, TowerRunUiViews view, UIRoot root, Action refreshSaveSlots)
    {
        this.coordinator=coordinator;this.view=view;this.root=root;this.refreshSaveSlots=refreshSaveSlots;
        coordinator.StateChanged += Refresh;
        Refresh();
    }
    public void Dispose() => coordinator.StateChanged -= Refresh;
    public void Refresh()
    {
        if (coordinator == null || view.flowOverlay == null)
            return;

        view.floorLabel.text = coordinator.FloorDisplayName;
        TowerRunFlowState state = coordinator.State;
        bool showFlow = state != TowerRunFlowState.Playing;

        if (root.Manager.TryFind(UIRoutes.Flow, root.Scope, out UIHandle oldHandle))
            root.Manager.Close(oldHandle, UICloseReason.ContextInvalid);

        if (!showFlow)
        {
            refreshSaveSlots();
            return;
        }

        view.primaryButton.gameObject.SetActive(false);
        view.secondaryButton.gameObject.SetActive(false);

        for (int i = 0; i < view.flowSlotButtons.Length; i++)
            view.flowSlotButtons[i].gameObject.SetActive(false);

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
                view.flowTitle.text = "操作失败";
                view.flowMessage.text = coordinator.FailureMessage;
                TowerRunUiViews.ConfigureButton(
                    view.primaryButton,
                    "重试",
                    coordinator.RetryFailedOperation,
                    coordinator.CanRetryFailedOperation);
                awaitingAbandonConfirmation = false;
                TowerRunUiViews.ConfigureButton(view.secondaryButton, "返回启动选择", RequestReturnToStartup, true);
                break;
            default:
                view.flowTitle.text = "正在准备冒险";
                view.flowMessage.text = "数据、地图、玩家与依赖完成最终校验后才会开放玩法。";
                break;
        }
        // Configure active/interactable controls before Open selects the keyboard target.
        bool choice = state == TowerRunFlowState.StartupChoice || state == TowerRunFlowState.DeathChoice || state == TowerRunFlowState.Failure;
        root.Manager.ConfigureClosedLayer(UIRoutes.Flow, choice ? UILayer.System : UILayer.Blocking);
        root.Manager.Open(UIRoutes.Flow, new UIEmptyArgs(), root.Scope);
    }

    private void RequestReturnToStartup()
    {
        if (coordinator.OperationKind != TowerRunOperationKind.FloorTransition || awaitingAbandonConfirmation)
        {
            coordinator.ReturnToStartupChoice();
            return;
        }

        awaitingAbandonConfirmation = true;
        view.flowMessage.text = "返回启动选择将放弃当前未正式保存的运行进展。正式槽位不会改变。";
        TowerRunUiViews.ConfigureButton(view.secondaryButton, "确认放弃并返回", RequestReturnToStartup, true);
    }

    private void ConfigureChoice(string title, string message, bool allowNewRun)
    {
        view.flowTitle.text = title;
        view.flowMessage.text = message;
        TowerRunUiViews.ConfigureButton(view.primaryButton, "开始新冒险", coordinator.BeginNewRun, allowNewRun);

        for (int i = 0; i < view.flowSlotButtons.Length; i++)
        {
            int slotNumber = i + 1;
            TowerRunSlotReadResult read = coordinator.ReadSlot(slotNumber);
            view.flowSlotButtons[i].gameObject.SetActive(true);
            view.flowSlotButtons[i].interactable = read.state == TowerRunSlotState.Valid;
            view.flowSlotTexts[i].text = TowerRunUiViews.FormatSlot(slotNumber, read);
            view.flowSlotButtons[i].onClick.RemoveAllListeners();
            string displayedToken = read.confirmationToken;
            view.flowSlotButtons[i].onClick.AddListener(
                () => coordinator.BeginRestoreSlot(slotNumber, displayedToken));
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

        view.flowMessage.text += "\n\n" + result.message;
        TowerRunUiViews.ConfigureButton(view.secondaryButton, "知道了", () =>
        {
            preferences.SetInt(LegacyAcknowledgedKey, 1);
            preferences.Save();
            Refresh();
        }, true);
    }
}

using System;
using UnityEngine;

public sealed class TowerRunSaveSlotsPresenter
{
    private readonly TowerRunCoordinator coordinator;
    private readonly TowerRunUiViews view;
    private readonly UIRoot root;
    private TowerRunCheckpointRecord requestedSaveCandidate;
    private int pendingOverwriteSlot, requestVersion;
    private string pendingOverwriteToken;
    public TowerRunSaveSlotsPresenter(TowerRunCoordinator coordinator,TowerRunUiViews view,UIRoot root)
    {this.coordinator=coordinator;this.view=view;this.root=root;}
    public void Reset()
    {requestVersion++;requestedSaveCandidate=null;pendingOverwriteSlot=0;pendingOverwriteToken=null;}
    private async void SelectSaveSlot(int slotNumber)
    {
        if(requestedSaveCandidate==null)return;
        TowerRunSlotReadResult read=coordinator.ReadSlot(slotNumber);
        if(read.state==TowerRunSlotState.Empty){CommitSlot(slotNumber,false);return;}
        if(!root.Manager.TryFind(UIRoutes.Settings,root.Scope,out UIHandle owner))return;
        pendingOverwriteSlot=slotNumber;pendingOverwriteToken=read.confirmationToken;
        int version=++requestVersion;
        string message=$"现有记录\n{TowerRunUiViews.FormatSlotForDialog(slotNumber,read)}\n\n即将覆盖为\n{TowerRunUiViews.FormatCandidateForDialog(requestedSaveCandidate)}";
        try
        {
            UIConfirmResult result=await UIConfirmationDialog.ShowAsync(root,owner,view.overwriteDialog,message);
            if(version!=requestVersion || !root.Manager.TryFind(UIRoutes.Settings,root.Scope,out UIHandle current) || !current.Equals(owner))return;
            if(result==UIConfirmResult.Confirmed)CommitSlot(slotNumber,true);
            else {pendingOverwriteSlot=0;pendingOverwriteToken=null;RefreshSaveSlots();}
        }
        catch(Exception error){Debug.LogException(error);pendingOverwriteSlot=0;pendingOverwriteToken=null;RefreshSaveSlots();}
    }
    public void ShowPage()
    {
        view.audioPage.SetActive(false);
        view.savePage.SetActive(true);
        Reset();
        view.StyleSettingsTabs(false);
        view.saveMessage.text = coordinator.HasEntranceCandidate
            ? "先请求“保存本层入口”，再选择正式槽位。\n进入本层后的变化不会写入。"
            : "当前没有可保存的入口候选。";
        view.beginSaveButton.interactable = coordinator.HasEntranceCandidate;
        view.StyleSaveButton(view.beginSaveButton, false);
        view.beginSaveButton.gameObject.SetActive(true);
        view.overwriteConfirmButton.gameObject.SetActive(false);
        view.overwriteCancelButton.gameObject.SetActive(false);
        RefreshSaveSlots();
    }
    public void BeginSaveRequest()
    {
        if (!coordinator.TryCreateSaveRequest(
                out TowerRunCheckpointRecord candidate,
                out string message))
        {
            view.saveMessage.text = message;
            return;
        }

        requestedSaveCandidate = candidate;
        view.beginSaveButton.gameObject.SetActive(false);
        view.saveMessage.text = "待保存：\n" + TowerRunUiViews.FormatCandidate(candidate) +
                           "\n请选择一个正式槽位。";
        RefreshSaveSlots();
    }

    public void RefreshSaveSlots()
    {
        if (coordinator == null || view.saveSlotButtons[0] == null)
            return;

        for (int i = 0; i < view.saveSlotButtons.Length; i++)
        {
            int slotNumber = i + 1;
            TowerRunSlotReadResult read = coordinator.ReadSlot(slotNumber);
            view.saveSlotTexts[i].text = TowerRunUiViews.FormatSlotCard(slotNumber, read);
            view.saveSlotButtons[i].interactable = requestedSaveCandidate != null;
            view.StyleSaveButton(view.saveSlotButtons[i], true);
            view.saveSlotButtons[i].onClick.RemoveAllListeners();
            view.saveSlotButtons[i].onClick.AddListener(() => SelectSaveSlot(slotNumber));
        }
    }

    private void CommitSlot(int slotNumber, bool overwriteConfirmed)
    {
        TowerRunSlotWriteResult result = coordinator.TrySaveEntranceCandidate(
            slotNumber,
            requestedSaveCandidate,
            overwriteConfirmed,
            overwriteConfirmed ? pendingOverwriteToken : null);
        view.saveMessage.text = result.message;
        pendingOverwriteSlot = 0;
        pendingOverwriteToken = null;
        view.overwriteConfirmButton.gameObject.SetActive(false);
        view.overwriteCancelButton.gameObject.SetActive(false);

        if (result.succeeded)
        {
            requestedSaveCandidate = null;
            view.beginSaveButton.gameObject.SetActive(true);
            view.beginSaveButton.interactable = coordinator.HasEntranceCandidate;
            view.StyleSaveButton(view.beginSaveButton, false);
        }

        RefreshSaveSlots();
    }
}

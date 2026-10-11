using System;
using System.Collections.Generic;
using UnityEngine;

public enum TowerRunFlowState
{
    StartupChoice,
    Initializing,
    Playing,
    DeathChoice,
    Restoring,
    Transitioning,
    Failure
}

public enum TowerRunOperationKind
{
    None,
    NewRun,
    Restore,
    FloorTransition
}

[DisallowMultipleComponent]
public sealed class TowerRunCoordinator : MonoBehaviour
{
    public const int GeneratorVersion = 1;
    public const string ConfigurationIdentity = "random-map-default-v1";
    public const string DefaultProfileId = "default";

    private MapGenerationController mapController;
    private TowerFloorRuntimeController floorRuntime;
    private PlayerStatsRuntime stats;
    private PlayerInventory inventory;
    private readonly List<ShopTradeController> shops = new List<ShopTradeController>();
    private PlayerDamageController playerDamage;
    private TowerRunSlotStore slotStore;
    private string slotStoreRoot;
    private TowerRunCheckpointRecord newRunBaseline;
    private TowerRunCheckpointRecord stagedRecord;
    private TowerRunCheckpointRecord currentRecord;
    private TowerRunCheckpointRecord entranceCandidate;
    private Guid operationId;
    private Guid finalizedGenerationId;
    private TowerRunOperationKind operationKind;
    private TowerRunGateLease startupGameplayGate;
    private TowerRunGateLease startupPauseGate;
    private TowerRunGateLease deathGameplayGate;
    private TowerRunGateLease deathPauseGate;
    private TowerRunGateLease operationGameplayGate;
    private TowerRunGateLease operationPauseGate;
    private string failureMessage;

    public event Action StateChanged;
    public TowerRunFlowState State { get; private set; } = TowerRunFlowState.StartupChoice;
    public bool HasStagedOperation => operationId != Guid.Empty && stagedRecord != null;
    public bool CanRetryFailedOperation =>
        State == TowerRunFlowState.Failure &&
        HasStagedOperation &&
        operationKind != TowerRunOperationKind.None;
    public bool HasEntranceCandidate => entranceCandidate != null;
    public string FailureMessage => failureMessage ?? string.Empty;
    public string FloorDisplayName => currentRecord?.floor?.displayName ?? string.Empty;
    public TowerRunOperationKind OperationKind => operationKind;

    private void Awake()
    {
        EnsureSlotStore();
        AcquireStartupGates();
    }

    private void EnsureSlotStore()
    {
        string requestedRoot = TowerRunRuntimeEnvironment.SlotRoot;

        if (slotStore != null &&
            string.Equals(slotStoreRoot, requestedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        slotStore = new TowerRunSlotStore(
            requestedRoot,
            new TowerRunCompatibilityPolicy(
                TowerRunCheckpointRecord.CurrentSchemaVersion,
                GeneratorVersion,
                ConfigurationIdentity));
        slotStoreRoot = requestedRoot;
    }

    private void OnDestroy()
    {
        UnbindScene();
        ReleaseAllGates();
    }

    public void BindScene()
    {
        UnbindScene();
        EnsureSlotStore();
        mapController = FindSceneObject<MapGenerationController>();
        floorRuntime = FindSceneObject<TowerFloorRuntimeController>();
        stats = GameSession.Instance == null ? null : GameSession.Instance.PlayerStats;
        inventory = FindSceneObject<PlayerInventory>();
        shops.Clear();
        shops.AddRange(FindSceneObjects<ShopTradeController>());
        playerDamage = FindSceneObject<PlayerDamageController>();

        if (mapController != null)
            mapController.Orchestrator.GameplayReady += HandleGameplayReady;

        if (floorRuntime != null)
            floorRuntime.ExitUseRequested += HandleExitUseRequested;

        if (playerDamage != null)
            playerDamage.OnDefeated += HandlePlayerDefeated;

        CaptureNewRunBaselineIfNeeded();
        TowerRunUiController.EnsureForScene(this);
        PublishStateChanged();
    }

    public TowerRunSlotReadResult ReadSlot(int slotNumber) => slotStore.ReadSlot(slotNumber);

    public void BeginNewRun()
    {
        if (State != TowerRunFlowState.StartupChoice && State != TowerRunFlowState.DeathChoice)
            return;

        if (newRunBaseline == null)
        {
            Fail("新冒险基线尚未准备完成。", TowerRunOperationKind.NewRun);
            return;
        }

        TowerRunCheckpointRecord record = newRunBaseline.DeepClone();
        record.runId = Guid.NewGuid().ToString("D");
        record.savedAtUtc = string.Empty;
        record.floor = new TowerRunFloorRecord { floorNumber = 0, displayName = "新手关卡" };
        record.generation = CreateFreshGenerationRecord();
        BeginOperation(TowerRunOperationKind.NewRun, record);
    }

    public void BeginRestoreSlot(int slotNumber)
    {
        if (State != TowerRunFlowState.StartupChoice && State != TowerRunFlowState.DeathChoice)
            return;

        TowerRunSlotReadResult read = slotStore.ReadSlot(slotNumber);

        if (read.state != TowerRunSlotState.Valid || read.record == null)
        {
            Fail(read.message, TowerRunOperationKind.Restore);
            return;
        }

        BeginOperation(TowerRunOperationKind.Restore, read.record.DeepClone());
    }

    public bool TryCreateSaveRequest(
        out TowerRunCheckpointRecord candidate,
        out string message)
    {
        candidate = null;

        if (State != TowerRunFlowState.Playing || entranceCandidate == null || IsOperationInProgress())
        {
            message = "当前没有可提交的稳定楼层入口候选。";
            return false;
        }

        candidate = entranceCandidate.DeepClone();
        message = string.Empty;
        return true;
    }

    public TowerRunSlotWriteResult TrySaveEntranceCandidate(
        int slotNumber,
        TowerRunCheckpointRecord requestedCandidate,
        bool overwriteConfirmed,
        string expectedConfirmationToken = null)
    {
        if (State != TowerRunFlowState.Playing || requestedCandidate == null ||
            IsOperationInProgress())
        {
            return new TowerRunSlotWriteResult
            {
                succeeded = false,
                message = "当前没有可提交的稳定楼层入口候选。"
            };
        }

        return slotStore.TryCommit(
            slotNumber,
            requestedCandidate.DeepClone(),
            overwriteConfirmed,
            expectedConfirmationToken);
    }

    public void RetryFailedOperation()
    {
        if (!CanRetryFailedOperation)
            return;

        TowerRunCheckpointRecord retry = stagedRecord.DeepClone();

        if (operationKind == TowerRunOperationKind.NewRun || operationKind == TowerRunOperationKind.FloorTransition)
            retry.generation = CreateFreshGenerationRecord();

        BeginOperation(operationKind, retry, preserveExistingGates: true);
    }

    public void ReturnToStartupChoice()
    {
        InvalidateOperation();
        currentRecord = null;
        entranceCandidate = null;
        ReleaseOperationGates();
        ReleaseDeathGates();
        AcquireStartupGates();
        State = TowerRunFlowState.StartupChoice;
        failureMessage = string.Empty;
        PublishStateChanged();
    }

    public bool TryFinalizeStagedOperation(
        MapRuntimeContext context,
        Guid generationId,
        out string reason)
    {
        if (!HasStagedOperation || context == null || context.ActiveGenerationId != generationId ||
            context.ActiveMap == null)
        {
            reason = "恢复最终化没有匹配的 staged 操作或当前 Generation。";
            return false;
        }

        string actualBaseline = TowerRunMapBaseline.Compute(
            context.ActiveMap,
            context.ActivePlacementPlan,
            mapController == null ? null : mapController.LastGeneratedSimpleDecorations);
        string expectedBaseline = stagedRecord.generation.staticBaselineSignature;

        if (string.IsNullOrWhiteSpace(actualBaseline))
        {
            reason = "无法形成完整的地图、装饰与 placement 静态基线。";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(expectedBaseline) &&
            !string.Equals(expectedBaseline, actualBaseline, StringComparison.Ordinal))
        {
            reason = "生成结果无法兑现记录中的静态地图基线。";
            return false;
        }

        MapRuntimeDiagnosticSnapshot snapshot = context.CreateDiagnosticSnapshot();
        MapGenerationAttempt successfulAttempt = FindSuccessfulAttempt(snapshot.AttemptChain);

        if (successfulAttempt == null || snapshot.ActiveRequest == null ||
            snapshot.ActiveRequest.RequestedSeed != stagedRecord.generation.requestedSeed ||
            !string.Equals(snapshot.ActiveRequest.ProfileId, stagedRecord.generation.profileId, StringComparison.Ordinal))
        {
            reason = "当前生成请求与 staged 恢复身份不匹配。";
            return false;
        }

        if (operationKind == TowerRunOperationKind.Restore &&
            successfulAttempt.AttemptIndex != stagedRecord.generation.attemptIndex)
        {
            reason = "恢复生成没有兑现记录中的成功 attempt。";
            return false;
        }

        stagedRecord.generation.attemptIndex = successfulAttempt.AttemptIndex;
        stagedRecord.generation.staticBaselineSignature = actualBaseline;

        if (!TowerRunRuntimeStateBridge.TryApply(
                stagedRecord,
                stats,
                inventory,
                shops,
                false,
                out reason))
        {
            return false;
        }

        playerDamage = FindSceneObject<PlayerDamageController>();
        playerDamage?.SynchronizeAfterRestore();
        finalizedGenerationId = generationId;
        reason = string.Empty;
        return true;
    }

    private void BeginOperation(
        TowerRunOperationKind kind,
        TowerRunCheckpointRecord record,
        bool preserveExistingGates = false)
    {
        if (mapController == null || record == null)
        {
            Fail("正式地图运行实例尚未准备完成。", kind);
            return;
        }

        operationId = Guid.NewGuid();
        finalizedGenerationId = Guid.Empty;
        operationKind = kind;
        stagedRecord = record.DeepClone();
        failureMessage = string.Empty;

        if (!preserveExistingGates)
            AcquireOperationGates();

        State = kind == TowerRunOperationKind.FloorTransition
            ? TowerRunFlowState.Transitioning
            : kind == TowerRunOperationKind.Restore
                ? TowerRunFlowState.Restoring
                : TowerRunFlowState.Initializing;
        PublishStateChanged();

        MapGenerationRequest request = CreateRequest(stagedRecord.generation);

        bool generated;

        try
        {
            generated = mapController.TryGenerateMap(request);
        }
        catch (Exception exception)
        {
            Fail($"地图生成或最终化发生异常：{exception.Message}", kind, keepStagedRecord: true);
            return;
        }

        if (!generated && State != TowerRunFlowState.Playing)
        {
            string reason = mapController.Orchestrator.Context.Failure?.Reason ?? "地图生成或最终化失败。";
            Fail(reason, kind, keepStagedRecord: true);
        }
    }

    private void HandleGameplayReady(Guid generationId)
    {
        if (!HasStagedOperation || generationId != finalizedGenerationId)
            return;

        currentRecord = stagedRecord.DeepClone();
        entranceCandidate = stagedRecord.DeepClone();
        entranceCandidate.savedAtUtc = string.Empty;
        stagedRecord = null;
        operationId = Guid.Empty;
        finalizedGenerationId = Guid.Empty;
        operationKind = TowerRunOperationKind.None;
        failureMessage = string.Empty;
        State = TowerRunFlowState.Playing;
        ReleaseOperationGates();
        ReleaseStartupGates();
        ReleaseDeathGates();
        NotifyNonFatal(() => stats?.SendStatsChanged());
        NotifyNonFatal(() => inventory?.NotifyInventoryChanged());

        for (int i = 0; i < shops.Count; i++)
        {
            ShopTradeController shop = shops[i];
            NotifyNonFatal(() => shop?.NotifyCheckpointApplied());
        }

        PublishStateChanged();
    }

    private void HandleExitUseRequested(Guid sourceGenerationId)
    {
        if (State != TowerRunFlowState.Playing || currentRecord == null ||
            mapController == null || !mapController.Orchestrator.IsCurrentActive(sourceGenerationId))
        {
            return;
        }

        TowerRunFloorRecord nextFloor = new TowerRunFloorRecord
        {
            floorNumber = currentRecord.floor.floorNumber + 1,
            displayName = $"第 {currentRecord.floor.floorNumber + 1} 层"
        };

        TowerRunCheckpointRecord carry;
        string reason;

        try
        {
            if (!TowerRunRuntimeStateBridge.TryCapture(
                    currentRecord.runId,
                    currentRecord.playerName,
                    nextFloor,
                    CreateFreshGenerationRecord(),
                    stats,
                    inventory,
                    shops,
                    out carry,
                    out reason))
            {
                Debug.LogError($"换层请求未被接受：{reason}", this);
                return;
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            return;
        }

        if (floorRuntime == null || !floorRuntime.TryAcceptExitTransition(sourceGenerationId))
            return;

        BeginOperation(TowerRunOperationKind.FloorTransition, carry);
    }

    private void HandlePlayerDefeated(PlayerDamageController defeatedPlayer)
    {
        if (State != TowerRunFlowState.Playing || defeatedPlayer == null)
            return;

        PlayerAction action = defeatedPlayer.GetComponent<PlayerAction>();
        action?.ResetMapTransitionState();
        PlayerHurtController hurt = defeatedPlayer.GetComponent<PlayerHurtController>();
        hurt?.ResetMapTransitionState();
        FindSceneObject<PackagePanelController>()?.ClosePackagePanel();
        FindSceneObject<ShopPanelController>()?.CloseShopPanel();
        AcquireDeathGates();
        State = TowerRunFlowState.DeathChoice;
        PublishStateChanged();
    }

    private void CaptureNewRunBaselineIfNeeded()
    {
        if (newRunBaseline != null || stats == null || inventory == null)
            return;

        TowerRunRuntimeStateBridge.TryCapture(
            "new-run-baseline",
            "玩家",
            new TowerRunFloorRecord { floorNumber = 0, displayName = "新手关卡" },
            CreateFreshGenerationRecord(),
            stats,
            inventory,
            shops,
            out newRunBaseline,
            out _);
    }

    private void Fail(
        string message,
        TowerRunOperationKind kind,
        bool keepStagedRecord = false)
    {
        operationKind = kind;
        failureMessage = string.IsNullOrWhiteSpace(message) ? "操作失败。" : message;

        if (!keepStagedRecord && stagedRecord == null)
            operationId = Guid.Empty;

        AcquireOperationGates();
        State = TowerRunFlowState.Failure;
        PublishStateChanged();
    }

    private static TowerRunGenerationRecord CreateFreshGenerationRecord()
    {
        return new TowerRunGenerationRecord
        {
            generatorVersion = GeneratorVersion,
            configurationIdentity = ConfigurationIdentity,
            profileId = DefaultProfileId,
            requestedSeed = Guid.NewGuid().GetHashCode(),
            attemptIndex = 0,
            staticBaselineSignature = string.Empty
        };
    }

    private static MapGenerationRequest CreateRequest(TowerRunGenerationRecord generation)
    {
        int attemptCount = Mathf.Max(1, generation.attemptIndex + 1);
        MapRetryPolicy policy = attemptCount == 1
            ? MapRetryPolicy.StrictSeed
            : MapRetryPolicy.BoundedDeterministicRetry;
        return new MapGenerationRequest(
            Guid.NewGuid(),
            generation.requestedSeed,
            generation.profileId,
            policy,
            attemptCount,
            1);
    }

    private static MapGenerationAttempt FindSuccessfulAttempt(
        IReadOnlyList<MapGenerationAttemptDiagnostic> attempts)
    {
        if (attempts == null)
            return null;

        for (int i = attempts.Count - 1; i >= 0; i--)
        {
            if (attempts[i].Outcome == MapAttemptOutcome.Succeeded)
                return attempts[i].Attempt;
        }

        return null;
    }

    private bool IsOperationInProgress()
    {
        return State == TowerRunFlowState.Initializing ||
               State == TowerRunFlowState.Restoring ||
               State == TowerRunFlowState.Transitioning;
    }

    private void InvalidateOperation()
    {
        operationId = Guid.Empty;
        finalizedGenerationId = Guid.Empty;
        stagedRecord = null;
        operationKind = TowerRunOperationKind.None;
    }

    private void UnbindScene()
    {
        if (mapController != null)
            mapController.Orchestrator.GameplayReady -= HandleGameplayReady;

        if (floorRuntime != null)
            floorRuntime.ExitUseRequested -= HandleExitUseRequested;

        if (playerDamage != null)
            playerDamage.OnDefeated -= HandlePlayerDefeated;

        mapController = null;
        floorRuntime = null;
        playerDamage = null;
    }

    private void AcquireStartupGates()
    {
        startupGameplayGate ??= TowerRunGateHub.AcquireGameplay("flow.startup");
        startupPauseGate ??= TowerRunGateHub.AcquireWorldPause("flow.startup");
    }

    private void AcquireDeathGates()
    {
        deathGameplayGate ??= TowerRunGateHub.AcquireGameplay("flow.death");
        deathPauseGate ??= TowerRunGateHub.AcquireWorldPause("flow.death");
    }

    private void AcquireOperationGates()
    {
        operationGameplayGate ??= TowerRunGateHub.AcquireGameplay("flow.operation");
        operationPauseGate ??= TowerRunGateHub.AcquireWorldPause("flow.operation");
    }

    private void ReleaseStartupGates()
    {
        startupGameplayGate?.Dispose();
        startupGameplayGate = null;
        startupPauseGate?.Dispose();
        startupPauseGate = null;
    }

    private void ReleaseDeathGates()
    {
        deathGameplayGate?.Dispose();
        deathGameplayGate = null;
        deathPauseGate?.Dispose();
        deathPauseGate = null;
    }

    private void ReleaseOperationGates()
    {
        operationGameplayGate?.Dispose();
        operationGameplayGate = null;
        operationPauseGate?.Dispose();
        operationPauseGate = null;
    }

    private void ReleaseAllGates()
    {
        ReleaseOperationGates();
        ReleaseDeathGates();
        ReleaseStartupGates();
    }

    private void PublishStateChanged()
    {
        Delegate[] subscribers = StateChanged?.GetInvocationList();

        if (subscribers == null)
            return;

        for (int i = 0; i < subscribers.Length; i++)
            NotifyNonFatal(() => ((Action)subscribers[i]).Invoke());
    }

    private void NotifyNonFatal(Action notification)
    {
        if (notification == null)
            return;

        try
        {
            notification.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    private static T FindSceneObject<T>() where T : Component
    {
        T[] values = Resources.FindObjectsOfTypeAll<T>();

        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] != null && values[i].gameObject.scene.IsValid())
                return values[i];
        }

        return null;
    }

    private static T[] FindSceneObjects<T>() where T : Component
    {
        List<T> result = new List<T>();
        T[] values = Resources.FindObjectsOfTypeAll<T>();

        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] != null && values[i].gameObject.scene.IsValid())
                result.Add(values[i]);
        }

        return result.ToArray();
    }
}

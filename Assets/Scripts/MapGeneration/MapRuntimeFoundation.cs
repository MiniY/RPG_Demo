using System;

/// <summary>
/// 当前会话明确选择的地图运行模式。
/// </summary>
public enum MapRuntimeMode
{
    RandomGenerated = 0,
    LegacyStatic = 1
}

/// <summary>
/// 地图集成生命周期的显式阶段表示。
/// Stage 1 只建立表达能力；后续阶段才实现完整转换规则和推进器。
/// </summary>
public enum MapLifecyclePhase
{
    NotStarted = 0,
    ValidatingConfiguration = 1,
    Initialized = 2,
    Preparing = 3,
    Generating = 4,
    Planning = 5,
    Committing = 6,
    Projecting = 7,
    Materializing = 8,
    SpawningPlayer = 9,
    Reinitializing = 10,
    BindingCamera = 11,
    ValidatingRuntime = 12,
    Ready = 13,
    Failed = 14
}

/// <summary>
/// 失败诊断的大类；稳定 code 提供更细粒度的机器可读原因。
/// </summary>
public enum MapFailureCategory
{
    None = 0,
    Configuration = 1,
    ModeConflict = 2,
    Coordinate = 3,
    Lifecycle = 4,
    Unknown = 5
}

/// <summary>
/// 运行时地图权威对象当前是否处于激活状态。
/// 该状态只用于验证和诊断，绝不用于反向选择 Runtime Mode。
/// </summary>
public readonly struct MapRuntimeAuthorityState
{
    public MapRuntimeAuthorityState(
        bool randomGeneratedActive,
        bool legacyStaticActive)
    {
        RandomGeneratedActive = randomGeneratedActive;
        LegacyStaticActive = legacyStaticActive;
    }

    public bool RandomGeneratedActive { get; }

    public bool LegacyStaticActive { get; }
}

/// <summary>
/// 一次地图集成失败的统一、不可变诊断记录。
/// </summary>
public sealed class MapFailureDiagnostic
{
    public MapFailureDiagnostic(
        MapRuntimeMode mode,
        Guid? generationId,
        MapLifecyclePhase phase,
        MapFailureCategory category,
        string code,
        string reason)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("失败诊断 code 不能为空。", nameof(code));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("失败诊断 reason 不能为空。", nameof(reason));

        Mode = mode;
        GenerationId = generationId;
        Phase = phase;
        Category = category;
        Code = code;
        Reason = reason;
    }

    public MapRuntimeMode Mode { get; }

    public Guid? GenerationId { get; }

    public MapLifecyclePhase Phase { get; }

    public MapFailureCategory Category { get; }

    public string Code { get; }

    public string Reason { get; }
}

/// <summary>
/// 生产代码和测试都可读取、但不可通过其修改生产状态的诊断快照。
/// </summary>
public sealed class MapRuntimeDiagnosticSnapshot
{
    internal MapRuntimeDiagnosticSnapshot(
        MapRuntimeMode mode,
        Guid? activeGenerationId,
        Guid? pendingGenerationId,
        MapLifecyclePhase phase,
        MapRuntimeAuthorityState authorityState,
        MapFailureDiagnostic failure)
    {
        Mode = mode;
        ActiveGenerationId = activeGenerationId;
        PendingGenerationId = pendingGenerationId;
        Phase = phase;
        AuthorityState = authorityState;
        Failure = failure;
        IsReady = phase == MapLifecyclePhase.Ready && failure == null;
        IsFailed = phase == MapLifecyclePhase.Failed || failure != null;
        StateSummary = BuildStateSummary();
    }

    public MapRuntimeMode Mode { get; }

    public Guid? ActiveGenerationId { get; }

    public Guid? PendingGenerationId { get; }

    public MapLifecyclePhase Phase { get; }

    public MapRuntimeAuthorityState AuthorityState { get; }

    public bool IsReady { get; }

    public bool IsFailed { get; }

    public MapFailureDiagnostic Failure { get; }

    public string StateSummary { get; }

    private string BuildStateSummary()
    {
        return
            $"Mode={Mode}; " +
            $"ActiveGenerationId={FormatGenerationId(ActiveGenerationId)}; " +
            $"PendingGenerationId={FormatGenerationId(PendingGenerationId)}; " +
            $"Phase={Phase}; Ready={IsReady}; Failed={IsFailed}; " +
            $"RandomAuthorityActive={AuthorityState.RandomGeneratedActive}; " +
            $"LegacyAuthorityActive={AuthorityState.LegacyStaticActive}; " +
            $"FailureCode={Failure?.Code ?? "none"}";
    }

    private static string FormatGenerationId(Guid? generationId)
    {
        return generationId.HasValue ? generationId.Value.ToString("D") : "none";
    }
}

/// <summary>
/// 保存当前地图会话的最小运行上下文。
/// Stage 1 不在此实现 Pending/Active 提交、Registry、Placement 或 Retry。
/// </summary>
public sealed class MapRuntimeContext
{
    private Guid? activeGenerationId;
    private Guid? pendingGenerationId;
    private MapLifecyclePhase phase;
    private MapRuntimeAuthorityState authorityState;
    private MapFailureDiagnostic failure;

    public MapRuntimeContext(MapRuntimeMode mode)
    {
        Mode = mode;
        phase = MapLifecyclePhase.NotStarted;
    }

    public MapRuntimeMode Mode { get; }

    public Guid? ActiveGenerationId => activeGenerationId;

    public Guid? PendingGenerationId => pendingGenerationId;

    public MapLifecyclePhase Phase => phase;

    public bool IsReady => phase == MapLifecyclePhase.Ready && failure == null;

    public bool IsFailed => phase == MapLifecyclePhase.Failed || failure != null;

    public MapFailureDiagnostic Failure => failure;

    internal void SetPhase(MapLifecyclePhase nextPhase)
    {
        phase = nextPhase;
    }

    internal void SetGenerationIdentifiers(
        Guid? nextActiveGenerationId,
        Guid? nextPendingGenerationId)
    {
        activeGenerationId = nextActiveGenerationId;
        pendingGenerationId = nextPendingGenerationId;
    }

    internal void RecordAuthorityState(MapRuntimeAuthorityState nextAuthorityState)
    {
        authorityState = nextAuthorityState;
    }

    internal void RecordFailure(MapFailureDiagnostic diagnostic)
    {
        failure = diagnostic ?? throw new ArgumentNullException(nameof(diagnostic));
        phase = MapLifecyclePhase.Failed;
    }

    /// <summary>
    /// 捕获当前状态的不可变只读快照。
    /// </summary>
    public MapRuntimeDiagnosticSnapshot CreateDiagnosticSnapshot()
    {
        return new MapRuntimeDiagnosticSnapshot(
            Mode,
            activeGenerationId,
            pendingGenerationId,
            phase,
            authorityState,
            failure);
    }
}

/// <summary>
/// 验证显式 Runtime Mode 与场景权威对象的激活状态是否互斥。
/// </summary>
public static class MapRuntimeModeValidator
{
    public static MapFailureDiagnostic Validate(
        MapRuntimeMode mode,
        MapRuntimeAuthorityState authorityState)
    {
        switch (mode)
        {
            case MapRuntimeMode.RandomGenerated:
                if (authorityState.LegacyStaticActive)
                {
                    return Failure(
                        mode,
                        MapFailureCategory.ModeConflict,
                        "RandomModeLegacyAuthorityActive",
                        "RandomGenerated 模式下 LegacyStatic 地图权威仍处于激活状态。");
                }

                if (!authorityState.RandomGeneratedActive)
                {
                    return Failure(
                        mode,
                        MapFailureCategory.Configuration,
                        "RandomAuthorityInactive",
                        "RandomGenerated 模式要求随机地图生产权威处于激活状态。");
                }

                return null;

            case MapRuntimeMode.LegacyStatic:
                if (authorityState.RandomGeneratedActive)
                {
                    return Failure(
                        mode,
                        MapFailureCategory.ModeConflict,
                        "LegacyModeRandomAuthorityActive",
                        "LegacyStatic 模式下随机地图生产权威仍处于激活状态。");
                }

                if (!authorityState.LegacyStaticActive)
                {
                    return Failure(
                        mode,
                        MapFailureCategory.Configuration,
                        "LegacyAuthorityInactive",
                        "LegacyStatic 模式要求旧静态地图权威处于激活状态。");
                }

                return null;

            default:
                return Failure(
                    mode,
                    MapFailureCategory.Configuration,
                    "UnsupportedRuntimeMode",
                    $"不支持的地图运行模式值：{mode}。");
        }
    }

    private static MapFailureDiagnostic Failure(
        MapRuntimeMode mode,
        MapFailureCategory category,
        string code,
        string reason)
    {
        return new MapFailureDiagnostic(
            mode,
            null,
            MapLifecyclePhase.ValidatingConfiguration,
            category,
            code,
            reason);
    }
}

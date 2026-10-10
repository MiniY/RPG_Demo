using UnityEngine;

/// <summary>
/// 在场景启动前提供显式 Runtime Mode，并验证 Random/Legacy 权威互斥。
/// </summary>
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed class MapRuntimeBootstrap : MonoBehaviour
{
    /// <summary>
    /// 当前会话明确选择的地图运行模式；不得由对象 active state 推断。
    /// </summary>
    [SerializeField] private MapRuntimeMode runtimeMode = MapRuntimeMode.RandomGenerated;

    /// <summary>
    /// RandomGenerated 模式的生产权威根对象。
    /// </summary>
    [SerializeField] private GameObject randomGeneratedAuthority;

    /// <summary>
    /// LegacyStatic 模式的生产权威根对象。
    /// </summary>
    [SerializeField] private GameObject legacyStaticAuthority;

    private MapRuntimeContext context;
    private bool initialized;

    public MapRuntimeMode Mode => runtimeMode;

    public GameObject RandomGeneratedAuthority => randomGeneratedAuthority;

    public GameObject LegacyStaticAuthority => legacyStaticAuthority;

    public MapRuntimeContext Context
    {
        get
        {
            EnsureInitialized();
            return context;
        }
    }

    public MapRuntimeDiagnosticSnapshot DiagnosticSnapshot
    {
        get
        {
            EnsureInitialized();
            return context.CreateDiagnosticSnapshot();
        }
    }

    /// <summary>
    /// 只有显式选择 RandomGenerated 且启动校验通过时，随机生产路径才可运行。
    /// </summary>
    public bool CanRunRandomGeneration
    {
        get
        {
            EnsureInitialized();
            return runtimeMode == MapRuntimeMode.RandomGenerated && !context.IsFailed;
        }
    }

    /// <summary>
    /// Re-reads both authority roots at the final RandomGenerated gate.
    /// This validates the configured mode without changing active state or falling back to LegacyStatic.
    /// </summary>
    public bool TryValidateCurrentAuthority(
        MapLifecyclePhase validationPhase,
        out string reason)
    {
        EnsureInitialized();
        reason = string.Empty;

        if (context.IsFailed)
        {
            reason = context.Failure?.Reason ?? "Map runtime is already failed.";
            return false;
        }

        if (randomGeneratedAuthority == null || legacyStaticAuthority == null)
        {
            string missingAuthority = randomGeneratedAuthority == null
                ? "RandomGenerated"
                : "LegacyStatic";
            MapFailureDiagnostic missingFailure = new MapFailureDiagnostic(
                runtimeMode,
                context.ActiveGenerationId,
                validationPhase,
                MapFailureCategory.Configuration,
                "MissingAuthorityReference",
                $"MapRuntimeBootstrap 缺少 {missingAuthority} authority 引用。");
            RecordFailure(missingFailure);
            reason = missingFailure.Reason;
            return false;
        }

        MapRuntimeAuthorityState authorityState = ReadAuthorityState();
        context.RecordAuthorityState(authorityState);
        MapFailureDiagnostic failure = MapRuntimeModeValidator.Validate(
            runtimeMode,
            authorityState,
            validationPhase);
        if (failure == null)
            return true;

        RecordFailure(failure);
        reason = failure.Reason;
        return false;
    }

    /// <summary>
    /// 在其他组件 Start 前完成只读配置检查。
    /// </summary>
    private void Awake()
    {
        Initialize();
    }

    /// <summary>
    /// 建立最小 Runtime Context 并验证显式 Mode 与 authority active state。
    /// 本方法不会切换 Mode、激活 Legacy 或自动修复冲突。
    /// </summary>
    public void Initialize()
    {
        if (initialized)
            return;

        context = new MapRuntimeContext(runtimeMode);
        context.SetPhase(MapLifecyclePhase.ValidatingConfiguration);

        if (randomGeneratedAuthority == null || legacyStaticAuthority == null)
        {
            string missingAuthority = randomGeneratedAuthority == null
                ? "RandomGenerated"
                : "LegacyStatic";
            Fail(
                MapFailureCategory.Configuration,
                "MissingAuthorityReference",
                $"MapRuntimeBootstrap 缺少 {missingAuthority} authority 引用。");
            initialized = true;
            return;
        }

        MapRuntimeAuthorityState authorityState = ReadAuthorityState();
        context.RecordAuthorityState(authorityState);

        MapFailureDiagnostic failure = MapRuntimeModeValidator.Validate(
            runtimeMode,
            authorityState);

        if (failure != null)
            RecordFailure(failure);
        else
            context.SetPhase(MapLifecyclePhase.Initialized);

        initialized = true;
    }

    private MapRuntimeAuthorityState ReadAuthorityState()
    {
        return new MapRuntimeAuthorityState(
            randomGeneratedAuthority.activeInHierarchy,
            legacyStaticAuthority.activeInHierarchy);
    }

    private void EnsureInitialized()
    {
        if (!initialized)
            Initialize();
    }

    private void Fail(
        MapFailureCategory category,
        string code,
        string reason)
    {
        RecordFailure(new MapFailureDiagnostic(
            runtimeMode,
            null,
            MapLifecyclePhase.ValidatingConfiguration,
            category,
            code,
            reason));
    }

    private void RecordFailure(MapFailureDiagnostic failure)
    {
        context.RecordFailure(failure);
        Debug.LogError(
            $"Map runtime bootstrap failed [{failure.Code}]: {failure.Reason}",
            this);
    }
}

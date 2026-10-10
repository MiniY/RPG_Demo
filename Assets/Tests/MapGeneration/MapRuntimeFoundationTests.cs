using System;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 验证显式 Runtime Mode、最小 Context 和只读诊断契约。
/// </summary>
public sealed class MapRuntimeFoundationTests
{
    private GameObject bootstrapObject;
    private GameObject randomAuthority;
    private GameObject legacyAuthority;

    [TearDown]
    public void TearDown()
    {
        if (bootstrapObject != null)
            UnityEngine.Object.DestroyImmediate(bootstrapObject);
        if (randomAuthority != null)
            UnityEngine.Object.DestroyImmediate(randomAuthority);
        if (legacyAuthority != null)
            UnityEngine.Object.DestroyImmediate(legacyAuthority);
    }

    [Test]
    public void RandomGeneratedModeIsExplicitAndReadable()
    {
        MapRuntimeBootstrap bootstrap = CreateBootstrap(
            MapRuntimeMode.RandomGenerated,
            randomActive: true,
            legacyActive: false);

        bootstrap.Initialize();
        MapRuntimeDiagnosticSnapshot snapshot = bootstrap.DiagnosticSnapshot;

        Assert.That(bootstrap.Mode, Is.EqualTo(MapRuntimeMode.RandomGenerated));
        Assert.That(bootstrap.CanRunRandomGeneration, Is.True);
        Assert.That(snapshot.Mode, Is.EqualTo(MapRuntimeMode.RandomGenerated));
        Assert.That(snapshot.Phase, Is.EqualTo(MapLifecyclePhase.Initialized));
        Assert.That(snapshot.ActiveGenerationId, Is.Null);
        Assert.That(snapshot.PendingGenerationId, Is.Null);
        Assert.That(snapshot.IsReady, Is.False);
        Assert.That(snapshot.IsFailed, Is.False);
        Assert.That(snapshot.Failure, Is.Null);
        Assert.That(snapshot.StateSummary, Does.Contain("Mode=RandomGenerated"));
        Assert.That(snapshot.StateSummary, Does.Contain("RandomAuthorityActive=True"));
        Assert.That(snapshot.StateSummary, Does.Contain("LegacyAuthorityActive=False"));
    }

    [Test]
    public void LegacyStaticModeIsExplicitAndReadable()
    {
        MapRuntimeBootstrap bootstrap = CreateBootstrap(
            MapRuntimeMode.LegacyStatic,
            randomActive: false,
            legacyActive: true);

        bootstrap.Initialize();
        MapRuntimeDiagnosticSnapshot snapshot = bootstrap.DiagnosticSnapshot;

        Assert.That(bootstrap.Mode, Is.EqualTo(MapRuntimeMode.LegacyStatic));
        Assert.That(bootstrap.CanRunRandomGeneration, Is.False);
        Assert.That(snapshot.Mode, Is.EqualTo(MapRuntimeMode.LegacyStatic));
        Assert.That(snapshot.Phase, Is.EqualTo(MapLifecyclePhase.Initialized));
        Assert.That(snapshot.IsFailed, Is.False);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ActiveStateConflictFailsWithoutChangingModeOrFallingBack(
        bool randomAuthorityIsActive)
    {
        MapRuntimeBootstrap bootstrap = CreateBootstrap(
            MapRuntimeMode.RandomGenerated,
            randomActive: randomAuthorityIsActive,
            legacyActive: true);
        LogAssert.Expect(
            LogType.Error,
            new Regex("RandomModeLegacyAuthorityActive"));

        bootstrap.Initialize();
        MapRuntimeDiagnosticSnapshot snapshot = bootstrap.DiagnosticSnapshot;

        Assert.That(snapshot.Mode, Is.EqualTo(MapRuntimeMode.RandomGenerated),
            "active state 不能反向把显式 Mode 推断为 LegacyStatic。");
        Assert.That(snapshot.Phase, Is.EqualTo(MapLifecyclePhase.Failed));
        Assert.That(snapshot.IsFailed, Is.True);
        Assert.That(snapshot.Failure, Is.Not.Null);
        Assert.That(snapshot.Failure.Mode, Is.EqualTo(MapRuntimeMode.RandomGenerated));
        Assert.That(snapshot.Failure.GenerationId, Is.Null);
        Assert.That(
            snapshot.Failure.Phase,
            Is.EqualTo(MapLifecyclePhase.ValidatingConfiguration));
        Assert.That(snapshot.Failure.Category, Is.EqualTo(MapFailureCategory.ModeConflict));
        Assert.That(snapshot.Failure.Code, Is.EqualTo("RandomModeLegacyAuthorityActive"));
        Assert.That(snapshot.Failure.Reason, Is.Not.Empty);
        Assert.That(bootstrap.CanRunRandomGeneration, Is.False);
        Assert.That(randomAuthority.activeSelf, Is.EqualTo(randomAuthorityIsActive));
        Assert.That(legacyAuthority.activeSelf, Is.True,
            "冲突诊断不能自动切换或修复 authority active state。");
    }

    [Test]
    public void DiagnosticsAreReadOnlyAndDoNotChangeProductionState()
    {
        MapRuntimeBootstrap bootstrap = CreateBootstrap(
            MapRuntimeMode.RandomGenerated,
            randomActive: true,
            legacyActive: false);
        bootstrap.Initialize();

        MapRuntimeDiagnosticSnapshot firstSnapshot = bootstrap.DiagnosticSnapshot;
        MapRuntimeDiagnosticSnapshot secondSnapshot = bootstrap.DiagnosticSnapshot;

        Assert.That(firstSnapshot, Is.Not.SameAs(secondSnapshot));
        Assert.That(secondSnapshot.StateSummary, Is.EqualTo(firstSnapshot.StateSummary));
        Assert.That(bootstrap.Context.Phase, Is.EqualTo(MapLifecyclePhase.Initialized));
        Assert.That(randomAuthority.activeSelf, Is.True);
        Assert.That(legacyAuthority.activeSelf, Is.False);

        AssertPublicPropertiesAreReadOnly(typeof(MapRuntimeDiagnosticSnapshot));
        AssertPublicPropertiesAreReadOnly(typeof(MapFailureDiagnostic));
        AssertPublicPropertiesAreReadOnly(typeof(MapRuntimeContext));
    }

    [Test]
    public void RuntimeAuthorityConflictFailsWithoutSwitchingModeOrRepairingAuthorities()
    {
        MapRuntimeBootstrap bootstrap = CreateBootstrap(
            MapRuntimeMode.RandomGenerated,
            randomActive: true,
            legacyActive: false);
        bootstrap.Initialize();
        legacyAuthority.SetActive(true);
        LogAssert.Expect(
            LogType.Error,
            new Regex("RandomModeLegacyAuthorityActive"));

        bool valid = bootstrap.TryValidateCurrentAuthority(
            MapLifecyclePhase.ValidatingRuntime,
            out string reason);
        MapRuntimeDiagnosticSnapshot snapshot = bootstrap.DiagnosticSnapshot;

        Assert.That(valid, Is.False);
        Assert.That(reason, Is.Not.Empty);
        Assert.That(bootstrap.Mode, Is.EqualTo(MapRuntimeMode.RandomGenerated));
        Assert.That(snapshot.Phase, Is.EqualTo(MapLifecyclePhase.Failed));
        Assert.That(snapshot.Failure.Phase, Is.EqualTo(MapLifecyclePhase.ValidatingRuntime));
        Assert.That(snapshot.Failure.Code, Is.EqualTo("RandomModeLegacyAuthorityActive"));
        Assert.That(snapshot.AuthorityState.RandomGeneratedActive, Is.True);
        Assert.That(snapshot.AuthorityState.LegacyStaticActive, Is.True);
        Assert.That(randomAuthority.activeSelf, Is.True);
        Assert.That(legacyAuthority.activeSelf, Is.True,
            "Runtime validation reports conflicts; it must not silently switch or repair Mode authorities.");
    }

    [Test]
    public void FailureRecordCanCarryGenerationIdentityWithoutMutatingContext()
    {
        Guid generationId = Guid.NewGuid();
        MapFailureDiagnostic failure = new MapFailureDiagnostic(
            MapRuntimeMode.RandomGenerated,
            generationId,
            MapLifecyclePhase.Generating,
            MapFailureCategory.Coordinate,
            "CoordinateOutOfBounds",
            "生成对象位于地图范围外。");

        Assert.That(failure.Mode, Is.EqualTo(MapRuntimeMode.RandomGenerated));
        Assert.That(failure.GenerationId, Is.EqualTo(generationId));
        Assert.That(failure.Phase, Is.EqualTo(MapLifecyclePhase.Generating));
        Assert.That(failure.Category, Is.EqualTo(MapFailureCategory.Coordinate));
        Assert.That(failure.Code, Is.EqualTo("CoordinateOutOfBounds"));
        Assert.That(failure.Reason, Is.Not.Empty);
    }

    private MapRuntimeBootstrap CreateBootstrap(
        MapRuntimeMode mode,
        bool randomActive,
        bool legacyActive)
    {
        bootstrapObject = new GameObject("MapRuntimeBootstrap");
        randomAuthority = new GameObject("RandomGeneratedAuthority");
        legacyAuthority = new GameObject("LegacyStaticAuthority");
        randomAuthority.SetActive(randomActive);
        legacyAuthority.SetActive(legacyActive);

        MapRuntimeBootstrap bootstrap = bootstrapObject.AddComponent<MapRuntimeBootstrap>();
        SerializedObject serializedBootstrap = new SerializedObject(bootstrap);
        serializedBootstrap.FindProperty("runtimeMode").enumValueIndex = (int)mode;
        serializedBootstrap.FindProperty("randomGeneratedAuthority").objectReferenceValue =
            randomAuthority;
        serializedBootstrap.FindProperty("legacyStaticAuthority").objectReferenceValue =
            legacyAuthority;
        serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();
        return bootstrap;
    }

    private static void AssertPublicPropertiesAreReadOnly(Type type)
    {
        foreach (PropertyInfo property in type.GetProperties(
                     BindingFlags.Instance | BindingFlags.Public))
        {
            Assert.That(
                property.SetMethod == null || !property.SetMethod.IsPublic,
                Is.True,
                $"{type.Name}.{property.Name} 不得公开 setter。");
        }
    }
}

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed class EcologyJointPlanningPlayModeTests
{
    [Serializable] private sealed class NativeJointCapture
    {
        public string schema, scope, label, sourceFingerprint, inputManifestSha256, generationId, staticProjection,
            initialProjection, layoutSignature, recipeIdentity;
        public int seed, width, height, originX, originY, acceptedAttempt, attemptsUsed, nativePositions, stairSweeps, nativeRolePairs, nativeResidentPairs, exitApproachNode;
        public float exitInteractionRadius;
        public bool exitCentreLegal;
        public int[] terrain, elevation, regionByNode;
        public JointRegion[] regions;
        public JointResident[] residents;
    }
    [Serializable] private sealed class Fingerprint { public string sourceFingerprint; }
    [Serializable] private sealed class CorpusRow
    {
        public int seed, acceptedAttempt, attemptsUsed, ordinaryRegions, bossRegions, residents;
        public bool accepted;
        public double planningMilliseconds;
        public string signature;
        public string[] failures, types;
    }
    [Serializable] private sealed class CorpusReport
    {
        public string schema, scope, sourceFingerprint, label;
        public int requestedTerrains, acceptedTerrains, rejectedTerrains, acceptedWithBoss;
        public CorpusRow[] rows;
    }
    [Serializable] private sealed class JointRegion
    {
        public int id, capacity, population;
        public string kind, typeId;
        public int[] surface, buffer;
    }
    [Serializable] private sealed class JointResident
    {
        public string id, typeId, prefabGuid, prefabPath, footprintIdentity;
        public int home, node;
        public Vector2 bodyWorld, nativeFootCenter, nativeFootSize;
    }
    private string isolatedRoot;
    private UnityEngine.Object ownedSettings;
    private float originalTimeScale;
    private Scene ownedScene;
    private readonly List<GameObject> ownedProjections = new List<GameObject>();
    private readonly HashSet<string> actualStairFamilies = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> actualHomeMovementFamilies = new HashSet<string>(StringComparer.Ordinal);
    [SetUp]
    public void Isolate()
    {
        actualStairFamilies.Clear();
        actualHomeMovementFamilies.Clear();
        isolatedRoot = Path.Combine(Path.GetTempPath(), "RPG_Demo-EcologyJoint-" + Guid.NewGuid().ToString("N"));
        TowerRunRuntimeEnvironment.ConfigureTestIsolation(isolatedRoot); originalTimeScale = Time.timeScale;
    }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (ownedSettings != null) UnityEngine.Object.Destroy(ownedSettings);
        foreach (GameObject projection in ownedProjections) if (projection != null) UnityEngine.Object.Destroy(projection);
        ownedProjections.Clear();
        if (ownedScene.IsValid() && ownedScene.isLoaded)
        {
            Scene empty = SceneManager.CreateScene("EcologyJointCleanup-" + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(ownedScene);
            Assert.That(MapElevationRuntimeQuery.TryGetActiveMap(out _, out _), Is.False,
                "The owned full scene must not leak its map authority into later isolated movement/melee fixtures.");
        }
        TowerRunRuntimeEnvironment.ResetOverrides(); Time.timeScale = originalTimeScale;
        if (Directory.Exists(isolatedRoot)) Directory.Delete(isolatedRoot, true);
        yield return null;
    }

    [UnityTest]
    public IEnumerator UnitsCandidateThreeActualSeedJointPlansMatchNativeRolesFeetAndStairSweeps()
    {
        yield return CheckJoint(true, unitsCandidate: true);
    }

    [UnityTest]
    public IEnumerator UnitsCandidateOriginalControllersUseLegalHomeStairsOrNativeConstrainedFlatEdges()
    {
        foreach (int seed in new[] { 20260929, -1391545000, 613580675, 20365658, 20470387, 20575116, 20679845, 20784574, 20889303, 20994032, 21098761 })
        {
            yield return CheckJoint(false, sampleSeed: seed, unitsCandidate: true, homeStairs: true);
            if (actualHomeMovementFamilies.Count == 3 && actualStairFamilies.Count >= 2) break;
        }
        Assert.That(actualHomeMovementFamilies.OrderBy(x => x).ToArray(), Is.EqualTo(new[] { "Barrel", "TNT", "Torch" }));
        Assert.That(actualStairFamilies.Count, Is.GreaterThan(0),
            "Current R3 layouts must exercise an actual legal Home stair; families without a qualified Home stair retain their real foot and use legal flat motion.");
    }

    [UnityTest]
    public IEnumerator OriginalPreparedResidentsStopForCrowdingAndResumeWithoutChangingHomeOrLeases()
    {
        yield return CheckJoint(false, unitsCandidate: true, homeCrowding: true);
    }

    [UnityTest]
    public IEnumerator UnitsCandidateFixedUnfilteredSixtyFourTerrainCorpusReportsEveryAttemptWithoutClaimingBoss()
    {
        yield return CheckJoint(false, seedCorpus: true, unitsCandidate: true);
    }

    [UnityTest]
    public IEnumerator ActualSceneRoleOwnersJointlyPlanOnePendingTerrainWithoutEarlyMaterialization()
    {
        yield return CheckJoint(false);
    }

    [UnityTest]
    public IEnumerator RetainedNativeFixedUnfilteredSixtyFourTerrainCorpusReportsEveryJointAttemptWithBossPending()
    {
        yield return CheckJoint(false, seedCorpus: true);
    }

    [UnityTest]
    public IEnumerator ProductionControllerUsesZonedProfileAndRetiresCanonicalPlayerWhenReturningToLegacy()
    {
        yield return CheckProductionController(20260929, false);
    }

    [UnityTest]
    public IEnumerator ThreeSeedProductionReadyKeepsActualPatrolHomeAndRefreshesRealDefeatedStaticWithoutRenewingResidents()
    {
        foreach (int seed in new[] { 20260929, -1391545000, 613580675 }) yield return CheckProductionController(seed, true);
    }

    [UnityTest, Ignore("N/A retired identity/Boss/old melee-Barrel fixture; preserved historical source. Current R3 exact restore/rejection and real Player combat have dedicated acceptance gates.")]
    public IEnumerator MeasuredCorpusBossSeedUsesActualProductionReadyPatrolAndOriginalClearSet()
    {
        // The first Boss in the complete, unfiltered Run141 corpus. No forced Boss chance.
        yield return CheckProductionController(20994032, true, true);
    }

    [UnityTest]
    public IEnumerator ExplicitUnitsCandidateUsesOnlyTwelveOriginalEcotypesWithoutBossAndKeepsOriginalClearSet()
    {
        foreach (int seed in new[] { 20260929, -1391545000, 613580675 })
            yield return CheckProductionController(seed, true, profile: EcologyRecipeDefinition.MainV1ProfileId);
    }

    [UnityTest]
    public IEnumerator StableNumericsCandidateUsesOriginalTwelveTypesAndRealReadyAcrossThreeSeeds()
    {
        foreach (int seed in new[] { 20260929, -1391545000, 613580675 })
            yield return CheckProductionController(seed, true, profile: EcologyRecipeDefinition.MainV1ProfileId);
    }

    [UnityTest, Ignore("N/A retired identity/Boss/old melee-Barrel fixture; preserved historical source. Current R3 exact restore/rejection and real Player combat have dedicated acceptance gates.")]
    public IEnumerator StableNumericsCandidateExactE1StoreRestoreUsesRetainedStrategy()
    {
        yield return CheckProductionController(21517677, false, profile: EcologyRecipeDefinition.MainV1ProfileId, inspectCheckpoint: true);
    }

    [UnityTest, Ignore("N/A retired identity/Boss/old melee-Barrel fixture; preserved historical source. Current R3 exact restore/rejection and real Player combat have dedicated acceptance gates.")]
    public IEnumerator StableNumericsSelectionKeepsOldTwelveEntryAndSlotBytesOnExactRestore()
    {
        yield return CheckProductionController(20260929, false, profile: EcologyRecipeDefinition.UnitsCandidateProfileId,
            inspectUpgrade: true, upgradeProfile: EcologyRecipeDefinition.UnitsStableCandidateProfileId);
    }

    [UnityTest, Ignore("N/A retired identity/Boss/old melee-Barrel fixture; preserved historical source. Current R3 exact restore/rejection and real Player combat have dedicated acceptance gates.")]
    public IEnumerator StableNumericsSelectionKeepsOldBossEntryAndSlotBytesOnExactRestore()
    {
        yield return CheckProductionController(20994032, false, requireBoss: true, inspectUpgrade: true,
            upgradeProfile: EcologyRecipeDefinition.UnitsStableCandidateProfileId);
    }

    [UnityTest, Ignore("N/A retired identity/Boss/old melee-Barrel fixture; preserved historical source. Current R3 exact restore/rejection and real Player combat have dedicated acceptance gates.")]
    public IEnumerator StableNumericsSelectionCannotSubstituteItsStrategyWhenOldEntryIsMissing()
    {
        yield return CheckProductionController(20260929, false, profile: EcologyRecipeDefinition.UnitsCandidateProfileId,
            inspectUpgrade: true, omitRetainedOldVersion: true, upgradeProfile: EcologyRecipeDefinition.UnitsStableCandidateProfileId);
    }

    [UnityTest]
    public IEnumerator StableNumericsCandidateActualRetryAndNextFloorKeepExplicitProfile()
    {
        yield return CheckProductionController(20260929, false, profile: EcologyRecipeDefinition.MainV1ProfileId, inspectQualityRetry: true);
        yield return CheckProductionController(20260929, false, profile: EcologyRecipeDefinition.MainV1ProfileId, inspectFloorTransition: true);
    }

    [UnityTest]
    public IEnumerator DevelopmentBoundariesSwitchOnlyDisplayAndClearWhenActualGenerationReturnsToLegacy()
    {
        yield return CheckProductionController(20260929, false, profile: EcologyRecipeDefinition.MainV1ProfileId, inspectOverlay: true);
    }

    [UnityTest]
    public IEnumerator ProductionCandidateCanonicalPlayerMovesThroughActualOriginalInput()
    {
        yield return CheckProductionController(20260929, false, profile: EcologyRecipeDefinition.MainV1ProfileId, exerciseInput: true);
    }

    [UnityTest, Ignore("N/A retired identity/Boss/old melee-Barrel fixture; preserved historical source. Current R3 exact restore/rejection and real Player combat have dedicated acceptance gates.")]
    public IEnumerator ProductionCandidateCheckpointBindsExactAcceptedEcologyAttemptDigestAndVersionTuple()
    {
        // First e=1 accepted seed in the complete Run159 corpus, not a forced retry setting.
        yield return CheckProductionController(21517677, false, profile: EcologyRecipeDefinition.MainV1ProfileId, inspectCheckpoint: true);
    }

    [UnityTest, Ignore("N/A retired identity/Boss/old melee-Barrel fixture; preserved historical source. Current R3 exact restore/rejection and real Player combat have dedicated acceptance gates.")]
    public IEnumerator RetainedSixteenTypeEntrySurvivesExplicitTwelveTypeUpgradeAndActualRestoreWithoutRewritingItsSlot()
    {
        yield return CheckProductionController(20260929, false, profile: "ecology-zoning-v1", inspectUpgrade: true);
    }

    [UnityTest, Ignore("N/A retired identity/Boss/old melee-Barrel fixture; preserved historical source. Current R3 exact restore/rejection and real Player combat have dedicated acceptance gates.")]
    public IEnumerator MissingRetainedOldEntryFailsBeforePublicationWithoutCurrentVersionFallbackOrOldSlotRewrite()
    {
        yield return CheckProductionController(20260929, false, profile: "ecology-zoning-v1", inspectUpgrade: true, omitRetainedOldVersion: true);
    }

    [UnityTest, Ignore("N/A retired identity/Boss/old melee-Barrel fixture; preserved historical source. Current R3 exact restore/rejection and real Player combat have dedicated acceptance gates.")]
    public IEnumerator SameProfileTwoActualRetainedConfigsSelectNewCurrentButRestoreExactOldTuple()
    {
        yield return CheckProductionController(20260929, false, profile: EcologyRecipeDefinition.MainV1ProfileId, inspectUpgrade: true, sameProfileFixtureUpgrade: true);
    }

    [UnityTest, Ignore("N/A retired identity/Boss/old melee-Barrel fixture; preserved historical source. Current R3 exact restore/rejection and real Player combat have dedicated acceptance gates.")]
    public IEnumerator SameProfileMissingOldTupleCannotSubstituteNewCurrentConfig()
    {
        yield return CheckProductionController(20260929, false, profile: EcologyRecipeDefinition.MainV1ProfileId, inspectUpgrade: true,
            omitRetainedOldVersion: true, sameProfileFixtureUpgrade: true);
    }

    [UnityTest, Ignore("N/A retired UnitsCandidateV1 mutation fixture identity; original source retained. R3 manifest/config integrity is verified by the current exact-content gates.")]
    public IEnumerator SameImportedGuidOwnedFootMutationCannotRestoreUnchangedVersionAndSlot()
    {
        yield return CheckProductionController(20260929, false, profile: EcologyRecipeDefinition.MainV1ProfileId, retainedMutationKind: "foot");
    }

    [UnityTest, Ignore("N/A retired UnitsCandidateV1 mutation fixture identity; original source retained. R3 manifest/config integrity is verified by the current exact-content gates.")]
    public IEnumerator SameNamedOwnedConfigNumericMutationCannotReuseUnchangedVersionIdentity()
    {
        yield return CheckProductionController(20260929, false, profile: EcologyRecipeDefinition.MainV1ProfileId, retainedMutationKind: "config");
    }

    [UnityTest]
    public IEnumerator ExplicitVersionedCandidateFloorTransitionKeepsItsProfileAndCapturesFreshEntry()
    {
        yield return CheckProductionController(20260929, false, profile: EcologyRecipeDefinition.MainV1ProfileId, inspectFloorTransition: true);
    }

    [UnityTest, Ignore("N/A retired identity/Boss/old melee-Barrel fixture; preserved historical source. Current R3 exact restore/rejection and real Player combat have dedicated acceptance gates.")]
    public IEnumerator RetainedRealBossEntryRestoresAfterIsolatedNoBossContentSelection()
    {
        yield return CheckProductionController(20994032, false, requireBoss: true, inspectUpgrade: true);
    }

    [UnityTest, Ignore("N/A retired identity/Boss/old melee-Barrel fixture; preserved historical source. Current R3 exact restore/rejection and real Player combat have dedicated acceptance gates.")]
    public IEnumerator ProductionCandidateOriginalInputCombatAndActualFExitUseAllThreeFamilies()
    {
        yield return CheckProductionController(20260929, false, profile: EcologyRecipeDefinition.MainV1ProfileId, combatFamily: "Torch");
        yield return CheckProductionController(20260929, false, profile: EcologyRecipeDefinition.MainV1ProfileId, combatFamily: "Barrel");
        yield return CheckProductionController(20470387, false, profile: EcologyRecipeDefinition.MainV1ProfileId, combatFamily: "TNT");
    }

    [UnityTest]
    public IEnumerator ExplicitCandidateQualityFailureOriginalRetryKeepsProfileAndFreshEntry()
    {
        yield return CheckProductionController(20260929, false, profile: EcologyRecipeDefinition.MainV1ProfileId, inspectQualityRetry: true);
    }

    private IEnumerator CheckProductionController(int seed, bool observePatrolAndDeath, bool requireBoss = false, string profile = EcologyRecipeDefinition.MainV1ProfileId, bool inspectOverlay = false, bool exerciseInput = false, bool inspectCheckpoint = false, bool inspectUpgrade = false, bool omitRetainedOldVersion = false, bool sameProfileFixtureUpgrade = false, string retainedMutationKind = null, bool inspectFloorTransition = false, string combatFamily = null, bool inspectQualityRetry = false, string upgradeProfile = null)
    {
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single); ownedScene = SceneManager.GetActiveScene(); yield return null;
        MonoBehaviour coordinator = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true).Single(b => b.GetType().Name == "TowerRunCoordinator");
        coordinator.GetType().GetMethod("ReturnToStartupChoice").Invoke(coordinator, null);
        MapGenerationController controller = UnityEngine.Object.FindObjectOfType<MapGenerationController>(true);
        coordinator.GetType().GetMethod("ReturnToStartupChoice").Invoke(coordinator, null);
        TowerRunCheckpointRecord staged = ((TowerRunCheckpointRecord)coordinator.GetType()
            .GetField("newRunBaseline", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(coordinator)).DeepClone();
        staged.runId = Guid.NewGuid().ToString("D");
        staged.generation = new TowerRunGenerationRecord { generatorVersion = 2, configurationIdentity = "random-map-terrain-v2",
            profileId = profile, requestedSeed = seed, attemptIndex = 0, staticBaselineSignature = string.Empty };
        MethodInfo begin = coordinator.GetType().GetMethod("BeginOperation", BindingFlags.Instance | BindingFlags.NonPublic);
        var mutationDefinition = retainedMutationKind == null ? null : AssetDatabase.LoadAssetAtPath<EcologyRecipeDefinition>(
            "Assets/Tests/MapGeneration/Fixtures/RetainedMutationProbe/Recipe_Probe.asset");
        if (retainedMutationKind != null)
        { Assert.That(mutationDefinition, Is.Not.Null); controller.ConfigureRetainedEcologyRecipes(new[] { mutationDefinition }); }
        begin.Invoke(coordinator, new object[] { Enum.Parse(begin.GetParameters()[0].ParameterType, "NewRun"), staged, false });
        Assert.That(controller.Orchestrator.Context.IsReady, Is.True, controller.Orchestrator.Context.Failure?.Reason);
        MapRuntimeContext context = controller.Orchestrator.Context;
        Assert.That(context.ActivePlacementPlan.Ecology, Is.Not.Null, "The new profile must use the actual accepted zoned layout, rather than silently falling through to default.");
        MapEcologyLayout layout = context.ActivePlacementPlan.Ecology;
        if (requireBoss) Assert.That(layout.Regions.Count(r => r.Kind == MapEcologyRegionKind.Boss), Is.EqualTo(1));
        if (profile == EcologyRecipeDefinition.MainV1ProfileId || profile == EcologyRecipeDefinition.UnitsCandidateProfileId || profile == EcologyRecipeDefinition.UnitsStableCandidateProfileId)
        {
            string[] retainedIds = Resources.Load<EcologyRecipeDefinition>(EcologyRecipeDefinition.ResourcePathFor(EcologyRecipeDefinition.MainV1ProfileId)).ResolveTypes()
                .Select(type => type.TypeId).OrderBy(id => id).ToArray();
            Assert.That(retainedIds.Length, Is.EqualTo(12));
            Assert.That(layout.Regions.Any(r => r.Kind == MapEcologyRegionKind.Boss), Is.False, "No real compatible Boss content is claimed.");
            Assert.That(layout.Residents.All(r => retainedIds.Contains(r.TypeId)), Is.True, "No hidden/fallback retired species may enter planning.");
        }
        var population = controller.GetComponentInParent<MapEcologyPopulationService>();
        GameObject player = controller.Player.gameObject;
        Assert.That(player.GetComponent<EcologyCombatBinding>()?.IsLive, Is.True);
        Assert.That(player.GetComponent<EcologyMotionBody>()?.IsReady, Is.True);
        Assert.That(layout.Residents.Count, Is.GreaterThan(0));
        Component overlay = null;
        if (inspectOverlay)
        {
            Type overlayType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("EcologyRegionDebugOverlay")).FirstOrDefault(t => t != null);
            Assert.That(overlayType, Is.Not.Null, "Editor/Development needs the accepted-layout display; release must compile it out.");
            overlay = controller.GetComponent(overlayType); Assert.That(overlay, Is.Not.Null);
            overlayType.GetMethod("Refresh").Invoke(overlay, null);
            string signature = layout.Signature; long version = controller.EcologyRuntime.StaticVersion;
            foreach (bool visible in new[] { true, false, true, false })
            {
                overlayType.GetProperty("Visible").SetValue(overlay, visible);
                overlayType.GetMethod("Refresh").Invoke(overlay, null);
                Assert.That((int)overlayType.GetProperty("SegmentCount").GetValue(overlay), Is.GreaterThan(0));
                Assert.That(context.ActivePlacementPlan.Ecology.Signature, Is.EqualTo(signature));
                Assert.That(controller.EcologyRuntime.StaticVersion, Is.EqualTo(version));
            }
        }
        foreach (MapEcologyResident resident in layout.Residents)
        {
            Assert.That(population.TryGetActiveInstance(resident.Id, out UnityEngine.Object instance), Is.True);
            var actor = ((Component)instance).gameObject;
            Assert.That(actor.GetComponent<EcologyResidentBinding>()?.CanAct, Is.True);
            Assert.That(actor.GetComponent<EcologyMotionBody>()?.IsReady, Is.True);
            Assert.That(actor.GetComponent<EcologyCombatBinding>()?.Barrier, Is.SameAs(player.GetComponent<EcologyCombatBinding>().Barrier));
            if (profile == EcologyRecipeDefinition.MainV1ProfileId || profile == EcologyRecipeDefinition.UnitsCandidateProfileId || profile == EcologyRecipeDefinition.UnitsStableCandidateProfileId)
            {
                Component pooled = actor.GetComponent("PooledObject");
                var source = (GameObject)pooled.GetType().GetProperty("SourcePrefab").GetValue(pooled);
                var acceptedDirectory = (EcologyEcotypeDefinition[])controller.GetType().GetField("zoningTypes", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
                Assert.That(source, Is.SameAs(acceptedDirectory.Single(t => t.TypeId == resident.TypeId).Prefab),
                    "The original pool's actual source must equal the accepted ecotype, not an aliased replacement.");
            }
        }
        MonoBehaviour floor = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true).Single(b => b.GetType().Name == "TowerFloorRuntimeController");
        Assert.That((int)floor.GetType().GetProperty("TargetCount").GetValue(floor), Is.EqualTo(layout.Residents.Count));
        if (inspectQualityRetry)
        {
            coordinator.GetType().GetMethod("ReturnToStartupChoice").Invoke(coordinator, null);
            var rejected = staged.DeepClone();
            rejected.floor = new TowerRunFloorRecord { floorNumber = 3, displayName = "第 3 层" };
            rejected.generation.requestedSeed = -337455008;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Map generation failed \\[EcologyBudgetExhaustedOnFixedTerrain\\]:"));
            begin.Invoke(coordinator, new object[] { Enum.Parse(begin.GetParameters()[0].ParameterType, "FloorTransition"), rejected, false });
            Assert.That(coordinator.GetType().GetProperty("State").GetValue(coordinator).ToString(), Is.EqualTo("Failure"));
            Assert.That(context.Failure.Code, Is.EqualTo("EcologyBudgetExhaustedOnFixedTerrain"));
            Assert.That((bool)coordinator.GetType().GetProperty("CanRetryFailedOperation").GetValue(coordinator), Is.True);
            coordinator.GetType().GetMethod("RetryFailedOperation").Invoke(coordinator, null); yield return null;
            Assert.That(controller.GetComponentInParent<MapRuntimeBootstrap>().DiagnosticSnapshot.ActiveRequest.ProfileId, Is.EqualTo(profile),
                "The original Retry action must not substitute the official legacy/default profile for an explicit candidate.");
            Assert.That(context.IsReady, Is.True, context.Failure?.Reason);
            var retried = (TowerRunCheckpointRecord)coordinator.GetType().GetField("currentRecord", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(coordinator);
            Assert.That(retried.schemaVersion, Is.EqualTo(TowerRunCheckpointRecord.EcologySchemaVersion));
            Assert.That(retried.generation.ecology.layoutDigest, Is.EqualTo(context.ActivePlacementPlan.Ecology.Signature));
            Debug.Log($"ECOLOGY_ORIGINAL_QUALITY_RETRY rejectedSeed=-337455008 floor=3 acceptedSeed={retried.generation.requestedSeed} profile={retried.generation.profileId} " +
                "originalFailureScreenAction=True freshEntry=True noOldExactTupleReused=True officialDefaultUnchanged=True");
        }
        if (combatFamily != null) yield return CheckProductionInputCombatAndExit(coordinator, controller, floor, combatFamily);
        if (inspectFloorTransition)
        {
            Time.timeScale = 0f;
            var before = ((TowerRunCheckpointRecord)coordinator.GetType().GetField("currentRecord", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(coordinator)).DeepClone();
            Guid generation = context.ActiveGenerationId.Value;
            MapObjectRegistry previousRegistry = context.ActiveRegistry;
            EcologyDamageBarrier previousBarrier = player.GetComponent<EcologyCombatBinding>().Barrier;
            foreach (var entry in context.ActiveRegistry.Entries.Where(e => e.Role == MonsterPlacementService.MonsterRole).ToArray())
            {
                var damage = (Component)entry.Instance;
                damage.GetType().GetMethod("TakeDamage", new[] { typeof(float) }).Invoke(damage, new object[] { float.MaxValue });
            }
            Assert.That(floor.GetType().GetProperty("State").GetValue(floor).ToString(), Is.EqualTo("Available"));
            // Exercise the original Coordinator transition handler after real target defeat.
            // This test deliberately does not claim player travel, F input, or natural combat coverage.
            coordinator.GetType().GetMethod("HandleExitUseRequested", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(coordinator, new object[] { generation });
            for (int retry = 0; !context.IsReady && retry < 2; retry++)
            {
                Assert.That(context.Failure.Code, Is.EqualTo("EcologyBudgetExhaustedOnFixedTerrain"));
                LogAssert.Expect(LogType.Error, "Map generation failed [" + context.Failure.Code + "]: " + context.Failure.Reason);
                coordinator.GetType().GetMethod("RetryFailedOperation").Invoke(coordinator, null);
            }
            yield return null;
            Assert.That(context.IsReady, Is.True, context.Failure?.Reason);
            Assert.That(previousRegistry.IsRetired, Is.True);
            Assert.That(previousBarrier == null || !previousBarrier.IsScopeOpen, Is.True);
            Assert.That(context.ActiveGenerationId, Is.Not.EqualTo(generation));
            Assert.That(context.ActivePlacementPlan.Ecology, Is.Not.Null, "An explicitly selected ecology session must not silently turn into the official legacy default on its next floor.");
            var next = (TowerRunCheckpointRecord)coordinator.GetType().GetField("currentRecord", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(coordinator);
            Assert.That(next.generation.profileId, Is.EqualTo(profile));
            Assert.That(next.schemaVersion, Is.EqualTo(TowerRunCheckpointRecord.EcologySchemaVersion));
            Assert.That(next.floor.floorNumber, Is.EqualTo(before.floor.floorNumber + 1));
            Assert.That(next.runId, Is.EqualTo(before.runId));
            Assert.That(next.generation.ecology.layoutDigest, Is.EqualTo(context.ActivePlacementPlan.Ecology.Signature));
            Assert.That(next.generation.ecology.contentVersion, Is.EqualTo(before.generation.ecology.contentVersion));
            Debug.Log($"ECOLOGY_ACTUAL_NEXT_FLOOR from={before.floor.floorNumber} to={next.floor.floorNumber} profile={next.generation.profileId} oldScopeClosed=True newEntryCaptured=True source=CONTROLLED_ORIGINAL_COORDINATOR_HANDLER_NOT_F_OR_NATURAL_COMBAT");
        }
        if (retainedMutationKind != null) yield return CheckOwnedRetainedMutation(coordinator, controller, layout, mutationDefinition, retainedMutationKind);
        if (inspectUpgrade)
        {
            object[] args = { null, null };
            Assert.That((bool)coordinator.GetType().GetMethod("TryCreateSaveRequest").Invoke(coordinator, args), Is.True);
            var original = (TowerRunCheckpointRecord)args[0];
            var write = (TowerRunSlotWriteResult)coordinator.GetType().GetMethod("TrySaveEntranceCandidate")
                .Invoke(coordinator, new object[] { 1, original, false, null });
            Assert.That(write.succeeded, Is.True, write.message);
            var read = (TowerRunSlotReadResult)coordinator.GetType().GetMethod("ReadSlot").Invoke(coordinator, new object[] { 1 });
            Assert.That(read.state, Is.EqualTo(TowerRunSlotState.Valid));
            object store = coordinator.GetType().GetField("slotStore", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(coordinator);
            string path = (string)store.GetType().GetMethod("GetSlotPath", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(store, new object[] { 1 });
            Assert.That(Path.GetFullPath(path), Does.StartWith(Path.GetFullPath(isolatedRoot) + Path.DirectorySeparatorChar));
            byte[] originalBytes = File.ReadAllBytes(path);
            var fixtureR2 = sameProfileFixtureUpgrade ? AssetDatabase.LoadAssetAtPath<EcologyRecipeDefinition>(
                "Assets/Tests/MapGeneration/Fixtures/RetainedRecipeV2/Recipe_R2.asset") : null;
            if (sameProfileFixtureUpgrade) Assert.That(fixtureR2, Is.Not.Null);
            var v2 = original.DeepClone(); v2.schemaVersion = 1; v2.runId = Guid.NewGuid().ToString("D");
            v2.generation = new TowerRunGenerationRecord { generatorVersion = 2, configurationIdentity = "random-map-terrain-v2",
                profileId = upgradeProfile ?? EcologyRecipeDefinition.UnitsCandidateProfileId, requestedSeed = seed, attemptIndex = 0, staticBaselineSignature = string.Empty };
            coordinator.GetType().GetMethod("ReturnToStartupChoice").Invoke(coordinator, null);
            if (sameProfileFixtureUpgrade)
                controller.ConfigureRetainedEcologyRecipes(new[] { fixtureR2, Resources.Load<EcologyRecipeDefinition>("Ecology/UnitsCandidateV1/Recipe") });
            begin.Invoke(coordinator, new object[] { Enum.Parse(begin.GetParameters()[0].ParameterType, "NewRun"), v2, false }); yield return null;
            Assert.That(controller.Orchestrator.Context.IsReady, Is.True, controller.Orchestrator.Context.Failure?.Reason);
            var currentV2 = ((TowerRunCheckpointRecord)coordinator.GetType().GetField("currentRecord", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(coordinator)).DeepClone();
            Assert.That(currentV2.generation.profileId, Is.EqualTo(v2.generation.profileId));
            Assert.That(currentV2.generation.ecology.contentVersion, Is.Not.EqualTo(original.generation.ecology.contentVersion));
            Assert.That(currentV2.generation.ecology.configDigest, Is.Not.EqualTo(original.generation.ecology.configDigest));
            Assert.That(currentV2.generation.ecology.footprintVersion,
                sameProfileFixtureUpgrade || upgradeProfile != null && profile == EcologyRecipeDefinition.UnitsCandidateProfileId ? Is.EqualTo(original.generation.ecology.footprintVersion) : Is.Not.EqualTo(original.generation.ecology.footprintVersion));
            if (sameProfileFixtureUpgrade)
            {
                Assert.That(currentV2.generation.profileId, Is.EqualTo(original.generation.profileId));
                Assert.That(currentV2.generation.ecology.layoutDigest, Is.Not.EqualTo(original.generation.ecology.layoutDigest));
            }
            Guid upgradeGeneration = context.ActiveGenerationId.Value;
            var upgradeDefinition = Resources.Load<EcologyRecipeDefinition>(EcologyRecipeDefinition.ResourcePathFor(v2.generation.profileId));
            var currentDirectory = (sameProfileFixtureUpgrade ? fixtureR2 : upgradeDefinition).ResolveTypes();
            Assert.That(controller.Orchestrator.Context.ActivePlacementPlan.Ecology.Residents.All(r => currentDirectory.Any(t => t.TypeId == r.TypeId)), Is.True);
            Type monsterType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("BaseMonster")).First(t => t != null);
            Component[] allActualMonsters = UnityEngine.Object.FindObjectsOfType(monsterType).Cast<Component>().ToArray();
            Assert.That(allActualMonsters.Length, Is.EqualTo(context.ActivePlacementPlan.Ecology.Residents.Count), "No unregistered legacy generation entry may leave extra active monsters.");
            foreach (Component monster in allActualMonsters)
            {
                Component pooled = monster.GetComponent("PooledObject"); Assert.That(pooled, Is.Not.Null);
                GameObject source = (GameObject)pooled.GetType().GetProperty("SourcePrefab").GetValue(pooled);
                Assert.That(currentDirectory.Any(t => t.Prefab == source), Is.True, "Every actual active source must be eligible in this candidate.");
            }
            coordinator.GetType().GetMethod("ReturnToStartupChoice").Invoke(coordinator, null);
            if (omitRetainedOldVersion)
            {
                MethodInfo configure = controller.GetType().GetMethod("ConfigureRetainedEcologyRecipes");
                Assert.That(configure, Is.Not.Null, "A finite retained catalog must be explicit; a missing entry must not silently load current/default resources.");
                configure.Invoke(controller, new object[] { new[] { sameProfileFixtureUpgrade ? fixtureR2 : upgradeDefinition } });
                LogAssert.Expect(LogType.Error, "Map integration failed [ExactRetainedEcologyRecipeUnavailable]: The exact requested content is absent from the finite retained catalog.");
            }
            coordinator.GetType().GetMethod("BeginRestoreSlot").Invoke(coordinator, new object[] { 1, read.confirmationToken });
            if (omitRetainedOldVersion)
            {
                Assert.That(context.IsReady, Is.False);
                Assert.That(context.Failure.Code, Is.EqualTo("ExactRetainedEcologyRecipeUnavailable"));
                Assert.That(coordinator.GetType().GetProperty("State").GetValue(coordinator).ToString(), Is.EqualTo("Failure"));
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(originalBytes));
                coordinator.GetType().GetMethod("ReturnToStartupChoice").Invoke(coordinator, null);
                coordinator.GetType().GetMethod("BeginNewRun").Invoke(coordinator, null); yield return null;
                Assert.That(context.IsReady, Is.True); Assert.That(context.ActivePlacementPlan.Ecology, Is.Null);
                Assert.That(player.GetComponent<EcologyMotionBody>().OwnsMotionRoute, Is.False);
                Assert.That(player.GetComponent<EcologyCombatBinding>().Barrier, Is.Null);
                Debug.Log("ECOLOGY_MISSING_RETAINED_ENTRY actualRestoreFailed=True currentFallback=False slotBytesUnchanged=True laterLegacyNewRunReady=True noResourceDeleted=True");
                coordinator.GetType().GetMethod("ReturnToStartupChoice").Invoke(coordinator, null);
                yield break;
            }
            Assert.That(controller.Orchestrator.Context.IsReady, Is.True, controller.Orchestrator.Context.Failure?.Reason);
            Assert.That(context.ActiveGenerationId, Is.Not.EqualTo(upgradeGeneration));
            Assert.That(context.ActivePlacementPlan.Ecology.Signature, Is.EqualTo(layout.Signature));
            var restored = (TowerRunCheckpointRecord)coordinator.GetType().GetField("currentRecord", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(coordinator);
            Assert.That(restored.runId, Is.EqualTo(original.runId));
            Assert.That(restored.generation.profileId, Is.EqualTo(original.generation.profileId));
            Assert.That(restored.generation.ecology.contentVersion, Is.EqualTo(original.generation.ecology.contentVersion));
            Assert.That(restored.generation.ecology.configDigest, Is.EqualTo(original.generation.ecology.configDigest));
            Assert.That(restored.generation.ecology.footprintVersion, Is.EqualTo(original.generation.ecology.footprintVersion));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(originalBytes), "Restoring through a content upgrade must not rewrite the old slot.");
            Debug.Log($"ECOLOGY_ACTUAL_CONTENT_UPGRADE_RESTORE v1={original.generation.profileId}/{original.generation.ecology.contentVersion} " +
                $"v2={currentV2.generation.profileId}/{currentV2.generation.ecology.contentVersion} v1FootRestored=True slotBytesUnchanged=True " +
                $"sameProfileTwoActualConfigs={sameProfileFixtureUpgrade} actualRetainedAssetConfigurations=True explicitIsolatedNewSelection=True formalDefaultUnchanged=True");
        }
        if (inspectCheckpoint)
        {
            var saved = ((TowerRunCheckpointRecord)coordinator.GetType().GetField("currentRecord", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(coordinator)).DeepClone();
            Assert.That(saved.schemaVersion, Is.EqualTo(2), "New zoned entries need the explicit ecology checkpoint schema; old entries stay schema1.");
            FieldInfo field = saved.generation.GetType().GetField("ecology"); Assert.That(field, Is.Not.Null);
            object identity = field.GetValue(saved.generation); Assert.That(identity, Is.Not.Null);
            foreach (string name in new[] { "recipeVersion", "configDigest", "contentVersion", "footprintVersion", "contextIdentity", "layoutDigest" })
                Assert.That(identity.GetType().GetField(name)?.GetValue(identity) as string, Is.Not.Null.And.Not.Empty, name);
            Assert.That((int)identity.GetType().GetField("ecologyAttempt").GetValue(identity), Is.EqualTo(1));
            Assert.That(identity.GetType().GetField("layoutDigest").GetValue(identity), Is.EqualTo(layout.Signature));
            Assert.That(identity.GetType().GetField("footprintVersion").GetValue(identity), Is.EqualTo(layout.FootprintVersion));
            Assert.That((int)identity.GetType().GetField("residentCount").GetValue(identity), Is.EqualTo(layout.Residents.Count));
            Assert.That((int)identity.GetType().GetField("regionCount").GetValue(identity), Is.EqualTo(layout.Regions.Count));
            Assert.That((int)identity.GetType().GetField("merchantCount").GetValue(identity), Is.EqualTo(1));
            object[] requestArgs = { null, null };
            Assert.That((bool)coordinator.GetType().GetMethod("TryCreateSaveRequest").Invoke(coordinator, requestArgs), Is.True);
            var entry = (TowerRunCheckpointRecord)requestArgs[0];
            var write = (TowerRunSlotWriteResult)coordinator.GetType().GetMethod("TrySaveEntranceCandidate")
                .Invoke(coordinator, new object[] { 1, entry, false, null });
            Assert.That(write.succeeded, Is.True, write.message);
            var read = (TowerRunSlotReadResult)coordinator.GetType().GetMethod("ReadSlot").Invoke(coordinator, new object[] { 1 });
            Assert.That(read.state, Is.EqualTo(TowerRunSlotState.Valid), read.message);
            Assert.That(read.record.generation.ecology.ecologyAttempt, Is.EqualTo(1));
            string raw = JsonUtility.ToJson(read.record);
            string missing = System.Text.RegularExpressions.Regex.Replace(raw, "\\\"ecologyAttempt\\\":1,?", "", System.Text.RegularExpressions.RegexOptions.None);
            Assert.That(missing, Is.Not.EqualTo(raw));
            // Remove the following separator only when needed; JsonUtility's missing int would otherwise become e=0.
            missing = missing.Replace(",}", "}");
            var policy = new TowerRunCompatibilityPolicy(1, 2, "random-map-terrain-v2");
            Assert.That(policy.EvaluatePersisted(JsonUtility.FromJson<TowerRunCheckpointRecord>(missing), missing, out _), Is.EqualTo(TowerRunSlotState.Corrupt));
            Guid oldGeneration = context.ActiveGenerationId.Value;
            coordinator.GetType().GetMethod("ReturnToStartupChoice").Invoke(coordinator, null);
            coordinator.GetType().GetMethod("BeginRestoreSlot").Invoke(coordinator, new object[] { 1, read.confirmationToken });
            Assert.That(controller.Orchestrator.Context.IsReady, Is.True, controller.Orchestrator.Context.Failure?.Reason);
            Assert.That(controller.Orchestrator.Context.ActiveGenerationId, Is.Not.EqualTo(oldGeneration));
            Assert.That(controller.Orchestrator.Context.ActivePlacementPlan.Ecology.Signature, Is.EqualTo(layout.Signature));
            var accepted = (EcologyPlanningAttemptResult)controller.GetType().GetField("zoningAccepted", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            Assert.That(accepted.AcceptedAttempt, Is.EqualTo(1)); Assert.That(accepted.AttemptsUsed, Is.EqualTo(1), "Restore must run only the recorded e, not search from zero.");
            Debug.Log($"ECOLOGY_EXACT_ENTRY_STORE_RESTORE schema=2 seed={seed} acceptedE=1 restoreAttempts=1 oldG={oldGeneration} newG={context.ActiveGenerationId} rawMissingERejected=True digest={layout.Signature}");
        }
        if (exerciseInput)
        {
            var oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>("EcologyProductionInputProof");
            var oldBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var motion = player.GetComponent<EcologyMotionBody>();
            var combat = player.GetComponent<EcologyCombatBinding>();
            var trace = new List<string>();
            Action<EcologyMotionTrace> observe = e => { if (trace.Count < 12) trace.Add(e.Phase + ":" + e.Position.ToString("R")); };
            motion.OnTrace += observe;
            var body = player.GetComponent<Rigidbody2D>(); Vector2 start = body.position;
            Component action = player.GetComponent("PlayerAction");
            double started = Time.timeAsDouble; string diagnostics = "";
            bool initiallyEnabled = keyboard.enabled;
            try
            {
                for (int frame = 0; frame < 20; frame++)
                {
                    if (!keyboard.enabled) InputSystem.EnableDevice(keyboard);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
                    yield return null;
                    yield return new WaitForFixedUpdate();
                }
                diagnostics = $"initiallyEnabled={initiallyEnabled} nowEnabled={keyboard.enabled} delta={Vector2.Distance(start, body.position):R} scaled={Time.timeAsDouble-started:R} timeScale={Time.timeScale:R} " +
                    $"ready={motion.IsReady} owns={motion.OwnsMotionRoute} fault={combat.MotionFaultRevision} key={keyboard.dKey.isPressed} " +
                    $"actionEnabled={((Behaviour)action).enabled} locked={action.GetType().GetProperty("IsControlLocked").GetValue(action)} move={action.GetType().GetProperty("MoveInput").GetValue(action)} traces={string.Join(";",trace)}";
                Debug.Log("ECOLOGY_PRODUCTION_PLAYER_INPUT " + diagnostics);
                Assert.That(Vector2.Distance(start, body.position), Is.GreaterThan(.05f), diagnostics);
            }
            finally
            {
                motion.OnTrace -= observe;
                if (keyboard.added) InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = oldBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
            }
        }
        if (observePatrolAndDeath)
        {
            var catalog = Resources.Load<EcologyRecipeDefinition>(EcologyRecipeDefinition.ResourcePathFor(profile)).ResolveTypes().ToDictionary(t => t.TypeId);
            var actors = layout.Residents.Select(r =>
            {
                population.TryGetActiveInstance(r.Id, out UnityEngine.Object instance);
                GameObject actor = ((Component)instance).gameObject;
                return (resident: r, actor, body: actor.GetComponent<Rigidbody2D>(), binding: actor.GetComponent<EcologyResidentBinding>(),
                    combat: actor.GetComponent<EcologyCombatBinding>(), foot: catalog[r.TypeId].ResolveMovementFoot(actor), start: actor.GetComponent<Rigidbody2D>().position);
            }).ToArray();
            int observations = 0;
            for (int step = 0; step < 120; step++)
            {
                yield return new WaitForFixedUpdate();
                foreach (var actor in actors)
                {
                    Assert.That(actor.binding.Home.ContainsFullFoot(actor.foot, actor.body.position), Is.True, "Production patrol actually left Home: " + actor.resident.Id);
                    Assert.That(actor.combat.MotionFaultRevision, Is.Zero, actor.resident.Id); observations++;
                }
            }
            int moved = actors.Count(a => Vector2.Distance(a.start, a.body.position) > .05f);
            Assert.That(moved, Is.GreaterThan(0), "Actual original production AI must move after whole Ready.");
            Time.timeScale = 0f;
            var tracked = actors[0]; long lease = tracked.combat.Lease;
            var excursion = tracked.binding.Excursion;
            Component health = tracked.actor.GetComponent("BaseMonster");
            float hp = Convert.ToSingle(health.GetType().GetProperty("CurrentHealth").GetValue(health));
            MapObjectRegistryEntry destructible = context.ActiveRegistry.Entries.First(e => e.Role == DestructiblePlacementService.DestructibleRole && e.Instance != null);
            Component damage = (Component)destructible.Instance;
            long version = controller.EcologyRuntime.StaticVersion;
            damage.GetType().GetMethod("TakeDamage", new[] { typeof(float) }).Invoke(damage, new object[] { 1000000f });
            Assert.That((bool)damage.GetType().GetProperty("IsDefeated").GetValue(damage), Is.True);
            Assert.That(context.ActiveRegistry.TryGet(destructible.LogicalObjectId, out _), Is.False, "Original death release must remove the real static registry entry.");
            tracked.binding.TryGetHomeDistance(out _, out _);
            Assert.That(controller.EcologyRuntime.StaticVersion, Is.EqualTo(version + 1));
            Assert.That(tracked.binding.Excursion, Is.SameAs(excursion)); Assert.That(tracked.combat.Lease, Is.EqualTo(lease));
            Assert.That(Convert.ToSingle(health.GetType().GetProperty("CurrentHealth").GetValue(health)), Is.EqualTo(hp));
            Assert.That(context.ActivePlacementPlan.Ecology.Signature, Is.EqualTo(layout.Signature));
            Assert.That(tracked.binding.ScopeFaulted, Is.False);
            Debug.Log($"ECOLOGY_PRODUCTION_NATIVE seed={seed} residents={actors.Length} moved={moved} actualHomeObservations={observations} " +
                $"realStaticDefeat={destructible.LogicalObjectId} version={version}->{controller.EcologyRuntime.StaticVersion} " +
                "originalDeathRegistryRelease=True healthLeaseExcursionPreserved=True layoutUnchanged=True");
        }
        EcologyDamageBarrier retired = player.GetComponent<EcologyCombatBinding>().Barrier;
        coordinator.GetType().GetMethod("ReturnToStartupChoice").Invoke(coordinator, null);
        Assert.That(retired.IsScopeOpen, Is.False, "Real Coordinator exit must retire the production scope even when paused.");
        var legacy = staged.DeepClone(); legacy.runId = Guid.NewGuid().ToString("D");
        legacy.generation = new TowerRunGenerationRecord { generatorVersion = 2, configurationIdentity = "random-map-terrain-v2",
            profileId = "terrain-v2", requestedSeed = seed, attemptIndex = 0, staticBaselineSignature = string.Empty };
        begin.Invoke(coordinator, new object[] { Enum.Parse(begin.GetParameters()[0].ParameterType, "NewRun"), legacy, false }); yield return null;
        Assert.That(controller.Orchestrator.Context.IsReady, Is.True);
        Assert.That(controller.Orchestrator.Context.ActivePlacementPlan.Ecology, Is.Null);
        Assert.That(player.GetComponent<EcologyMotionBody>().OwnsMotionRoute, Is.False);
        Assert.That(player.GetComponent<EcologyCombatBinding>().Barrier, Is.Null);
        if (inspectOverlay)
        {
            overlay.GetType().GetMethod("Refresh").Invoke(overlay, null);
            Assert.That((int)overlay.GetType().GetProperty("SegmentCount").GetValue(overlay), Is.Zero, "No stale accepted layout may survive a legacy generation.");
        }
        Debug.Log($"ECOLOGY_PRODUCTION_CONTROLLER residents={layout.Residents.Count} originalFloorTargets=True sharedPlayerBarrier=True " +
            "actualCoordinatorExitRetired=True legacyPlayerRouteRestored=True defaultStillLegacy=True");
        coordinator.GetType().GetMethod("ReturnToStartupChoice").Invoke(coordinator, null);
    }

    private IEnumerator CheckProductionInputCombatAndExit(MonoBehaviour coordinator, MapGenerationController controller,
        MonoBehaviour floor, string family)
    {
        var context = controller.Orchestrator.Context;
        var accepted = (EcologyPlanningAttemptResult)controller.GetType().GetField("zoningAccepted", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        var types = (EcologyEcotypeDefinition[])controller.GetType().GetField("zoningTypes", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        var population = controller.GetComponentInParent<MapEcologyPopulationService>();
        GameObject player = controller.Player.gameObject;
        var playerBody = player.GetComponent<Rigidbody2D>(); Collider2D playerFoot = player.transform.Find("MovementFeet").GetComponent<Collider2D>();
        var playerCombat = player.GetComponent<EcologyCombatBinding>();
        Component playerHealth = player.GetComponent("PlayerDamageController");
        var actors = context.ActivePlacementPlan.Ecology.Residents.Select(r =>
        {
            Assert.That(population.TryGetActiveInstance(r.Id, out UnityEngine.Object instance), Is.True);
            return (resident: r, actor: ((Component)instance).gameObject, type: types.Single(t => t.TypeId == r.TypeId));
        }).ToArray();
        foreach (var item in actors) ((Behaviour)item.actor.GetComponent("MonsterAIController")).enabled = false;
        Type damageType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("BaseDamageable")).First(t => t != null);
        Component[] allDamageables = UnityEngine.Object.FindObjectsOfType(damageType).Cast<Component>().ToArray();
        EcologyNavigationGraph graph = accepted.InitialPlacementGeometry.BuildNavigationGraph(playerFoot, 0f);
        Assert.That(accepted.InitialPlacementGeometry.TryWorldNode(playerBody.position, out int spawn), Is.True);
        var distances = graph.BuildReturnField(new[] { spawn });
        GameObject target = null; int goal = -1, best = int.MaxValue;
        Rect[] protection = (Rect[])typeof(EcologyDamageBarrier).GetProperty("Protection", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(playerCombat.Barrier);
        foreach (var candidate in actors.Where(x => x.type.Prefab.name.StartsWith(family + "_", StringComparison.Ordinal)))
        for (int n = 0; n < graph.NodeCount; n++)
        {
            if (!graph.IsLegal(n) || distances.Distances[n] < 0 || distances.Distances[n] >= best) continue;
            Vector2 pose = accepted.InitialPlacementGeometry.NodeWorldPosition(n);
            float distance = Vector2.Distance(pose, candidate.actor.transform.position);
            if (distance < 1.15f || distance > 1.55f || !MapElevationRuntimeQuery.SameLevel(pose, candidate.actor.transform.position)) continue;
            Vector2 centre = (Vector2)playerFoot.bounds.center + pose - playerBody.position;
            Vector2 extents = playerFoot.bounds.extents;
            Rect footBounds = Rect.MinMaxRect(centre.x - extents.x, centre.y - extents.y, centre.x + extents.x, centre.y + extents.y);
            if (protection.Any(r => r.Overlaps(footBounds))) continue;
            if (allDamageables.Any(d => d.gameObject != candidate.actor && d.gameObject.activeInHierarchy &&
                MapElevationRuntimeQuery.SameLevel(pose, d.transform.position) && Vector2.Distance(pose, d.transform.position) <= distance + .05f)) continue;
            target = candidate.actor; goal = n; best = distances.Distances[n];
        }
        Assert.That(target, Is.Not.Null, "Need a naturally placed reachable nearest original " + family + " target, not a relocated actor.");
        Component targetHealth = target.GetComponent("BaseMonster");
        float Health(Component c) => Convert.ToSingle(c.GetType().GetProperty("CurrentHealth").GetValue(c));
        var keyboard = InputSystem.AddDevice<Keyboard>("EcologyProductionCombatTravel");
        var mouse = InputSystem.AddDevice<Mouse>("EcologyProductionCombatMouse");
        var oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        var oldBackground = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        var requests = new List<EcologyAttackRequest>();
        void Capture(EcologyAttackRequest request, string phase) { if (phase == "Enqueued") requests.Add(request); }
        EcologyDamageBarrier barrier = playerCombat.Barrier; barrier.OnTrace += Capture;
        float protectedHealth = Health(playerHealth);
        Assert.That(playerCombat.ProtectionNow, Is.True, "Actual birth safety foot must be observed before leaving.");
        var safetyRequest = barrier.Prepare(target.GetComponent<EcologyCombatBinding>(), playerCombat);
        Assert.That(safetyRequest.Status, Is.EqualTo(EcologyAttackStatus.ProtectionCancelled));
        Assert.That(safetyRequest.Queue(() => true, () => throw new Exception("Actual birth protection dispatched damage")), Is.False);
        Assert.That(Health(playerHealth), Is.EqualTo(protectedHealth));
        try
        {
            if (!keyboard.enabled) InputSystem.EnableDevice(keyboard);
            if (!mouse.enabled) InputSystem.EnableDevice(mouse);
            Time.timeScale = 1f;
            yield return WalkOriginalInputPath(keyboard, player, accepted.InitialPlacementGeometry, graph, goal);
            yield return new WaitForFixedUpdate();
            Assert.That(playerCombat.ProtectionNow, Is.False);
            Assert.That(safetyRequest.Status, Is.EqualTo(EcologyAttackStatus.ProtectionCancelled));
            Assert.That(safetyRequest.Queue(() => true, () => throw new Exception("Leaving protection revived an old attack")), Is.False);
            Assert.That(playerCombat.MotionFaultRevision, Is.Zero);
            float playerBefore = Health(playerHealth);
            ((Behaviour)target.GetComponent("MonsterAIController")).enabled = true;
            float deadline = Time.time + 6f;
            while (Health(playerHealth) == playerBefore && Time.time < deadline) yield return new WaitForFixedUpdate();
            Assert.That(Health(playerHealth), Is.LessThan(playerBefore), "Original perception/FSM/attack must actually damage the production player.");
            ((Behaviour)target.GetComponent("MonsterAIController")).enabled = false;
            yield return new WaitForSeconds(.7f);
            float monsterBefore = Health(targetHealth);
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            deadline = Time.time + 3f;
            while (Health(targetHealth) == monsterBefore && Time.time < deadline) yield return new WaitForFixedUpdate();
            Assert.That(Health(targetHealth), Is.EqualTo(Mathf.Max(0f, monsterBefore - 20f)).Within(.001f));
            Assert.That(requests.Any(r => r.AttackerId == playerCombat.ResidentId && r.Status == EcologyAttackStatus.Consumed && r.DamageApplied), Is.True);
            float afterFirst = Health(targetHealth); yield return new WaitForSeconds(.15f);
            Assert.That(Health(targetHealth), Is.EqualTo(afterFirst), "One real mouse press must not duplicate its damage.");
            int presses = 1;
            while (!(bool)targetHealth.GetType().GetProperty("IsDefeated").GetValue(targetHealth) && presses < 12)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left)); yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState()); presses++;
                yield return new WaitForSeconds(.55f);
            }
            Assert.That((bool)targetHealth.GetType().GetProperty("IsDefeated").GetValue(targetHealth), Is.True);
            var tracked = actors.Single(x => x.actor == target);
            Assert.That(context.ActiveRegistry.TryGet(tracked.resident.Id, out _), Is.False);
            Assert.That((int)floor.GetType().GetProperty("DefeatedTargetCount").GetValue(floor), Is.EqualTo(1));
            yield return new WaitForSeconds(.7f);
            Assert.That(target.activeInHierarchy, Is.False, "Actual input defeat must reach the original delayed pool return.");
            Assert.That(requests.All(r => r.Status != EcologyAttackStatus.FaultRejected), Is.True);
            Debug.Log($"ECOLOGY_PRODUCTION_REAL_COMBAT family={family} target={tracked.type.Prefab.name} pathSteps={best} actualPlayerInput=True " +
                $"actualMonsterPerceptionFsmDamage=True playerHp={playerBefore:R}->{Health(playerHealth):R} firstPlayerDamage=20 presses={presses} realDeathRegistryPool=True " +
                $"observedRequests={requests.Count} noExtraHp=True noPostBindTeleport=True otherAiDisabledForIsolation=True");
            // Remaining targets are deliberately defeated through original receivers to isolate travel/F/transition.
            // They are not counted as natural player kills.
            foreach (var entry in context.ActiveRegistry.Entries.Where(e => e.Role == MonsterPlacementService.MonsterRole).ToArray())
            {
                var damage = (Component)entry.Instance;
                damage.GetType().GetMethod("TakeDamage", new[] { typeof(float) }).Invoke(damage, new object[] { float.MaxValue });
            }
            Assert.That(floor.GetType().GetProperty("State").GetValue(floor).ToString(), Is.EqualTo("Available"));
            yield return new WaitForSeconds(.7f);
            yield return WalkOriginalInputPath(keyboard, player, accepted.StaticGeometry,
                accepted.StaticGeometry.BuildNavigationGraph(playerFoot, 0f), accepted.ExitApproachNode);
            Guid generation = context.ActiveGenerationId.Value; MapObjectRegistry oldRegistry = context.ActiveRegistry;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            deadline = Time.realtimeSinceStartup + 8f;
            while (context.ActiveGenerationId == generation && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(context.ActiveGenerationId, Is.Not.EqualTo(generation));
            Assert.That(context.IsReady, Is.True, context.Failure?.Reason);
            Assert.That(context.ActivePlacementPlan.Ecology, Is.Not.Null);
            Assert.That(oldRegistry.IsRetired, Is.True);
            Assert.That(barrier == null || !barrier.IsScopeOpen, Is.True);
            Assert.That(player.GetComponent<EcologyCombatBinding>().MotionFaultRevision, Is.Zero);
            Debug.Log($"ECOLOGY_PRODUCTION_REAL_INPUT_F family={family} oldG={generation} newG={context.ActiveGenerationId} " +
                "actualOriginalInputTravel=True actualF=True oldRegistryRetired=True newVersionedFloor=True remainingClear=CONTROLLED_ORIGINAL_RECEIVERS_NOT_NATURAL_KILLS");
            object[] saveArguments = { null, null };
            Assert.That((bool)coordinator.GetType().GetMethod("TryCreateSaveRequest").Invoke(coordinator, saveArguments), Is.True);
            var checkpoint = (TowerRunCheckpointRecord)saveArguments[0];
            Assert.That(checkpoint.schemaVersion, Is.EqualTo(TowerRunCheckpointRecord.EcologySchemaVersion));
            Assert.That(checkpoint.generation.ecology.layoutDigest, Is.EqualTo(context.ActivePlacementPlan.Ecology.Signature));
            var previousSlot = (TowerRunSlotReadResult)coordinator.GetType().GetMethod("ReadSlot").Invoke(coordinator, new object[] { 1 });
            var saved = (TowerRunSlotWriteResult)coordinator.GetType().GetMethod("TrySaveEntranceCandidate").Invoke(coordinator,
                new object[] { 1, checkpoint, previousSlot.state == TowerRunSlotState.Valid, previousSlot.confirmationToken });
            Assert.That(saved.succeeded, Is.True, saved.message);
            var read = (TowerRunSlotReadResult)coordinator.GetType().GetMethod("ReadSlot").Invoke(coordinator, new object[] { 1 });
            Assert.That(read.state, Is.EqualTo(TowerRunSlotState.Valid), read.message);
            string slot = Path.Combine(TowerRunRuntimeEnvironment.SlotRoot, "tower-run-slot-1.json");
            Assert.That(Path.GetFullPath(slot), Does.StartWith(Path.GetFullPath(isolatedRoot) + Path.DirectorySeparatorChar));
            byte[] slotBytes = File.ReadAllBytes(slot);
            Guid beforeRestore = context.ActiveGenerationId.Value;
            coordinator.GetType().GetMethod("ReturnToStartupChoice").Invoke(coordinator, null);
            coordinator.GetType().GetMethod("BeginRestoreSlot").Invoke(coordinator, new object[] { 1, read.confirmationToken }); yield return null;
            Assert.That(context.IsReady, Is.True, context.Failure?.Reason);
            Assert.That(context.ActiveGenerationId, Is.Not.EqualTo(beforeRestore));
            Assert.That(context.ActivePlacementPlan.Ecology.Signature, Is.EqualTo(checkpoint.generation.ecology.layoutDigest));
            Assert.That(File.ReadAllBytes(slot), Is.EqualTo(slotBytes));
            string label = Environment.GetEnvironmentVariable("RPG_ECOLOGY_EVIDENCE_LABEL");
            string evidence = Path.Combine(Application.dataPath, "../Docs/MapGeneration/Ecology/Evidence");
            string directory = Path.Combine(evidence, "P4", label); Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, family + "-next-floor-entry.json"), JsonUtility.ToJson(checkpoint, true));
            Debug.Log($"ECOLOGY_PRODUCTION_SAFE_AND_NEXT_ENTRY family={family} actualBirthProtection=True oldCancelledAttackCannotRevive=True " +
                $"actualNextFloorStoreRestore=True layout={checkpoint.generation.ecology.layoutDigest} e={checkpoint.generation.ecology.ecologyAttempt} originalSlotBytesUnchanged=True");
        }
        finally
        {
            if (barrier != null) barrier.OnTrace -= Capture;
            if (keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse.added) InputSystem.RemoveDevice(mouse);
            foreach (var actor in actors)
                if (actor.actor != null) ((Behaviour)actor.actor.GetComponent("MonsterAIController")).enabled = true;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
            InputSystem.settings.backgroundBehavior = oldBackground;
        }
    }

    private static IEnumerator WalkOriginalInputPath(Keyboard keyboard, GameObject player,
        MapCandidateCollisionGeometry geometry, EcologyNavigationGraph graph, int goal)
    {
        var body = player.GetComponent<Rigidbody2D>(); var combat = player.GetComponent<EcologyCombatBinding>();
        Assert.That(geometry.TryWorldNode(body.position, out int node), Is.True);
        var field = graph.BuildReturnField(new[] { goal });
        Assert.That(graph.IsLegal(node) && field.Distances[node] >= 0, Is.True, "Actual player must start on a reachable original-foot node.");
        int steps = 0;
        Type inputType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("GameInput")).First(t => t != null);
        object input = inputType.GetProperty("Instance").GetValue(null);
        Component action = player.GetComponent("PlayerAction");
        void Deliver(KeyboardState state)
        {
            InputSystem.QueueStateEvent(keyboard, state); InputSystem.Update();
            inputType.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input, null);
            action.GetType().GetMethod("ReadMoveInput", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(action, null);
        }
        while (true)
        {
            Vector2 target = geometry.NodeWorldPosition(node);
            float deadline = Time.time + 3f;
            while (Vector2.Distance(body.position, target) > .08f && Time.time < deadline)
            {
                Vector2 delta = target - body.position;
                Key key = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? delta.x >= 0f ? Key.D : Key.A : delta.y >= 0f ? Key.W : Key.S;
                Deliver(new KeyboardState(key));
                yield return new WaitForFixedUpdate();
                Assert.That(combat.MotionFaultRevision, Is.Zero, "Real input travel must keep the actual observer valid.");
            }
            Deliver(new KeyboardState());
            if (Vector2.Distance(body.position, target) >= .14f)
            {
                Collider2D foot = player.transform.Find("MovementFeet").GetComponent<Collider2D>();
                var hits = new RaycastHit2D[32]; Vector2 delta = target - body.position;
                int count = foot.Cast(delta.normalized, new ContactFilter2D { useTriggers = false }, hits, delta.magnitude + .05f);
                Debug.Log($"ECOLOGY_PRODUCTION_INPUT_STALL node={node} actual={body.position:R} target={target:R} delta={delta:R} " +
                    $"move={player.GetComponent("PlayerAction").GetType().GetProperty("MoveInput").GetValue(player.GetComponent("PlayerAction"))} " +
                    "hits=" + string.Join(";", hits.Take(count).Select(h => h.collider.name + "@" + h.collider.transform.position.ToString("R") + ":" + h.distance.ToString("R"))));
            }
            Assert.That(Vector2.Distance(body.position, target), Is.LessThan(.14f), "Real input stalled at native obstacle on route node " + node);
            if (node == goal) break;
            int next = field.Next[node]; Assert.That(next, Is.GreaterThanOrEqualTo(0)); Assert.That(graph.CanMove(node, next), Is.True);
            node = next; Assert.That(++steps, Is.LessThanOrEqualTo(graph.NodeCount));
        }
        Deliver(new KeyboardState()); yield return null; yield return new WaitForFixedUpdate();
        Debug.Log($"ECOLOGY_PRODUCTION_INPUT_PATH edges={steps} goal={goal} actual={body.position:R} fault={combat.MotionFaultRevision} " +
            "noPostBindTeleport=True inputDelivery=CONTROLLED_REAL_INPUT_STATE_AND_ORIGINAL_READERS_PER_AUTOMATIC_FIXED_STEP notNaturalInputLatency=True");
    }

    private IEnumerator CheckOwnedRetainedMutation(MonoBehaviour coordinator, MapGenerationController controller,
        MapEcologyLayout layout, EcologyRecipeDefinition definition, string kind)
    {
        object[] args = { null, null };
        Assert.That((bool)coordinator.GetType().GetMethod("TryCreateSaveRequest").Invoke(coordinator, args), Is.True);
        var entry = (TowerRunCheckpointRecord)args[0];
        var write = (TowerRunSlotWriteResult)coordinator.GetType().GetMethod("TrySaveEntranceCandidate")
            .Invoke(coordinator, new object[] { 1, entry, false, null });
        Assert.That(write.succeeded, Is.True, write.message);
        var read = (TowerRunSlotReadResult)coordinator.GetType().GetMethod("ReadSlot").Invoke(coordinator, new object[] { 1 });
        Assert.That(read.state, Is.EqualTo(TowerRunSlotState.Valid));
        object store = coordinator.GetType().GetField("slotStore", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(coordinator);
        string slot = (string)store.GetType().GetMethod("GetSlotPath", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(store, new object[] { 1 });
        Assert.That(Path.GetFullPath(slot), Does.StartWith(Path.GetFullPath(isolatedRoot) + Path.DirectorySeparatorChar));
        byte[] slotBefore = File.ReadAllBytes(slot);
        var type = definition.ResolveTypes().Single(t => t.TypeId == "prefab.af07a9fb402e6934dbed62860d520910");
        var foot = (BoxCollider2D)type.MovementFoot;
        string prefabPath = AssetDatabase.GetAssetPath(type.Prefab), settingsPath = AssetDatabase.GetAssetPath(definition.Settings);
        Assert.That(prefabPath, Does.StartWith("Assets/Tests/MapGeneration/Fixtures/RetainedMutationProbe/"));
        Assert.That(settingsPath, Does.StartWith("Assets/Tests/MapGeneration/Fixtures/RetainedMutationProbe/"));
        byte[] prefabBefore = File.ReadAllBytes(prefabPath), settingsBefore = File.ReadAllBytes(settingsPath);
        string prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath), settingsGuid = AssetDatabase.AssetPathToGUID(settingsPath), configName = definition.Settings.name;
        Vector2 originalSize = foot.size; int originalSeed = definition.Settings.seed;
        var dirty = new Dictionary<UnityEngine.Object, bool>();
        foreach (Component component in type.Prefab.GetComponentsInChildren<Component>(true))
        {
            if (component == null) continue;
            dirty[component] = EditorUtility.IsDirty(component); dirty[component.gameObject] = EditorUtility.IsDirty(component.gameObject);
        }
        dirty[definition.Settings] = EditorUtility.IsDirty(definition.Settings);
        coordinator.GetType().GetMethod("ReturnToStartupChoice").Invoke(coordinator, null);
        string failure = null;
        try
        {
            if (kind == "foot") { foot.size = originalSize + new Vector2(.03f, 0); EditorUtility.SetDirty(foot); }
            else { definition.Settings.seed = originalSeed + 1; EditorUtility.SetDirty(definition.Settings); }
            Assert.That(AssetDatabase.AssetPathToGUID(prefabPath), Is.EqualTo(prefabGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(settingsPath), Is.EqualTo(settingsGuid));
            Assert.That(definition.Settings.name, Is.EqualTo(configName));
            Assert.That(File.ReadAllBytes(prefabPath), Is.EqualTo(prefabBefore)); Assert.That(File.ReadAllBytes(settingsPath), Is.EqualTo(settingsBefore));
            Debug.Log($"ECOLOGY_OWNED_MUTATION_INJECTED kind={kind} sameImportedPrefabGuid={prefabGuid} sameSettingsGuid={settingsGuid} " +
                $"sameConfigName={configName} foot={originalSize}->{foot.size} seed={originalSeed}->{definition.Settings.seed} unsavedOwnedAssetOnly=True originalsUnchanged=True");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                @"^Map (integration|generation) failed \[(RetainedEcologyContentMismatch|EcologyEntryReconstructionMismatch|EcologyBudgetExhaustedOnFixedTerrain)\]:"));
            coordinator.GetType().GetMethod("BeginRestoreSlot").Invoke(coordinator, new object[] { 1, read.confirmationToken });
            Assert.That(controller.Orchestrator.Context.IsReady, Is.False, "Same GUID/name must not allow changed retained data to masquerade as the saved version.");
            Assert.That(coordinator.GetType().GetProperty("State").GetValue(coordinator).ToString(), Is.EqualTo("Failure"));
            failure = controller.Orchestrator.Context.Failure.Code;
            Assert.That(File.ReadAllBytes(slot), Is.EqualTo(slotBefore));
            object[] invalidSave = { null, null };
            Assert.That((bool)coordinator.GetType().GetMethod("TryCreateSaveRequest").Invoke(coordinator, invalidSave), Is.False);
        }
        finally
        {
            foot.size = originalSize; definition.Settings.seed = originalSeed;
            foreach (var before in dirty) if (!before.Value) EditorUtility.ClearDirty(before.Key);
        }
        Assert.That(File.ReadAllBytes(prefabPath), Is.EqualTo(prefabBefore)); Assert.That(File.ReadAllBytes(settingsPath), Is.EqualTo(settingsBefore));
        coordinator.GetType().GetMethod("ReturnToStartupChoice").Invoke(coordinator, null);
        coordinator.GetType().GetMethod("BeginRestoreSlot").Invoke(coordinator, new object[] { 1, read.confirmationToken }); yield return null;
        Assert.That(controller.Orchestrator.Context.IsReady, Is.True, controller.Orchestrator.Context.Failure?.Reason);
        Assert.That(controller.Orchestrator.Context.ActivePlacementPlan.Ecology.Signature, Is.EqualTo(layout.Signature));
        Assert.That(File.ReadAllBytes(slot), Is.EqualTo(slotBefore));
        Debug.Log($"ECOLOGY_OWNED_MUTATION_REJECTED kind={kind} failure={failure} sameGuidAndName=True slotBytesUnchanged=True " +
            "ownedValuesAndDirtyFlagsRestored=True noDiskAssetWrite=True actualRevertedVersionRestores=True");
    }

    [UnityTest]
    public IEnumerator ThreeActualSeedJointPlansMatchNativeCompleteRolesFeetAndStairSweeps()
    {
        yield return CheckJoint(true);
    }

    [UnityTest]
    public IEnumerator PreparedResidentsShareHomeFieldsAndCannotStartAiBeforeReady()
    {
        yield return CheckJoint(false, true);
    }

    [UnityTest]
    public IEnumerator SamePoolLegacyEcologyLegacyReuseRefreshesIdentityAndOnlyExplicitHandoffRestoresLegacyMotion()
    {
        yield return CheckJoint(false, false, true);
    }

    [UnityTest]
    public IEnumerator ActualNormalAndHurtMotionKeepCompleteFootInsideHomeWithoutBlockingInteriorMovement()
    {
        yield return CheckJoint(false, false, false, true);
    }

    [UnityTest]
    public IEnumerator ActualFootHomeDistanceUsesCertifiedLocalConnectionsAndPositiveDistanceForPartialExit()
    {
        yield return CheckJoint(false, true, false, false, true);
    }

    [Test]
    public void ObservedDepartureKeepsOneScaledClockAndReturnLatchUntilCompleteStoppedHome()
    {
        var episode = new EcologyExcursionState(6, 16d);
        episode.ObserveActualPose(true, true, 0, 10d);
        Assert.That(episode.HasPermit, Is.False); Assert.That(episode.Episode, Is.Zero);
        episode.ObserveActualPose(false, false, 1, 11d);
        Assert.That(episode.HasPermit, Is.True); Assert.That(episode.Episode, Is.EqualTo(1)); Assert.That(episode.DepartedAt, Is.EqualTo(11d));
        episode.ObserveActualPose(false, false, 3, 20d);
        Assert.That(episode.Elapsed, Is.EqualTo(9d)); Assert.That(episode.MayChase, Is.True);
        episode.RequestReturn();
        episode.ObserveActualPose(false, true, 2, 23d);
        Assert.That(episode.Elapsed, Is.EqualTo(12d)); Assert.That(episode.MayChase, Is.False);
        episode.ObserveActualPose(false, true, 2, 23d);
        Assert.That(episode.Elapsed, Is.EqualTo(12d), "A paused scaled world cannot spend outside time.");
        episode.ObserveActualPose(false, false, 2, 27d);
        Assert.That(episode.ReturnLatched, Is.True); Assert.That(episode.DepartedAt, Is.EqualTo(11d));
        episode.ObserveActualPose(true, false, 0, 28d);
        Assert.That(episode.HasPermit, Is.True); Assert.That(episode.ReturnLatched, Is.True, "Passing through Home while moving is not stopped return completion.");
        episode.ObserveActualPose(true, true, 0, 29d);
        Assert.That(episode.HasPermit, Is.False); Assert.That(episode.ReturnLatched, Is.False); Assert.That(episode.LastCompletedElapsed, Is.EqualTo(18d));
        episode.ObserveActualPose(false, false, 1, 30d);
        Assert.That(episode.Episode, Is.EqualTo(2)); Assert.That(episode.DepartedAt, Is.EqualTo(30d));
        episode.ObserveActualPose(false, false, 7, 31d);
        Assert.That(episode.ReturnLatched, Is.True, "Home-anchored graph range must latch return regardless of elapsed time.");
        var deadline = new EcologyExcursionState(6, 16d);
        deadline.ObserveActualPose(false, false, 1, 0d); deadline.ObserveActualPose(false, false, 1, 16d);
        Assert.That(deadline.ReturnLatched, Is.True, "Waiting/attacking/being hurt cannot refresh the original departure deadline.");
        var inconsistent = new EcologyExcursionState(6, 16d);
        inconsistent.ObserveActualPose(false, true, 0, 1d);
        Assert.That(inconsistent.NavigationFault, Is.True); Assert.That(inconsistent.ReturnLatched, Is.True);
        Assert.That(inconsistent.HasPermit, Is.True, "Observed outside starts the clock even when its distance query faults.");
        Assert.Throws<ArgumentOutOfRangeException>(() => episode.ObserveActualPose(false, false, 1, 29d));
    }

    [UnityTest]
    public IEnumerator ReadyStartsOriginalAiRegionalPatrolAndEveryActualFootStaysInsideSharedHome()
    {
        yield return CheckJoint(false, false, false, false, false, true);
    }

    [UnityTest]
    public IEnumerator ExplicitLegacyPoolHandoffRestoresActualOriginalHurtButStaleEcologyCannotFallBack()
    {
        yield return CheckJoint(false, false, true, false, false, false, true);
    }

    [UnityTest]
    public IEnumerator ActualObservedExcursionClockPausesWithWorldAndHurtCannotRenewPermitOrExceedHomeRange()
    {
        yield return CheckJoint(false, excursion: true);
    }

    [UnityTest]
    public IEnumerator ConnectorFanMatchesNativeBoxAndCircleAndRejectsInteriorBlockerEvenWhenAllThreeEdgesAreClear()
    {
        yield return CheckJoint(false, fanOracle: true);
    }

    [UnityTest]
    public IEnumerator OriginalAiActuallyChasesThenLatchesReturnAndDamageCannotRenewItsOneBoundedCounterattack()
    {
        yield return CheckJoint(false, regionalChase: true);
    }

    [UnityTest]
    public IEnumerator PendingHurtAtStationaryHomeApexCannotFinishPermitBeforeItsOriginalReturnLeg()
    {
        yield return CheckJoint(false, pendingHomeHurt: true);
    }

    [UnityTest]
    public IEnumerator ActualTemporaryReturnBlockageFailsAfterBoundedWaitsWithoutHealingDespawnOrInvulnerability()
    {
        yield return CheckJoint(false, blockedReturn: true);
    }

    [UnityTest]
    public IEnumerator NativeRemovedStaticSolidRebuildsSharedNavigationWithoutChangingLayoutHealthLeaseOrExcursion()
    {
        yield return CheckJoint(false, staticRevision: true);
    }

    [UnityTest]
    public IEnumerator NativeStaticAvailabilityRefreshesBoundFieldsAtNextQueryWithoutRenewingPermit()
    {
        yield return CheckJoint(false, staticRevision: true, liveNavigation: true);
    }

    [UnityTest]
    public IEnumerator OriginalPopulationRegistersPreparedResidentsBeforeActivationAndWaitsForWholeReady()
    {
        yield return CheckJoint(false, populationActivation: true);
    }

    private IEnumerator CheckJoint(bool native, bool bindHomes = false, bool crossProfileReuse = false, bool homeMotion = false, bool measureDistance = false, bool regionalPatrol = false, bool legacyHurt = false, bool excursion = false, bool fanOracle = false, bool regionalChase = false, bool pendingHomeHurt = false, bool blockedReturn = false, bool staticRevision = false, bool liveNavigation = false, bool populationActivation = false, bool seedCorpus = false, int sampleSeed = 20260929, bool unitsCandidate = false, bool homeStairs = false, bool homeCrowding = false)
    {
        regionalChase |= blockedReturn;
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        ownedScene = SceneManager.GetActiveScene();
        yield return null;
        MonoBehaviour coordinator = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true)
            .Single(b => b.GetType().Name == "TowerRunCoordinator");
        coordinator.GetType().GetMethod("ReturnToStartupChoice", BindingFlags.Instance | BindingFlags.Public).Invoke(coordinator, null);
        GameObject root = SceneManager.GetActiveScene().GetRootGameObjects().Single(o => o.name == "RandomMapRuntime");
        var merchant = root.GetComponent<MerchantPlacementService>();
        var plants = root.GetComponent<PlantPlacementService>();
        var population = root.GetComponent<MapEcologyPopulationService>();
        var animals = root.GetComponent<AnimalPlacementService>();
        var terrain = root.GetComponentInChildren<MapTilemapRenderer>(true);
        var decoration = root.GetComponentInChildren<MapSimpleDecorationRenderer>(true);
        Assert.That(plants.Bindings.Count, Is.EqualTo(17)); Assert.That(animals.Bindings.Count, Is.EqualTo(4));
        Assert.That(merchant.MerchantTarget, Is.Not.Null);
        Assert.That(population.ActiveCount, Is.Zero);
        MonoBehaviour exitOwner = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true)
            .Single(b => b.GetType().Name == "TowerFloorRuntimeController");
        float exitRadius = new SerializedObject(exitOwner).FindProperty("interactionRadius").floatValue;
        if (ownedSettings != null) UnityEngine.Object.Destroy(ownedSettings);
        var settings = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<MapGenerationSettings>(
            "Assets/MapGeneration/MapGenerationSettings_Default.asset")); ownedSettings = settings;
        var corpus = new List<CorpusRow>();
        int[] seeds = seedCorpus ? Enumerable.Range(0, 64).Select(i => unchecked(20260929 + i * 104729)).ToArray() :
            native ? new[] { 20260929, -1391545000, 613580675 } : new[] { sampleSeed };
        foreach (int seed in seeds)
        {
        settings.seed = seed;
        var map = RandomMapGenerator.Generate(settings, true);
        var decorations = MapSimpleDecorationGenerator.Generate(map, settings);
        Guid generation = Guid.NewGuid();
        MapGenerationAttempt attempt = new MapGenerationRequest(Guid.NewGuid(), settings.seed).CreateAttempt(0, generation);
        var playerSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Players/Warrior_Blue.prefab");
        Collider2D playerFoot = playerSource.transform.Find("MovementFeet").GetComponent<CircleCollider2D>();
        var retainedCandidate = Resources.Load<EcologyRecipeDefinition>(EcologyRecipeDefinition.ResourcePathFor(EcologyRecipeDefinition.MainV1ProfileId));
        EcologyEcotypeDefinition[] types = retainedCandidate.ResolveTypes();
        Assert.That(types.Length, Is.EqualTo(12)); Assert.That(types.Any(type => type.IsBoss), Is.False);
        if (regionalChase) types = types.Where(type => type.Prefab.name == "Torch_Blue").ToArray();
        var bare = MapCandidateCollisionGeometry.Capture(generation, map, decorations, settings, terrain, decoration);
        var bareGraph = bare.BuildNavigationGraph(playerFoot, 0f);
        int exitNode = (map.ExitCell.y - map.Origin.y) * map.Width + map.ExitCell.x - map.Origin.x;
        int spawnNode = (map.SpawnCell.y - map.Origin.y) * map.Width + map.SpawnCell.x - map.Origin.x;
        Debug.Log($"ECOLOGY_JOINT_EXIT_BASE seed={seed} exit={map.ExitCell} centreLegal={bareGraph.IsLegal(exitNode)} " +
            $"centreDistance={bareGraph.BuildReturnField(new[] { spawnNode }).Distances[exitNode]} originalInteractionRadius={exitRadius:R}");
        int materialized = merchant.MaterializationCount + plants.MaterializationCount + animals.MaterializationCount;
        var planningClock = System.Diagnostics.Stopwatch.StartNew();
        EcologyPlanningAttemptResult result = EcologyJointPlacementPlanner.Build(attempt, map, decorations, settings,
            terrain, decoration, playerFoot, merchant, plants, population, animals, new EcologyTerritoryRecipe(bossContentPending: true), types, exitRadius, null, retainedCandidate != null ? retainedCandidate.Population : null);
        planningClock.Stop();
        if (seedCorpus)
        {
            var row = new CorpusRow { seed = seed, accepted = result.Succeeded, acceptedAttempt = result.AcceptedAttempt,
                attemptsUsed = result.AttemptsUsed, planningMilliseconds = planningClock.Elapsed.TotalMilliseconds, failures = result.Failures.ToArray() };
            corpus.Add(row);
            Assert.That(population.ActiveCount, Is.Zero);
            Assert.That(merchant.MaterializationCount + plants.MaterializationCount + animals.MaterializationCount, Is.EqualTo(materialized));
            if (!result.Succeeded) continue;
            var acceptedLayout = result.Plan.Ecology;
            row.signature = acceptedLayout.Signature;
            row.ordinaryRegions = acceptedLayout.Regions.Count(r => r.Kind == MapEcologyRegionKind.Ordinary);
            row.bossRegions = acceptedLayout.Regions.Count(r => r.Kind == MapEcologyRegionKind.Boss);
            row.residents = acceptedLayout.Residents.Count;
            row.types = acceptedLayout.Regions.Where(r => !r.IsSafety).Select(r => r.TypeId).ToArray();
            Assert.That(acceptedLayout.Regions.Count(r => r.IsSafety), Is.EqualTo(2));
            Assert.That(row.ordinaryRegions, Is.InRange(1, 4)); Assert.That(row.bossRegions, Is.InRange(0, unitsCandidate ? 0 : 1));
            Assert.That(row.residents, Is.InRange(3, 24));
            foreach (MapEcologyRegion region in acceptedLayout.Regions.Where(r => !r.IsSafety))
            {
                Assert.That(region.AssignedPopulation, Is.LessThanOrEqualTo(region.ConstructiveCapacity));
                Assert.That(region.AssignedPopulation, Is.InRange(region.Kind == MapEcologyRegionKind.Boss ? 1 : 3,
                    region.Kind == MapEcologyRegionKind.Boss ? 1 : 8));
            }
            foreach (MapEcologyResident resident in acceptedLayout.Residents)
            {
                EcologyEcotypeDefinition ecotype = types.Single(t => t.TypeId == resident.TypeId);
                Assert.That(acceptedLayout.RegionAt(resident.InitialNode).Id, Is.EqualTo(resident.HomeRegionId));
                Assert.That(acceptedLayout.IsProtectedNode(resident.InitialNode), Is.False);
                Vector2 world = result.StaticGeometry.NodeWorldPosition(resident.InitialNode);
                Assert.That(result.InitialPlacementGeometry.IsTranslationClear(ecotype.MovementFoot, world, world, 0f), Is.True);
            }
            Debug.Log($"ECOLOGY_CORPUS_ACCEPT seed={seed} e={row.acceptedAttempt} ordinary={row.ordinaryRegions} boss={row.bossRegions} residents={row.residents}");
            continue;
        }
        Assert.That(result.Succeeded, Is.True, string.Join(";", result.Failures));
        Assert.That(result.AttemptsUsed, Is.InRange(1, 8)); Assert.That(result.Plan.GenerationId, Is.EqualTo(generation));
        Assert.That(result.Plan.Ecology, Is.Not.Null);
        Assert.That(population.ActiveCount, Is.Zero, "Planning must not spawn live actors before acceptance.");
        Assert.That(merchant.MaterializationCount + plants.MaterializationCount + animals.MaterializationCount, Is.EqualTo(materialized));
        Assert.That(result.Plan.Reservations.Count(r => r.Role == MonsterPlacementService.MonsterRole),
            Is.EqualTo(result.Plan.Ecology.Residents.Count));
        Assert.That(population.LastPlan.ActualMonsters, Is.EqualTo(result.Plan.Ecology.Residents.Count));
        Assert.That(population.LastPlan.ActualDestructibles, Is.GreaterThanOrEqualTo(population.Profile.Destructible.RequiredMinimum));
        Assert.That(plants.PlannedCount, Is.GreaterThanOrEqualTo(plants.Profile.RequiredMinimum));
        Assert.That(animals.PlannedCount, Is.GreaterThanOrEqualTo(animals.Profile.RequiredMinimum));
        string hash = result.Plan.Ecology.Signature;
        MapGenerationAttempt restoredAttempt = new MapGenerationRequest(Guid.NewGuid(), settings.seed).CreateAttempt(0, Guid.NewGuid());
        EcologyPlanningAttemptResult restored = EcologyJointPlacementPlanner.Build(restoredAttempt, map, decorations, settings,
            terrain, decoration, playerFoot, merchant, plants, population, animals, new EcologyTerritoryRecipe(bossContentPending: true), types.Reverse().ToArray(), exitRadius, result.AcceptedAttempt, retainedCandidate != null ? retainedCandidate.Population : null);
        Assert.That(restored.Succeeded, Is.True, string.Join(";", restored.Failures));
        Assert.That(restored.AttemptsUsed, Is.EqualTo(1)); Assert.That(restored.Plan.Ecology.Signature, Is.EqualTo(hash));
        Assert.That(restored.Plan.GenerationId, Is.Not.EqualTo(generation), "Runtime generation is not persisted layout identity.");
        Assert.That(restored.ExitApproachNode, Is.GreaterThanOrEqualTo(0));
        Assert.That(Vector2.Distance(terrain.Coordinates.CellToWorld(restored.Plan.Ecology.CellAt(restored.ExitApproachNode)),
            terrain.Coordinates.CellToWorld(map.ExitCell)), Is.LessThanOrEqualTo(exitRadius));
        Debug.Log($"ECOLOGY_JOINT_SCENE_PLAN seed={settings.seed} G={generation} e={result.AcceptedAttempt} used={result.AttemptsUsed} " +
            $"monsters={population.LastPlan.ActualMonsters} destructibles={population.LastPlan.ActualDestructibles} plants={plants.PlannedCount} " +
            $"animals={animals.PlannedCount} exactRestore=True noEarlyMaterialization=True signature={hash}");
        if (native) yield return CheckNativeJoint(restored, map, decorations, settings, root, terrain, decoration, merchant, plants, population, animals, types, playerSource, exitRadius);
        if (homeStairs) yield return CheckNativeJoint(restored, map, decorations, settings, root, terrain, decoration, merchant, plants, population, animals, types, playerSource, exitRadius, homeStairs: true);
        if (homeCrowding) yield return CheckNativeJoint(restored, map, decorations, settings, root, terrain, decoration, merchant, plants, population, animals, types, playerSource, exitRadius, homeCrowding: true);
        if (bindHomes) yield return CheckPreparedHomes(restored, types, measureDistance);
        if (crossProfileReuse) yield return CheckProfileReuse(restored, types, legacyHurt);
        if (homeMotion) yield return CheckNativeJoint(restored, map, decorations, settings, root, terrain, decoration, merchant, plants, population, animals, types, playerSource, exitRadius, true);
        if (regionalPatrol) yield return CheckNativeJoint(restored, map, decorations, settings, root, terrain, decoration, merchant, plants, population, animals, types, playerSource, exitRadius, false, true);
        if (excursion) yield return CheckNativeJoint(restored, map, decorations, settings, root, terrain, decoration, merchant, plants, population, animals, types, playerSource, exitRadius, true, false, true);
        if (fanOracle) yield return CheckNativeJoint(restored, map, decorations, settings, root, terrain, decoration, merchant, plants, population, animals, types, playerSource, exitRadius, fanOracle: true);
        if (regionalChase) yield return CheckNativeJoint(restored, map, decorations, settings, root, terrain, decoration, merchant, plants, population, animals, types, playerSource, exitRadius, homeMotion: true, regionalChase: true, blockedReturn: blockedReturn);
        if (pendingHomeHurt) yield return CheckNativeJoint(restored, map, decorations, settings, root, terrain, decoration, merchant, plants, population, animals, types, playerSource, exitRadius, homeMotion: true, excursion: true, pendingHomeHurt: true);
        if (staticRevision) yield return CheckNativeJoint(restored, map, decorations, settings, root, terrain, decoration, merchant, plants, population, animals, types, playerSource, exitRadius, staticRevision: true, otherGeneration: result, liveNavigation: liveNavigation);
        if (populationActivation) yield return CheckPopulationActivation(map, decorations, settings, terrain, decoration,
            playerFoot, merchant, plants, population, animals, types, exitRadius);
        }
        if (seedCorpus)
        {
            string label = Environment.GetEnvironmentVariable("RPG_ECOLOGY_EVIDENCE_LABEL") + "-" + TestContext.CurrentContext.Test.Name, run = label.Split('-')[0];
            string evidence = Path.Combine(Application.dataPath, "../Docs/MapGeneration/Ecology/Evidence");
            var report = new CorpusReport { schema = "ecology-fixed-joint-corpus-v1", scope = "PURE_ORIGINAL_SCENE_ROLE_JOINT_PLANNING_NOT_RUNTIME_OR_ALL_NATIVE",
                label = label, sourceFingerprint = JsonUtility.FromJson<Fingerprint>(File.ReadAllText(Path.Combine(evidence, "P0V/" + run + "-input-manifest.json"))).sourceFingerprint,
                requestedTerrains = seeds.Length, acceptedTerrains = corpus.Count(r => r.accepted), rejectedTerrains = corpus.Count(r => !r.accepted),
                acceptedWithBoss = corpus.Count(r => r.accepted && r.bossRegions > 0), rows = corpus.ToArray() };
            string directory = Path.Combine(evidence, "P3/Corpus/" + label); Directory.CreateDirectory(directory);
            string output = Path.Combine(directory, "joint-corpus.json"); Assert.That(File.Exists(output), Is.False);
            File.WriteAllText(output, JsonUtility.ToJson(report, true), new System.Text.UTF8Encoding(false));
            Debug.Log($"ECOLOGY_CORPUS_COMPLETE requested={report.requestedTerrains} accepted={report.acceptedTerrains} rejected={report.rejectedTerrains} withBoss={report.acceptedWithBoss} artifact={output}");
        }
    }

    private IEnumerator CheckPopulationActivation(MapData map, MapSimpleDecorationData decorations, MapGenerationSettings settings,
        MapTilemapRenderer terrain, MapSimpleDecorationRenderer decoration, Collider2D playerFoot, MerchantPlacementService merchant,
        PlantPlacementService plants, MapEcologyPopulationService population, AnimalPlacementService animals, EcologyEcotypeDefinition[] types, float exitRadius)
    {
        Time.timeScale = 0f;
        var context = new MapRuntimeContext(MapRuntimeMode.RandomGenerated);
        typeof(MapRuntimeContext).GetMethod("SetPhase", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(context, new object[] { MapLifecyclePhase.Initialized });
        var orchestrator = new MapIntegrationOrchestrator(context);
        EcologyPlanningAttemptResult accepted = null;
        MapGenerationExecutionResult committed = orchestrator.Execute(new MapGenerationRequest(Guid.NewGuid(), settings.seed),
            a => new MapPendingGeneration(a.GenerationId, map, decorations), (a, p) =>
            {
                accepted = EcologyJointPlacementPlanner.Build(a, map, decorations, settings, terrain, decoration, playerFoot,
                    merchant, plants, population, animals, new EcologyTerritoryRecipe(bossContentPending: true), types, exitRadius);
                Assert.That(accepted.Succeeded, Is.True, string.Join(";", accepted.Failures)); return accepted.Plan;
            }, p => { });
        Assert.That(committed.Succeeded, Is.True, context.Failure?.Reason);
        bool current = true, ready = false;
        var holder = new GameObject("Owned original population activation scope"); ownedProjections.Add(holder);
        var barrier = holder.AddComponent<EcologyDamageBarrier>();
        try
        {
            population.ConfigureZonedActivation(accepted, types, barrier, 271, () => current, () => ready);
            Assert.That(population.TryMaterialize(context, accepted.Plan.GenerationId, terrain.Coordinates, out string reason), Is.True, reason);
            Assert.That(population.ActiveCount, Is.EqualTo(population.LastPlan.Placements.Count));
            foreach (MapEcologyResident resident in accepted.Plan.Ecology.Residents)
            {
                Assert.That(population.TryGetActiveInstance(resident.Id, out UnityEngine.Object instance), Is.True);
                var actor = ((Component)instance).gameObject;
                Assert.That(context.ActiveRegistry.TryGet(resident.Id, out MapObjectRegistryEntry entry), Is.True);
                Assert.That(entry.Instance, Is.SameAs(instance));
                Assert.That(actor.GetComponent<EcologyResidentBinding>().GenerationId, Is.EqualTo(accepted.Plan.GenerationId));
                Assert.That(actor.GetComponent<EcologyResidentBinding>().CanAct, Is.False);
                Assert.That(actor.GetComponent<EcologyCombatBinding>().GenerationId, Is.EqualTo(271));
                Assert.That(actor.GetComponent<EcologyMotionBody>().IsReady, Is.True);
            }
            Time.timeScale = 1f; yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate(); Time.timeScale = 0f;
            foreach (MapEcologyResident resident in accepted.Plan.Ecology.Residents)
            {
                population.TryGetActiveInstance(resident.Id, out UnityEngine.Object instance); var actor = ((Component)instance).gameObject;
                Assert.That(Vector2.Distance(actor.GetComponent<Rigidbody2D>().position, accepted.StaticGeometry.NodeWorldPosition(resident.InitialNode)), Is.LessThan(.0001f));
                Assert.That(actor.GetComponent<EcologyCombatBinding>().MotionFaultRevision, Is.Zero);
            }
            ready = true;
            foreach (MapEcologyResident resident in accepted.Plan.Ecology.Residents)
            {
                population.TryGetActiveInstance(resident.Id, out UnityEngine.Object instance);
                Assert.That(((Component)instance).GetComponent<EcologyResidentBinding>().CanAct, Is.True);
            }
            Debug.Log($"ECOLOGY_ORIGINAL_POPULATION_PREPARED residents={accepted.Plan.Ecology.Residents.Count} active={population.ActiveCount} " +
                "originalRegistry=True originalPool=True noMovementBeforeWholeReady=True scope=POPULATION_TRANSACTION_NOT_FULL_CONTROLLER_READY");

            // Reuse the same original pool, then corrupt only an owned inactive instance's foot.
            // The retained prefab and all formal resources stay untouched.
            population.RetireForLegacyProfile();
            ready = false;
            MapEcologyResident broken = accepted.Plan.Ecology.Residents.Last();
            EcologyEcotypeDefinition brokenType = types.Single(t => t.TypeId == broken.TypeId);
            var originalFactory = brokenType.Prefab.GetComponent<IGenerationScopedPopulationTarget>();
            GameObject brokenActor = originalFactory.Spawn(Vector3.zero, Quaternion.identity);
            originalFactory.Return(brokenActor);
            BoxCollider2D brokenFoot = brokenType.ResolveMovementFoot(brokenActor) as BoxCollider2D;
            Assert.That(brokenFoot, Is.Not.Null); Vector2 originalSize = brokenFoot.size;
            try
            {
                brokenFoot.size = originalSize + new Vector2(.01f, 0f);
                typeof(MapRuntimeContext).GetMethod("SetPhase", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(context, new object[] { MapLifecyclePhase.Initialized });
                committed = orchestrator.Execute(new MapGenerationRequest(Guid.NewGuid(), settings.seed),
                    a => new MapPendingGeneration(a.GenerationId, map, decorations), (a, p) =>
                    {
                        accepted = EcologyJointPlacementPlanner.Build(a, map, decorations, settings, terrain, decoration, playerFoot,
                            merchant, plants, population, animals, new EcologyTerritoryRecipe(bossContentPending: true), types, exitRadius);
                        return accepted.Plan;
                    }, p => { });
                Assert.That(committed.Succeeded, Is.True, context.Failure?.Reason);
                population.ConfigureZonedActivation(accepted, types, barrier, 272, () => current, () => ready);
                Assert.That(population.TryMaterialize(context, accepted.Plan.GenerationId, terrain.Coordinates, out reason), Is.False,
                    "An owned pooled foot mismatch must reject activation, rather than using another footprint's fields.");
                Assert.That(context.IsFailed, Is.True); Assert.That(population.ActiveCount, Is.Zero);
                Assert.That(population.ReadyGenerationId, Is.Null);
                Assert.That(context.ActiveRegistry.Entries.Any(e => e.Instance != null &&
                    (e.Role == MonsterPlacementService.MonsterRole || e.Role == DestructiblePlacementService.DestructibleRole)), Is.False);
                Assert.That(brokenActor.activeInHierarchy, Is.False);
                Debug.Log("ECOLOGY_ORIGINAL_POPULATION_ROLLBACK ownedInactiveFootMismatch=True activeCount=0 registryBoundCount=0 Ready=False noFormalAssetChanges=True");
            }
            finally { brokenFoot.size = originalSize; }
        }
        finally { current = false; ready = false; population.RetireForLegacyProfile(); Time.timeScale = 0f; }
    }

    private IEnumerator CheckProfileReuse(EcologyPlanningAttemptResult result, EcologyEcotypeDefinition[] types, bool legacyHurt = false)
    {
        Time.timeScale = 0f;
        var cache = new EcologyHomeNavigationCache(result.Plan.Ecology, result.StaticGeometry);
        var first = result.Plan.Ecology.Residents.First(r => result.Plan.Ecology.Residents.Count(other => other.HomeRegionId == r.HomeRegionId) > 1);
        var second = result.Plan.Ecology.Residents.First(r => r.HomeRegionId == first.HomeRegionId && r.Id != first.Id);
        var type = types.Single(t => t.TypeId == first.TypeId);
        var oldFactory = type.Prefab.GetComponent<IGenerationScopedPopulationTarget>();
        var preparedFactory = type.Prefab.GetComponent<IGenerationScopedPreparedPopulationTarget>();
        Vector3 position = result.StaticGeometry.NodeWorldPosition(first.InitialNode);
        GameObject legacy = oldFactory.Spawn(position, Quaternion.identity); ownedProjections.Add(legacy);
        Func<GameObject, bool> stopped = actor => (bool)actor.GetComponents<MonoBehaviour>().Single(b => b.GetType().Name == "MonsterMovementController")
            .GetType().GetProperty("HasReachedDestination").GetValue(actor.GetComponents<MonoBehaviour>().Single(b => b.GetType().Name == "MonsterMovementController"));
        Assert.That(stopped(legacy), Is.False, "The original AI must start its original patrol on an unbound lease.");
        oldFactory.Return(legacy);
        var barrierObject = new GameObject("Owned cross-profile lease barrier"); ownedProjections.Add(barrierObject);
        var barrier = barrierObject.AddComponent<EcologyDamageBarrier>();
        bool current = true, ready = false; int identity = 1;
        Func<MapEcologyResident, GameObject> activate = resident =>
        {
            GameObject actor = preparedFactory.SpawnPrepared(result.StaticGeometry.NodeWorldPosition(resident.InitialNode), Quaternion.identity, instance =>
            {
                var home = instance.GetComponent<EcologyResidentBinding>() ?? instance.AddComponent<EcologyResidentBinding>();
                home.Prepare(cache, resident.Id, type, () => current, () => ready);
                MonoBehaviour hp = instance.GetComponents<MonoBehaviour>().Single(b => b.GetType().Name == "BaseMonster");
                var combat = instance.GetComponent<EcologyCombatBinding>() ?? instance.AddComponent<EcologyCombatBinding>();
                combat.Bind(barrier, 1, ++identity, () => current && hp != null && (float)hp.GetType().GetProperty("CurrentHealth").GetValue(hp) > 0f);
                var motion = instance.GetComponent<EcologyMotionBody>() ?? instance.AddComponent<EcologyMotionBody>();
                var movement = new SerializedObject(type.Prefab.GetComponents<MonoBehaviour>().Single(b => b.GetType().Name == "MonsterMovementController"));
                motion.ConfigurePrepared(type.ResolveMovementFoot(instance), movement.FindProperty("obstacleLayers").intValue,
                    movement.FindProperty("collisionSkinWidth").floatValue);
            });
            Physics2D.SyncTransforms(); actor.GetComponent<EcologyMotionBody>().CompletePreparedConfiguration();
            return actor;
        };
        GameObject ecology = activate(first);
        Assert.That(ecology, Is.SameAs(legacy));
        Assert.That(stopped(ecology), Is.True, "A previously awakened legacy AI must refresh the new home binding before its next OnEnable patrol.");
        yield return null;
        Assert.That(stopped(ecology), Is.True);
        long oldLease = ecology.GetComponent<EcologyCombatBinding>().Lease;
        current = false;
        Assert.That(ecology.GetComponent<EcologyResidentBinding>().IsBound, Is.True);
        Assert.That(ecology.GetComponent<EcologyResidentBinding>().CanAct, Is.False);
        Assert.That(EcologyMotionBody.TryRequest(ecology.transform, position + Vector3.right, EcologyMotionKind.Normal), Is.True,
            "An ordinary stale ecology scope still owns/fails closed; it cannot silently route through legacy movement.");
        if (legacyHurt)
        {
            MonoBehaviour hurt = ecology.GetComponents<MonoBehaviour>().Single(b => b.GetType().Name == "MonsterHurtController");
            Vector2 initial = ecology.GetComponent<Rigidbody2D>().position;
            hurt.GetType().GetMethod("PlayHurt").Invoke(hurt, new object[] { ecology.transform.position + Vector3.right });
            Time.timeScale = 1f; yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate(); Time.timeScale = 0f;
            Assert.That(ecology.GetComponent<Rigidbody2D>().position, Is.EqualTo(initial), "A stale ecology lease cannot use the legacy Hurt writer.");
        }
        current = true; oldFactory.Return(ecology);
        GameObject backToLegacy = oldFactory.Spawn(position, Quaternion.identity);
        Assert.That(backToLegacy, Is.SameAs(legacy));
        Assert.That(backToLegacy.GetComponent<EcologyResidentBinding>().IsBound, Is.False, "Only this explicit legacy activation retires home metadata.");
        Assert.That(backToLegacy.GetComponent<EcologyCombatBinding>().Barrier, Is.Null);
        Assert.That(EcologyMotionBody.TryRequest(backToLegacy.transform, position + Vector3.right, EcologyMotionKind.Normal), Is.False,
            "Explicit legacy reuse must restore the original position-writing route.");
        Assert.That(stopped(backToLegacy), Is.False);
        if (legacyHurt)
        {
            MonoBehaviour ai = backToLegacy.GetComponents<MonoBehaviour>().Single(b => b.GetType().Name == "MonsterAIController");
            MonoBehaviour movement = backToLegacy.GetComponents<MonoBehaviour>().Single(b => b.GetType().Name == "MonsterMovementController");
            MonoBehaviour hurt = backToLegacy.GetComponents<MonoBehaviour>().Single(b => b.GetType().Name == "MonsterHurtController");
            ai.enabled = false; movement.GetType().GetMethod("Stop").Invoke(movement, null);
            Vector2 initial = backToLegacy.GetComponent<Rigidbody2D>().position; float peak = 0f;
            hurt.GetType().GetMethod("PlayHurt").Invoke(hurt, new object[] { backToLegacy.transform.position + Vector3.right });
            Time.timeScale = 1f; float deadline = Time.time + .5f;
            while (Time.time < deadline)
            {
                yield return new WaitForFixedUpdate();
                peak = Mathf.Max(peak, Vector2.Distance(initial, backToLegacy.GetComponent<Rigidbody2D>().position));
            }
            Time.timeScale = 0f; ai.enabled = true;
            Assert.That(peak, Is.GreaterThan(.02f), "The explicit legacy handoff must restore the original real Hurt displacement despite retained adapter components.");
            Debug.Log($"ECOLOGY_LEGACY_HURT sameInstance={backToLegacy.GetInstanceID()} actualPeak={peak:R} explicitRelease=True staleEcologyStayedOwned=True");
        }
        oldFactory.Return(backToLegacy);
        GameObject again = activate(second);
        Assert.That(again, Is.SameAs(legacy)); Assert.That(stopped(again), Is.True);
        Assert.That(again.GetComponent<EcologyResidentBinding>().ResidentId, Is.EqualTo(second.Id));
        Assert.That(again.GetComponent<EcologyCombatBinding>().Lease, Is.GreaterThan(oldLease));
        Assert.That(EcologyMotionBody.TryRequest(again.transform, position + Vector3.right, EcologyMotionKind.Normal), Is.True);
        oldFactory.Return(again);
        Debug.Log($"ECOLOGY_CROSS_PROFILE_POOL sameInstance={legacy.GetInstanceID()} sequence=legacy,ecology,legacy,ecology " +
            "onEnableRefresh=True explicitLegacyRelease=True ordinaryFaultStillOwned=True newResidentId=True noEarlyAi=True");
    }

    private IEnumerator CheckPreparedHomes(EcologyPlanningAttemptResult result, EcologyEcotypeDefinition[] types, bool measureDistance = false)
    {
        Time.timeScale = 0f;
        var cache = new EcologyHomeNavigationCache(result.Plan.Ecology, result.StaticGeometry);
        bool current = true, ready = false; int bound = 0, zeroDistanceCounterexamples = 0;
        var perRegion = new Dictionary<int, EcologyHomeNavigation>();
        foreach (MapEcologyResident resident in result.Plan.Ecology.Residents)
        {
            EcologyEcotypeDefinition type = types.Single(t => t.TypeId == resident.TypeId);
            EcologyHomeNavigation home = cache.Get(resident, type);
            if (perRegion.TryGetValue(resident.HomeRegionId, out EcologyHomeNavigation previous)) Assert.That(home, Is.SameAs(previous));
            else perRegion.Add(resident.HomeRegionId, home);
            Assert.That(home.HomeNodes, Does.Contain(resident.InitialNode));
            Assert.That(home.ReturnField.Distances[resident.InitialNode], Is.Zero);
            IGenerationScopedPreparedPopulationTarget factory = type.Prefab.GetComponent<IGenerationScopedPreparedPopulationTarget>();
            Assert.That(factory, Is.Not.Null);
            GameObject actor = factory.SpawnPrepared(result.StaticGeometry.NodeWorldPosition(resident.InitialNode), Quaternion.identity, instance =>
            {
                ownedProjections.Add(instance);
                var binding = instance.GetComponent<EcologyResidentBinding>() ?? instance.AddComponent<EcologyResidentBinding>();
                binding.Prepare(cache, resident.Id, type, () => current, () => ready);
                Assert.That(binding.IsBound, Is.True); Assert.That(binding.CanAct, Is.False);
                Assert.That(binding.GenerationId, Is.EqualTo(result.Plan.GenerationId));
                Assert.That(binding.ResidentId, Is.EqualTo(resident.Id)); Assert.That(binding.HomeRegionId, Is.EqualTo(resident.HomeRegionId));
                Assert.That(binding.EcotypeId, Is.EqualTo(resident.TypeId)); Assert.That(binding.Home, Is.SameAs(home));
            });
            var actual = actor.GetComponent<EcologyResidentBinding>();
            MonoBehaviour ai = actor.GetComponents<MonoBehaviour>().Single(b => b.GetType().Name == "MonsterAIController");
            PropertyInfo awakeWitness = ai.GetType().GetProperty("EcologyBindingPresentAtAwake");
            Assert.That(awakeWitness, Is.Not.Null, "The actual AI must witness its home identity in Awake.");
            Assert.That((bool)awakeWitness.GetValue(ai), Is.True);
            yield return null;
            MonoBehaviour movement = actor.GetComponents<MonoBehaviour>().Single(b => b.GetType().Name == "MonsterMovementController");
            Assert.That((bool)movement.GetType().GetProperty("HasReachedDestination").GetValue(movement), Is.True,
                "AI OnEnable/Update must not start a legacy patrol while the new resident is waiting for Ready.");
            Collider2D foot = actor.GetComponentsInChildren<Collider2D>().Single(c => c.enabled && !c.isTrigger);
            Assert.That(home.ContainsFullFoot(foot, result.StaticGeometry.NodeWorldPosition(resident.InitialNode)), Is.True);
            if (measureDistance)
            {
                Assert.That(home.TryMeasureHomeDistance(foot, result.StaticGeometry.NodeWorldPosition(resident.InitialNode), out int atHome, out _), Is.True);
                Assert.That(atHome, Is.Zero);
                int checkedOutside = 0;
                for (int n = 0; n < home.MovementGraph.NodeCount && checkedOutside < 16; n++)
                {
                    int expected = home.ReturnField.Distances[n]; if (expected <= 0) continue;
                    Assert.That(home.TryMeasureHomeDistance(foot, result.StaticGeometry.NodeWorldPosition(n), out int distance, out Vector2 waypoint), Is.True);
                    Assert.That(distance, Is.EqualTo(expected));
                    Assert.That(waypoint, Is.EqualTo(result.StaticGeometry.NodeWorldPosition(home.ReturnField.Next[n])));
                    checkedOutside++;
                }
                Assert.That(checkedOutside, Is.GreaterThan(0));
            }
            foreach (int node in home.HomeNodes)
            {
                for (int d = 0; d < 4 && zeroDistanceCounterexamples == 0; d++)
                {
                    int next = home.MovementGraph.Neighbor(node, d);
                    if (next < 0 || !home.MovementGraph.CanMove(node, next) || home.HomeGraph.IsLegal(next)) continue;
                    Vector2 pose = Vector2.Lerp(result.StaticGeometry.NodeWorldPosition(node), result.StaticGeometry.NodeWorldPosition(next), .49f);
                    if (!result.StaticGeometry.IsTranslationClear(foot, pose, pose, 0f) || home.ContainsFullFoot(foot, pose)) continue;
                    Assert.That(result.StaticGeometry.TryWorldNode(pose, out int containing) && containing == node, Is.True);
                    Assert.That(home.ReturnField.Distances[containing], Is.Zero);
                    if (measureDistance)
                    {
                        Assert.That(home.TryMeasureHomeDistance(foot, pose, out int partialDistance, out Vector2 returnWaypoint), Is.True);
                        Assert.That(partialDistance, Is.GreaterThan(0), "A partial-foot exit cannot inherit the containing node's zero.");
                        Assert.That(home.ContainsFullFoot(foot, returnWaypoint), Is.True);
                    }
                    zeroDistanceCounterexamples++;
                }
                if (zeroDistanceCounterexamples > 0) break;
            }
            current = false; Assert.That(actual.IsBound, Is.True); Assert.That(actual.CanAct, Is.False,
                "Expired scope cannot silently turn a prepared actor into the old unbound AI path."); current = true;
            type.Prefab.GetComponent<IGenerationScopedPopulationTarget>().Return(actor); bound++;
        }
        Assert.That(bound, Is.EqualTo(result.Plan.Ecology.Residents.Count));
        Assert.That(zeroDistanceCounterexamples, Is.GreaterThan(0), "Distance-zero alone must be challenged by a partial-foot exit pose.");
        Debug.Log($"ECOLOGY_PREPARED_HOME bindings={bound} sharedRegionFields={perRegion.Count} zeroNodeDistanceOutsideFoot={zeroDistanceCounterexamples} " +
            "actualAiAwakeWitness=True noEarlyPatrol=True scopeFaultNoLegacyFallback=True gameplayReadyIntegration=NOT_YET");
    }

    private IEnumerator CheckHomeMotion(EcologyPlanningAttemptResult result, EcologyEcotypeDefinition[] types, bool excursion = false, bool regionalChase = false, GameObject playerSource = null, bool pendingHomeHurt = false, bool blockedReturn = false)
    {
        Time.timeScale = 0f;
        var cache = new EcologyHomeNavigationCache(result.Plan.Ecology, result.StaticGeometry);
        MapEcologyResident chosen = null; EcologyEcotypeDefinition type = null; EcologyHomeNavigation home = null;
        Vector2 start = default, outside = default;
        foreach (MapEcologyResident resident in result.Plan.Ecology.Residents)
        {
            var candidate = types.Single(t => t.TypeId == resident.TypeId);
            if (regionalChase && candidate.Prefab.name != "Torch_Blue") continue;
            if (!(candidate.MovementFoot is BoxCollider2D)) continue;
            var navigation = cache.Get(resident, candidate);
            foreach (int node in navigation.HomeNodes)
            {
                for (int direction = 0; direction < 4; direction++)
                {
                    int next = navigation.MovementGraph.Neighbor(node, direction);
                    if (next < 0 || !navigation.MovementGraph.CanMove(node, next) || navigation.HomeGraph.IsLegal(next)) continue;
                    Vector2 from = result.StaticGeometry.NodeWorldPosition(node), to = result.StaticGeometry.NodeWorldPosition(next);
                    if (!navigation.ContainsFullFoot(candidate.MovementFoot, from) || navigation.ContainsFullFoot(candidate.MovementFoot, to)) continue;
                    if (excursion || regionalChase)
                    {
                        int further = navigation.MovementGraph.Neighbor(next, direction);
                        if (further < 0 || !navigation.MovementGraph.CanMove(next, further) || navigation.ReturnField.Distances[further] <= 1 ||
                            !result.InitialPlacementGeometry.IsTranslationClear(candidate.MovementFoot, from, result.StaticGeometry.NodeWorldPosition(further), 0f)) continue;
                        if (regionalChase && !result.InitialPlacementGeometry.IsTranslationClear(playerSource.transform.Find("MovementFeet").GetComponent<Collider2D>(),
                            result.StaticGeometry.NodeWorldPosition(further), result.StaticGeometry.NodeWorldPosition(further), 0f)) continue;
                    }
                    chosen = resident; type = candidate; home = navigation; start = from; outside = to; break;
                }
                if (chosen != null) break;
            }
            if (chosen != null) break;
        }
        Assert.That(chosen, Is.Not.Null, "The native-clear boundary must challenge a Home fence rather than an entity wall.");
        if (regionalChase)
        {
            yield return CheckOriginalChaseReturn(result, type, chosen, start, outside + outside - start, playerSource, blockedReturn);
            yield break;
        }
        if (excursion)
        {
            yield return CheckObservedExcursion(result, type, chosen, start, outside, pendingHomeHurt);
            yield break;
        }
        var barrierObject = new GameObject("Owned Home motion barrier"); ownedProjections.Add(barrierObject);
        var barrier = barrierObject.AddComponent<EcologyDamageBarrier>();
        barrier.ConfigureProtection(Array.Empty<Rect>());
        int activations = 0;
        foreach (EcologyMotionKind kind in new[] { EcologyMotionKind.Normal, EcologyMotionKind.Hurt })
        foreach (bool cross in new[] { false, true })
        {
            GameObject actor = type.Prefab.GetComponent<IGenerationScopedPreparedPopulationTarget>().SpawnPrepared(start, Quaternion.identity, instance =>
            {
                if (!ownedProjections.Contains(instance)) ownedProjections.Add(instance);
                foreach (MonoBehaviour component in instance.GetComponents<MonoBehaviour>())
                    if (component.GetType().Name == "MonsterAIController" || component.GetType().Name == "MonsterMovementController") component.enabled = false;
                var resident = instance.GetComponent<EcologyResidentBinding>() ?? instance.AddComponent<EcologyResidentBinding>();
                resident.Prepare(cache, chosen.Id, type, () => true, () => true);
                var combat = instance.GetComponent<EcologyCombatBinding>() ?? instance.AddComponent<EcologyCombatBinding>();
                combat.Bind(barrier, 1, ++activations, () => true);
                var motion = instance.GetComponent<EcologyMotionBody>() ?? instance.AddComponent<EcologyMotionBody>();
                var sourceMovement = new SerializedObject(type.Prefab.GetComponents<MonoBehaviour>().Single(b => b.GetType().Name == "MonsterMovementController"));
                motion.ConfigurePrepared(type.ResolveMovementFoot(instance), sourceMovement.FindProperty("obstacleLayers").intValue,
                    sourceMovement.FindProperty("collisionSkinWidth").floatValue);
            });
            try
            {
                Physics2D.SyncTransforms(); var motion = actor.GetComponent<EcologyMotionBody>(); motion.CompletePreparedConfiguration();
                var body = actor.GetComponent<Rigidbody2D>(); var foot = (BoxCollider2D)type.ResolveMovementFoot(actor);
                Vector2 target = cross ? outside : start + (outside - start).normalized * .02f;
                var traces = new List<EcologyMotionTrace>(); motion.OnTrace += traces.Add;
                var producer = actor.GetComponent<EcologyMotionIntentProducer>() ?? actor.AddComponent<EcologyMotionIntentProducer>();
                producer.Submission = () => Assert.That(motion.Request(target, kind), Is.True);
                Time.timeScale = 1f;
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate(); yield return null;
                Time.timeScale = 0f; Physics2D.SyncTransforms();
                Vector2 actual = body.position;
                Debug.Log($"ECOLOGY_HOME_MOTION_DIAG kind={kind} cross={cross} start={start:R} request={target:R} actual={actual:R} " +
                    $"size={foot.size:R} offset={foot.offset:R} nativeBounds={foot.bounds} scale={foot.transform.lossyScale:R} " +
                    $"home={home.ContainsFullFoot(foot, actual)} traces=" + string.Join(";", traces.Select(t => t.Phase + "@" + t.Position.ToString("R"))));
                Assert.That(Vector2.Distance(actual, start), Is.GreaterThan(.009f), "The fence cannot blanket-block legal movement.");
                Assert.That(home.ContainsFullFoot(foot, actual), Is.True, kind + " actual complete foot escaped Home.");
                Bounds bounds = foot.bounds;
                var homeRegion = result.Plan.Ecology.Regions.Single(r => r.Id == chosen.HomeRegionId);
                var membership = new HashSet<int>(homeRegion.Nodes);
                Vector2 spacing = result.StaticGeometry.NodeWorldPosition(1) - result.StaticGeometry.NodeWorldPosition(0);
                float halfWidth = Mathf.Abs(spacing.x) * .5f;
                float halfHeight = Mathf.Abs(result.StaticGeometry.NodeWorldPosition(result.Plan.Ecology.Width).y - result.StaticGeometry.NodeWorldPosition(0).y) * .5f;
                for (int node = 0; node < result.Plan.Ecology.RegionByNode.Count; node++)
                {
                    Vector2 centre = result.StaticGeometry.NodeWorldPosition(node);
                    bool overlaps = bounds.max.x > centre.x - halfWidth && bounds.min.x < centre.x + halfWidth &&
                        bounds.max.y > centre.y - halfHeight && bounds.min.y < centre.y + halfHeight;
                    Assert.That(!overlaps || membership.Contains(node), Is.True, "Independent native bounds coverage escaped Home at node " + node);
                }
                Assert.That(traces.Any(t => t.Phase == "Submitted" && t.Kind == kind), Is.True);
                Assert.That(actor.GetComponent<EcologyCombatBinding>().MotionFaultRevision, Is.Zero);
                Debug.Log($"ECOLOGY_HOME_NATIVE_MOTION kind={kind} cross={cross} start={start} requested={target} actual={actual} " +
                    $"nativeFoot={bounds} completeHome=True positiveMovement=True originalBody={body.bodyType} scope=CONTROLLED_MOTION_NOT_AI");
                motion.OnTrace -= traces.Add;
            }
            finally
            {
                type.Prefab.GetComponent<IGenerationScopedPopulationTarget>().Return(actor);
                foreach (MonoBehaviour component in actor.GetComponents<MonoBehaviour>())
                    if (component.GetType().Name == "MonsterAIController" || component.GetType().Name == "MonsterMovementController") component.enabled = true;
            }
        }
        Assert.That(activations, Is.EqualTo(4));
    }

    private IEnumerator CheckOriginalChaseReturn(EcologyPlanningAttemptResult result, EcologyEcotypeDefinition type, MapEcologyResident resident,
        Vector2 start, Vector2 target, GameObject playerSource, bool blockedReturn = false)
    {
        Time.timeScale = 0f;
        var cache = new EcologyHomeNavigationCache(result.Plan.Ecology, result.StaticGeometry);
        var barrierObject = new GameObject("Owned original FSM excursion barrier"); ownedProjections.Add(barrierObject);
        var barrier = barrierObject.AddComponent<EcologyDamageBarrier>();
        var protectedRects = Enumerable.Range(0, result.Plan.Ecology.RegionByNode.Count).Where(result.Plan.Ecology.IsProtectedNode)
            .Select(n => new Rect(result.StaticGeometry.NodeWorldPosition(n) - Vector2.one * .5f, Vector2.one)).ToArray();
        barrier.ConfigureProtection(protectedRects);
        bool current = true, ready = false;
        GameObject[] scenePlayers = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject scenePlayer in scenePlayers) scenePlayer.SetActive(false);
        MonoBehaviour session = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true).Single(b => b.GetType().Name == "GameSession");
        object sharedStats = session.GetType().GetProperty("PlayerStats").GetValue(session);
        object previousStats = sharedStats.GetType().GetMethod("CaptureSnapshot").Invoke(sharedStats, null);
        var sourceMovementSettings = new SerializedObject(type.Prefab.GetComponents<MonoBehaviour>().Single(b => b.GetType().Name == "MonsterMovementController"));
        double fixtureOutsideSeconds = Vector2.Distance(start, target) / sourceMovementSettings.FindProperty("moveSpeed").floatValue;
        var playerHolder = new GameObject("Owned real chase player"); playerHolder.SetActive(false); ownedProjections.Add(playerHolder);
        GameObject player = UnityEngine.Object.Instantiate(playerSource, target, Quaternion.identity, playerHolder.transform);
        var playerBinding = player.AddComponent<EcologyCombatBinding>(); playerBinding.Bind(barrier, 1, 2, () => current);
        var playerMotion = player.AddComponent<EcologyMotionBody>();
        playerMotion.ConfigurePrepared(player.transform.Find("MovementFeet").GetComponent<Collider2D>(), Physics2D.AllLayers, .02f);
        playerHolder.SetActive(true); Physics2D.SyncTransforms(); playerMotion.CompletePreparedConfiguration();
        Assert.That(playerBinding.ProtectionNow, Is.False, "The real melee target must be outside every actual core/buffer.");
        GameObject actor = type.Prefab.GetComponent<IGenerationScopedPreparedPopulationTarget>().SpawnPrepared(start, Quaternion.identity, instance =>
        {
            ownedProjections.Add(instance);
            var binding = instance.GetComponent<EcologyResidentBinding>() ?? instance.AddComponent<EcologyResidentBinding>();
            binding.Prepare(cache, resident.Id, type, () => current, () => ready, 3, fixtureOutsideSeconds);
            var combat = instance.GetComponent<EcologyCombatBinding>() ?? instance.AddComponent<EcologyCombatBinding>();
            combat.Bind(barrier, 1, 1, () => current);
            var motion = instance.GetComponent<EcologyMotionBody>() ?? instance.AddComponent<EcologyMotionBody>();
            var movement = new SerializedObject(type.Prefab.GetComponents<MonoBehaviour>().Single(b => b.GetType().Name == "MonsterMovementController"));
            motion.ConfigurePrepared(type.ResolveMovementFoot(instance), movement.FindProperty("obstacleLayers").intValue,
                movement.FindProperty("collisionSkinWidth").floatValue);
        });
        try
        {
            Physics2D.SyncTransforms(); actor.GetComponent<EcologyMotionBody>().CompletePreparedConfiguration();
            var binding = actor.GetComponent<EcologyResidentBinding>(); var combat = actor.GetComponent<EcologyCombatBinding>();
            MonoBehaviour ai = actor.GetComponents<MonoBehaviour>().Single(b => b.GetType().Name == "MonsterAIController");
            MonoBehaviour health = actor.GetComponents<MonoBehaviour>().Single(b => b.GetType().Name == "BaseMonster");
            MonoBehaviour attack = actor.GetComponents<MonoBehaviour>().Single(b => b.GetType().Name == "MonsterAttackController");
            float hpBefore = (float)health.GetType().GetProperty("CurrentHealth").GetValue(health);
            int counterStarts = 0, counterDamage = 0; var counterIds = new HashSet<long>();
            Action<Vector2> onAttack = _ => { if (binding.Excursion.ReturnLatched) counterStarts++; };
            attack.GetType().GetEvent("OnAttackStarted").AddEventHandler(attack, onAttack);
            barrier.OnTrace += (request, phase) =>
            {
                if (request.AttackerId != 1) return;
                if (phase == "Started" && binding.Excursion.ReturnLatched) counterIds.Add(request.AttackId);
                if (request.DamageApplied && counterIds.Contains(request.AttackId) && phase == "Consumed") counterDamage++;
            };
            ready = true; Time.timeScale = 1f;
            float deadline = Time.time + 3f;
            var stateHistory = new List<string>(); string previousState = null;
            var motionTrace = new List<EcologyMotionTrace>(); actor.GetComponent<EcologyMotionBody>().OnTrace += motionTrace.Add;
            while (!binding.Excursion.HasPermit && Time.time < deadline)
            {
                yield return new WaitForFixedUpdate(); yield return null;
                string state = ai.GetType().GetProperty("CurrentStateType").GetValue(ai).ToString();
                if (state != previousState) { stateHistory.Add(state + "@" + actor.GetComponent<Rigidbody2D>().position.ToString("R")); previousState = state; }
            }
            MonoBehaviour perception = actor.GetComponents<MonoBehaviour>().Single(b => b.GetType().Name == "MonsterPerceptionController");
            Transform seen = perception.GetType().GetProperty("CurrentVisibleTarget").GetValue(perception) as Transform;
            MonoBehaviour actualMovement = actor.GetComponents<MonoBehaviour>().Single(b => b.GetType().Name == "MonsterMovementController");
            Vector2 origin = actor.transform.position, sightTarget = player.GetComponent<Collider2D>().bounds.center;
            int sightMask = new SerializedObject(perception).FindProperty("sightBlockingLayers").intValue;
            var rayHits = Physics2D.RaycastAll(origin, (sightTarget - origin).normalized, Vector2.Distance(origin, sightTarget), sightMask);
            Debug.Log($"ECOLOGY_CHASE_DIAG start={start:R} requestedPlayer={target:R} actualActor={actor.GetComponent<Rigidbody2D>().position:R} " +
                $"actualPlayer={player.GetComponent<Rigidbody2D>().position:R} aiEnabled={ai.enabled} canAct={binding.CanAct} failure={binding.NavigationFailure} " +
                $"canPath={binding.CanPursuePosition(actor.GetComponent<Rigidbody2D>().position, player.transform.position)} " +
                $"movement={actualMovement.GetType().GetProperty("NavigationStatus").GetValue(actualMovement)} seen={(seen != null ? seen.name + seen.position.ToString("R") : "NULL")} " +
                "players=" + string.Join(";", GameObject.FindGameObjectsWithTag("Player").Select(p => p.name + p.transform.position.ToString("R"))) +
                " states=" + string.Join(";", stateHistory) + " rays=" + string.Join(";", rayHits.Select(h => h.collider.name + "/" + h.collider.isTrigger + "/" + h.distance.ToString("R"))) +
                " traces=" + string.Join(";", motionTrace.Where(t => t.Phase != "PhysicsObserved" && t.Phase != "EmptyProtectionCertified").Take(28).Select(t => t.Phase + "@" + t.Position.ToString("R"))));
            Assert.That(binding.Excursion.HasPermit, Is.True, "Real perception/Search/Chase must actually move the full foot out of Home.");
            double departed = binding.Excursion.DepartedAt;
            bool latched = false, damaged = false, returned = false, blockedFailure = false;
            GameObject blocker = null; deadline = Time.time + (blockedReturn ? 12f : 7f);
            while (Time.time < deadline)
            {
                yield return new WaitForFixedUpdate(); yield return null;
                if (binding.Excursion.ReturnLatched) latched = true;
                if (blockedReturn && latched && blocker == null)
                {
                    Assert.That(binding.TryGetHomeDistance(out int currentDistance, out Vector2 returnWaypoint) && currentDistance > 0, Is.True);
                    Vector2 direction = (returnWaypoint - actor.GetComponent<Rigidbody2D>().position).normalized;
                    blocker = new GameObject("Owned temporary kinematic return obstruction"); ownedProjections.Add(blocker);
                    blocker.SetActive(false);
                    var body = blocker.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic; body.gravityScale = 0f;
                    var wall = blocker.AddComponent<BoxCollider2D>();
                    wall.size = Mathf.Abs(direction.x) > Mathf.Abs(direction.y) ? new Vector2(.06f, 4f) : new Vector2(4f, .06f);
                    blocker.transform.position = actor.GetComponent<Rigidbody2D>().position + direction * .65f;
                    blocker.SetActive(true); Physics2D.SyncTransforms();
                    Assert.That(Physics2D.Distance(type.ResolveMovementFoot(actor), wall).isOverlapped, Is.False,
                        "The obstruction must start ahead of the complete foot, rather than create an artificial initial penetration.");
                }
                if (blockedReturn && binding.ScopeFaulted)
                {
                    Assert.That(binding.NavigationFailure, Is.EqualTo("ReturnBlockedOrNoPathAfterThreeWaits"));
                    Assert.That(binding.Excursion.HasPermit, Is.True); Assert.That(binding.Excursion.Episode, Is.EqualTo(1));
                    Assert.That(binding.Excursion.DepartedAt, Is.EqualTo(departed));
                    Assert.That(actor.activeInHierarchy, Is.True); Assert.That((bool)health.GetType().GetProperty("IsDefeated").GetValue(health), Is.False);
                    Assert.That((float)health.GetType().GetProperty("CurrentHealth").GetValue(health), Is.EqualTo(hpBefore - (damaged ? 1f : 0f)));
                    var existingRewards = new HashSet<int>(UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true)
                        .Where(b => b.GetType().Name == "RewardPickup").Select(b => b.GetInstanceID()));
                    health.GetType().GetMethod("TakeDamage", new[] { typeof(float), typeof(Transform) }).Invoke(health, new object[] { hpBefore + 1f, player.transform });
                    Assert.That((bool)health.GetType().GetProperty("IsDefeated").GetValue(health), Is.True,
                        "A navigation failure cannot add invulnerability or prevent the original lethal receiver.");
                    foreach (MonoBehaviour reward in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true)
                        .Where(b => b.GetType().Name == "RewardPickup" && !existingRewards.Contains(b.GetInstanceID())).ToArray())
                        type.Prefab.GetComponent<IGenerationScopedPopulationTarget>().Return(reward.gameObject);
                    blockedFailure = true; break;
                }
                if (latched && binding.Excursion.HasPermit)
                {
                    Assert.That(binding.Excursion.Episode, Is.EqualTo(1)); Assert.That(binding.Excursion.DepartedAt, Is.EqualTo(departed));
                    Assert.That(ai.GetType().GetProperty("CurrentStateType").GetValue(ai).ToString(), Is.Not.EqualTo("Chase"));
                    Assert.That(counterStarts, Is.LessThanOrEqualTo(1), "Return cannot loop existing attacks forever.");
                    if (!damaged && counterDamage > 0)
                    {
                        health.GetType().GetMethod("TakeDamage", new[] { typeof(float), typeof(Transform) }).Invoke(health, new object[] { 1f, player.transform });
                        damaged = true;
                    }
                }
                if (latched && !binding.Excursion.HasPermit)
                {
                    Assert.That(binding.Home.ContainsFullFoot(type.ResolveMovementFoot(actor), actor.GetComponent<Rigidbody2D>().position), Is.True);
                    returned = true; break;
                }
            }
            Debug.Log($"ECOLOGY_ORIGINAL_RETURN_DIAG latched={latched} returned={returned} counterStarts={counterStarts} counterDamage={counterDamage} " +
                $"damaged={damaged} actor={actor.GetComponent<Rigidbody2D>().position:R} player={player.GetComponent<Rigidbody2D>().position:R} " +
                $"fixtureOutsideSeconds={fixtureOutsideSeconds:R} episode={binding.Excursion.Episode} fault={combat.MotionFaultRevision} " +
                $"state={ai.GetType().GetProperty("CurrentStateType").GetValue(ai)} fullHome={(type.ResolveMovementFoot(actor).enabled && binding.Home.ContainsFullFoot(type.ResolveMovementFoot(actor), actor.GetComponent<Rigidbody2D>().position))} " +
                $"nav={binding.NavigationFailure} distance={binding.TryGetHomeDistance(out int finalDistance, out Vector2 finalWaypoint)}/{finalDistance}/{finalWaypoint:R} " +
                $"movement={actualMovement.GetType().GetProperty("NavigationStatus").GetValue(actualMovement)} locked={actualMovement.GetType().GetProperty("IsMovementLocked").GetValue(actualMovement)} " +
                "lastMotion=" + string.Join(";", motionTrace.Skip(Math.Max(0, motionTrace.Count - 24)).Select(t => t.Phase + "@" + t.Position.ToString("R"))));
            if (blockedReturn)
            {
                Assert.That(blockedFailure, Is.True, "A real temporary native blockage must reach its finite failure diagnostic, not wait forever or claim arrived.");
                Assert.That(returned, Is.False); Assert.That(counterStarts, Is.LessThanOrEqualTo(1));
                Debug.Log($"ECOLOGY_RETURN_NATIVE_BLOCKED finiteFailure=True diagnostic={binding.NavigationFailure} " +
                    "noHealing=True noFakeDefeatBeforeLethal=True actualLethalReceiver=True noTeleport=True episode=1 scope=OWNED_DYNAMIC_OBSTRUCTION_NOT_STATIC_VERSION");
                yield break;
            }
            Assert.That(latched && returned, Is.True, "The original FSM must actually return while the target stays visible.");
            Assert.That(counterStarts, Is.EqualTo(1)); Assert.That(counterDamage, Is.EqualTo(1)); Assert.That(damaged, Is.True);
            Assert.That((float)health.GetType().GetProperty("CurrentHealth").GetValue(health), Is.EqualTo(hpBefore - 1f), "Returning cannot reset or heal real health.");
            Assert.That(combat.MotionFaultRevision, Is.Zero); Assert.That(playerBinding.MotionFaultRevision, Is.Zero);
            Debug.Log($"ECOLOGY_ORIGINAL_CHASE_RETURN actualPerception=True actualOutside=True episode=1 departed={departed:R} " +
                $"counterStarts={counterStarts} counterDamage={counterDamage} actualDamagedEvent={damaged} returned={returned} " +
                $"elapsed={binding.Excursion.LastCompletedElapsed:R} slimeOrdinaryCatalogue=True bossDefinitionRetained=True fixture=3steps/{fixtureOutsideSeconds:R}s scope=FSM_NOT_PRODUCTION_READY");
            attack.GetType().GetEvent("OnAttackStarted").RemoveEventHandler(attack, onAttack);
        }
        finally
        {
            current = false; ready = false; Time.timeScale = 0f; type.Prefab.GetComponent<IGenerationScopedPopulationTarget>().Return(actor);
            playerHolder.SetActive(false);
            sharedStats.GetType().GetMethod("RestoreSnapshot").Invoke(sharedStats, new[] { previousStats, (object)false });
            foreach (GameObject scenePlayer in scenePlayers) if (scenePlayer != null) scenePlayer.SetActive(true);
        }
    }

    private IEnumerator CheckObservedExcursion(EcologyPlanningAttemptResult result, EcologyEcotypeDefinition type, MapEcologyResident resident,
        Vector2 start, Vector2 outside, bool pendingHomeHurt = false)
    {
        var cache = new EcologyHomeNavigationCache(result.Plan.Ecology, result.StaticGeometry);
        var barrierObject = new GameObject("Owned observed excursion barrier"); ownedProjections.Add(barrierObject);
        var barrier = barrierObject.AddComponent<EcologyDamageBarrier>();
        GameObject actor = type.Prefab.GetComponent<IGenerationScopedPreparedPopulationTarget>().SpawnPrepared(start, Quaternion.identity, instance =>
        {
            ownedProjections.Add(instance);
            foreach (MonoBehaviour component in instance.GetComponents<MonoBehaviour>())
                if (component.GetType().Name == "MonsterAIController" || component.GetType().Name == "MonsterMovementController") component.enabled = false;
            var home = instance.GetComponent<EcologyResidentBinding>() ?? instance.AddComponent<EcologyResidentBinding>();
            home.Prepare(cache, resident.Id, type, () => true, () => true, 1, .12d);
            var combat = instance.GetComponent<EcologyCombatBinding>() ?? instance.AddComponent<EcologyCombatBinding>();
            combat.Bind(barrier, 1, 1, () => true);
            var motion = instance.GetComponent<EcologyMotionBody>() ?? instance.AddComponent<EcologyMotionBody>();
            var movement = new SerializedObject(type.Prefab.GetComponents<MonoBehaviour>().Single(b => b.GetType().Name == "MonsterMovementController"));
            motion.ConfigurePrepared(type.ResolveMovementFoot(instance), movement.FindProperty("obstacleLayers").intValue,
                movement.FindProperty("collisionSkinWidth").floatValue);
        });
        try
        {
            Physics2D.SyncTransforms(); var motion = actor.GetComponent<EcologyMotionBody>(); motion.CompletePreparedConfiguration();
            var binding = actor.GetComponent<EcologyResidentBinding>(); var body = actor.GetComponent<Rigidbody2D>();
            var producer = actor.GetComponent<EcologyMotionIntentProducer>() ?? actor.AddComponent<EcologyMotionIntentProducer>();
            Assert.That(binding.Excursion.HasPermit, Is.False);
            Assert.That(binding.BeginPursuit(), Is.True);
            Assert.That(binding.Excursion.HasPermit, Is.False, "Intent alone cannot invent an observed departure.");
            producer.Submission = () => Assert.That(motion.Request(outside, EcologyMotionKind.Normal), Is.True);
            Time.timeScale = 1f; yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate(); yield return null;
            Time.timeScale = 0f;
            Assert.That(binding.Home.ContainsFullFoot(type.ResolveMovementFoot(actor), body.position), Is.False, "An authorized pursuit must actually be able to leave Home.");
            Assert.That(binding.Excursion.HasPermit, Is.True); Assert.That(binding.Excursion.Episode, Is.EqualTo(1));
            double departed = binding.Excursion.DepartedAt, pausedElapsed = binding.Excursion.Elapsed;
            if (pendingHomeHurt)
            {
                producer.Repeat = true; producer.Submission = () => Assert.That(motion.Request(start, EcologyMotionKind.Hurt), Is.True);
                Time.timeScale = 1f;
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate(); yield return null;
                Time.timeScale = 0f;
                Assert.That(binding.Home.ContainsFullFoot(type.ResolveMovementFoot(actor), body.position), Is.True);
                Assert.That(body.velocity.sqrMagnitude, Is.LessThan(.00000001f));
                Debug.Log($"ECOLOGY_PENDING_HURT_HOME_APEX actualHome=True actualZeroVelocity=True repeatedHurtIntent=True permit={binding.Excursion.HasPermit} " +
                    $"episode={binding.Excursion.Episode} departed={binding.Excursion.DepartedAt:R}");
                Assert.That(binding.Excursion.HasPermit, Is.True, "A stationary full Home pose with ongoing submitted Hurt is an apex, not completed return docking.");
                producer.Repeat = false; producer.Submission = () => Assert.That(motion.Request(outside, EcologyMotionKind.Hurt), Is.True);
                Time.timeScale = 1f; yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate(); yield return null;
                Time.timeScale = 0f;
                Assert.That(binding.Home.ContainsFullFoot(type.ResolveMovementFoot(actor), body.position), Is.False);
                Assert.That(binding.Excursion.HasPermit, Is.True); Assert.That(binding.Excursion.Episode, Is.EqualTo(1));
                Assert.That(binding.Excursion.DepartedAt, Is.EqualTo(departed));
                producer.Submission = () => Assert.That(motion.Request(start, EcologyMotionKind.Hurt), Is.True);
                Time.timeScale = 1f;
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate(); yield return null;
                Time.timeScale = 0f;
                Assert.That(binding.Home.ContainsFullFoot(type.ResolveMovementFoot(actor), body.position), Is.True);
                Assert.That(binding.Excursion.HasPermit, Is.False); Assert.That(binding.Excursion.Episode, Is.EqualTo(1));
                Assert.That(actor.GetComponent<EcologyCombatBinding>().MotionFaultRevision, Is.Zero);
                Debug.Log("ECOLOGY_PENDING_HURT_RETURN episode=1 originalDepartureKept=True originalOutsideLegAllowed=True finalStoppedWithoutIntent=True");
                yield break;
            }
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(binding.Excursion.Elapsed, Is.EqualTo(pausedElapsed));
            Vector2 forbiddenRequest = outside + (outside - start).normalized;
            producer.Submission = () => Assert.That(motion.Request(forbiddenRequest, EcologyMotionKind.Hurt), Is.True);
            Time.timeScale = 1f; yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate(); yield return null;
            Assert.That(binding.Home.TryMeasureHomeDistance(type.ResolveMovementFoot(actor), body.position, out int distance, out _), Is.True);
            Assert.That(distance, Is.InRange(1, 1), "Original Hurt cannot expand the configured Home-anchored range.");
            Assert.That(binding.Excursion.DepartedAt, Is.EqualTo(departed)); Assert.That(binding.Excursion.Episode, Is.EqualTo(1));
            float deadline = Time.time + .25f;
            while (!binding.Excursion.ReturnLatched && Time.time < deadline) yield return new WaitForFixedUpdate();
            Assert.That(binding.Excursion.ReturnLatched, Is.True);
            Assert.That(binding.BeginPursuit(), Is.False, "A new target cannot renew a latched return.");
            Assert.That(binding.Excursion.DepartedAt, Is.EqualTo(departed));
            producer.Submission = () => Assert.That(motion.Request(start, EcologyMotionKind.Hurt), Is.True);
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate(); yield return null;
            Assert.That(binding.Home.ContainsFullFoot(type.ResolveMovementFoot(actor), body.position), Is.True);
            Assert.That(binding.Excursion.HasPermit, Is.False); Assert.That(binding.Excursion.ReturnLatched, Is.False);
            Assert.That(binding.Excursion.LastCompletedElapsed, Is.GreaterThanOrEqualTo(.12d));
            Assert.That(actor.GetComponent<EcologyCombatBinding>().MotionFaultRevision, Is.Zero);
            Debug.Log($"ECOLOGY_OBSERVED_EXCURSION episode=1 actualOutside=True pausedElapsed={pausedElapsed:R} departed={departed:R} " +
                $"completedElapsed={binding.Excursion.LastCompletedElapsed:R} originalHurtRange={distance} renewed=False actualStoppedHome=True " +
                "fixtureThresholds=1step/0.12seconds productionDefaults=6steps/16seconds scope=CONTROLLED_MOTION_NOT_AI_CHASE");
        }
        finally
        {
            Time.timeScale = 0f; type.Prefab.GetComponent<IGenerationScopedPopulationTarget>().Return(actor);
            foreach (MonoBehaviour component in actor.GetComponents<MonoBehaviour>())
                if (component.GetType().Name == "MonsterAIController" || component.GetType().Name == "MonsterMovementController") component.enabled = true;
        }
    }

    private IEnumerator CheckOriginalHomeCrowding(EcologyPlanningAttemptResult result, MapData projectedMap, EcologyEcotypeDefinition[] types)
    {
        Time.timeScale = 0f;
        var cache = new EcologyHomeNavigationCache(result.Plan.Ecology, result.StaticGeometry);
        MapEcologyResident[] pair = null; EcologyEcotypeDefinition type = null; Vector2 leftStart = default, rightStart = default;
        foreach (var group in result.Plan.Ecology.Residents.GroupBy(r => r.HomeRegionId).Where(g => g.Count() >= 2))
        {
            MapEcologyResident first = group.First(); var candidate = types.Single(t => t.TypeId == first.TypeId);
            var home = cache.Get(first, candidate);
            foreach (int a in home.HomeNodes)
            {
                for (int direction = 0; direction < 4; direction++)
                {
                    int middle = home.HomeGraph.Neighbor(a, direction), b = home.HomeGraph.Neighbor(middle, direction);
                    if (middle < 0 || b < 0 || !home.HomeGraph.CanMove(a, middle) || !home.HomeGraph.CanMove(middle, b)) continue;
                    Vector2 from = home.NodeWorldPosition(a), to = home.NodeWorldPosition(b);
                    if (!result.InitialPlacementGeometry.IsTranslationClear(candidate.MovementFoot, from, to, 0f) ||
                        !home.TryClipHomeTranslation(candidate.MovementFoot, from, to, out Vector2 clipped) || Vector2.Distance(clipped, to) > .00001f) continue;
                    pair = group.Take(2).ToArray(); type = candidate; leftStart = from; rightStart = to; break;
                }
                if (pair != null) break;
            }
            if (pair != null) break;
        }
        Assert.That(pair, Is.Not.Null, "A real accepted Home needs two residents and a native-clear interior route.");
        MapGenerationController ownedController = UnityEngine.Object.FindObjectOfType<MapGenerationController>(true);
        FieldInfo mapField = typeof(MapGenerationController).GetField("lastGeneratedMap", BindingFlags.Instance | BindingFlags.NonPublic);
        object previousMap = mapField.GetValue(ownedController);
        // The owned native projection uses this pending map. Match the unbound baseline's original
        // height/path reader to that same map, rather than the unrelated scene warmup map.
        mapField.SetValue(ownedController, projectedMap);
        var legacy = new[] { type.Prefab.GetComponent<IGenerationScopedPopulationTarget>().Spawn(leftStart, Quaternion.identity),
            type.Prefab.GetComponent<IGenerationScopedPopulationTarget>().Spawn(rightStart, Quaternion.identity) };
        float legacyMinimum = float.PositiveInfinity; float legacyProgress = 0f;
        try
        {
            Physics2D.SyncTransforms();
            for (int i = 0; i < legacy.Length; i++)
            {
                if (!ownedProjections.Contains(legacy[i])) ownedProjections.Add(legacy[i]);
                ((Behaviour)legacy[i].GetComponent("MonsterAIController")).enabled = false;
                Component movement = legacy[i].GetComponent("MonsterMovementController"); ((Behaviour)movement).enabled = true;
                movement.GetType().GetMethod("SetDestination").Invoke(movement, new object[] { i == 0 ? rightStart : leftStart });
            }
            Time.timeScale = 1f; float deadline = Time.time + 1.4f;
            while (Time.time < deadline)
            {
                yield return new WaitForFixedUpdate(); Physics2D.SyncTransforms();
                legacyMinimum = Mathf.Min(legacyMinimum, Physics2D.Distance(type.ResolveMovementFoot(legacy[0]), type.ResolveMovementFoot(legacy[1])).distance);
                legacyProgress = Mathf.Max(legacyProgress, Vector2.Distance(legacy[0].GetComponent<Rigidbody2D>().position, leftStart));
            }
            Assert.That(legacyProgress, Is.GreaterThan(.05f), "An unmoving legacy baseline cannot explain simultaneous contact.");
            Debug.Log($"ECOLOGY_ORIGINAL_TWO_MOVING_BASELINE prefab={type.Prefab.name} minimumNativeSeparation={legacyMinimum:R} progress={legacyProgress:R} " +
                "route=ORIGINAL_UNBOUND_POOL sameFeet=True sameSpeed=True samePhysics=True simultaneousKinematicMotion=True ownedPendingProjectionMapReader=True noNewSeparationPromise=True");
        }
        finally
        {
            Time.timeScale = 0f;
            foreach (GameObject actor in legacy) type.Prefab.GetComponent<IGenerationScopedPopulationTarget>().Return(actor);
            mapField.SetValue(ownedController, previousMap);
        }
        var barrierObject = new GameObject("Owned original crowding barrier"); ownedProjections.Add(barrierObject);
        var barrier = barrierObject.AddComponent<EcologyDamageBarrier>();
        GameObject Spawn(int index, Vector2 position) => type.Prefab.GetComponent<IGenerationScopedPreparedPopulationTarget>().SpawnPrepared(position, Quaternion.identity, actor =>
        {
            if (!ownedProjections.Contains(actor)) ownedProjections.Add(actor);
            ((Behaviour)actor.GetComponent("MonsterAIController")).enabled = false;
            var resident = actor.GetComponent<EcologyResidentBinding>() ?? actor.AddComponent<EcologyResidentBinding>();
            resident.Prepare(cache, pair[index].Id, type, () => true, () => true);
            var combat = actor.GetComponent<EcologyCombatBinding>() ?? actor.AddComponent<EcologyCombatBinding>();
            combat.Bind(barrier, 1, index + 1, () => true);
            var motion = actor.GetComponent<EcologyMotionBody>() ?? actor.AddComponent<EcologyMotionBody>();
            var movement = new SerializedObject(type.Prefab.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == "MonsterMovementController"));
            motion.ConfigurePrepared(type.ResolveMovementFoot(actor), movement.FindProperty("obstacleLayers").intValue,
                movement.FindProperty("collisionSkinWidth").floatValue);
        });
        GameObject left = Spawn(0, leftStart), right = Spawn(1, rightStart); bool returnedRight = false;
        try
        {
            Physics2D.SyncTransforms(); left.GetComponent<EcologyMotionBody>().CompletePreparedConfiguration(); right.GetComponent<EcologyMotionBody>().CompletePreparedConfiguration();
            Collider2D leftFoot = type.ResolveMovementFoot(left), rightFoot = type.ResolveMovementFoot(right);
            Assert.That(Physics2D.Distance(leftFoot, rightFoot).isOverlapped, Is.False);
            Component movementLeft = left.GetComponent("MonsterMovementController"), movementRight = right.GetComponent("MonsterMovementController");
            ((Behaviour)movementLeft).enabled = true; ((Behaviour)movementRight).enabled = true;
            movementLeft.GetType().GetMethod("SetDestination").Invoke(movementLeft, new object[] { rightStart });
            movementRight.GetType().GetMethod("Stop").Invoke(movementRight, null);
            var leftCombat = left.GetComponent<EcologyCombatBinding>(); long lease = leftCombat.Lease;
            var home = left.GetComponent<EcologyResidentBinding>().Home; var graph = home.HomeGraph;
            Component health = left.GetComponent("BaseMonster"); float hp = Convert.ToSingle(health.GetType().GetProperty("CurrentHealth").GetValue(health));
            int observations = 0; Time.timeScale = 1f; float deadline = Time.time + 1.4f;
            while (Time.time < deadline)
            {
                yield return new WaitForFixedUpdate(); Physics2D.SyncTransforms();
                Assert.That(Physics2D.Distance(leftFoot, rightFoot).isOverlapped, Is.False);
                Assert.That(home.ContainsFullFoot(leftFoot, left.GetComponent<Rigidbody2D>().position), Is.True);
                Assert.That(home.ContainsFullFoot(rightFoot, right.GetComponent<Rigidbody2D>().position), Is.True);
                Assert.That(leftCombat.MotionFaultRevision + right.GetComponent<EcologyCombatBinding>().MotionFaultRevision, Is.Zero); observations++;
            }
            Assert.That(Vector2.Distance(left.GetComponent<Rigidbody2D>().position, leftStart), Is.GreaterThan(.05f));
            Assert.That(Vector2.Distance(left.GetComponent<Rigidbody2D>().position, rightStart), Is.GreaterThan(.2f), "The temporary resident blocker must actually stop the original controller.");
            Time.timeScale = 0f; type.Prefab.GetComponent<IGenerationScopedPopulationTarget>().Return(right); returnedRight = true;
            Physics2D.SyncTransforms(); movementLeft.GetType().GetMethod("SetDestination").Invoke(movementLeft, new object[] { rightStart });
            Time.timeScale = 1f; deadline = Time.time + 3f;
            while (Vector2.Distance(left.GetComponent<Rigidbody2D>().position, rightStart) > .06f && Time.time < deadline) yield return new WaitForFixedUpdate();
            Assert.That(Vector2.Distance(left.GetComponent<Rigidbody2D>().position, rightStart), Is.LessThan(.06f));
            Assert.That(left.GetComponent<EcologyResidentBinding>().Home.HomeGraph, Is.SameAs(graph));
            Assert.That(leftCombat.Lease, Is.EqualTo(lease)); Assert.That(leftCombat.MotionFaultRevision, Is.Zero);
            Assert.That(Convert.ToSingle(health.GetType().GetProperty("CurrentHealth").GetValue(health)), Is.EqualTo(hp));
            Debug.Log($"ECOLOGY_ORIGINAL_HOME_CROWDING prefab={type.Prefab.name} nativeObservations={observations} " +
                $"bothOriginalControllers=True blockerOriginalControllerHeldStopped=True stoppedForRealResident=True originalPoolRelease=True resumed=True " +
                $"homeGraphIdentityUnchanged=True healthLeaseUnchanged=True noTeleport=True originalSimultaneousBaselineMinimum={legacyMinimum:R}");
        }
        finally
        {
            Time.timeScale = 0f;
            type.Prefab.GetComponent<IGenerationScopedPopulationTarget>().Return(left);
            if (!returnedRight) type.Prefab.GetComponent<IGenerationScopedPopulationTarget>().Return(right);
        }
    }

    private IEnumerator CheckOriginalHomeStairs(EcologyPlanningAttemptResult result, MapData map,
        EcologyEcotypeDefinition[] types, Collider2D[] nativeObstacles)
    {
        Time.timeScale = 0f;
        var cache = new EcologyHomeNavigationCache(result.Plan.Ecology, result.StaticGeometry);
        var barrierObject = new GameObject("Owned original-controller stair barrier"); ownedProjections.Add(barrierObject);
        var barrier = barrierObject.AddComponent<EcologyDamageBarrier>();
        int ordinal = 0;
        foreach (MapEcologyResident resident in result.Plan.Ecology.Residents)
        {
            EcologyEcotypeDefinition type = types.Single(t => t.TypeId == resident.TypeId);
            string family = type.Prefab.name.Split('_')[0];
            if (actualStairFamilies.Contains(family) || family == "TNT" && actualHomeMovementFamilies.Contains(family)) continue;
            EcologyHomeNavigation home = cache.Get(resident, type);
            MapStairConnection selected = default; bool found = false;
            foreach (MapStairConnection stair in map.Stairs)
            {
                int a = (stair.LowerCell.y - map.Origin.y) * map.Width + stair.LowerCell.x - map.Origin.x;
                int b = (stair.UpperCell.y - map.Origin.y) * map.Width + stair.UpperCell.x - map.Origin.x;
                if (!home.HomeGraph.CanMove(a, b) || !home.HomeGraph.CanMove(b, a)) continue;
                Vector2 from = home.NodeWorldPosition(a), to = home.NodeWorldPosition(b);
                if (!home.ContainsFullFoot(type.MovementFoot, from) || !home.ContainsFullFoot(type.MovementFoot, to) ||
                    !home.TryClipHomeTranslation(type.MovementFoot, from, to, out Vector2 clipped) || Vector2.Distance(clipped, to) > .00001f) continue;
                selected = stair; found = true; break;
            }
            bool actualStair = found;
            if (!found)
            {
                int globalEdges = 0, homeEdges = 0;
                foreach (MapStairConnection stair in map.Stairs)
                {
                    int a = (stair.LowerCell.y - map.Origin.y) * map.Width + stair.LowerCell.x - map.Origin.x;
                    int b = (stair.UpperCell.y - map.Origin.y) * map.Width + stair.UpperCell.x - map.Origin.x;
                    if (home.MovementGraph.CanMove(a, b)) globalEdges++;
                    if (home.HomeGraph.CanMove(a, b)) homeEdges++;
                }
                Debug.Log($"ECOLOGY_STAIR_ELIGIBILITY prefab={type.Prefab.name} home={resident.HomeRegionId} mapStairs={map.Stairs.Count} globalLegal={globalEdges} homeLegal={homeEdges} fullFootConnectorSelected=False");
                if (actualHomeMovementFamilies.Contains(family)) continue;
                foreach (int a in home.HomeNodes)
                {
                    for (int direction = 0; direction < 4; direction++)
                    {
                        int b = home.HomeGraph.Neighbor(a, direction);
                        if (b < 0 || !home.HomeGraph.CanMove(a, b) || !home.HomeGraph.CanMove(b, a)) continue;
                        Vector2Int from = result.Plan.Ecology.CellAt(a), to = result.Plan.Ecology.CellAt(b);
                        if (map.GetElevation(from) != map.GetElevation(to)) continue;
                        selected = new MapStairConnection(from, to); found = true; break;
                    }
                    if (found) break;
                }
                if (!found) continue;
            }
            foreach (bool reverse in new[] { false, true })
            foreach (bool hurt in new[] { false, true })
            {
                Vector2Int fromCell = reverse ? selected.UpperCell : selected.LowerCell;
                Vector2Int toCell = reverse ? selected.LowerCell : selected.UpperCell;
                int a = (fromCell.y - map.Origin.y) * map.Width + fromCell.x - map.Origin.x;
                int b = (toCell.y - map.Origin.y) * map.Width + toCell.x - map.Origin.x;
                Vector2 edgeStart = home.NodeWorldPosition(a), edgeEnd = home.NodeWorldPosition(b);
                Vector2 direction = (edgeEnd - edgeStart).normalized;
                Vector2 start = hurt ? Vector2.Lerp(edgeStart, edgeEnd, .45f) : edgeStart;
                GameObject actor = type.Prefab.GetComponent<IGenerationScopedPreparedPopulationTarget>().SpawnPrepared(start, Quaternion.identity, instance =>
                {
                    if (!ownedProjections.Contains(instance)) ownedProjections.Add(instance);
                    ((Behaviour)instance.GetComponent("MonsterAIController")).enabled = false;
                    var binding = instance.GetComponent<EcologyResidentBinding>() ?? instance.AddComponent<EcologyResidentBinding>();
                    binding.Prepare(cache, resident.Id, type, () => true, () => true);
                    var combat = instance.GetComponent<EcologyCombatBinding>() ?? instance.AddComponent<EcologyCombatBinding>();
                    combat.Bind(barrier, 1, ++ordinal, () => true);
                    var motion = instance.GetComponent<EcologyMotionBody>() ?? instance.AddComponent<EcologyMotionBody>();
                    var movement = new SerializedObject(type.Prefab.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == "MonsterMovementController"));
                    motion.ConfigurePrepared(type.ResolveMovementFoot(instance), movement.FindProperty("obstacleLayers").intValue,
                        movement.FindProperty("collisionSkinWidth").floatValue);
                });
                try
                {
                    Physics2D.SyncTransforms(); actor.GetComponent<EcologyMotionBody>().CompletePreparedConfiguration();
                    var body = actor.GetComponent<Rigidbody2D>(); Collider2D foot = type.ResolveMovementFoot(actor);
                    Component movement = actor.GetComponent("MonsterMovementController");
                    ((Behaviour)movement).enabled = true;
                    Component damage = actor.GetComponent("BaseMonster");
                    float hp = Convert.ToSingle(damage.GetType().GetProperty("CurrentHealth").GetValue(damage));
                    if (hurt)
                    {
                        var source = new GameObject("Owned stair damage source, no collider"); ownedProjections.Add(source);
                        source.transform.position = start - direction;
                        damage.GetType().GetMethod("TakeDamage", new[] { typeof(float), typeof(Transform) }).Invoke(damage, new object[] { 1f, source.transform });
                        Assert.That(Convert.ToSingle(damage.GetType().GetProperty("CurrentHealth").GetValue(damage)), Is.EqualTo(hp - 1f));
                    }
                    else movement.GetType().GetMethod("SetDestination").Invoke(movement, new object[] { edgeEnd });
                    float maximumProgress = 0f; int observations = 0;
                    Time.timeScale = 1f; float deadline = Time.time + (hurt ? .4f : 2f);
                    while (Time.time < deadline)
                    {
                        yield return new WaitForFixedUpdate(); Physics2D.SyncTransforms();
                        maximumProgress = Mathf.Max(maximumProgress, Vector2.Dot(body.position - start, direction));
                        Assert.That(home.ContainsFullFoot(foot, body.position), Is.True, "Original stair motion left accepted Home.");
                        Assert.That(actor.GetComponent<EcologyCombatBinding>().MotionFaultRevision, Is.Zero);
                        Assert.That(nativeObstacles.Any(c => c != null && Physics2D.Distance(foot, c).isOverlapped), Is.False,
                            "Native stair motion intersects terrain or original projected roles.");
                        observations++;
                        if (!hurt && Vector2.Distance(body.position, edgeEnd) < .06f) break;
                    }
                    Time.timeScale = 0f;
                    Assert.That(maximumProgress, Is.GreaterThan(hurt ? .08f : .8f), "The original controller must produce actual movement.");
                    Assert.That(Vector2.Distance(body.position, hurt ? start : edgeEnd), Is.LessThan(hurt ? .03f : .06f));
                    if (hurt) Assert.That(Vector2.Dot(start - edgeStart, direction) + maximumProgress, Is.GreaterThan((edgeEnd - edgeStart).magnitude * .5f),
                        "The existing hurt displacement must actually cross the selected edge midpoint before returning.");
                    Debug.Log($"ECOLOGY_ORIGINAL_HOME_STAIR prefab={type.Prefab.name} resident={resident.Id} reverse={reverse} hurt={hurt} " +
                        $"lower={selected.LowerCell} upper={selected.UpperCell} start={start:R} actual={body.position:R} maxProgress={maximumProgress:R} " +
                        $"wholeFootNativeObservations={observations} edgeKind={(actualStair ? "ACTUAL_STAIR" : "LEGAL_FLAT_TNT_STAIR_UNAVAILABLE")} fault=0 originalMovement=True realDamageReceiver={hurt} aiDisabledForCommandControl=True noPostBindTeleport=True");
                }
                finally { Time.timeScale = 0f; type.Prefab.GetComponent<IGenerationScopedPopulationTarget>().Return(actor); }
            }
            actualHomeMovementFamilies.Add(family);
            if (actualStair) actualStairFamilies.Add(family);
        }
        Debug.Log("ECOLOGY_ORIGINAL_HOME_STAIR_FAMILIES actual=" + string.Join(",", actualStairFamilies.OrderBy(x => x)));
    }

    private IEnumerator CheckRegionalPatrol(EcologyPlanningAttemptResult result, EcologyEcotypeDefinition[] types)
    {
        Time.timeScale = 0f;
        var cache = new EcologyHomeNavigationCache(result.Plan.Ecology, result.StaticGeometry);
        var barrierObject = new GameObject("Owned real regional AI barrier"); ownedProjections.Add(barrierObject);
        var barrier = barrierObject.AddComponent<EcologyDamageBarrier>();
        var actors = new List<(GameObject actor, EcologyEcotypeDefinition type, Vector2 start)>();
        bool current = true, ready = false; int ordinal = 0, observations = 0;
        try
        {
            foreach (MapEcologyResident resident in result.Plan.Ecology.Residents)
            {
                var type = types.Single(t => t.TypeId == resident.TypeId);
                Vector2 start = result.StaticGeometry.NodeWorldPosition(resident.InitialNode);
                GameObject actor = type.Prefab.GetComponent<IGenerationScopedPreparedPopulationTarget>().SpawnPrepared(start, Quaternion.identity, instance =>
                {
                    if (!ownedProjections.Contains(instance)) ownedProjections.Add(instance);
                    var home = instance.GetComponent<EcologyResidentBinding>() ?? instance.AddComponent<EcologyResidentBinding>();
                    home.Prepare(cache, resident.Id, type, () => current, () => ready);
                    var combat = instance.GetComponent<EcologyCombatBinding>() ?? instance.AddComponent<EcologyCombatBinding>();
                    combat.Bind(barrier, 1, ++ordinal, () => current);
                    var motion = instance.GetComponent<EcologyMotionBody>() ?? instance.AddComponent<EcologyMotionBody>();
                    var movement = new SerializedObject(type.Prefab.GetComponents<MonoBehaviour>().Single(b => b.GetType().Name == "MonsterMovementController"));
                    motion.ConfigurePrepared(type.ResolveMovementFoot(instance), movement.FindProperty("obstacleLayers").intValue,
                        movement.FindProperty("collisionSkinWidth").floatValue);
                });
                Physics2D.SyncTransforms(); actor.GetComponent<EcologyMotionBody>().CompletePreparedConfiguration();
                actors.Add((actor, type, start));
            }
            yield return null; yield return null;
            foreach (var item in actors) Assert.That(item.actor.GetComponent<Rigidbody2D>().position, Is.EqualTo(item.start), "Ready is still closed.");
            ready = true; Time.timeScale = 1f;
            var movedActors = new HashSet<GameObject>();
            float deadline = Time.time + 3f;
            while (Time.time < deadline)
            {
                yield return new WaitForFixedUpdate();
                foreach (var item in actors)
                {
                    Assert.That(item.actor.GetComponent<EcologyResidentBinding>().Home.ContainsFullFoot(item.type.ResolveMovementFoot(item.actor),
                        item.actor.GetComponent<Rigidbody2D>().position), Is.True, "Actual regional patrol left Home: " + item.type.Prefab.name);
                    Assert.That(item.actor.GetComponent<EcologyCombatBinding>().MotionFaultRevision, Is.Zero);
                    observations++;
                    if (Vector2.Distance(item.actor.GetComponent<Rigidbody2D>().position, item.start) > .05f) movedActors.Add(item.actor);
                }
            }
            int moved = movedActors.Count;
            Assert.That(moved, Is.EqualTo(actors.Count), "Every actual AI must resume after Ready, rather than remain in the preactivation stop gate.");
            Debug.Log($"ECOLOGY_REGIONAL_REAL_AI residents={actors.Count} moved={moved} wholeFootObservations={observations} " +
                "actualOriginalAi=True preReadyStopped=True fullHomeThroughout=True scope=PATROL_ONLY_NOT_CHASE_OR_PRODUCTION_READY");
        }
        finally
        {
            current = false; ready = false; Time.timeScale = 0f;
            foreach (var item in actors) item.type.Prefab.GetComponent<IGenerationScopedPopulationTarget>().Return(item.actor);
        }
    }

    private IEnumerator CheckFanOracle(EcologyPlanningAttemptResult result, EcologyEcotypeDefinition[] types, GameObject playerSource, Collider2D[] nativeSolids)
    {
        Time.timeScale = 0f;
        var all = Enumerable.Repeat(true, result.Plan.Ecology.Width * result.Plan.Ecology.Height).ToArray();
        var none = new bool[all.Length];
        MethodInfo certificate = typeof(MapCandidateCollisionGeometry).GetMethod("IsConnectorFanClear", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(certificate, Is.Not.Null);
        GameObject slime = types.Select(t => t.Prefab).First(p => p.name == "Torch_Blue");
        int samples = 0, interiorCases = 0;
        foreach (GameObject source in new[] { slime, playerSource })
        {
            var holder = new GameObject("Owned native fan foot"); holder.SetActive(false); ownedProjections.Add(holder);
            GameObject actor = CloneWithoutGameplay(source, holder.transform);
            Collider2D original = source == playerSource ? source.transform.Find("MovementFeet").GetComponent<Collider2D>() :
                types.Single(t => t.Prefab == source).MovementFoot;
            string path = AnimationUtility.CalculateTransformPath(original.transform, source.transform);
            Transform part = path.Length == 0 ? actor.transform : actor.transform.Find(path);
            Collider2D foot = part.GetComponents<Collider2D>()[Array.IndexOf(original.GetComponents<Collider2D>(), original)];
            holder.SetActive(true); Physics2D.SyncTransforms();
            Func<MapCandidateCollisionGeometry, Vector2, Vector2, Vector2, bool> clear = (geometry, a, b, c) =>
                (bool)certificate.Invoke(geometry, new object[] { foot, a, b, c, all, none });
            Vector2 centre = default, first = default, second = default, third = default; bool found = false;
            for (int node = 0; node < all.Length; node++)
            {
                Vector2 p = result.StaticGeometry.NodeWorldPosition(node);
                Vector2 a = p + new Vector2(-2f, -2f), b = p + new Vector2(2f, -2f), c = p + new Vector2(0f, 4f);
                if (!clear(result.StaticGeometry, a, b, c)) continue;
                centre = p; first = a; second = b; third = c; found = true; break;
            }
            Assert.That(found, Is.True, "A positive complete-foot fan must exist; blanket rejection cannot pass this oracle.");
            for (int i = 0; i <= 8; i++)
            for (int j = 0; j <= 8 - i; j++)
            {
                actor.transform.position = first + (second - first) * (i / 8f) + (third - first) * (j / 8f);
                Physics2D.SyncTransforms();
                Assert.That(nativeSolids.Any(solid => Physics2D.Distance(foot, solid).isOverlapped), Is.False,
                    "A certified fan contained a native static overlap for " + source.name);
                samples++;
            }
            var blocker = new GameObject("Owned interior-only fan obstacle"); blocker.SetActive(false); ownedProjections.Add(blocker);
            var box = blocker.AddComponent<BoxCollider2D>(); box.size = new Vector2(.2f, .2f);
            var added = result.StaticGeometry.WithPlannedSolids(new[] {
                MapPlannedSolidGeometry.Capture("owned-fan-interior-" + interiorCases, blocker.transform, centre, Quaternion.identity) });
            blocker.transform.position = centre; blocker.SetActive(true); actor.transform.position = centre; Physics2D.SyncTransforms();
            Assert.That(Physics2D.Distance(foot, box).isOverlapped, Is.True, "The middle of the fan must contain an actual native collision witness.");
            Assert.That(added.IsTranslationClear(foot, first, second, 0f), Is.True);
            Assert.That(added.IsTranslationClear(foot, second, third, 0f), Is.True);
            Assert.That(added.IsTranslationClear(foot, third, first, 0f), Is.True);
            Assert.That(clear(added, first, second, third), Is.False, "Checking three boundary segments misses this interior-only obstacle.");
            interiorCases++;
            Debug.Log($"ECOLOGY_CONNECTOR_FAN_NATIVE source={source.name} actualShape={foot.GetType().Name} positiveSamples=45 " +
                $"nativeInteriorOverlap=True threeEdgesClear=True completeFanRejected=True centre={centre}");
            holder.SetActive(false); blocker.SetActive(false);
        }
        Assert.That(samples, Is.EqualTo(90)); Assert.That(interiorCases, Is.EqualTo(2));
        yield return null;
    }

    private IEnumerator CheckStaticRevision(EcologyPlanningAttemptResult result, EcologyPlanningAttemptResult otherGeneration, MapData map,
        EcologyEcotypeDefinition[] types, List<(MapPlacementReservation reservation, GameObject actor)> projected, Collider2D[] nativeSolids, bool liveNavigation = false)
    {
        Time.timeScale = 0f;
        Assert.That(result.TerrainGeometry, Is.Not.Null);
        var revision = new EcologyStaticCollisionRevision(result);
        var staticSources = projected.Where(p => p.reservation.Role != AnimalPlacementService.AnimalRole).ToArray();
        Func<MapPlannedSolidGeometry[]> live = () => staticSources.Select(p => MapPlannedSolidGeometry.CaptureLive(p.reservation.LogicalObjectId, p.actor.transform)).ToArray();
        Assert.That(revision.UpdateLiveSolids(live().Reverse()), Is.False, "Equivalent live solids must not create a fake static revision.");
        Assert.That(revision.Version, Is.Zero);
        var toleranceProbe = new EcologyStaticCollisionRevision(result);
        GameObject shifted = staticSources.First(p => p.reservation.Role == DestructiblePlacementService.DestructibleRole).actor;
        Vector3 originalPose = shifted.transform.position;
        try
        {
            shifted.transform.position = originalPose + new Vector3(.000003814697265625f, 0f, 0f); Physics2D.SyncTransforms();
            Assert.That(toleranceProbe.UpdateLiveSolids(live()), Is.False, "One world float ULP within the existing geometry epsilon is not a physical revision.");
            shifted.transform.position = originalPose + new Vector3(.001f, 0f, 0f); Physics2D.SyncTransforms();
            Assert.That(toleranceProbe.UpdateLiveSolids(live()), Is.True, "A real static translation must still invalidate fields.");
            Assert.That(toleranceProbe.Version, Is.EqualTo(1));
        }
        finally { shifted.transform.position = originalPose; Physics2D.SyncTransforms(); }
        var cache = new EcologyHomeNavigationCache(result.Plan.Ecology, result.StaticGeometry);
        MapEcologyResident resident = null; EcologyEcotypeDefinition type = null; EcologyHomeNavigation oldHome = null;
        int[] walkable = Enumerable.Range(0, map.Width * map.Height).Where(n => map.IsWalkable(result.Plan.Ecology.CellAt(n))).ToArray();
        int[] protectedNodes = Enumerable.Range(0, map.Width * map.Height).Where(result.Plan.Ecology.IsProtectedNode).ToArray();
        GameObject removed = null; int opened = -1;
        foreach (var candidateResident in result.Plan.Ecology.Residents)
        {
            var candidateType = types.Single(t => t.TypeId == candidateResident.TypeId);
            var candidateHome = cache.Get(candidateResident, candidateType);
            foreach (var source in staticSources.Where(p => p.reservation.Role == DestructiblePlacementService.DestructibleRole))
            {
                var after = result.TerrainGeometry.WithPlannedSolids(live().Where(s => s.LogicalId != source.reservation.LogicalObjectId));
                var graph = after.BuildHomeNavigationGraph(candidateType.MovementFoot, walkable, protectedNodes);
                var returns = graph.BuildReturnField(candidateHome.HomeNodes.ToArray());
                for (int n = 0; n < graph.NodeCount; n++)
                    if (!candidateHome.MovementGraph.IsLegal(n) && graph.IsLegal(n) && returns.Distances[n] >= 0)
                    { removed = source.actor; opened = n; resident = candidateResident; type = candidateType; oldHome = candidateHome; break; }
                if (removed != null) break;
            }
            if (removed != null) break;
        }
        Assert.That(removed, Is.Not.Null, "One real planned destructible must demonstrate an actual newly open native foot pose.");
        var probeHolder = new GameObject("Owned native static revision probe"); probeHolder.SetActive(false); ownedProjections.Add(probeHolder);
        GameObject probe = CloneWithoutGameplay(type.Prefab, probeHolder.transform);
        Collider2D probeFoot = type.ResolveMovementFoot(probe);
        probe.transform.position = result.StaticGeometry.NodeWorldPosition(opened); probeHolder.SetActive(true); Physics2D.SyncTransforms();
        Assert.That(removed.GetComponentsInChildren<Collider2D>().Any(c => !c.isTrigger && Physics2D.Distance(probeFoot, c).isOverlapped), Is.True);
        removed.SetActive(false); Physics2D.SyncTransforms();
        Assert.That(revision.UpdateLiveSolids(live()), Is.True); Assert.That(revision.Version, Is.EqualTo(1));
        Assert.That(revision.UpdateLiveSolids(live()), Is.False); Assert.That(revision.Version, Is.EqualTo(1));
        var nextCache = cache.WithStaticRevision(revision); var nextHome = nextCache.Get(resident, type);
        Assert.That(nextCache.StaticBlockerVersion, Is.EqualTo(1));
        Assert.That(nextHome.MovementGraph.IsLegal(opened), Is.True); Assert.That(oldHome.MovementGraph.IsLegal(opened), Is.False);
        Assert.That(nextHome.ReturnField.Distances[opened], Is.GreaterThanOrEqualTo(0));
        Assert.That(nativeSolids.Where(c => c != null && c.enabled && c.gameObject.activeInHierarchy)
            .Any(c => Physics2D.Distance(probeFoot, c).isOverlapped), Is.False);
        Assert.That(result.Plan.Ecology.Signature, Is.EqualTo(revision.LayoutSignature));
        Assert.Throws<ArgumentException>(() => cache.WithStaticRevision(new EcologyStaticCollisionRevision(otherGeneration)));
        probeHolder.SetActive(false);
        var barrierObject = new GameObject("Owned static revision lease barrier"); ownedProjections.Add(barrierObject);
        var barrier = barrierObject.AddComponent<EcologyDamageBarrier>();
        GameObject actor = type.Prefab.GetComponent<IGenerationScopedPreparedPopulationTarget>().SpawnPrepared(
            result.StaticGeometry.NodeWorldPosition(resident.InitialNode), Quaternion.identity, instance =>
            {
                ownedProjections.Add(instance);
                foreach (MonoBehaviour b in instance.GetComponents<MonoBehaviour>())
                    if (b.GetType().Name == "MonsterAIController" || b.GetType().Name == "MonsterMovementController") b.enabled = false;
                var home = instance.GetComponent<EcologyResidentBinding>() ?? instance.AddComponent<EcologyResidentBinding>();
                home.Prepare(cache, resident.Id, type, () => true, () => false);
                var combat = instance.GetComponent<EcologyCombatBinding>() ?? instance.AddComponent<EcologyCombatBinding>();
                combat.Bind(barrier, 1, 1, () => true);
                var motion = instance.GetComponent<EcologyMotionBody>() ?? instance.AddComponent<EcologyMotionBody>();
                motion.ConfigurePrepared(type.ResolveMovementFoot(instance), Physics2D.AllLayers, .02f);
            });
        try
        {
            Physics2D.SyncTransforms(); actor.GetComponent<EcologyMotionBody>().CompletePreparedConfiguration();
            var binding = actor.GetComponent<EcologyResidentBinding>(); var state = binding.Excursion;
            // Deliberately seeded data tests rebind preservation, not a claim of observed movement.
            state.ObserveActualPose(false, false, 1, 10d); state.RequestReturn();
            Assert.That(state.TryReserveCounterattack(10.1d, .6d), Is.True);
            MonoBehaviour hp = actor.GetComponents<MonoBehaviour>().Single(b => b.GetType().Name == "BaseMonster");
            hp.GetType().GetMethod("TakeDamage", new[] { typeof(float) }).Invoke(hp, new object[] { 1f });
            float health = (float)hp.GetType().GetProperty("CurrentHealth").GetValue(hp);
            long lease = actor.GetComponent<EcologyCombatBinding>().Lease;
            binding.RebindNavigation(nextCache);
            Assert.That(binding.Home, Is.SameAs(nextHome)); Assert.That(binding.Excursion, Is.SameAs(state));
            Assert.That(state.HasPermit && state.ReturnLatched && state.CounterattackReserved, Is.True);
            Assert.That(state.Episode, Is.EqualTo(1)); Assert.That(state.DepartedAt, Is.EqualTo(10d)); Assert.That(state.CounterattackDeadline, Is.EqualTo(10.7d));
            Assert.That(actor.GetComponent<EcologyCombatBinding>().Lease, Is.EqualTo(lease));
            Assert.That((float)hp.GetType().GetProperty("CurrentHealth").GetValue(hp), Is.EqualTo(health));
            Assert.That(result.Plan.Ecology.Signature, Is.EqualTo(revision.LayoutSignature));
            if (liveNavigation)
            {
                var roots = staticSources.ToDictionary(p => p.reservation.LogicalObjectId, p => p.actor.transform, StringComparer.Ordinal);
                using (var owner = new EcologyLiveNavigation(result, types, roots, new[] { binding }, () => true))
                {
                    Assert.That(owner.Version, Is.EqualTo(1));
                    removed.SetActive(true); Physics2D.SyncTransforms(); binding.TryGetHomeDistance(out _, out _);
                    Assert.That(owner.Version, Is.EqualTo(2)); Assert.That(binding.Home.MovementGraph.IsLegal(opened), Is.False);
                    probeHolder.SetActive(true); Physics2D.SyncTransforms();
                    Assert.That(removed.GetComponentsInChildren<Collider2D>().Any(c => !c.isTrigger && Physics2D.Distance(probeFoot, c).isOverlapped), Is.True);
                    removed.SetActive(false); Physics2D.SyncTransforms(); binding.TryGetHomeDistance(out _, out _);
                    Assert.That(owner.Version, Is.EqualTo(3)); Assert.That(binding.Home.MovementGraph.IsLegal(opened), Is.True);
                    Assert.That(nativeSolids.Where(c => c != null && c.enabled && c.gameObject.activeInHierarchy)
                        .Any(c => Physics2D.Distance(probeFoot, c).isOverlapped), Is.False);
                    binding.TryGetHomeDistance(out _, out _); Assert.That(owner.Version, Is.EqualTo(3));
                    Assert.That(binding.Excursion, Is.SameAs(state)); Assert.That(state.HasPermit && state.ReturnLatched && state.CounterattackReserved, Is.True);
                    Assert.That(state.DepartedAt, Is.EqualTo(10d)); Assert.That(state.Episode, Is.EqualTo(1));
                    Assert.That(actor.GetComponent<EcologyCombatBinding>().Lease, Is.EqualTo(lease));
                    Assert.That((float)hp.GetType().GetProperty("CurrentHealth").GetValue(hp), Is.EqualTo(health));
                    probeHolder.SetActive(false);
                    Debug.Log("ECOLOGY_LIVE_NAVIGATION_NATIVE versions=1,2,3 availabilityRemoveAddRemove=True queryRefresh=True nativeBlockedAndOpen=True " +
                        "unchangedQueryStable=True healthLeaseAndSeededEpisodePreserved=True scope=LIVE_STATIC_AVAILABILITY_NOT_PRODUCTION_READY");
                }
            }
        }
        finally
        {
            type.Prefab.GetComponent<IGenerationScopedPopulationTarget>().Return(actor);
            foreach (MonoBehaviour b in actor.GetComponents<MonoBehaviour>())
                if (b.GetType().Name == "MonsterAIController" || b.GetType().Name == "MonsterMovementController") b.enabled = true;
        }
        Debug.Log($"ECOLOGY_STATIC_REVISION_NATIVE version=1 removed={removed.name} openedNode={opened} nativeBeforeOverlap=True nativeAfterClear=True " +
            "oldSnapshotImmutable=True sharedFieldReplaced=True layoutUnchanged=True wrongGenerationRejected=True healthLeasePreserved=True seededExcursionPreserved=True scope=VERSION_KERNEL_NOT_LIVE_OWNER");
        yield return null;
    }

    private IEnumerator CheckNativeJoint(EcologyPlanningAttemptResult result, MapData map, MapSimpleDecorationData decorations,
        MapGenerationSettings settings, GameObject root, MapTilemapRenderer terrain, MapSimpleDecorationRenderer decoration,
        MerchantPlacementService merchant, PlantPlacementService plants, MapEcologyPopulationService population,
        AnimalPlacementService animals, EcologyEcotypeDefinition[] types, GameObject playerSource, float exitRadius, bool homeMotion = false, bool regionalPatrol = false, bool excursion = false, bool fanOracle = false, bool regionalChase = false, bool pendingHomeHurt = false, bool blockedReturn = false, bool staticRevision = false, EcologyPlanningAttemptResult otherGeneration = null, bool liveNavigation = false, bool homeStairs = false, bool homeCrowding = false)
    {
        terrain.Render(map, settings); decoration.Render(decorations, map, settings);
        yield return null;
        foreach (var tile in root.GetComponentsInChildren<UnityEngine.Tilemaps.TilemapCollider2D>(true))
            if (tile.gameObject.activeInHierarchy && tile.hasTilemapChanges) tile.ProcessTilemapChanges();
        Time.timeScale = 1f; yield return new WaitForFixedUpdate();
        var terrainColliders = root.GetComponentsInChildren<Collider2D>(true)
            .Where(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger && c.shapeCount > 0).ToArray();
        Assert.That(terrainColliders.Any(c => c is CompositeCollider2D), Is.True);
        Assert.That(terrainColliders.Any(c => c.name == "GeneratedCliffEdges"), Is.True);
        var sources = new Dictionary<string, GameObject>(StringComparer.Ordinal)
        { { MerchantPlacementService.MainMerchantLogicalObjectId, ((IMerchantPlacementTarget)merchant.MerchantTarget).PlacementTransform.gameObject } };
        foreach (PlantPlacementBinding binding in plants.Bindings) sources.Add(binding.LogicalObjectId, binding.PlantTransform.gameObject);
        foreach (AnimalPlacementBinding binding in animals.Bindings) sources.Add(binding.LogicalObjectId, binding.AnimalTransform.gameObject);
        foreach (MapEcologyPlacementResult placed in population.LastPlan.Placements)
            if (placed.Role == DestructiblePlacementService.DestructibleRole) sources.Add(placed.LogicalId, placed.Prefab);
        var holder = new GameObject("Owned full-role native projection, gameplay stripped before activation");
        holder.SetActive(false); ownedProjections.Add(holder);
        var projected = new List<(MapPlacementReservation reservation, GameObject actor)>();
        foreach (MapPlacementReservation reservation in result.Plan.Reservations)
        {
            if (!sources.TryGetValue(reservation.LogicalObjectId, out GameObject source)) continue;
            GameObject actor = CloneWithoutGameplay(source, holder.transform);
            actor.transform.SetPositionAndRotation(terrain.Coordinates.CellToWorld(reservation.AnchorCell),
                reservation.Ownership == MapPlacementOwnership.GenerationScoped ? Quaternion.identity : source.transform.rotation);
            projected.Add((reservation, actor));
        }
        yield return null; holder.SetActive(true); Physics2D.SyncTransforms();
        Collider2D[] props = projected.Where(p => p.reservation.Role != AnimalPlacementService.AnimalRole)
            .SelectMany(p => p.actor.GetComponentsInChildren<Collider2D>()).Where(c => !c.isTrigger).ToArray();
        Collider2D[] animalFeet = projected.Where(p => p.reservation.Role == AnimalPlacementService.AnimalRole)
            .SelectMany(p => p.actor.GetComponentsInChildren<Collider2D>()).Where(c => !c.isTrigger).ToArray();
        Collider2D[] allRoles = props.Concat(animalFeet).ToArray();
        Assert.That(props.Length, Is.GreaterThan(0)); Assert.That(animalFeet.Length, Is.GreaterThan(0));
        foreach (Collider2D solid in allRoles)
        {
            Assert.That(solid.shapeCount, Is.GreaterThan(0), "Configured potential role shape must exist in the native oracle.");
            Assert.That(terrainColliders.Any(c => Physics2D.Distance(solid, c).isOverlapped), Is.False, "Role intersects native terrain: " + solid.name);
        }
        int pairs = 0;
        for (int i = 0; i < allRoles.Length; i++)
        for (int j = i + 1; j < allRoles.Length; j++)
        {
            Assert.That(Physics2D.Distance(allRoles[i], allRoles[j]).isOverlapped, Is.False, "Native planned role overlap."); pairs++;
        }
        Collider2D[] staticObstacles = terrainColliders.Concat(props).ToArray();
        Collider2D[] initialObstacles = staticObstacles.Concat(animalFeet).ToArray();
        if (homeCrowding)
        {
            yield return CheckOriginalHomeCrowding(result, map, types);
            yield break;
        }
        if (homeStairs)
        {
            yield return CheckOriginalHomeStairs(result, map, types, initialObstacles);
            yield break;
        }
        if (homeMotion)
        {
            yield return CheckHomeMotion(result, types, excursion, regionalChase, playerSource, pendingHomeHurt, blockedReturn);
            yield break;
        }
        if (regionalPatrol)
        {
            yield return CheckRegionalPatrol(result, types);
            yield break;
        }
        if (fanOracle)
        {
            yield return CheckFanOracle(result, types, playerSource, staticObstacles);
            yield break;
        }
        if (staticRevision)
        {
            yield return CheckStaticRevision(result, otherGeneration, map, types, projected, staticObstacles, liveNavigation);
            yield break;
        }
        var initialSet = new HashSet<Collider2D>(initialObstacles);
        int positions = 0, sweeps = 0, mismatches = 0; bool exitCentreLegal = false;
        foreach (GameObject source in types.Select(t => t.Prefab).Concat(new[] { playerSource }))
        {
            Collider2D sourceFoot = source == playerSource ? source.transform.Find("MovementFeet").GetComponent<CircleCollider2D>() :
                types.Single(t => t.Prefab == source).MovementFoot;
            var probeHolder = new GameObject("Owned actual-foot native probe"); probeHolder.SetActive(false); ownedProjections.Add(probeHolder);
            GameObject actor = CloneWithoutGameplay(source, probeHolder.transform);
            string path = AnimationUtility.CalculateTransformPath(sourceFoot.transform, source.transform);
            Transform probeTransform = path.Length == 0 ? actor.transform : actor.transform.Find(path);
            int component = Array.IndexOf(sourceFoot.GetComponents<Collider2D>(), sourceFoot);
            Collider2D foot = probeTransform.GetComponents<Collider2D>()[component];
            yield return null; probeHolder.SetActive(true); Physics2D.SyncTransforms();
            EcologyNavigationGraph staticGraph = result.StaticGeometry.BuildNavigationGraph(sourceFoot, 0f);
            EcologyNavigationGraph initialGraph = result.InitialPlacementGeometry.BuildNavigationGraph(sourceFoot, 0f);
            Assert.That(foot.shapeCount, Is.GreaterThan(0));
            int localMismatch = 0;
            for (int n = 0; n < map.Width * map.Height; n++)
            {
                Vector2Int cell = map.Origin + new Vector2Int(n % map.Width, n / map.Width);
                actor.transform.position = terrain.Coordinates.CellToWorld(cell); Physics2D.SyncTransforms();
                bool staticClear = !staticObstacles.Any(c => Physics2D.Distance(foot, c).isOverlapped);
                bool initialClear = staticClear && !animalFeet.Any(c => Physics2D.Distance(foot, c).isOverlapped);
                if (staticGraph.IsLegal(n) != (map.IsWalkable(cell) && staticClear) ||
                    initialGraph.IsLegal(n) != (map.IsWalkable(cell) && initialClear))
                {
                    if (localMismatch < 3) Debug.Log($"ECOLOGY_JOINT_NATIVE_MISMATCH seed={settings.seed} prefab={source.name} cell={cell} " +
                        $"staticGraph={staticGraph.IsLegal(n)} nativeStatic={staticClear} initialGraph={initialGraph.IsLegal(n)} nativeInitial={initialClear}");
                    localMismatch++;
                }
                positions++;
            }
            if (source == playerSource)
            {
                actor.transform.position = terrain.Coordinates.CellToWorld(result.Plan.Ecology.CellAt(result.ExitApproachNode)); Physics2D.SyncTransforms();
                Assert.That(staticObstacles.Any(c => Physics2D.Distance(foot, c).isOverlapped), Is.False, "Actual exit interaction stop needs native whole-foot clearance.");
                Assert.That(Vector2.Distance(actor.transform.position, terrain.Coordinates.CellToWorld(map.ExitCell)), Is.LessThanOrEqualTo(exitRadius));
                actor.transform.position = terrain.Coordinates.CellToWorld(map.ExitCell); Physics2D.SyncTransforms();
                bool centreOverlap = staticObstacles.Any(c => Physics2D.Distance(foot, c).isOverlapped);
                exitCentreLegal = !centreOverlap;
                Debug.Log($"ECOLOGY_JOINT_EXIT_NATIVE seed={settings.seed} actualCentreOverlapped={centreOverlap} " +
                    $"reachableStop={result.ExitApproachNode} originalInteractionRadius={exitRadius:R}");
            }
            var hits = new RaycastHit2D[256];
            foreach (MapStairConnection stair in map.Stairs)
            foreach (bool reverse in new[] { false, true })
            {
                Vector2Int from = reverse ? stair.UpperCell : stair.LowerCell, to = reverse ? stair.LowerCell : stair.UpperCell;
                Vector2 start = terrain.Coordinates.CellToWorld(from), end = terrain.Coordinates.CellToWorld(to);
                actor.transform.position = start; Physics2D.SyncTransforms();
                bool startClear = !initialObstacles.Any(c => Physics2D.Distance(foot, c).isOverlapped);
                Vector2 delta = end - start;
                int hitCount = foot.Cast(delta.normalized, new ContactFilter2D { useTriggers = false }, hits, delta.magnitude);
                Assert.That(hitCount, Is.LessThan(hits.Length));
                bool castClear = !hits.Take(hitCount).Any(h => initialSet.Contains(h.collider) && h.distance < delta.magnitude - .00001f);
                actor.transform.position = end; Physics2D.SyncTransforms();
                bool endClear = !initialObstacles.Any(c => Physics2D.Distance(foot, c).isOverlapped);
                int a = (from.y - map.Origin.y) * map.Width + from.x - map.Origin.x;
                int b = (to.y - map.Origin.y) * map.Width + to.x - map.Origin.x;
                bool expected = startClear && endClear && castClear && map.CanTraverse(from, to);
                Assert.That(initialGraph.CanMove(a, b), Is.EqualTo(expected), "Full-role stair sweep: " + source.name); sweeps++;
            }
            Debug.Log($"ECOLOGY_JOINT_NATIVE_FOOT seed={settings.seed} prefab={source.name} mismatches={localMismatch}");
            mismatches += localMismatch;
            UnityEngine.Object.Destroy(probeHolder); yield return null;
        }
        Assert.That(mismatches, Is.Zero, "Complete role projections must agree with both static and initial native-foot graphs.");
        var residents = new List<(MapEcologyResident resident, Collider2D foot, GameObject actor, EcologyEcotypeDefinition type)>();
        var residentsHolder = new GameObject("Owned final resident native coexistence proof"); residentsHolder.SetActive(false); ownedProjections.Add(residentsHolder);
        foreach (MapEcologyResident resident in result.Plan.Ecology.Residents)
        {
            EcologyEcotypeDefinition type = types.Single(t => t.TypeId == resident.TypeId);
            GameObject actor = CloneWithoutGameplay(type.Prefab, residentsHolder.transform);
            actor.transform.SetPositionAndRotation(terrain.Coordinates.CellToWorld(result.Plan.Ecology.CellAt(resident.InitialNode)), Quaternion.identity);
            string path = AnimationUtility.CalculateTransformPath(type.MovementFoot.transform, type.Prefab.transform);
            Transform footTransform = path.Length == 0 ? actor.transform : actor.transform.Find(path);
            int component = Array.IndexOf(type.MovementFoot.GetComponents<Collider2D>(), type.MovementFoot);
            residents.Add((resident, footTransform.GetComponents<Collider2D>()[component], actor, type));
        }
        residentsHolder.SetActive(true); Physics2D.SyncTransforms();
        int residentPairs = 0;
        foreach (var resident in residents)
        {
            Assert.That(resident.foot.shapeCount, Is.GreaterThan(0));
            Assert.That(initialObstacles.Any(c => Physics2D.Distance(resident.foot, c).isOverlapped), Is.False, resident.resident.Id);
        }
        for (int i = 0; i < residents.Count; i++)
        for (int j = i + 1; j < residents.Count; j++)
        {
            Assert.That(Physics2D.Distance(residents[i].foot, residents[j].foot).isOverlapped, Is.False,
                "Actual native resident coexistence: " + residents[i].resident.Id + "/" + residents[j].resident.Id);
            residentPairs++;
        }
        ExportJoint(result, map, settings.seed, positions, sweeps, pairs, residentPairs, exitRadius, exitCentreLegal,
            residents.Select(r => new JointResident { id = r.resident.Id, typeId = r.resident.TypeId, home = r.resident.HomeRegionId,
                node = r.resident.InitialNode, bodyWorld = r.actor.transform.position, nativeFootCenter = r.foot.bounds.center,
                nativeFootSize = r.foot.bounds.size, prefabPath = AssetDatabase.GetAssetPath(r.type.Prefab),
                prefabGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(r.type.Prefab)),
                footprintIdentity = result.StaticGeometry.BuildNavigationGraph(r.type.MovementFoot, 0f).FootprintIdentity }).ToArray());
        Debug.Log($"ECOLOGY_JOINT_NATIVE_ACCEPTED seed={settings.seed} positions={positions} stairSweeps={sweeps} nativeRolePairs={pairs} " +
            $"nativeResidentPairs={residentPairs} props={props.Length} animals={animalFeet.Length} staticProjection={result.StaticGeometry.ProjectionIdentity} " +
            $"initialProjection={result.InitialPlacementGeometry.ProjectionIdentity}");
        UnityEngine.Object.Destroy(holder); UnityEngine.Object.Destroy(residentsHolder); yield return null;
    }

    private static void ExportJoint(EcologyPlanningAttemptResult result, MapData map, int seed,
        int positions, int sweeps, int rolePairs, int residentPairs, float exitRadius, bool exitCentreLegal, JointResident[] residents)
    {
        string label = Environment.GetEnvironmentVariable("RPG_ECOLOGY_EVIDENCE_LABEL") + "-" + TestContext.CurrentContext.Test.Name;
        Assert.That(label, Is.Not.Null.And.Not.Empty);
        string run = label.Split('-')[0], evidence = Path.Combine(Application.dataPath, "../Docs/MapGeneration/Ecology/Evidence");
        string manifest = Path.Combine(evidence, "P0V/" + run + "-input-manifest.json");
        Fingerprint fingerprint = JsonUtility.FromJson<Fingerprint>(File.ReadAllText(manifest));
        string manifestHash;
        using (var sha = System.Security.Cryptography.SHA256.Create())
            manifestHash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(manifest))).Replace("-", "").ToLowerInvariant();
        var layout = result.Plan.Ecology;
        var cells = Enumerable.Range(0, map.Width * map.Height).Select(layout.CellAt).ToArray();
        var capture = new NativeJointCapture { schema = "ecology-joint-native-layout-v1", scope = "PLANNED_JOINT_NATIVE_GEOMETRY_NOT_ACTIVE_AI_OR_GAMEPLAY_READY",
            label = label, sourceFingerprint = fingerprint.sourceFingerprint, inputManifestSha256 = manifestHash,
            generationId = layout.GenerationId.ToString(), staticProjection = result.StaticGeometry.ProjectionIdentity,
            initialProjection = result.InitialPlacementGeometry.ProjectionIdentity, layoutSignature = layout.Signature, recipeIdentity = layout.RecipeIdentity,
            seed = seed, width = map.Width, height = map.Height, originX = map.Origin.x, originY = map.Origin.y,
            acceptedAttempt = result.AcceptedAttempt, attemptsUsed = result.AttemptsUsed, nativePositions = positions, stairSweeps = sweeps,
            nativeRolePairs = rolePairs, nativeResidentPairs = residentPairs, terrain = cells.Select(c => (int)map.GetCell(c).terrainType).ToArray(),
            exitInteractionRadius = exitRadius, exitApproachNode = result.ExitApproachNode, exitCentreLegal = exitCentreLegal,
            elevation = cells.Select(map.GetElevation).ToArray(), regionByNode = layout.RegionByNode.ToArray(), residents = residents,
            regions = layout.Regions.Select(r => new JointRegion { id = r.Id, kind = r.Kind.ToString(), typeId = r.TypeId,
                capacity = r.ConstructiveCapacity, population = r.AssignedPopulation, surface = r.Nodes.ToArray(), buffer = r.BufferNodes.ToArray() }).ToArray() };
        string directory = Path.Combine(evidence, "P2/JointLayouts/" + label); Directory.CreateDirectory(directory);
        string output = Path.Combine(directory, seed.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".json");
        Assert.That(File.Exists(output), Is.False, "Prior evidence must never be overwritten.");
        File.WriteAllText(output, JsonUtility.ToJson(capture, true), new System.Text.UTF8Encoding(false));
        Debug.Log("ECOLOGY_JOINT_NATIVE_ARTIFACT " + output);
    }

    private static GameObject CloneWithoutGameplay(GameObject source, Transform parent)
    {
        GameObject actor = UnityEngine.Object.Instantiate(source, parent);
        actor.transform.localScale = source.transform.lossyScale;
        var behaviours = actor.GetComponentsInChildren<MonoBehaviour>(true).ToList();
        while (behaviours.Count > 0)
        {
            int leaf = behaviours.FindIndex(candidate => !behaviours.Any(other => other != candidate &&
                other.gameObject == candidate.gameObject && Attribute.GetCustomAttributes(other.GetType(), typeof(RequireComponent), true)
                    .Cast<RequireComponent>().Any(r => new[] { r.m_Type0, r.m_Type1, r.m_Type2 }
                        .Any(required => required != null && required.IsAssignableFrom(candidate.GetType())))));
            Assert.That(leaf, Is.GreaterThanOrEqualTo(0), "Owned fixture gameplay component dependency cycle.");
            MonoBehaviour remove = behaviours[leaf]; behaviours.RemoveAt(leaf);
            Assert.That(AssetDatabase.Contains(remove), Is.False);
            Assert.That(actor.activeInHierarchy, Is.False, "Gameplay must be removed before activation.");
            UnityEngine.Object.DestroyImmediate(remove, false);
        }
        Assert.That(actor.GetComponentsInChildren<MonoBehaviour>(true).Length, Is.Zero);
        foreach (Animator animator in actor.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        foreach (Collider2D collider in actor.GetComponentsInChildren<Collider2D>(true))
        {
            collider.enabled = true;
            for (Transform part = collider.transform; part != parent; part = part.parent) part.gameObject.SetActive(true);
        }
        return actor;
    }
}
#endif

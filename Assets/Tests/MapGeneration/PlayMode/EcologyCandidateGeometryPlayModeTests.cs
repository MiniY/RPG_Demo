#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Profiling;

public sealed class EcologyCandidateGeometryPlayModeTests
{
    [Serializable] private sealed class FingerprintHeader { public string sourceFingerprint; }
    [Serializable] private sealed class LayoutCapture
    {
        public string schema, scope, label, sourceFingerprint, inputManifestSha256, generationId, recipeIdentity,
            projectionIdentity, mapContentIdentity, layoutSignature, byteScope;
        public int seed, width, height, originX, originY, catalogCount, bossChanceDenominator, repairs, activityNodes, allocationEvents;
        public double warmGraphBuildMs;
        public long maxFrameAllocatedBytes;
        public int[] terrain, elevation, regionByNode;
        public CapturedRegion[] regions;
        public CapturedResident[] residents;
        public CapturedType[] types;
    }
    [Serializable] private sealed class CapturedRegion
    {
        public int id, capacity, population;
        public string kind, typeId;
        public int[] surface, buffer, authorizedHomeStoppingNodes;
    }
    [Serializable] private sealed class CapturedResident
    {
        public string id, typeId, nativeFootKind;
        public int home, node;
        public Vector2 bodyWorld, nativeFootCenter, nativeFootSize;
    }
    [Serializable] private sealed class CapturedType
    {
        public string typeId, prefabPath, prefabGuid, prefabName, footprintIdentity;
        public int terrains, weight;
        public bool boss;
    }
    private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
    private float previousTimeScale;
    private static bool allocationCalibrationComplete, frameByteCalibrationComplete;
    private static long rawCounterControlDelta, profilerControlEvents, frameControlBytes;

    private sealed class FrameAllocationScope : IDisposable
    {
        private readonly bool previousProfilerEnabled;
        internal ProfilerRecorder Recorder;
        internal FrameAllocationScope()
        {
            previousProfilerEnabled = UnityEngine.Profiling.Profiler.enabled;
            UnityEngine.Profiling.Profiler.enabled = true;
            try { Recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 8); }
            catch { UnityEngine.Profiling.Profiler.enabled = previousProfilerEnabled; throw; }
        }
        public void Dispose()
        {
            Recorder.Dispose(); UnityEngine.Profiling.Profiler.enabled = previousProfilerEnabled;
        }
    }

    private static long MaximumFrameBytes(ProfilerRecorder recorder)
    {
        Assert.That(recorder.Valid, Is.True); Assert.That(recorder.UnitType.ToString(), Is.EqualTo("Bytes"));
        Assert.That(recorder.Count, Is.InRange(1, recorder.Capacity - 1));
        long maximum = 0;
        for (int i = 0; i < recorder.Count; i++) maximum = Math.Max(maximum, recorder.GetSample(i).Value);
        return maximum;
    }

    [UnityTest]
    public IEnumerator AllocationMeasurementMustObserveTheControlledMegabyte()
    {
        CalibrateAllocations();
        yield return CalibrateFrameBytes();
    }

    private static ProfilerRecorder BeginAllocationRecorder(int capacity) => ProfilerRecorder.StartNew(
        ProfilerCategory.Internal, "GC.Alloc", capacity,
        ProfilerRecorderOptions.CollectOnlyOnCurrentThread | ProfilerRecorderOptions.StartImmediately);

    private static int CountAllocationEvents(ProfilerRecorder recorder)
    {
        Assert.That(recorder.Valid, Is.True, "GC.Alloc recorder must be available; missing measurement is not zero allocation.");
        Assert.That(recorder.Count, Is.LessThan(recorder.Capacity), "A saturated allocation trace is incomplete.");
        Assert.That(recorder.UnitType.ToString(), Is.EqualTo("TimeNanoseconds"), "GC.Alloc marker samples are timing events; use their count, not timing as bytes.");
        return recorder.Count;
    }

    private static void CalibrateAllocations()
    {
        if (allocationCalibrationComplete) return;
        MethodInfo rawApi = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Public | BindingFlags.Static);
        Func<long> rawCounter = rawApi == null ? null : (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), rawApi);
        const int payloadBytes = 1024 * 1024;
        using (var recorder = BeginAllocationRecorder(128))
        {
            long before = rawCounter != null ? rawCounter() : -1;
            var payload = new byte[payloadBytes]; payload[0] = 11; payload[payload.Length - 1] = 13;
            GC.KeepAlive(payload);
            rawCounterControlDelta = rawCounter != null ? rawCounter() - before : -1;
            recorder.Stop(); profilerControlEvents = CountAllocationEvents(recorder);
        }
        Debug.Log($"ECOLOGY_ALLOCATION_CONTROL payload={payloadBytes} rawThreadCounterDelta={rawCounterControlDelta} " +
                  $"profilerMainThreadAllocationEvents={profilerControlEvents} rawCounterQualified={rawCounterControlDelta >= payloadBytes}");
        Assert.That(profilerControlEvents, Is.GreaterThan(0), "Native event recorder must observe the real array allocation.");
        allocationCalibrationComplete = true;
    }

    private static IEnumerator CalibrateFrameBytes()
    {
        if (frameByteCalibrationComplete) yield break;
        using (var scope = new FrameAllocationScope())
        {
            var bytes = scope.Recorder;
            Assert.That(bytes.Valid, Is.True, "The frame byte counter must be available.");
            Assert.That(bytes.UnitType.ToString(), Is.EqualTo("Bytes"));
            yield return null;
            bytes.Reset(); bytes.Start();
            var payload = new byte[1024 * 1024]; payload[0] = 17; payload[payload.Length - 1] = 19; GC.KeepAlive(payload);
            yield return null;
            yield return null;
            yield return null;
            bytes.Stop(); Assert.That(bytes.Count, Is.GreaterThan(0));
            frameControlBytes = MaximumFrameBytes(bytes);
            Debug.Log($"ECOLOGY_FRAME_ALLOCATION_CONTROL payload=1048576 frameAllocatedBytes={frameControlBytes} unit={bytes.UnitType} " +
                      $"frames={bytes.Count} profilerEnabled={UnityEngine.Profiling.Profiler.enabled} " +
                      "scope=MAX_FRAME_IN_CONTROL_WINDOW_INCLUDING_HARNESS_AND_OTHER_THREADS");
            Assert.That(frameControlBytes, Is.GreaterThanOrEqualTo(1024 * 1024), "The byte counter must observe the controlled payload frame.");
        }
        frameByteCalibrationComplete = true;
    }

    [UnitySetUp]
    public IEnumerator CaptureClock()
    {
        previousTimeScale = Time.timeScale;
        Assert.That(Physics2D.simulationMode, Is.EqualTo(SimulationMode2D.FixedUpdate));
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        foreach (UnityEngine.Object item in owned) if (item != null) UnityEngine.Object.Destroy(item);
        owned.Clear();
        yield return null;
        Time.timeScale = previousTimeScale;
    }

    [UnityTest]
    public IEnumerator CandidateSnapshotUsesSourceProjectionAndCompleteFootWithoutReadingOldScenePhysics()
    {
        yield return CheckCandidateSnapshot(false);
    }

    [UnityTest]
    public IEnumerator PlannedRoleSolidsUseOwnedCompleteShapesAndMatchNativeProjection()
    {
        yield return CheckCandidateSnapshot(true);
    }

    [UnityTest]
    public IEnumerator FixedTerrainAttemptsStopAtEightAndRestoreNeverSearchesOtherAttempts()
    {
        yield return CheckCandidateSnapshot(false, true);
    }

    private IEnumerator CheckCandidateSnapshot(bool plannedSolids, bool boundedAttempts = false)
    {
        yield return null;
        Type captureType = typeof(MapData).Assembly.GetType("MapCandidateCollisionGeometry");
        Assert.That(captureType, Is.Not.Null, "The approved candidate-projection geometry boundary is missing.");
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MapGeneration/RandomMapRuntime.prefab");
        Assert.That(source, Is.Not.Null);
        GameObject holder = new GameObject("Inactive source-config fixture, no gameplay bootstrap");
        holder.SetActive(false); owned.Add(holder);
        GameObject configured = UnityEngine.Object.Instantiate(source, holder.transform);
        MapTilemapRenderer terrain = configured.GetComponentInChildren<MapTilemapRenderer>(true);
        MapSimpleDecorationRenderer decoration = configured.GetComponentInChildren<MapSimpleDecorationRenderer>(true);
        Assert.That(terrain, Is.Not.Null); Assert.That(decoration, Is.Not.Null);
        foreach (var tilemap in new[] { terrain.WaterBaseTilemap, terrain.TerrainCollisionTilemap, decoration.DecorationCollisionTilemap })
            Debug.Log($"ECOLOGY_GEOMETRY_GRID name={tilemap.name} asset=False grid={tilemap.layoutGrid != null} " +
                      $"anchor={tilemap.tileAnchor} orientation={tilemap.orientationMatrix} " +
                      $"layout={(tilemap.layoutGrid == null ? "missing" : tilemap.layoutGrid.cellLayout.ToString())}");
        var settings = AssetDatabase.LoadAssetAtPath<MapGenerationSettings>("Assets/MapGeneration/MapGenerationSettings_Default.asset");
        Assert.That(settings, Is.Not.Null);
        GameObject slimeSource = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Monsters/Torch/Torch_Blue.prefab");
        BoxCollider2D sourceFoot = slimeSource.GetComponent<BoxCollider2D>();
        Rigidbody2D sourceBody = slimeSource.GetComponent<Rigidbody2D>();
        Assert.That(sourceFoot, Is.Not.Null); Assert.That(sourceBody, Is.Not.Null);
        Assert.That(sourceFoot.transform, Is.SameAs(slimeSource.transform));
        var configuredSlime = new GameObject("Native source-foot geometry probe, no actor behaviour");
        configuredSlime.transform.SetParent(holder.transform, false);
        configuredSlime.layer = slimeSource.layer;
        configuredSlime.transform.localScale = slimeSource.transform.localScale;
        configuredSlime.transform.localRotation = slimeSource.transform.localRotation;
        Rigidbody2D probeBody = configuredSlime.AddComponent<Rigidbody2D>();
        probeBody.bodyType = sourceBody.bodyType; probeBody.gravityScale = sourceBody.gravityScale;
        probeBody.simulated = sourceBody.simulated; probeBody.constraints = sourceBody.constraints;
        probeBody.collisionDetectionMode = sourceBody.collisionDetectionMode;
        probeBody.interpolation = sourceBody.interpolation;
        BoxCollider2D foot = configuredSlime.AddComponent<BoxCollider2D>();
        foot.size = sourceFoot.size; foot.offset = sourceFoot.offset;
        foot.edgeRadius = sourceFoot.edgeRadius; foot.isTrigger = sourceFoot.isTrigger;
        Assert.That(foot.isTrigger, Is.False);
        // The native Grid must initialize, while this fixture must never bootstrap gameplay.
        foreach (MonoBehaviour behaviour in holder.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour != terrain && behaviour != decoration) UnityEngine.Object.Destroy(behaviour);
        foreach (Animator animator in holder.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        yield return null;
        foreach (Transform target in new[] { terrain.WaterBaseTilemap.transform,
            terrain.TerrainCollisionTilemap.transform, decoration.DecorationCollisionTilemap.transform, foot.transform })
            for (Transform ancestor = target; ancestor != null && ancestor != holder.transform; ancestor = ancestor.parent)
                ancestor.gameObject.SetActive(true);
        holder.SetActive(true);
        yield return null;
        foreach (var tilemap in new[] { terrain.WaterBaseTilemap, terrain.TerrainCollisionTilemap, decoration.DecorationCollisionTilemap })
        {
            Debug.Log($"ECOLOGY_GEOMETRY_GRID_INITIALIZED name={tilemap.name} grid={tilemap.layoutGrid != null} active={tilemap.gameObject.activeInHierarchy}");
            Assert.That(tilemap.layoutGrid, Is.Not.Null);
        }
        Debug.Log($"ECOLOGY_GEOMETRY_FOOT attached={foot.attachedRigidbody != null} " +
                  $"parentBody={foot.GetComponentInParent<Rigidbody2D>(true) != null} offset={foot.offset}");
        var map = new MapData(9, 9, new Vector2Int(-6, 7));
        map.SpawnCell = map.Origin + Vector2Int.one;
        map.ExitCell = map.Origin + new Vector2Int(7, 7);
        Vector2Int water = map.Origin + new Vector2Int(4, 4);
        map.SetTerrain(water, MapTerrainType.Water);
        var decorations = new MapSimpleDecorationData();
        Guid generation = Guid.NewGuid();
        MethodInfo capture = captureType.GetMethod("Capture");
        object[] inputs = { generation, map, decorations, settings, terrain, decoration };
        object first = capture.Invoke(null, inputs);
        string signature = (string)captureType.GetProperty("ProjectionIdentity").GetValue(first);
        MethodInfo build = captureType.GetMethod("BuildNavigationGraph");
        var graph = (EcologyNavigationGraph)build.Invoke(first, new object[] { foot, 0.02f });
        int waterNode = 4 * 9 + 4, clearNode = 1 * 9 + 1;
        Assert.That(graph.IsLegal(waterNode), Is.False);
        Assert.That(graph.IsLegal(clearNode), Is.True);
        Vector2 center = terrain.Coordinates.CellToWorld(water);
        MethodInfo clear = captureType.GetMethod("IsTranslationClear");
        Vector2 before = center - new Vector2(1.2f, 1.2f), after = center + new Vector2(1.2f, 1.2f);
        Assert.That((bool)clear.Invoke(first, new object[] { foot, before, before, 0.02f }), Is.True);
        Assert.That((bool)clear.Invoke(first, new object[] { foot, after, after, 0.02f }), Is.True);
        Assert.That((bool)clear.Invoke(first, new object[] { foot, before, after, 0.02f }), Is.False,
            "Two clear endpoints cannot certify a swept foot through the middle obstacle.");

        // Independent native oracle: materialize only the owned projection, not a gameplay scene.
        terrain.Render(map, settings);
        decoration.Render(decorations, map, settings);
        yield return null;
        foreach (var tileCollider in configured.GetComponentsInChildren<UnityEngine.Tilemaps.TilemapCollider2D>(true))
            if (tileCollider.gameObject.activeInHierarchy && tileCollider.hasTilemapChanges) tileCollider.ProcessTilemapChanges();
        Time.timeScale = 1f;
        Assert.That(Time.timeScale, Is.GreaterThan(0f), "Native oracle requires an automatic physics step.");
        yield return new WaitForFixedUpdate();
        Collider2D[] nativeObstacles = configured.GetComponentsInChildren<Collider2D>(true)
            .Where(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger && c.shapeCount > 0).ToArray();
        Assert.That(nativeObstacles.Length, Is.GreaterThan(0), "The native projection oracle cannot be empty.");
        var physicalGraph = (EcologyNavigationGraph)build.Invoke(first, new object[] { foot, 0f });
        Vector2 mapMinimum = terrain.WaterBaseTilemap.CellToWorld(new Vector3Int(map.Origin.x, map.Origin.y, 0));
        Vector2 mapMaximum = terrain.WaterBaseTilemap.CellToWorld(new Vector3Int(map.Origin.x + map.Width, map.Origin.y + map.Height, 0));
        int poses = 0, borderline = 0;
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            Vector2Int cell = map.Origin + new Vector2Int(x, y);
            foot.transform.position = terrain.Coordinates.CellToWorld(cell);
            Physics2D.SyncTransforms();
            float gap = float.PositiveInfinity; bool overlap = false;
            foreach (Collider2D obstacle in nativeObstacles)
            {
                ColliderDistance2D distance = Physics2D.Distance(foot, obstacle);
                Assert.That(distance.isValid, Is.True, "Native distance must have real shapes.");
                gap = Mathf.Min(gap, distance.distance); overlap |= distance.isOverlapped;
            }
            bool planned = physicalGraph.IsLegal(y * map.Width + x);
            Bounds footBounds = foot.bounds;
            bool supported = footBounds.min.x >= mapMinimum.x && footBounds.min.y >= mapMinimum.y &&
                footBounds.max.x <= mapMaximum.x && footBounds.max.y <= mapMaximum.y;
            if (!map.IsWalkable(cell) || overlap || !supported)
                Assert.That(planned, Is.False, $"Candidate false-safe at {cell}, native gap={gap:R} overlap={overlap}.");
            else if (gap > 0.0001f)
                Assert.That(planned, Is.True, $"Candidate unexpectedly rejects native clear pose {cell}, gap={gap:R}.");
            else borderline++;
            poses++;
        }
        foot.transform.position = before;
        Physics2D.SyncTransforms();
        var hits = new RaycastHit2D[32];
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = Physics2D.AllLayers, useTriggers = false };
        Vector2 sweep = after - before;
        int hitCount = foot.Cast(sweep.normalized, filter, hits, sweep.magnitude);
        Assert.That(hitCount, Is.LessThan(hits.Length), "A saturated native oracle is unknown, not clear.");
        Assert.That(hits.Take(hitCount).Any(h => nativeObstacles.Contains(h.collider)), Is.True,
            "The same full solid foot must hit the materialized middle obstacle.");
        var movedGraph = (EcologyNavigationGraph)build.Invoke(first, new object[] { foot, 0f });
        Debug.Log($"ECOLOGY_NATIVE_GEOMETRY_ORACLE poses={poses} borderline={borderline} obstacles={nativeObstacles.Length} " +
                  $"nativeSweepHits={hitCount} sourceFoot={sourceFoot.size}/{sourceFoot.offset} bodyType={probeBody.bodyType}");
        Assert.That(movedGraph.FootprintIdentity, Is.EqualTo(physicalGraph.FootprintIdentity),
            "A source footprint content identity cannot depend on the probe's world position.");
        var assetGraph = (EcologyNavigationGraph)build.Invoke(first, new object[] { sourceFoot, 0f });
        Assert.That(assetGraph.FootprintIdentity, Is.EqualTo(physicalGraph.FootprintIdentity),
            "Pending planning must accept the source prefab configuration without a materialized actor.");

        MethodInfo capacityBoundary = captureType.GetMethod("BuildCompatibleCapacity");
        Assert.That(capacityBoundary, Is.Not.Null, "Actual capacity must bind complete-foot pair geometry, not accept a caller's point count.");
        Vector2 originalSize = foot.size, originalOffset = foot.offset;
        // A fixture-only wider footprint makes adjacent legal grid poses collide;
        // final gameplay prefabs and physics settings remain untouched.
        foot.size = new Vector2(1.8f, 1.2f); foot.offset = new Vector2(0f, .6f);
        var wideGraph = (EcologyNavigationGraph)build.Invoke(first, new object[] { foot, 0f });
        int[] wideMembers = Enumerable.Range(0, wideGraph.NodeCount).Where(wideGraph.IsLegal).ToArray();
        ulong[] ranks = Enumerable.Range(0, wideGraph.NodeCount).Select(n => (ulong)n).ToArray();
        var capacity = (EcologyConstructiveCapacity)capacityBoundary.Invoke(first,
            new object[] { foot, wideMembers, Array.Empty<int>(), ranks, 1 });
        Assert.That(capacity.PlacementNodes.Count, Is.GreaterThan(1));
        Assert.That(capacity.RejectedByFootprint, Is.GreaterThan(0), "This native fixture must exercise full-foot conflicts that point qualification cannot detect.");
        var partnerObject = new GameObject("Native compatible packing partner, fixture only"); owned.Add(partnerObject);
        var partnerBody = partnerObject.AddComponent<Rigidbody2D>(); partnerBody.bodyType = probeBody.bodyType; partnerBody.gravityScale = 0f;
        var partner = partnerObject.AddComponent<BoxCollider2D>(); partner.size = foot.size; partner.offset = foot.offset;
        int nativePairs = 0;
        for (int i = 0; i < capacity.PlacementNodes.Count; i++)
        {
            int node = capacity.PlacementNodes[i];
            foot.transform.position = terrain.Coordinates.CellToWorld(map.Origin + new Vector2Int(node % map.Width, node / map.Width));
            Physics2D.SyncTransforms();
            foreach (Collider2D obstacle in nativeObstacles)
                Assert.That(Physics2D.Distance(foot, obstacle).isOverlapped, Is.False, "Constructed pose must have real static clearance.");
            for (int j = 0; j < i; j++)
            {
                int other = capacity.PlacementNodes[j];
                partner.transform.position = terrain.Coordinates.CellToWorld(map.Origin + new Vector2Int(other % map.Width, other / map.Width));
                Physics2D.SyncTransforms(); nativePairs++;
                Assert.That(Physics2D.Distance(foot, partner).isOverlapped, Is.False, "Every constructed pair must coexist in the native physics geometry.");
            }
        }
        Debug.Log($"ECOLOGY_NATIVE_CAPACITY members={wideMembers.Length} component={capacity.ComponentNodes.Count} " +
                  $"constructed={capacity.PlacementNodes.Count} rejectedByFoot={capacity.RejectedByFootprint} nativePairs={nativePairs}");
        foot.size = originalSize; foot.offset = originalOffset;
        UnityEngine.Object.Destroy(partnerObject); yield return null;

        GameObject playerSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Players/Warrior_Blue.prefab");
        Collider2D playerFoot = playerSource.transform.Find("MovementFeet").GetComponent<CircleCollider2D>();
        int spawnNode = 1 * map.Width + 1, merchantNode = spawnNode + 1;
        var candidateGeometry = (MapCandidateCollisionGeometry)first;
        bool safetyBuilt = candidateGeometry.TryBuildSafetyRegions(playerFoot, spawnNode, new[] { merchantNode }, new[] { spawnNode },
            1, 1, 1, ranks, out EcologySafetyRegions safety, out string safetyFailure);
        Assert.That(safetyBuilt, Is.True, safetyFailure);
        var birthCore = safety.BirthCore; var birthBuffer = safety.BirthBuffer;
        var restCore = safety.RestCore; var restBuffer = safety.RestBuffer;
        Assert.That(birthCore, Does.Contain(spawnNode)); Assert.That(birthCore, Does.Contain(merchantNode));
        Assert.That(birthBuffer.Count, Is.GreaterThan(0)); Assert.That(restBuffer.Count, Is.GreaterThan(0));
        Assert.That(birthCore.Concat(birthBuffer).Intersect(restCore.Concat(restBuffer)), Is.Empty);
        Assert.That(safety.MerchantNode, Is.EqualTo(merchantNode));
        var playerGraph = (EcologyNavigationGraph)build.Invoke(first, new object[] { playerFoot, 0f });
        foreach (int n in birthCore.Concat(birthBuffer).Concat(restCore).Concat(restBuffer))
            Assert.That(map.IsWalkable(map.Origin + new Vector2Int(n % map.Width, n / map.Width)), Is.True);
        var stoppingNodes = safety.BirthStoppingNodes.Concat(safety.RestStoppingNodes);
        foreach (int n in stoppingNodes) Assert.That(playerGraph.IsLegal(n), Is.True);
        Assert.That(birthCore, Does.Contain(spawnNode - map.Width),
            "Original player radius=.3 and offsetY=-.25 extend .05 into the lower surface cell; a body cannot stop at that edge-cell center, but its actually occupied floor still needs protection.");
        int restNode = safety.RestNode;
        var route = playerGraph.BuildReturnField(new[] { restNode });
        var protectedNodes = new HashSet<int>(birthCore.Concat(birthBuffer).Concat(restCore).Concat(restBuffer));
        bool crossedUnprotected = false; int current = spawnNode;
        for (int guard = 0; current != restNode && guard < playerGraph.NodeCount; guard++)
        {
            crossedUnprotected |= !protectedNodes.Contains(current);
            int next = route.Next[current]; Assert.That(playerGraph.CanMove(current, next), Is.True); current = next;
        }
        Assert.That(current, Is.EqualTo(restNode)); Assert.That(crossedUnprotected, Is.True);
        bool oversizedBuilt = candidateGeometry.TryBuildSafetyRegions(playerFoot, spawnNode, new[] { merchantNode }, new[] { spawnNode },
            4, 4, 3, ranks, out _, out string impossibleFailure);
        Assert.That(oversizedBuilt, Is.False, "An impossible pair must fail, not silently collapse to one safety region.");
        Assert.That(impossibleFailure, Is.EqualTo("InsufficientSeparateSafety"));
        Debug.Log($"ECOLOGY_NATIVE_SAFETY birth={birthCore.Count}+{birthBuffer.Count} rest={restCore.Count}+{restBuffer.Count} " +
                  $"spawn={spawnNode} merchant={merchantNode} restNode={restNode} unsafeRoute={crossedUnprotected}");
        if (plannedSolids) yield return CheckPlannedRoleSolids(candidateGeometry, map, terrain, holder.transform, foot, nativeObstacles);
        if (boundedAttempts) CheckBoundedAttempts(candidateGeometry, map);

        GameObject oldSceneObstacle = new GameObject("Explicit old-map decoy, not candidate geometry");
        owned.Add(oldSceneObstacle);
        oldSceneObstacle.transform.position = terrain.Coordinates.CellToWorld(map.SpawnCell);
        oldSceneObstacle.AddComponent<BoxCollider2D>().size = Vector2.one * 2f;
        Physics2D.SyncTransforms();
        object second = capture.Invoke(null, inputs);
        Assert.That(captureType.GetProperty("ProjectionIdentity").GetValue(second), Is.EqualTo(signature));
        var withDecoy = (EcologyNavigationGraph)build.Invoke(second, new object[] { foot, 0.02f });
        Assert.That(withDecoy.IsLegal(clearNode), Is.True, "The old scene must not contaminate pending planning.");
        map.SetTerrain(map.SpawnCell, MapTerrainType.Water);
        var frozen = (EcologyNavigationGraph)build.Invoke(first, new object[] { foot, 0.02f });
        Assert.That(frozen.IsLegal(clearNode), Is.True, "Capture must sever aliases to mutable MapData.");
        object changed = capture.Invoke(null, inputs);
        Assert.That(captureType.GetProperty("ProjectionIdentity").GetValue(changed), Is.Not.EqualTo(signature));
        Debug.Log($"ECOLOGY_CANDIDATE_GEOMETRY G={generation} projection={signature} origin={map.Origin} " +
                  "source=native-config-clone gameplayScriptsRemoved=True oldSceneDecoyIgnored=True endpointsClearMiddleBlocked=True");
    }

    private static void CheckBoundedAttempts(MapCandidateCollisionGeometry geometry, MapData map)
    {
        var visited = new List<int>();
        Func<int, MapPlacementPlan> reject = e =>
        {
            visited.Add(e);
            throw new MapPlanningException("InsufficientConstructiveCapacity", "Explicit infeasible candidate fixture.", true);
        };
        EcologyPlanningAttemptResult exhausted = EcologyPlanningAttempts.Run(map, geometry, reject);
        Assert.That(exhausted.Succeeded, Is.False); Assert.That(exhausted.AcceptedAttempt, Is.EqualTo(-1));
        Assert.That(exhausted.AttemptsUsed, Is.EqualTo(8)); Assert.That(exhausted.Failures.Count, Is.EqualTo(8));
        Assert.That(visited, Is.EqualTo(Enumerable.Range(0, 8)), "No terrain reroll and no ninth ecology candidate.");
        visited.Clear();
        EcologyPlanningAttemptResult restore = EcologyPlanningAttempts.Run(map, geometry, reject, 3);
        Assert.That(restore.Succeeded, Is.False); Assert.That(visited, Is.EqualTo(new[] { 3 }));
        visited.Clear();
        MapPlanningException invalid = Assert.Throws<MapPlanningException>(() => EcologyPlanningAttempts.Run(map, geometry, e =>
        { visited.Add(e); throw new MapPlanningException("UnknownContent", "Missing retained content is not unlucky placement.", false); }));
        Assert.That(invalid.Code, Is.EqualTo("UnknownContent")); Assert.That(visited, Is.EqualTo(new[] { 0 }));
        visited.Clear();
        MapPlacementPlan acceptedPlan = null;
        EcologyPlanningAttemptResult accepted = EcologyPlanningAttempts.Run(map, geometry, e =>
        {
            visited.Add(e);
            if (e < 2) throw new MapPlanningException("NoConnectedTerritory", "Bounded rejection fixture.", false);
            var builder = new MapEcologyLayout.Builder(geometry.GenerationId, map.Origin, map.Width, map.Height,
                "explicit-runner-fixture-not-production-layout", geometry.ProjectionIdentity, "fixture-foot", e);
            Assert.That(builder.TryAddRegion(1, "MerchantSafety", "", new[] { 0 }, new[] { 1 }, 0, 0));
            Assert.That(builder.TryAddRegion(2, "RestSafety", "", new[] { 80 }, new[] { 79 }, 0, 0));
            Assert.That(builder.TryAddRegion(3, "Ordinary", "fixture.type", new[] { 10, 11, 12 }, Array.Empty<int>(), 3, 2));
            Assert.That(builder.TryAddResident("r1", 3, "fixture.type", 10));
            Assert.That(builder.TryAddResident("r2", 3, "fixture.type", 11));
            var layout = builder.Freeze(); acceptedPlan = new MapPlacementPlan(geometry.GenerationId);
            foreach (MapEcologyResident resident in layout.Residents)
                Assert.That(acceptedPlan.TryAdd(new MapPlacementReservation(resident.Id, MonsterPlacementService.MonsterRole,
                    layout.CellAt(resident.InitialNode), new[] { layout.CellAt(resident.InitialNode) }, MapPlacementOwnership.GenerationScoped), out _));
            Assert.That(acceptedPlan.TryAttachEcology(layout, out _));
            return acceptedPlan;
        });
        Assert.That(accepted.Succeeded, Is.True); Assert.That(accepted.Plan, Is.SameAs(acceptedPlan));
        Assert.That(accepted.AcceptedAttempt, Is.EqualTo(2)); Assert.That(accepted.AttemptsUsed, Is.EqualTo(3));
        Assert.That(visited, Is.EqualTo(new[] { 0, 1, 2 })); Assert.That(accepted.Failures.Count, Is.EqualTo(2));
        Vector2Int originalExit = map.ExitCell;
        MapPlanningException mutated = Assert.Throws<MapPlanningException>(() => EcologyPlanningAttempts.Run(map, geometry, e =>
        {
            map.ExitCell = map.Origin;
            throw new MapPlanningException("NoConnectedTerritory", "Mutation must escape candidate retries.", true);
        }));
        map.ExitCell = originalExit;
        Assert.That(mutated.Code, Is.EqualTo("EcologyTerrainInputMutated"));
        Assert.That(geometry.MatchesMapData(map), Is.True);
        Debug.Log("ECOLOGY_FIXED_TERRAIN_ATTEMPTS maximum=8 restoreOnlyRecorded=3 accepted=2 used=3 invalidContentNotRetried=True mutationRejected=True frozenOriginalPlan=True");
    }

    private IEnumerator CheckPlannedRoleSolids(MapCandidateCollisionGeometry original, MapData map,
        MapTilemapRenderer terrain, Transform parent, BoxCollider2D probe, Collider2D[] terrainObstacles)
    {
        var source = new GameObject("Planned role source at an unrelated old-world pose"); owned.Add(source);
        source.transform.SetPositionAndRotation(new Vector3(500f, 600f), Quaternion.Euler(0f, 0f, 23f));
        source.transform.localScale = new Vector3(1.2f, .8f, 1f);
        source.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        var box = source.AddComponent<BoxCollider2D>(); box.size = new Vector2(.7f, .6f); box.offset = new Vector2(0f, .2f); box.enabled = false;
        var circleObject = new GameObject("Uniform circle after the configured parent scale"); circleObject.transform.SetParent(source.transform, false);
        circleObject.transform.localPosition = Vector3.right;
        circleObject.transform.localScale = new Vector3(1f / 1.2f, 1f / .8f, 1f);
        circleObject.AddComponent<CircleCollider2D>().radius = .32f;
        var polygonObject = new GameObject("Imported-style polygon configuration"); polygonObject.transform.SetParent(source.transform, false);
        polygonObject.transform.localPosition = new Vector3(-.6f, 1f);
        polygonObject.AddComponent<PolygonCollider2D>().points = new[] { new Vector2(-.25f, -.25f), new Vector2(.25f, -.25f), new Vector2(0f, .3f) };
        Vector2 destination = terrain.Coordinates.CellToWorld(map.Origin + new Vector2Int(5, 6));
        MapPlannedSolidGeometry solid = MapPlannedSolidGeometry.Capture("fixture.compound-role", source.transform, destination, Quaternion.identity);
        MapCandidateCollisionGeometry candidate = original.WithPlannedSolids(new[] { solid });
        Assert.That(candidate.GenerationId, Is.EqualTo(original.GenerationId));
        Assert.That(candidate.ProjectionIdentity, Is.Not.EqualTo(original.ProjectionIdentity));
        Vector2 before = destination - Vector2.right * 2f, after = destination + Vector2.right * 2.4f;
        Assert.That(original.IsTranslationClear(probe, before, after, 0f), Is.True);
        Assert.That(candidate.IsTranslationClear(probe, before, before, 0f), Is.True);
        Assert.That(candidate.IsTranslationClear(probe, after, after, 0f), Is.True);
        Assert.That(candidate.IsTranslationClear(probe, before, after, 0f), Is.False, "Complete planned solids must block the middle of a clear-endpoint sweep.");
        GameObject projected = UnityEngine.Object.Instantiate(source, parent); owned.Add(projected);
        projected.transform.SetPositionAndRotation(destination, Quaternion.identity);
        foreach (Collider2D collider in projected.GetComponentsInChildren<Collider2D>(true)) collider.enabled = true;
        yield return null; Physics2D.SyncTransforms();
        Collider2D[] solids = projected.GetComponentsInChildren<Collider2D>();
        Assert.That(solids.Length, Is.EqualTo(3));
        EcologyNavigationGraph graph = candidate.BuildNavigationGraph(probe, 0f); int poses = 0;
        for (int n = 0; n < map.Width * map.Height; n++)
        {
            Vector2Int cell = map.Origin + new Vector2Int(n % map.Width, n / map.Width);
            probe.transform.position = terrain.Coordinates.CellToWorld(cell); Physics2D.SyncTransforms();
            bool overlap = terrainObstacles.Concat(solids).Any(obstacle => Physics2D.Distance(probe, obstacle).isOverlapped);
            Vector2 mapMinimum = terrain.WaterBaseTilemap.CellToWorld(new Vector3Int(map.Origin.x, map.Origin.y, 0));
            Vector2 mapMaximum = terrain.WaterBaseTilemap.CellToWorld(new Vector3Int(map.Origin.x + map.Width, map.Origin.y + map.Height, 0));
            Bounds bounds = probe.bounds;
            bool supported = bounds.min.x >= mapMinimum.x && bounds.min.y >= mapMinimum.y &&
                bounds.max.x <= mapMaximum.x && bounds.max.y <= mapMaximum.y;
            Assert.That(graph.IsLegal(n), Is.EqualTo(map.IsWalkable(cell) && !overlap && supported), "Planned/native full-role geometry disagree at " + cell);
            poses++;
        }
        string identity = candidate.ProjectionIdentity;
        source.transform.position = new Vector3(-500f, -600f); box.size *= 3f;
        circleObject.GetComponent<CircleCollider2D>().radius = .01f;
        Assert.That(original.WithPlannedSolids(new[] { solid }).ProjectionIdentity, Is.EqualTo(identity),
            "Owned planned geometry must not retain aliases to mutable old-world sources.");
        Assert.That(original.IsTranslationClear(probe, before, after, 0f), Is.True, "Deriving a candidate must not mutate its static base.");
        Debug.Log($"ECOLOGY_NATIVE_PLANNED_SOLIDS poses={poses} shapes=3 disabledSourceIncluded=True oldSourceIgnored=True sourceAliasesSevered=True " +
                  $"projection={identity}");
    }

    [UnityTest]
    public IEnumerator NativePolygonContactEnvelopeMatchesConfiguredOffsetForBoxesAndCircles()
    {
        var obstacleObject = new GameObject("Bounded native polygon contact envelope oracle");
        obstacleObject.transform.position = new Vector3(500f, 500f); owned.Add(obstacleObject);
        var obstacle = obstacleObject.AddComponent<PolygonCollider2D>();
        obstacle.points = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        string[] paths = { "Assets/Resources/Ecology/RemediationV3/Barrel_Blue.prefab", "Assets/Prefabs/Players/Warrior_Blue.prefab" };
        int distanceModelMismatches = 0, overlapModelMismatches = 0;
        foreach (string path in paths)
        {
            GameObject actor = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Collider2D configured = path.Contains("Warrior") ? (Collider2D)actor.transform.Find("MovementFeet").GetComponent<CircleCollider2D>() : actor.GetComponent<BoxCollider2D>();
            Collider2D foot = CreateEffectiveFootProbe(configured, null); owned.Add(foot.gameObject);
            Physics2D.SyncTransforms();
            Vector2 half = foot.bounds.extents, centerOffset = (Vector2)foot.bounds.center - (Vector2)foot.transform.position;
            foreach (bool diagonal in new[] { false, true })
            foreach (float gap in new[] { .005f, .015f, .025f })
            {
                bool circle = foot is CircleCollider2D;
                Vector2 center = diagonal
                    ? circle ? new Vector2(500f, 500f) - Vector2.one * ((half.x + gap) / Mathf.Sqrt(2f))
                             : new Vector2(500f, 500f) - half - Vector2.one * (gap / Mathf.Sqrt(2f))
                    : new Vector2(500f - half.x - gap, 500.5f);
                foot.transform.position = center - centerOffset; Physics2D.SyncTransforms();
                ColliderDistance2D native = Physics2D.Distance(foot, obstacle);
                float expected = gap - Physics2D.defaultContactOffset * (circle ? 1f : 2f);
                var overlaps = new Collider2D[16];
                int count = foot.OverlapCollider(new ContactFilter2D { useTriggers = false }, overlaps);
                Debug.Log($"ECOLOGY_NATIVE_ENVELOPE circle={circle} diagonal={diagonal} gap={gap:R} offset={Physics2D.defaultContactOffset:R} " +
                          $"distance={native.distance:R} expected={expected:R} overlap={native.isOverlapped} overlapQuery={overlaps.Take(count).Contains(obstacle)}");
                // Penetration depth uses the native contact manifold; clearance
                // needs its overlap classification and separated distance only.
                if (!native.isOverlapped && Mathf.Abs(native.distance - expected) > .0001f) distanceModelMismatches++;
                if (overlaps.Take(count).Contains(obstacle) != (expected < 0f)) overlapModelMismatches++;
            }
            UnityEngine.Object.Destroy(foot.gameObject); yield return null;
        }
        Assert.That(distanceModelMismatches, Is.Zero, "Separated native distance must include each polygon's configured contact radius.");
        Assert.That(overlapModelMismatches, Is.Zero);
    }

    [UnityTest]
    public IEnumerator GeneratedProjectionMatchesRetainedNativeFeetAndPlayerFootAtNativePosesAndStairs()
    {
        yield return CheckGeneratedProjection(20260929);
    }

    [UnityTest]
    public IEnumerator GeneratedProjectionAlsoMatchesNegativeAndSecondP1Seeds()
    {
        yield return CheckGeneratedProjection(-1391545000);
        yield return CheckGeneratedProjection(613580675);
    }

    [UnityTest]
    public IEnumerator GeneratedOrdinaryTerritoriesKeepGroupsSafetyNativeFeetAndDeterministicIdentity()
    {
        yield return CheckGeneratedProjection(20260929, true);
    }

    [UnityTest, Ignore("N/A retired four-asset qualified Boss contract; exact historical source/evidence privately retained. R3 explicitly has no compatible Boss.")]
    public IEnumerator RetainedSixteenPrefabEcotypesAndQualifiedBossKeepNativeHomeConnectivity()
    {
        yield return CheckGeneratedProjection(-1391545000, true, true, true);
        yield return CheckGeneratedProjection(613580675, true, true, false);
    }

    private IEnumerator CheckGeneratedProjection(int seed, bool buildTerritories = false, bool retainedCatalog = false, bool guaranteeBoss = false)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MapGeneration/RandomMapRuntime.prefab");
        var holder = new GameObject("Translated native projection oracle, no gameplay");
        holder.SetActive(false); holder.transform.position = new Vector3(120f, 80f, 0f); owned.Add(holder);
        GameObject configured = UnityEngine.Object.Instantiate(source, holder.transform);
        var terrain = configured.GetComponentInChildren<MapTilemapRenderer>(true);
        var decoration = configured.GetComponentInChildren<MapSimpleDecorationRenderer>(true);
        Assert.That(terrain, Is.Not.Null); Assert.That(decoration, Is.Not.Null);
        foreach (MonoBehaviour behaviour in holder.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour != terrain && behaviour != decoration) UnityEngine.Object.Destroy(behaviour);
        yield return null;
        foreach (Transform target in new[] { terrain.WaterBaseTilemap.transform,
            terrain.TerrainCollisionTilemap.transform, decoration.DecorationCollisionTilemap.transform })
            for (Transform ancestor = target; ancestor != null && ancestor != holder.transform; ancestor = ancestor.parent)
                ancestor.gameObject.SetActive(true);
        holder.SetActive(true);
        yield return null;
        var settings = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<MapGenerationSettings>(
            "Assets/MapGeneration/MapGenerationSettings_Default.asset"));
        owned.Add(settings); settings.seed = seed;
        MapData map = RandomMapGenerator.Generate(settings, true);
        MapSimpleDecorationData decorations = MapSimpleDecorationGenerator.Generate(map, settings);
        Assert.That(map.HasElevation, Is.True); Assert.That(map.Stairs.Count, Is.GreaterThan(0));
        var snapshot = MapCandidateCollisionGeometry.Capture(Guid.NewGuid(), map, decorations, settings, terrain, decoration);
        terrain.Render(map, settings); decoration.Render(decorations, map, settings);
        yield return null;
        foreach (var tileCollider in configured.GetComponentsInChildren<UnityEngine.Tilemaps.TilemapCollider2D>(true))
            if (tileCollider.gameObject.activeInHierarchy && tileCollider.hasTilemapChanges) tileCollider.ProcessTilemapChanges();
        Time.timeScale = 1f;
        yield return new WaitForFixedUpdate();
        Collider2D[] obstacles = configured.GetComponentsInChildren<Collider2D>(true)
            .Where(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger && c.shapeCount > 0).ToArray();
        Assert.That(terrain.TerrainCollisionTilemap.GetComponent<CompositeCollider2D>().shapeCount, Is.GreaterThan(0));
        Assert.That(decoration.DecorationCollisionTilemap.GetComponent<CompositeCollider2D>().shapeCount, Is.GreaterThan(0),
            "This native oracle must actually include imported decoration physics outlines.");
        Assert.That(obstacles.Any(c => c.name == "GeneratedCliffEdges" && c is PolygonCollider2D), Is.True);
        string[] paths = {
            "Assets/Prefabs/Monsters/Torch/Torch_Blue.prefab",
            "Assets/Prefabs/Monsters/TNT/TNT_Red.prefab",
            "Assets/Prefabs/Monsters/Torch/Torch_Red.prefab",
            "Assets/Resources/Ecology/RemediationV3/Barrel_Blue.prefab",
            "Assets/Prefabs/Players/Warrior_Blue.prefab" };
        if (retainedCatalog)
        {
            paths = Resources.Load<EcologyRecipeDefinition>(EcologyRecipeDefinition.ResourcePathFor(EcologyRecipeDefinition.MainV1ProfileId)).ResolveTypes()
                .Select(s => AssetDatabase.GetAssetPath(s.Prefab)).Concat(new[] { "Assets/Prefabs/Players/Warrior_Blue.prefab" }).ToArray();
            Assert.That(paths.Length, Is.EqualTo(13)); Assert.That(paths.Distinct().Count(), Is.EqualTo(13));
        }
        int allFalseSafe = 0, allFalseRejected = 0, allStairMismatches = 0;
        foreach (string path in paths)
        {
            GameObject actor = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Collider2D sourceFoot = path.EndsWith("Warrior_Blue.prefab", StringComparison.Ordinal)
                ? actor.transform.Find("MovementFeet").GetComponent<CircleCollider2D>()
                : new EcologyEcotypeDefinition("oracle." + AssetDatabase.AssetPathToGUID(path), actor,
                    MapPopulationTerrainMask.Grass | MapPopulationTerrainMask.Path | MapPopulationTerrainMask.Forest | MapPopulationTerrainMask.Sand,
                    1, false).MovementFoot;
            Assert.That(sourceFoot, Is.Not.Null); Assert.That(sourceFoot.isTrigger, Is.False);
            EcologyNavigationGraph graph = snapshot.BuildNavigationGraph(sourceFoot, 0f);
            Collider2D probe = CreateEffectiveFootProbe(sourceFoot, holder.transform);
            int checkedPoses = 0, falseSafe = 0, falseRejected = 0, borderline = 0;
            float began = Time.realtimeSinceStartup;
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                Vector2Int cell = map.Origin + new Vector2Int(x, y);
                bool planned = graph.IsLegal(y * map.Width + x);
                if (!map.IsWalkable(cell)) { Assert.That(planned, Is.False); continue; }
                probe.transform.position = terrain.Coordinates.CellToWorld(cell);
                Physics2D.SyncTransforms();
                float gap = float.PositiveInfinity; bool overlap = false;
                Collider2D nearest = null; ColliderDistance2D nearestDistance = default;
                foreach (Collider2D obstacle in obstacles)
                {
                    ColliderDistance2D distance = Physics2D.Distance(probe, obstacle);
                    Assert.That(distance.isValid, Is.True);
                    if (distance.distance < gap) { gap = distance.distance; nearest = obstacle; nearestDistance = distance; }
                    overlap |= distance.isOverlapped;
                }
                if (overlap && planned) falseSafe++;
                else if (!overlap && gap > .0001f && !planned) falseRejected++;
                else if (!overlap && gap <= .0001f) borderline++;
                if ((overlap && planned || !overlap && gap > .0001f && !planned) && falseSafe + falseRejected <= 8)
                {
                    Debug.Log($"ECOLOGY_NATIVE_MISMATCH actor={actor.name} cell={cell} planned={planned} gap={gap:R} overlap={overlap} " +
                              $"nearest={nearest?.name}/{nearest?.GetType().Name} pointA={nearestDistance.pointA} pointB={nearestDistance.pointB} " +
                              $"normal={nearestDistance.normal} probeBounds={probe.bounds} contactOffset={Physics2D.defaultContactOffset:R}");
                    if (falseSafe + falseRejected == 1)
                    {
                        LogNativeShapes(probe, nearestDistance.pointA, "foot");
                        LogNativeShapes(nearest, nearestDistance.pointB, "obstacle");
                        LogNativeQueries(probe, nearest, nearestDistance);
                    }
                }
                checkedPoses++;
            }
            Debug.Log($"ECOLOGY_NATIVE_GENERATED_POSES seed={settings.seed} actor={actor.name} N={checkedPoses} " +
                      $"falseSafe={falseSafe} falseRejected={falseRejected} borderline={borderline} " +
                      $"seconds={Time.realtimeSinceStartup - began:R} projection={snapshot.ProjectionIdentity} " +
                      $"foot={graph.FootprintIdentity} bodyType={probe.attachedRigidbody.bodyType}");
            allFalseSafe += falseSafe; allFalseRejected += falseRejected;
            int stairSweeps = 0, rejectedEndpointSweeps = 0;
            foreach (MapStairConnection stair in map.Stairs)
            foreach (bool reverse in new[] { false, true })
            {
                Vector2Int from = reverse ? stair.UpperCell : stair.LowerCell;
                Vector2Int to = reverse ? stair.LowerCell : stair.UpperCell;
                int fromNode = (from.y - map.Origin.y) * map.Width + from.x - map.Origin.x;
                int toNode = (to.y - map.Origin.y) * map.Width + to.x - map.Origin.x;
                bool legalEndpoints = graph.IsLegal(fromNode) && graph.IsLegal(toNode);
                if (!legalEndpoints) rejectedEndpointSweeps++;
                Vector2 start = terrain.Coordinates.CellToWorld(from), end = terrain.Coordinates.CellToWorld(to);
                probe.transform.position = start; Physics2D.SyncTransforms();
                var hits = new RaycastHit2D[128];
                var filter = new ContactFilter2D { useLayerMask = true, layerMask = Physics2D.AllLayers, useTriggers = false };
                int count = probe.Cast((end - start).normalized, filter, hits, Vector2.Distance(start, end));
                Assert.That(count, Is.LessThan(hits.Length));
                bool nativeBlocked = hits.Take(count).Any(h => obstacles.Contains(h.collider));
                if (graph.CanMove(fromNode, toNode) != (legalEndpoints && !nativeBlocked))
                {
                    allStairMismatches++;
                    Debug.Log($"ECOLOGY_NATIVE_STAIR_MISMATCH actor={actor.name} from={from} to={to} nativeBlocked={nativeBlocked}");
                }
                stairSweeps++;
            }
            Assert.That(stairSweeps, Is.GreaterThan(0), actor.name + " must exercise actual legal stair edges.");
            Debug.Log($"ECOLOGY_NATIVE_GENERATED_STAIRS seed={settings.seed} actor={actor.name} sweeps={stairSweeps} " +
                      $"rejectedEndpointSweeps={rejectedEndpointSweeps} cumulativeMismatches={allStairMismatches}");
            UnityEngine.Object.Destroy(probe.gameObject);
            yield return null;
        }
        Assert.That(allFalseSafe, Is.Zero, "All native false-safe witnesses remain failures; collecting later types does not waive them.");
        Assert.That(allFalseRejected, Is.Zero);
        Assert.That(allStairMismatches, Is.Zero);
        EcologySafetyRegions safety = CheckGeneratedSafety(snapshot, map, decorations, settings);
        if (buildTerritories) yield return CheckGeneratedTerritories(snapshot, map, decorations, settings, safety, terrain, holder.transform, obstacles,
            retainedCatalog, guaranteeBoss);
    }

    private static EcologySafetyRegions CheckGeneratedSafety(MapCandidateCollisionGeometry snapshot, MapData map,
        MapSimpleDecorationData decorations, MapGenerationSettings settings)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Players/Warrior_Blue.prefab");
        Collider2D playerFoot = source.transform.Find("MovementFeet").GetComponent<CircleCollider2D>();
        EcologyNavigationGraph graph = snapshot.BuildNavigationGraph(playerFoot, 0f);
        int spawn = (map.SpawnCell.y - map.Origin.y) * map.Width + map.SpawnCell.x - map.Origin.x;
        Assert.That(graph.IsLegal(spawn), Is.True, "The original SpawnCell must support the original player's whole foot.");
        EcologyReturnField fromSpawn = graph.BuildReturnField(new[] { spawn });
        MapPlacementPlan basePlan = MapStaticPlacementPlanner.Build(snapshot.GenerationId, map, decorations, settings);
        var policy = new MerchantPlacementPolicy();
        var request = new MerchantSemanticPlacementRequest(new MapPlacementRequest(MerchantPlacementService.MainMerchantLogicalObjectId,
            MerchantPlacementService.MerchantRole, new[] { Vector2Int.zero }), new MerchantPlacementProfile());
        var merchants = new List<int>(); var approaches = new List<int>(); var ranks = new ulong[graph.NodeCount];
        for (int node = 0; node < graph.NodeCount; node++)
        {
            ranks[node] = (ulong)(uint)(node ^ settings.seed);
            if (fromSpawn.Distances[node] < 0 || fromSpawn.Distances[node] > 10) continue;
            Vector2Int cell = map.Origin + new Vector2Int(node % map.Width, node / map.Width);
            if (!policy.TryEvaluateCandidate(settings.seed, map, basePlan, request, cell,
                out MerchantPlacementResult qualified, out _)) continue;
            int approach = (qualified.InteractionApproachCell.y - map.Origin.y) * map.Width + qualified.InteractionApproachCell.x - map.Origin.x;
            if (!graph.IsLegal(approach)) continue;
            merchants.Add(node); approaches.Add(approach);
        }
        bool accepted = snapshot.TryBuildSafetyRegions(playerFoot, spawn, merchants.ToArray(), approaches.ToArray(),
            10, 3, 1, ranks, out EcologySafetyRegions safety, out string failure);
        Debug.Log($"ECOLOGY_GENERATED_SAFETY seed={settings.seed} qualifiedMerchantPairs={merchants.Count} accepted={accepted} failure={failure}");
        Assert.That(accepted, Is.True, "Actual pending map safety candidate failed: " + failure);
        Assert.That(safety.BirthCore, Does.Contain(spawn));
        Assert.That(safety.BirthCore, Does.Contain(safety.MerchantNode));
        Assert.That(safety.BirthCore, Does.Contain(safety.MerchantApproachNode));
        var protectedNodes = new HashSet<int>(safety.BirthCore.Concat(safety.BirthBuffer).Concat(safety.RestCore).Concat(safety.RestBuffer));
        EcologyReturnField home = graph.BuildReturnField(new[] { safety.RestNode });
        int cursor = spawn, steps = 0, unsafeNodes = 0;
        while (cursor != safety.RestNode && steps < graph.NodeCount)
        {
            if (!protectedNodes.Contains(cursor)) unsafeNodes++;
            int next = home.Next[cursor]; Assert.That(graph.CanMove(cursor, next), Is.True);
            cursor = next; steps++;
        }
        Assert.That(cursor, Is.EqualTo(safety.RestNode)); Assert.That(unsafeNodes, Is.GreaterThan(0));
        Assert.That(basePlan.Reservations.Any(r => r.Role == MerchantPlacementService.MerchantRole), Is.False,
            "Read-only merchant qualification and safety selection must not mutate the original static base.");
        GameObject merchantSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NPC/Merchant_Pawn.prefab");
        Collider2D[] merchantSolids = merchantSource.GetComponentsInChildren<Collider2D>(true).Where(c => c.enabled && !c.isTrigger).ToArray();
        Debug.Log($"ECOLOGY_GENERATED_SAFETY_ACCEPTED seed={settings.seed} birth={safety.BirthCore.Count}+{safety.BirthBuffer.Count} " +
                  $"rest={safety.RestCore.Count}+{safety.RestBuffer.Count} routeSteps={steps} unsafeNodes={unsafeNodes} " +
                  $"merchantNode={safety.MerchantNode} approach={safety.MerchantApproachNode} restNode={safety.RestNode} " +
                  $"merchantPrefabConfiguredSolids={merchantSolids.Length} sceneMerchantSolids=NOT_VERIFIED");
        return safety;
    }

    private IEnumerator CheckGeneratedTerritories(MapCandidateCollisionGeometry snapshot, MapData map,
        MapSimpleDecorationData decorations, MapGenerationSettings settings, EcologySafetyRegions safety,
        MapTilemapRenderer terrain, Transform parent, Collider2D[] nativeObstacles, bool retainedCatalog, bool guaranteeBoss)
    {
        EcologyEcotypeDefinition[] types = Resources.Load<EcologyRecipeDefinition>(EcologyRecipeDefinition.ResourcePathFor(EcologyRecipeDefinition.MainV1ProfileId)).ResolveTypes();
        Assert.That(types.Length, Is.EqualTo(12)); Assert.That(types.Any(type => type.IsBoss), Is.False);
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Players/Warrior_Blue.prefab");
        Collider2D playerFoot = player.transform.Find("MovementFeet").GetComponent<CircleCollider2D>();
        MapPlacementPlan basePlan = MapStaticPlacementPlanner.Build(snapshot.GenerationId, map, decorations, settings);
        var request = new MerchantSemanticPlacementRequest(new MapPlacementRequest(MerchantPlacementService.MainMerchantLogicalObjectId,
            MerchantPlacementService.MerchantRole, new[] { Vector2Int.zero }), new MerchantPlacementProfile());
        Vector2Int merchantCell = map.Origin + new Vector2Int(safety.MerchantNode % map.Width, safety.MerchantNode / map.Width);
        Assert.That(new MerchantPlacementPolicy().TryEvaluateCandidate(settings.seed, map, basePlan, request, merchantCell,
            out MerchantPlacementResult merchant, out _), Is.True);
        Assert.That(basePlan.TryAdd(new MapPlacementReservation(MerchantPlacementService.MainMerchantLogicalObjectId,
            MerchantPlacementService.MerchantRole, merchant.PlacementCell, merchant.FootprintCells, MapPlacementOwnership.SceneBound), out _), Is.True);
        Assert.That(basePlan.TryAdd(new MapPlacementReservation(MerchantPlacementService.InteractionApproachLogicalObjectId,
            MerchantPlacementService.InteractionApproachRole, merchant.InteractionApproachCell, new[] { merchant.InteractionApproachCell },
            MapPlacementOwnership.GenerationScoped), out _), Is.True);
        int baseCount = basePlan.ReservationCount;
        var terrainBefore = Enumerable.Range(0, map.Width * map.Height).Select(n =>
            map.GetCell(map.Origin + new Vector2Int(n % map.Width, n / map.Width)).terrainType).ToArray();
        var heightsBefore = Enumerable.Range(0, map.Width * map.Height).Select(n =>
            map.GetElevation(map.Origin + new Vector2Int(n % map.Width, n / map.Width))).ToArray();
        var recipe = new EcologyTerritoryRecipe(bossContentPending: true);
        CalibrateAllocations();
        yield return CalibrateFrameBytes();
        EcologyTerritoryDraft draft; double buildMs; long allocatedBytes; int allocationSamples;
        using (var frameScope = new FrameAllocationScope())
        {
            var frameBytes = frameScope.Recorder;
            Assert.That(frameBytes.Valid, Is.True); Assert.That(frameBytes.UnitType.ToString(), Is.EqualTo("Bytes"));
            yield return null;
            frameBytes.Reset(); frameBytes.Start();
            using (var recorder = BeginAllocationRecorder(65536))
            {
                long began = System.Diagnostics.Stopwatch.GetTimestamp();
                draft = EcologyTerritoryPlanner.Build(settings.seed, 0, map, snapshot, playerFoot, safety, basePlan, recipe, types);
                buildMs = (System.Diagnostics.Stopwatch.GetTimestamp() - began) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                recorder.Stop(); allocationSamples = CountAllocationEvents(recorder);
            }
            yield return null;
            yield return null;
            yield return null;
            frameBytes.Stop(); Assert.That(frameBytes.Count, Is.GreaterThan(0));
            allocatedBytes = MaximumFrameBytes(frameBytes);
        }
        MapEcologyLayout layout = draft.Layout;
        // Original four-asset Run80 goldens remain in the private history and cannot
        // be blessed as a twelve-type result. Rebuild the unchanged pending inputs.
        var repeat = EcologyTerritoryPlanner.Build(settings.seed, 0, map, snapshot, playerFoot, safety, basePlan, recipe, types.Reverse().ToArray());
        Assert.That(repeat.Layout.Signature, Is.EqualTo(layout.Signature), "Retained catalog ordering and repeated planning must be deterministic.");
        var ordinary = layout.Regions.Where(r => r.Kind == MapEcologyRegionKind.Ordinary).ToArray();
        Assert.That(ordinary.Length, Is.InRange(1, recipe.MaximumOrdinaryRegions));
        Assert.That(layout.Regions.Count(r => r.Kind == MapEcologyRegionKind.MerchantSafety), Is.EqualTo(1));
        Assert.That(layout.Regions.Count(r => r.Kind == MapEcologyRegionKind.RestSafety), Is.EqualTo(1));
        Assert.That(layout.Regions.Count(r => r.Kind == MapEcologyRegionKind.Boss), Is.InRange(0, 1));
        if (guaranteeBoss) Assert.That(layout.Regions.Count(r => r.Kind == MapEcologyRegionKind.Boss), Is.EqualTo(1),
            "This explicit fixture recipe must actually exercise a qualified one-resident Boss territory.");
        Assert.That(layout.Residents.Count, Is.LessThanOrEqualTo(recipe.TotalBudget));
        foreach (MapEcologyRegion region in ordinary)
        {
            Assert.That(region.AssignedPopulation, Is.GreaterThanOrEqualTo(recipe.MinimumGroup));
            Assert.That(region.AssignedPopulation, Is.LessThanOrEqualTo(region.ConstructiveCapacity));
            Assert.That(layout.Residents.Where(r => r.HomeRegionId == region.Id).Select(r => r.TypeId).Distinct(), Is.EquivalentTo(new[] { region.TypeId }));
        }
        int[] allProtected = safety.BirthCore.Concat(safety.BirthBuffer).Concat(safety.RestCore).Concat(safety.RestBuffer).ToArray();
        foreach (MapEcologyRegion region in layout.Regions.Where(r => !r.IsSafety))
        {
            MapEcologyResident[] residents = layout.Residents.Where(r => r.HomeRegionId == region.Id).ToArray();
            EcologyEcotypeDefinition type = types.Single(t => t.TypeId == region.TypeId);
            EcologyNavigationGraph homeGraph = snapshot.BuildHomeNavigationGraph(type.MovementFoot, region.Nodes, allProtected);
            EcologyReturnField homeComponent = homeGraph.BuildReturnField(new[] { residents[0].InitialNode });
            foreach (MapEcologyResident resident in residents)
            {
                Assert.That(homeGraph.IsLegal(resident.InitialNode), Is.True, "Initial whole foot must be inside its home and outside all protection contacts.");
                Assert.That(homeComponent.Distances[resident.InitialNode], Is.GreaterThanOrEqualTo(0), "Every resident must share one real-foot Home component.");
            }
            if (region.Kind == MapEcologyRegionKind.Boss) Assert.That(residents.Length, Is.EqualTo(1));
        }
        Assert.That(basePlan.ReservationCount, Is.EqualTo(baseCount), "A candidate must leave the original static/merchant base intact.");
        Assert.That(terrainBefore, Is.EqualTo(Enumerable.Range(0, map.Width * map.Height).Select(n =>
            map.GetCell(map.Origin + new Vector2Int(n % map.Width, n / map.Width)).terrainType)));
        Assert.That(heightsBefore, Is.EqualTo(Enumerable.Range(0, map.Width * map.Height).Select(n =>
            map.GetElevation(map.Origin + new Vector2Int(n % map.Width, n / map.Width)))));
        EcologyTerritoryDraft reordered = EcologyTerritoryPlanner.Build(settings.seed, 0, map, snapshot,
            playerFoot, safety, basePlan, recipe, types.Reverse().ToArray());
        Assert.That(reordered.Layout.Signature, Is.EqualTo(layout.Signature), "Configured list order cannot select the winners or alter the static layout.");
        var nativeResidents = new List<Collider2D>();
        foreach (MapEcologyResident resident in layout.Residents)
        {
            Assert.That(layout.TryGetRegion(resident.HomeRegionId, out MapEcologyRegion home), Is.True);
            Assert.That(home.Nodes, Does.Contain(resident.InitialNode));
            Assert.That(layout.IsProtectedNode(resident.InitialNode), Is.False);
            EcologyEcotypeDefinition type = types.Single(t => t.TypeId == resident.TypeId);
            Collider2D probe = CreateEffectiveFootProbe(type.MovementFoot, parent); owned.Add(probe.gameObject);
            probe.transform.position = terrain.Coordinates.CellToWorld(layout.CellAt(resident.InitialNode)); Physics2D.SyncTransforms();
            foreach (Collider2D obstacle in nativeObstacles)
                Assert.That(Physics2D.Distance(probe, obstacle).isOverlapped, Is.False, "Resident initial foot must have real native static clearance.");
            foreach (Collider2D other in nativeResidents)
                Assert.That(Physics2D.Distance(probe, other).isOverlapped, Is.False, "Different ecotypes must coexist, including across territory boundaries.");
            for (int n = 0; n < map.Width * map.Height; n++)
            {
                Vector2Int cell = layout.CellAt(n); var index = new Vector3Int(cell.x, cell.y, 0);
                Vector2 lower = terrain.WaterBaseTilemap.CellToWorld(index), upper = terrain.WaterBaseTilemap.CellToWorld(index + Vector3Int.right + Vector3Int.up);
                Bounds actual = probe.bounds; bool occupies;
                if (probe is CircleCollider2D circle)
                {
                    double dx = Math.Max(lower.x - actual.center.x, Math.Max(0d, actual.center.x - upper.x));
                    double dy = Math.Max(lower.y - actual.center.y, Math.Max(0d, actual.center.y - upper.y));
                    double radius = circle.radius * Math.Abs(circle.transform.lossyScale.x); occupies = dx * dx + dy * dy < radius * radius;
                }
                else occupies = actual.max.x > lower.x && actual.min.x < upper.x && actual.max.y > lower.y && actual.min.y < upper.y;
                if (occupies) Assert.That(home.Nodes, Does.Contain(n), "Native whole foot must be covered by its recorded home surface.");
            }
            nativeResidents.Add(probe);
        }
        Assert.That(draft.Plan.TryAttachEcology(layout, out string freezeFailure), Is.True, freezeFailure);
        Assert.That(draft.Plan.Ecology, Is.SameAs(layout));
        ExportCandidateEvidence(snapshot, map, layout, draft, recipe, types, nativeResidents, settings.seed,
            buildMs, allocatedBytes, allocationSamples);
        Debug.Log($"ECOLOGY_TERRITORY_CANDIDATE seed={settings.seed} ordinary={ordinary.Length} " +
                  $"residents={layout.Residents.Count} boss={layout.Regions.Count(r => r.Kind == MapEcologyRegionKind.Boss)} " +
                  $"activity={draft.EffectiveActivityNodes} repairs={draft.LocalRepairsUsed} signature={layout.Signature} " +
                  $"catalog={types.Length} guaranteeBossFixture={guaranteeBoss} warmGraphBuildMs={buildMs:R} buildFrameAllocatedBytes={allocatedBytes} " +
                  $"mainThreadAllocationEvents={allocationSamples} frameAllocationControlBytes={frameControlBytes} rawCounterControlDelta={rawCounterControlDelta} " +
                  "byteScope=MAX_FRAME_IN_BUILD_WINDOW_INCLUDING_HARNESS_AND_OTHER_THREADS profilerEnabledForMeasurement=True " +
                  "otherRoleJointValidation=NOT_IMPLEMENTED productionProfile=NOT_ACTIVATED");
    }

    private static void ExportCandidateEvidence(MapCandidateCollisionGeometry geometry, MapData map, MapEcologyLayout layout,
        EcologyTerritoryDraft draft, EcologyTerritoryRecipe recipe, EcologyEcotypeDefinition[] types,
        List<Collider2D> nativeResidents, int seed, double buildMs, long frameBytes, int allocationEvents)
    {
        string label = Environment.GetEnvironmentVariable("RPG_ECOLOGY_EVIDENCE_LABEL");
        if (string.IsNullOrEmpty(label)) return;
        string safeLabel = new string(label.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray());
        string run = safeLabel.Split('-')[0];
        string evidence = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "MapGeneration", "Ecology", "Evidence"));
        string manifest = Path.Combine(evidence, "P0V", run + "-input-manifest.json");
        Assert.That(File.Exists(manifest), Is.True, "A directed artifact needs its prepared source binding.");
        string manifestHash;
        using (var sha = System.Security.Cryptography.SHA256.Create())
            manifestHash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(manifest))).Replace("-", "").ToLowerInvariant();
        int[] protection = layout.Regions.Where(r => r.IsSafety).SelectMany(r => r.Nodes.Concat(r.BufferNodes)).ToArray();
        var capture = new LayoutCapture
        {
            schema = "ecology-directed-layout-candidate-v1", scope = "DIRECTED_CANDIDATE_PASS_NO_OTHER_ROLE_JOINT_VALIDATION_NO_ACTIVE_PROFILE",
            label = label, sourceFingerprint = JsonUtility.FromJson<FingerprintHeader>(File.ReadAllText(manifest)).sourceFingerprint,
            inputManifestSha256 = manifestHash, generationId = layout.GenerationId.ToString("N"), recipeIdentity = layout.RecipeIdentity,
            projectionIdentity = geometry.ProjectionIdentity, mapContentIdentity = geometry.MapContentIdentity, layoutSignature = layout.Signature,
            byteScope = "MAX_FRAME_IN_BUILD_WINDOW_INCLUDING_HARNESS_AND_OTHER_THREADS_PROFILER_ENABLED",
            seed = seed, width = map.Width, height = map.Height, originX = map.Origin.x, originY = map.Origin.y,
            catalogCount = types.Length, bossChanceDenominator = recipe.BossChanceDenominator,
            repairs = draft.LocalRepairsUsed, activityNodes = draft.EffectiveActivityNodes,
            allocationEvents = allocationEvents, warmGraphBuildMs = buildMs, maxFrameAllocatedBytes = frameBytes,
            terrain = Enumerable.Range(0, map.Width * map.Height).Select(n => (int)map.GetCell(layout.CellAt(n)).terrainType).ToArray(),
            elevation = Enumerable.Range(0, map.Width * map.Height).Select(n => (int)map.GetElevation(layout.CellAt(n))).ToArray(),
            regionByNode = layout.RegionByNode.ToArray(),
            types = types.OrderBy(t => t.TypeId, StringComparer.Ordinal).Select(t => new CapturedType
            {
                typeId = t.TypeId, prefabPath = AssetDatabase.GetAssetPath(t.Prefab),
                prefabGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(t.Prefab)), prefabName = t.Prefab.name,
                footprintIdentity = geometry.BuildNavigationGraph(t.MovementFoot, 0f).FootprintIdentity,
                terrains = (int)t.Terrains, weight = t.Weight, boss = t.IsBoss
            }).ToArray(),
            regions = layout.Regions.Select(r =>
            {
                int[] home = Array.Empty<int>();
                if (!r.IsSafety)
                {
                    var type = types.Single(t => t.TypeId == r.TypeId);
                    var graph = geometry.BuildHomeNavigationGraph(type.MovementFoot, r.Nodes, protection);
                    int anchor = layout.Residents.First(p => p.HomeRegionId == r.Id).InitialNode;
                    var field = graph.BuildReturnField(new[] { anchor });
                    home = Enumerable.Range(0, graph.NodeCount).Where(n => field.Distances[n] >= 0).ToArray();
                }
                return new CapturedRegion { id = r.Id, kind = r.Kind.ToString(), typeId = r.TypeId,
                    capacity = r.ConstructiveCapacity, population = r.AssignedPopulation,
                    surface = r.Nodes.ToArray(), buffer = r.BufferNodes.ToArray(), authorizedHomeStoppingNodes = home };
            }).ToArray(),
            residents = layout.Residents.Select((r, i) => new CapturedResident { id = r.Id, typeId = r.TypeId, home = r.HomeRegionId,
                node = r.InitialNode, bodyWorld = nativeResidents[i].transform.position,
                nativeFootCenter = nativeResidents[i].bounds.center, nativeFootSize = nativeResidents[i].bounds.size,
                nativeFootKind = nativeResidents[i].GetType().Name }).ToArray()
        };
        string directory = Path.Combine(evidence, "P2", "Layouts", safeLabel); Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "seed-" + seed.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            "-catalog-" + types.Length + "-boss-denominator-" + recipe.BossChanceDenominator + ".json");
        Assert.That(File.Exists(path), Is.False, "Preserve earlier actual candidate artifacts.");
        File.WriteAllText(path, JsonUtility.ToJson(capture, true), new System.Text.UTF8Encoding(false));
        Debug.Log("ECOLOGY_TERRITORY_ARTIFACT " + path);
    }

    private static void LogNativeQueries(Collider2D foot, Collider2D obstacle, ColliderDistance2D distance)
    {
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = Physics2D.AllLayers, useTriggers = false };
        var overlaps = new Collider2D[128];
        int overlapCount = foot.OverlapCollider(filter, overlaps);
        Assert.That(overlapCount, Is.LessThan(overlaps.Length));
        Debug.Log($"ECOLOGY_NATIVE_QUERY overlapCollider={overlaps.Take(overlapCount).Contains(obstacle)} " +
                  $"footClosestToB={foot.ClosestPoint(distance.pointB).ToString("R")} " +
                  $"obstacleClosestToA={obstacle.ClosestPoint(distance.pointA).ToString("R")} " +
                  $"footOverlapA={foot.OverlapPoint(distance.pointA)} obstacleOverlapA={obstacle.OverlapPoint(distance.pointA)}");
        Vector3 original = foot.transform.position;
        foreach (float offset in new[] { -.05f, 0f, .05f })
        {
            foot.transform.position = original + (Vector3)(distance.normal * offset); Physics2D.SyncTransforms();
            ColliderDistance2D shifted = Physics2D.Distance(foot, obstacle);
            Debug.Log($"ECOLOGY_NATIVE_SHIFT offset={offset:R} distance={shifted.distance:R} overlap={shifted.isOverlapped}");
            foreach (Vector2 direction in new[] { distance.normal, -distance.normal, new Vector2(-distance.normal.y, distance.normal.x) })
            {
                var hits = new RaycastHit2D[128]; int count = foot.Cast(direction, filter, hits, .1f);
                Assert.That(count, Is.LessThan(hits.Length));
                foreach (RaycastHit2D hit in hits.Take(count).Where(hit => hit.collider == obstacle))
                    Debug.Log($"ECOLOGY_NATIVE_CAST offset={offset:R} direction={direction.ToString("R")} " +
                              $"distance={hit.distance:R} normal={hit.normal.ToString("R")} point={hit.point.ToString("R")} " +
                              $"originalMovementBlocks={!(hit.distance <= .00001f && Vector2.Dot(hit.normal, direction) > 0f)}");
            }
        }
        foot.transform.position = original; Physics2D.SyncTransforms();
    }

    private static void LogNativeShapes(Collider2D collider, Vector2 near, string role)
    {
        if (collider == null) return;
        var group = new PhysicsShapeGroup2D(); int count = collider.GetShapes(group);
        var vertices = new List<Vector2>();
        int closest = -1; float best = float.PositiveInfinity;
        for (int shapeIndex = 0; shapeIndex < count; shapeIndex++)
        {
            group.GetShapeVertices(shapeIndex, vertices);
            foreach (Vector2 vertex in vertices)
            {
                Vector2 world = group.localToWorldMatrix.MultiplyPoint3x4(vertex);
                float distance = (world - near).sqrMagnitude;
                if (distance < best) { best = distance; closest = shapeIndex; }
            }
        }
        if (closest < 0) { Debug.Log($"ECOLOGY_NATIVE_SHAPES role={role} count={count}"); return; }
        PhysicsShape2D shape = group.GetShape(closest); group.GetShapeVertices(closest, vertices);
        string points = string.Join(";", vertices.Select(v => ((Vector2)group.localToWorldMatrix.MultiplyPoint3x4(v)).ToString("R")));
        Debug.Log($"ECOLOGY_NATIVE_SHAPES role={role} collider={collider.name}/{collider.GetType().Name} count={count} " +
                  $"shape={closest} kind={shape.shapeType} radius={shape.radius:R} vertices={points}");
    }

    private static Collider2D CreateEffectiveFootProbe(Collider2D source, Transform parent)
    {
        Rigidbody2D originalBody = source.GetComponentInParent<Rigidbody2D>(true);
        Assert.That(originalBody, Is.Not.Null);
        var go = new GameObject("Static geometry oracle probe for " + source.name);
        go.transform.SetParent(parent, false); go.layer = source.gameObject.layer;
        var body = go.AddComponent<Rigidbody2D>();
        body.bodyType = originalBody.bodyType; body.gravityScale = originalBody.gravityScale;
        body.simulated = originalBody.simulated; body.constraints = originalBody.constraints;
        body.collisionDetectionMode = originalBody.collisionDetectionMode; body.interpolation = originalBody.interpolation;
        Vector2 offset = source.transform.TransformPoint(source.offset) - originalBody.transform.position;
        Vector3 scale = source.transform.lossyScale;
        if (source is BoxCollider2D box)
        {
            Assert.That(Mathf.Abs(Mathf.Sin(source.transform.eulerAngles.z * Mathf.Deg2Rad)), Is.LessThan(.00001f));
            var result = go.AddComponent<BoxCollider2D>();
            result.size = Vector2.Scale(box.size, new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));
            result.offset = offset; result.edgeRadius = box.edgeRadius; result.isTrigger = false;
            return result;
        }
        var circle = source as CircleCollider2D;
        Assert.That(circle, Is.Not.Null); Assert.That(Mathf.Abs(scale.x), Is.EqualTo(Mathf.Abs(scale.y)).Within(.00001f));
        var circleResult = go.AddComponent<CircleCollider2D>();
        circleResult.radius = circle.radius * Mathf.Abs(scale.x); circleResult.offset = offset; circleResult.isTrigger = false;
        return circleResult;
    }
}
#endif

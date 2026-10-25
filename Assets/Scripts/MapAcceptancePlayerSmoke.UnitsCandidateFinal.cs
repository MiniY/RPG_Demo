#if RPG_DEMO_ECOLOGY_UNITS_CANDIDATE || RPG_DEMO_ECOLOGY_REMEDIATION
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed partial class MapAcceptancePlayerSmoke
{
    [Serializable] private sealed class UnitsNumericsRow
    {
        public int seed;
        public string originalMap, retainedMap, originalDecoration, retainedDecoration, originalError, retainedError;
        public int[] originalTerrain, retainedTerrain, originalElevation, retainedElevation;
        public Vector2Int[] originalStairLower, originalStairUpper, retainedStairLower, retainedStairUpper;
    }
    [Serializable] private sealed class UnitsNumericsProof
    {
        public string scope = "ISOLATED_NUMERICAL_PROTOTYPE_NO_GAMEPLAY_SELECTION", unity, buildGuid;
        public bool development;
        public List<UnitsNumericsRow> rows = new List<UnitsNumericsRow>();
    }

    private static void CollectRetainedNumericsPrototype(MapGenerationSettings retained)
    {
        var proof = new UnitsNumericsProof { unity = Application.unityVersion, buildGuid = Application.buildGUID, development = Debug.isDebugBuild };
        MethodInfo mapHash = typeof(MapCandidateCollisionGeometry).GetMethod("ComputeMapContentIdentity", BindingFlags.NonPublic | BindingFlags.Static);
        var seeds = Enumerable.Range(0, 64).Select(i => unchecked(20260929 + i * 104729))
            .Concat(new[] { -1391545000, 613580675 }).Distinct().ToArray();
        foreach (int seed in seeds)
        {
            var settings = Instantiate(retained); settings.seed = seed;
            var row = new UnitsNumericsRow { seed = seed }; proof.rows.Add(row);
            bool detailed = seed == 613580675 || seed == 20260929 || seed == -1391545000 || seed == 21517677;
            try
            {
                MapData original = RandomMapGenerator.Generate(settings, true);
                row.originalMap = (string)mapHash.Invoke(null, new object[] { original });
                var decoration = MapSimpleDecorationGenerator.Generate(original, settings);
                row.originalDecoration = string.Join(";", decoration.Placements.Select(p => p.VariantId + "@" + p.AnchorCell));
                if (detailed)
                {
                    row.originalTerrain = Enumerable.Range(0, original.Width * original.Height).Select(n => (int)original.GetCell(original.Origin + new Vector2Int(n % original.Width, n / original.Width)).terrainType).ToArray();
                    row.originalElevation = Enumerable.Range(0, original.Width * original.Height).Select(n => original.GetElevation(original.Origin + new Vector2Int(n % original.Width, n / original.Width))).ToArray();
                    row.originalStairLower = original.Stairs.Select(s => s.LowerCell).ToArray(); row.originalStairUpper = original.Stairs.Select(s => s.UpperCell).ToArray();
                }
            }
            catch (Exception error) { row.originalError = error.ToString(); }
            try
            {
                MapData canonical = RpgDemo.RetainedTerrain.Float32V1.RandomMapGenerator.Generate(settings, true);
                row.retainedMap = (string)mapHash.Invoke(null, new object[] { canonical });
                var decoration = RpgDemo.RetainedTerrain.Float32V1.MapSimpleDecorationGenerator.Generate(canonical, settings);
                row.retainedDecoration = string.Join(";", decoration.Placements.Select(p => p.VariantId + "@" + p.AnchorCell));
                if (detailed)
                {
                    row.retainedTerrain = Enumerable.Range(0, canonical.Width * canonical.Height).Select(n => (int)canonical.GetCell(canonical.Origin + new Vector2Int(n % canonical.Width, n / canonical.Width)).terrainType).ToArray();
                    row.retainedElevation = Enumerable.Range(0, canonical.Width * canonical.Height).Select(n => canonical.GetElevation(canonical.Origin + new Vector2Int(n % canonical.Width, n / canonical.Width))).ToArray();
                    row.retainedStairLower = canonical.Stairs.Select(s => s.LowerCell).ToArray(); row.retainedStairUpper = canonical.Stairs.Select(s => s.UpperCell).ToArray();
                }
            }
            catch (Exception error) { row.retainedError = error.ToString(); }
            Destroy(settings);
        }
        System.IO.File.WriteAllText(System.IO.Path.Combine(outputRoot, "UnitsNumericsProof.json"), JsonUtility.ToJson(proof, true));
    }

    [Serializable] private sealed class UnitsRoleInput
    {
        public string logicalId, role, potentialGeometry, name;
        public Vector3 scale, position;
        public Quaternion rotation;
    }

    private static void CaptureUnitsPlanningInputs(UnitsSeedProof row, MapGenerationController controller)
    {
        var accepted = (EcologyPlanningAttemptResult)typeof(MapGenerationController).GetField("zoningAccepted", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        var context = controller.Orchestrator.Context;
        row.mapContentIdentity = accepted.TerrainGeometry.MapContentIdentity; row.actualMapSeed = context.ActiveMap.GenerationSeed;
        row.terrainProjection = accepted.TerrainGeometry.ProjectionIdentity; row.terrainPhysical = accepted.TerrainGeometry.PhysicalContentIdentity;
        row.staticPhysical = accepted.StaticGeometry.PhysicalContentIdentity; row.initialPhysical = accepted.InitialPlacementGeometry.PhysicalContentIdentity;
        row.retainedSettingsJson = JsonUtility.ToJson(controller.Settings);
        var merchant = controller.GetComponentInParent<MerchantPlacementService>();
        var sources = new Dictionary<string, Transform>(StringComparer.Ordinal)
        { { MerchantPlacementService.MainMerchantLogicalObjectId, ((IMerchantPlacementTarget)merchant.MerchantTarget).PlacementTransform } };
        foreach (var binding in controller.GetComponentInParent<PlantPlacementService>().Bindings) sources.Add(binding.LogicalObjectId, binding.PlantTransform);
        foreach (var binding in controller.GetComponentInParent<AnimalPlacementService>().Bindings) sources.Add(binding.LogicalObjectId, binding.AnimalTransform);
        foreach (var reservation in context.ActivePlacementPlan.Reservations.OrderBy(r => r.LogicalObjectId, StringComparer.Ordinal))
        {
            if (!sources.TryGetValue(reservation.LogicalObjectId, out Transform source)) continue;
            var potential = MapPlannedSolidGeometry.Capture(reservation.LogicalObjectId, source,
                controller.TilemapRenderer.Coordinates.CellToWorld(reservation.AnchorCell), source.rotation);
            row.roleInputs.Add(new UnitsRoleInput { logicalId = reservation.LogicalObjectId, role = reservation.Role, name = source.name,
                scale = source.lossyScale, position = source.position, rotation = source.rotation,
                potentialGeometry = accepted.TerrainGeometry.WithPlannedSolids(new[] { potential }).PhysicalContentIdentity });
        }
        System.IO.File.WriteAllText(System.IO.Path.Combine(outputRoot, "Seed_" + row.seed + "_PlanningInputs.json"), JsonUtility.ToJson(row, true));
    }

    [Serializable] private sealed class UnitsCombatProof
    {
        public string family, mode, prefab, generation, layout, nextGeneration, status = "INCOMPLETE";
        public int seed, pathEdges, playerPresses, requests, frames, createdRewards, pickedRewards, targetDelta, explosions, explosionDamageCount;
        public double armedAt, explosionDueAt, damageAppliedAt, selfDefeatAt;
        public float playerHealthBefore, playerHealthAfter, monsterHealthBefore, monsterHealthAfter;
        public bool actualOriginalAiDamage, actualPlayerMouseDamage, deathRegistryPool, actualF, oldScopeRetired;
        public string arrangement = "Other monster AI temporarily disabled; remaining targets cleared via original receivers for exit. Real input delivered through original readers before automatic physics. No extra HP, teleport, physics changes or whole-floor natural combat claim.";
        public List<UnitsCombatFrame> motionFrames = new List<UnitsCombatFrame>();
        public List<UnitsQualityRejection> nextFloorRejections = new List<UnitsQualityRejection>();
    }
    [Serializable] private sealed class UnitsQualityRejection
    {
        public int requestedSeed;
        public string code, reason, profile;
    }
    [Serializable] private sealed class UnitsCombatFrame
    {
        public string file, phase, sprite;
        public float real, world, playerHealth, monsterHealth;
        public Vector2 playerPosition, monsterPosition;
    }

    private static void RequireUnits(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private IEnumerator CollectUnitsCombat(UnitsCandidateProof proof, TowerRunCoordinator coordinator,
        MapGenerationController controller, UnitsSeedProof seed, string family, bool earlyKill = false)
    {
        var context = controller.Orchestrator.Context;
        var accepted = (EcologyPlanningAttemptResult)typeof(MapGenerationController).GetField("zoningAccepted", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        GameObject player = controller.Player.gameObject; Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        Collider2D foot = player.transform.Find("MovementFeet").GetComponent<Collider2D>();
        var playerCombat = player.GetComponent<EcologyCombatBinding>(); var health = player.GetComponent<PlayerDamageController>();
        var population = controller.GetComponentInParent<MapEcologyPopulationService>();
        var actors = context.ActivePlacementPlan.Ecology.Residents.Select(r =>
        {
            RequireUnits(population.TryGetActiveInstance(r.Id, out UnityEngine.Object instance), "Actual production resident missing.");
            var actor = ((Component)instance).gameObject;
            return (resident: r, actor, ai: actor.GetComponent<MonsterAIController>(), enabled: actor.GetComponent<MonsterAIController>().enabled);
        }).ToArray();
        var capture = new UnitsCombatProof { family = family, mode = earlyKill ? "EarlyMouseKill" : "OriginalAiThenMouseOrSelfDefeat", seed = seed.seed, generation = context.ActiveGenerationId.ToString(), layout = context.ActivePlacementPlan.Ecology.Signature };
        proof.combats.Add(capture);
        var mouse = InputSystem.AddDevice<Mouse>("UnitsCandidateOriginalCombat");
        var barrier = playerCombat.Barrier; var requests = new List<EcologyAttackRequest>();
        void Trace(EcologyAttackRequest request, string phase) { if (phase == "Enqueued") requests.Add(request); }
        barrier.OnTrace += Trace;
        int pickedRewards = 0;
        void Picked(RewardSO reward, int amount) { pickedRewards++; }
        RewardPickup.OnRewardPickedUp += Picked;
        foreach (var actor in actors) actor.ai.enabled = false;
        BarrelExplosionAttack observedExplosion = null;
        void BarrelTrace(string label, double world)
        {
            if (label == "Armed") capture.armedAt = world;
            else if (label == "NominalExplosionDue") capture.explosionDueAt = world;
            else if (label == "DamageApplied") capture.damageAppliedAt = world;
            else if (label == "SelfDefeat") capture.selfDefeatAt = world;
        }
        try
        {
            if (!mouse.enabled) InputSystem.EnableDevice(mouse);
            MapCandidateCollisionGeometry travelGeometry = ObserveUnitsTravelGeometry(accepted.StaticGeometry);
            EcologyNavigationGraph graph = travelGeometry.BuildNavigationGraph(foot, 0f);
            RequireUnits(travelGeometry.TryWorldNode(body.position, out int start), "Player lacks actual graph node.");
            var fromSpawn = graph.BuildReturnField(new[] { start });
            BaseDamageable[] allDamageables = FindObjectsOfType<BaseDamageable>();
            GameObject target = null; int goal = -1, best = int.MaxValue;
            Rect[] protectedRects = (Rect[])typeof(EcologyDamageBarrier).GetProperty("Protection", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(barrier);
            foreach (var actor in actors.Where(a => a.actor.GetComponent<PooledObject>().SourcePrefab.name.StartsWith(family + "_", StringComparison.Ordinal)))
            for (int n = 0; n < graph.NodeCount; n++)
            {
                if (!graph.IsLegal(n) || fromSpawn.Distances[n] < 0 || fromSpawn.Distances[n] >= best) continue;
                Vector2 pose = travelGeometry.NodeWorldPosition(n); float distance = Vector2.Distance(pose, actor.actor.transform.position);
                if (distance < 1.15f || distance > 1.55f || !MapElevationRuntimeQuery.SameLevel(pose, actor.actor.transform.position)) continue;
                Vector2 centre = (Vector2)foot.bounds.center + pose - body.position, extents = foot.bounds.extents;
                if (protectedRects.Any(r => r.Overlaps(Rect.MinMaxRect(centre.x - extents.x, centre.y - extents.y, centre.x + extents.x, centre.y + extents.y)))) continue;
                if (allDamageables.Any(d => d.gameObject != actor.actor && d.gameObject.activeInHierarchy && MapElevationRuntimeQuery.SameLevel(pose, d.transform.position) && Vector2.Distance(pose, d.transform.position) <= distance + .05f)) continue;
                target = actor.actor; goal = n; best = fromSpawn.Distances[n];
            }
            RequireUnits(target != null, "No naturally placed reachable nearest " + family + " target.");
            capture.prefab = target.GetComponent<PooledObject>().SourcePrefab.name; capture.pathEdges = best;
            BaseMonster monster = target.GetComponent<BaseMonster>(); var tracked = actors.Single(a => a.actor == target);
            void Frame(string phase)
            {
                string file = "Combat_" + family + "_" + capture.mode + "_" + capture.motionFrames.Count.ToString("D3") + ".png";
                CaptureUnitsView(file, body.position, seed.gameplayCameraSize);
                capture.motionFrames.Add(new UnitsCombatFrame { file = file, phase = phase, real = Time.realtimeSinceStartup, world = Time.time,
                    playerPosition = body.position, monsterPosition = target.GetComponent<Rigidbody2D>().position,
                    playerHealth = health.CurrentHealth, monsterHealth = monster.CurrentHealth, sprite = target.GetComponentInChildren<SpriteRenderer>().sprite.name });
            }
            yield return WalkUnitsOriginalInput(player, travelGeometry, graph, goal);
            RequireUnits(!playerCombat.ProtectionNow && playerCombat.MotionFaultRevision == 0, "Actual combat approach remains protected or faulted.");
            int rewardsBefore = FindObjectsOfType<RewardPickup>().Length;
            int pickedBefore = pickedRewards;
            var floorOwner = FindObjectOfType<TowerFloorRuntimeController>();
            int defeatedBefore = floorOwner.DefeatedTargetCount;
            BarrelExplosionAttack explosion = target.GetComponent<BarrelExplosionAttack>();
            observedExplosion = explosion;
            if (observedExplosion != null) observedExplosion.OnTrace += BarrelTrace;
            capture.playerHealthBefore = health.CurrentHealth; Frame("BeforeOriginalAi"); target.GetComponent<MonsterAIController>().enabled = true;
            float deadline = Time.time + 6f; int step = 0;
            if (!earlyKill)
            {
                while (health.CurrentHealth == capture.playerHealthBefore && Time.time < deadline)
                { yield return new WaitForFixedUpdate(); if (++step % 5 == 0) Frame("OriginalAiWindup"); }
                RequireUnits(health.CurrentHealth < capture.playerHealthBefore, "Original Player build AI did not apply actual damage.");
                if (explosion == null) target.GetComponent<MonsterAIController>().enabled = false;
                capture.actualOriginalAiDamage = true;
                capture.playerHealthAfter = health.CurrentHealth; Frame("ActualPlayerDamage");
                // New Barrel must finish its original single death after the deferred damage fence.
                if (explosion != null)
                {
                    deadline = Time.time + 1f;
                    while (!monster.IsDefeated && Time.time < deadline) yield return null;
                    capture.explosions = explosion.ExplosionCount; capture.explosionDamageCount = explosion.DamageCount;
                    RequireUnits(monster.IsDefeated && explosion.ExplosionCount == 1 && explosion.DamageCount == 1,
                        $"Barrel self-explosion did not settle exactly once: defeated={monster.IsDefeated}, explosions={explosion.ExplosionCount}, damage={explosion.DamageCount}.");
                }
                else yield return new WaitForSeconds(.7f);
            }
            else
            {
                deadline = Time.time + 1f;
                while (explosion != null && !explosion.IsLatched && Time.time < deadline) yield return null;
                RequireUnits(explosion != null && explosion.IsLatched, "Early-kill proof must kill a genuinely armed original FSM Barrel.");
            }
            capture.monsterHealthBefore = monster.CurrentHealth;
            while (!monster.IsDefeated && capture.playerPresses < 12)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left)); yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState()); capture.playerPresses++;
                deadline = Time.time + .55f; step = 0;
                while (Time.time < deadline) { yield return new WaitForFixedUpdate(); if (++step % 5 == 0) Frame("OriginalMouseAttack"); }
                if (capture.playerPresses == 1)
                    RequireUnits(Mathf.Abs(monster.CurrentHealth - Mathf.Max(0f, capture.monsterHealthBefore - 20f)) < .001f, "First actual mouse press did not apply exactly the original 20 damage.");
            }
            capture.monsterHealthAfter = monster.CurrentHealth;
            RequireUnits(monster.IsDefeated && !context.ActiveRegistry.TryGet(tracked.resident.Id, out _), "Original defeat did not unregister the resident.");
            capture.pickedRewards = pickedRewards - pickedBefore;
            capture.createdRewards = FindObjectsOfType<RewardPickup>().Length - rewardsBefore + capture.pickedRewards;
            capture.targetDelta = floorOwner.DefeatedTargetCount - defeatedBefore;
            capture.explosions = explosion != null ? explosion.ExplosionCount : 0;
            if (explosion != null)
            {
                RequireUnits(capture.createdRewards == 1 && capture.targetDelta == 1, "Barrel must create its original one reward and count exactly one clear target.");
                int rewardsAfter = FindObjectsOfType<RewardPickup>().Length;
                monster.TakeDamage(float.MaxValue);
                RequireUnits(floorOwner.DefeatedTargetCount - defeatedBefore == 1 && FindObjectsOfType<RewardPickup>().Length == rewardsAfter, "Repeated defeat duplicated a reward or target settlement.");
                if (earlyKill) RequireUnits(explosion.ExplosionCount == 0 && health.CurrentHealth == capture.playerHealthBefore, "Early kill must prevent the blast and all player damage.");
            }
            yield return new WaitForSeconds(.7f); RequireUnits(!target.activeInHierarchy, "Actual defeat did not return to original pool.");
            RequireUnits(requests.All(r => r.Status != EcologyAttackStatus.FaultRejected) &&
                (explosion != null && !earlyKill || requests.Any(r => r.AttackerId == playerCombat.ResidentId && r.DamageApplied)), "Actual combat request fault or absent required player damage.");
            capture.actualPlayerMouseDamage = capture.playerPresses > 0; capture.deathRegistryPool = true; capture.requests = requests.Count;
            foreach (var entry in context.ActiveRegistry.Entries.Where(e => e.Role == MonsterPlacementService.MonsterRole).ToArray())
                ((Component)entry.Instance).GetComponent<BaseDamageable>().TakeDamage(float.MaxValue);
            var floor = FindObjectOfType<TowerFloorRuntimeController>(); RequireUnits(floor.State == TowerFloorExitState.Available, "Original exit did not unlock after controlled remaining clear.");
            yield return new WaitForSeconds(.7f);
            var exitTravel = ObserveUnitsTravelGeometry(accepted.StaticGeometry);
            yield return WalkUnitsOriginalInput(player, exitTravel, exitTravel.BuildNavigationGraph(foot, 0f), accepted.ExitApproachNode);
            Guid? oldGeneration = context.ActiveGenerationId; MapObjectRegistry oldRegistry = context.ActiveRegistry;
            InputSystem.QueueStateEvent(smokeKeyboard, new KeyboardState(Key.F)); yield return null;
            InputSystem.QueueStateEvent(smokeKeyboard, new KeyboardState());
            deadline = Time.realtimeSinceStartup + 8f;
            while (context.ActiveGenerationId == oldGeneration && Time.realtimeSinceStartup < deadline) yield return null;
            while (!context.IsReady && coordinator.State == TowerRunFlowState.Failure &&
                context.Failure?.Code == "EcologyBudgetExhaustedOnFixedTerrain" && capture.nextFloorRejections.Count < 2)
            {
                var failed = context.CreateDiagnosticSnapshot();
                capture.nextFloorRejections.Add(new UnitsQualityRejection { requestedSeed = failed.ActiveRequest.RequestedSeed,
                    profile = failed.ActiveRequest.ProfileId, code = context.Failure.Code, reason = context.Failure.Reason });
                // This is the existing failure-screen action, not an increased same-terrain budget.
                // All rejected terrain/attempt messages remain in the proof and original Player log.
                coordinator.RetryFailedOperation(); yield return null;
            }
            RequireUnits(context.IsReady && context.ActiveGenerationId != oldGeneration && context.ActivePlacementPlan.Ecology != null && oldRegistry.IsRetired && (barrier == null || !barrier.IsScopeOpen), "Actual F failed to enter a fresh versioned floor or retire old scope.");
            RequireUnits(context.CreateDiagnosticSnapshot().ActiveRequest.ProfileId == AcceptanceProfileId,
                "Original next-floor Retry substituted another profile.");
            capture.actualF = true; capture.oldScopeRetired = true; capture.nextGeneration = context.ActiveGenerationId.ToString();
            capture.frames = capture.motionFrames.Count; capture.status = "PASS";
        }
        finally
        {
            if (barrier != null) barrier.OnTrace -= Trace;
            if (observedExplosion != null) observedExplosion.OnTrace -= BarrelTrace;
            RewardPickup.OnRewardPickedUp -= Picked;
            if (mouse.added) InputSystem.RemoveDevice(mouse);
            foreach (var actor in actors) if (actor.ai != null) actor.ai.enabled = actor.enabled;
        }
    }

    private static MapCandidateCollisionGeometry ObserveUnitsTravelGeometry(MapCandidateCollisionGeometry basis)
    {
        var controller = FindObjectOfType<MapGenerationController>();
        var registry = controller.Orchestrator.Context.ActiveRegistry;
        var solids = registry.Entries.Where(e => e.Instance != null &&
            (e.Role == MonsterPlacementService.MonsterRole || e.Role == AnimalPlacementService.AnimalRole))
            .Select(e => MapPlannedSolidGeometry.CaptureLive("qa-observed/" + e.LogicalObjectId, ((Component)e.Instance).transform)).ToArray();
        return basis.WithPlannedSolids(solids);
    }

    private static IEnumerator WalkUnitsOriginalInput(GameObject player, MapCandidateCollisionGeometry geometry,
        EcologyNavigationGraph graph, int goal)
    {
        var body = player.GetComponent<Rigidbody2D>(); var action = player.GetComponent<PlayerAction>();
        var combat = player.GetComponent<EcologyCombatBinding>();
        RequireUnits(geometry.TryWorldNode(body.position, out int node), "Actual input travel has no node.");
        var field = graph.BuildReturnField(new[] { goal });
        if(!graph.IsLegal(node) || field.Distances[node]<0)
        {
            var controller=FindObjectOfType<MapGenerationController>();
            var accepted=(EcologyPlanningAttemptResult)typeof(MapGenerationController).GetField("zoningAccepted",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(controller);
            var foot=player.transform.Find("MovementFeet").GetComponent<Collider2D>();
            var baseGraph=accepted.StaticGeometry.BuildNavigationGraph(foot,0f);var baseField=baseGraph.BuildReturnField(new[]{goal});
            accepted.StaticGeometry.TryWorldNode(controller.TilemapRenderer.Coordinates.CellToWorld(controller.LastGeneratedMap.SpawnCell),out int spawn);
            var spawnField=baseGraph.BuildReturnField(new[]{spawn});
            string text="seed="+controller.LastGeneratedMap.GenerationSeed+" body="+body.position.ToString("R")+" node="+node+" goal="+goal+" goalWorld="+geometry.NodeWorldPosition(goal).ToString("R")+
                " frozenLegal="+graph.IsLegal(node)+" frozenDistance="+field.Distances[node]+" baseLegal="+baseGraph.IsLegal(node)+" baseDistance="+baseField.Distances[node]+" baseSpawnToGoal="+spawnField.Distances[goal]+" footBounds="+foot.bounds;
            foreach(var entry in controller.Orchestrator.Context.ActiveRegistry.Entries.Where(e=>e.Instance is Component))
                text+="\n"+entry.Role+"/"+entry.LogicalObjectId+" "+((Component)entry.Instance).transform.position.ToString("R");
            File.WriteAllText(Path.Combine(outputRoot,"ActualInputNoRoute.txt"),text);
            throw new InvalidOperationException("Actual input travel cannot reach goal. "+text.Split('\n')[0]);
        }
        void Deliver(KeyboardState state)
        {
            if (!smokeKeyboard.enabled) InputSystem.EnableDevice(smokeKeyboard);
            InputSystem.QueueStateEvent(smokeKeyboard, state); InputSystem.Update();
            typeof(GameInput).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(GameInput.Instance, null);
            typeof(PlayerAction).GetMethod("ReadMoveInput", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(action, null);
        }
        int steps = 0;
        while (true)
        {
            Vector2 target = geometry.NodeWorldPosition(node); float deadline = Time.time + 3f;
            while (Vector2.Distance(body.position, target) > .08f && Time.time < deadline)
            {
                Vector2 delta = target - body.position;
                Key key = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? delta.x >= 0f ? Key.D : Key.A : delta.y >= 0f ? Key.W : Key.S;
                Deliver(new KeyboardState(key)); yield return new WaitForFixedUpdate();
                RequireUnits(combat.MotionFaultRevision == 0, "Actual input travel observer fault.");
            }
            Deliver(new KeyboardState());
            if (Vector2.Distance(body.position, target) >= .14f)
            {
                Collider2D foot = player.transform.Find("MovementFeet").GetComponent<Collider2D>();
                var near = FindObjectsOfType<Collider2D>().Where(c => c.enabled && !c.isTrigger && c.attachedRigidbody != body && c.shapeCount > 0)
                    .Select(c => (c, d: foot.Distance(c))).Where(p => p.d.isValid).OrderBy(p => p.d.distance).Take(8)
                    .Select(p => p.c.name + "/" + p.c.GetType().Name + "/gap=" + p.d.distance.ToString("R") + "/overlap=" + p.d.isOverlapped + "/bounds=" + p.c.bounds);
                string actual = "node=" + node + " body=" + body.position.ToString("R") + " target=" + target.ToString("R") + " foot=" + foot.bounds + " native=" + string.Join(";", near);
                File.WriteAllText(Path.Combine(outputRoot, "ActualInputTravelBlocked.txt"), actual);
                throw new InvalidOperationException("Actual input travel stopped at a native obstacle: " + actual);
            }
            if (node == goal) break;
            int next = field.Next[node]; RequireUnits(next >= 0 && graph.CanMove(node, next) && ++steps <= graph.NodeCount, "Invalid original-foot route."); node = next;
        }
        Deliver(new KeyboardState()); yield return null; yield return new WaitForFixedUpdate();
    }
}
#endif

#if RPG_DEMO_ECOLOGY_REMEDIATION
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Tilemaps;

public sealed partial class MapAcceptancePlayerSmoke
{
    [Serializable] private sealed class RemediationSurfaceProof
    {
        public string status = "INCOMPLETE", arrangement = "Actual unchanged player, original input and automatic physics. Other monster AI temporarily paused. No actor teleport, collider/scale changes or simulated physics. Existing foam timing observed only; P7 visual rules remain WAITING_REFERENCE_DETAILS.";
        public int seed, stairLanes, stairTraversals, foamCells, foamFrameCount;
        public Vector3Int foamSample;
        public List<float> observedFoamTimes = new List<float>();
        public Vector2 wallStart, wallEnd;
        public float wallMinimumGap;
        public List<int> observedFoamFrames = new List<int>();
        public List<RemediationSurfaceFrame> frames = new List<RemediationSurfaceFrame>();
    }
    [Serializable] private sealed class RemediationSurfaceFrame
    {
        public string file, phase;
        public float real, world, nativeGap;
        public Vector2 body, foot;
    }

    private IEnumerator CollectRemediationSurface(MapGenerationController controller, UnitsSeedProof seed)
    {
        var result = new RemediationSurfaceProof { seed = seed.seed };
#if RPG_DEMO_SURFACE_V4
        result.arrangement = "R4 actual unchanged player and original input/physics; other AI paused during controlled wall/stair/visual collection. All R3 and legacy Mountain native faces checked. New project shoreline mapping and light shadows verified separately; human review pending.";
#endif
        var accepted = (EcologyPlanningAttemptResult)typeof(MapGenerationController).GetField("zoningAccepted", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        var player = controller.Player.gameObject; var body = player.GetComponent<Rigidbody2D>();
        var foot = player.transform.Find("MovementFeet").GetComponent<CircleCollider2D>();
        var action = player.GetComponent<PlayerAction>(); var combat = player.GetComponent<EcologyCombatBinding>();
        var geometry = ObserveUnitsTravelGeometry(accepted.StaticGeometry); var graph = geometry.BuildNavigationGraph(foot, 0f);
        var map = controller.Orchestrator.Context.ActiveMap;
        var nativeFaces = controller.TilemapRenderer.GroundTilemap.transform.parent.GetComponentsInChildren<PolygonCollider2D>(true)
            .Where(c => c.name == "GeneratedCliffFaces_R3" || c.name == "GeneratedLegacyMountainFaces_R4").Where(c => c.enabled && c.pathCount>0).ToArray();
        RequireUnits(nativeFaces.Length>0,"Actual projected native faces missing");
        float ClosestFaceGap() => nativeFaces.Min(c=>foot.Distance(c).distance);
        var ai = FindObjectsOfType<MonsterAIController>().Select(a => (a, enabled: a.enabled)).ToArray();
        foreach (var entry in ai) entry.a.enabled = false;
        void Deliver(Key? key)
        {
            if (!smokeKeyboard.enabled) InputSystem.EnableDevice(smokeKeyboard);
            InputSystem.QueueStateEvent(smokeKeyboard, key.HasValue ? new KeyboardState(key.Value) : new KeyboardState()); InputSystem.Update();
            typeof(GameInput).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(GameInput.Instance, null);
            typeof(PlayerAction).GetMethod("ReadMoveInput", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(action, null);
        }
        void Frame(string phase)
        {
            foreach(var faceCollider in nativeFaces)
            {var gap=foot.Distance(faceCollider);RequireUnits(gap.isValid && !gap.isOverlapped,"Actual unchanged player overlaps projected cliff face: "+faceCollider.name);}
            float distance=ClosestFaceGap();
            string file = "Surface_" + seed.seed + "_" + result.frames.Count.ToString("D4") + ".png";
            CaptureUnitsView(file, body.position, seed.gameplayCameraSize);
            result.frames.Add(new RemediationSurfaceFrame { file = file, phase = phase, real = Time.realtimeSinceStartup,
                world = Time.time, body = body.position, foot = foot.bounds.center, nativeGap = distance });
            RequireUnits(combat.MotionFaultRevision == 0, "Surface input observer fault.");
        }
        try
        {
            RequireUnits(geometry.TryWorldNode(body.position, out int current), "Surface probe lacks actual player node.");
            var reach = graph.BuildReturnField(new[] { current });
            var surface = MapSurfaceV4Policy.Build(map, controller.TilemapRenderer.TerrainV2Palette, controller.Settings);
            int wallGoal = -1, best = int.MaxValue;
            foreach (var face in surface.Faces)
            {
                Vector2 pose = controller.TilemapRenderer.Coordinates.CellToWorld(face.Cell + Vector2Int.down);
                if (geometry.TryWorldNode(pose, out int n) && graph.IsLegal(n) && reach.Distances[n] >= 0 && reach.Distances[n] < best)
                { wallGoal = n; best = reach.Distances[n]; }
            }
            RequireUnits(wallGoal >= 0, "No reachable actual south cliff approach in this fixed map.");
            yield return WalkUnitsOriginalInput(player, geometry, graph, wallGoal);
            result.wallStart = body.position; Frame("BeforeWallHold");
            Vector2 stableStart = body.position;
            for (int step = 0; step < 80; step++)
            {
                Deliver(Key.W); yield return new WaitForFixedUpdate();
                if (step == 59) stableStart = body.position;
                if (step % 4 == 0) Frame("OriginalNorthInputAtCliff");
            }
            Deliver(null); yield return new WaitForFixedUpdate();
            result.wallEnd = body.position; result.wallMinimumGap = ClosestFaceGap();
            RequireUnits(Vector2.Distance(stableStart, body.position) < .06f && result.wallMinimumGap < .3f,
                "Actual cliff approach did not settle beside the native face.");
            Frame("WallStoppedWithoutOverlap");
            // Return to a certified centre through the same original input owner.
            yield return WalkUnitsOriginalInput(player, geometry, graph, wallGoal);
            foreach (var stair in map.Stairs)
            {
                bool hasLower = geometry.TryWorldNode(controller.TilemapRenderer.Coordinates.CellToWorld(stair.LowerCell), out int lower);
                bool hasUpper = geometry.TryWorldNode(controller.TilemapRenderer.Coordinates.CellToWorld(stair.UpperCell), out int upper);
                RequireUnits(hasLower && hasUpper, "Stair endpoints missing.");
                yield return WalkUnitsOriginalInput(player, geometry, graph, lower);
                foreach (int destination in new[] { upper, lower })
                {
                    Frame("BeforeStairCrossing/" + stair.LowerCell + "->" + destination);
                    var crossing = WalkUnitsOriginalInput(player, geometry, graph, destination); int steps = 0;
                    try { while (crossing.MoveNext()) { yield return crossing.Current; if (++steps % 2 == 0) Frame("OriginalInputStairCrossing"); } }
                    finally { (crossing as IDisposable)?.Dispose(); }
                    result.stairTraversals++; Frame("CompletedStairCrossing");
                }
                result.stairLanes++;
            }
            RequireUnits(result.stairLanes > 0 && result.stairTraversals == result.stairLanes * 2, "All actual stair lanes need both directions.");
#if RPG_DEMO_SURFACE_V4
            yield return CollectSurfaceV4VisualEvidence(controller,seed);
            result.foamCells=controller.TilemapRenderer.GroundTilemap.transform.parent.GetComponentInChildren<MapSurfaceV4Visuals>(true).ShoreCells;
#else
            var foam = controller.TilemapRenderer.GroundTilemap.transform.parent.GetComponentsInChildren<Tilemap>(true).Single(t => t.name == "GeneratedWaterFoam");
            var foamCells = new List<Vector3Int>();
            foreach (Vector3Int cell in foam.cellBounds.allPositionsWithin) if (foam.HasTile(cell)) foamCells.Add(cell);
            result.foamCells = foamCells.Count;
            RequireUnits(result.foamCells > 0, "Existing foam sample is empty.");
            Vector3Int sample = foamCells.OrderBy(cell => Vector2.Distance(foam.GetCellCenterWorld(cell), body.position)).First();
            result.foamSample = sample;
            result.foamFrameCount = foam.GetAnimationFrameCount(sample);
            for (int frame = 0; frame < 32; frame++)
            {
                // Tile animation culling must not be confused with a frozen visible tile.
                string file = "Foam_" + seed.seed + "_" + frame.ToString("D3") + ".png";
                CaptureUnitsView(file, foam.GetCellCenterWorld(sample), seed.gameplayCameraSize);
                result.observedFoamFrames.Add(foam.GetAnimationFrame(sample));
                result.observedFoamTimes.Add(foam.GetAnimationTime(sample));
                yield return new WaitForSeconds(.05f);
            }
            RequireUnits(result.foamFrameCount > 1 && result.observedFoamFrames.Distinct().Count() > 1, "Existing dynamic foam did not actually animate.");
#endif
            result.status = "PASS";
        }
        finally
        {
            Deliver(null);
            foreach (var entry in ai) if (entry.a != null) entry.a.enabled = entry.enabled;
            File.WriteAllText(Path.Combine(outputRoot, "Seed_" + seed.seed + "_SurfacePlayerProof.json"), JsonUtility.ToJson(result, true));
        }
    }
}
#endif

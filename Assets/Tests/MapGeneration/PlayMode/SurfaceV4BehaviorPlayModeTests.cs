using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

public sealed class SurfaceV4BehaviorPlayModeTests
{
    private GameObject holder;
    private MapTilemapRenderer terrain;
    private MapSimpleDecorationRenderer decoration;
    private MapSurfaceV4Settings settings;
    private MapData fixtureMap;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly string[] prefabs = { "Assets/Prefabs/Players/Warrior_Blue.prefab",
        "Assets/Prefabs/Monsters/Torch/Torch_Blue.prefab", "Assets/Resources/Ecology/RemediationV3/Barrel_Blue.prefab",
        "Assets/Prefabs/Monsters/TNT/TNT_Blue.prefab" };
    [Serializable] private sealed class Motion
    {
        public string actor, scenario;
        public Vector2 start, goal, end;
        public int startLevel, endLevel, physicsSteps;
        public float minimumGap = float.PositiveInfinity;
        public List<Vector2> poses = new List<Vector2>();
    }
    [Serializable] private sealed class Proof
    {
        public string scope = "Original prefab, original movement method, automatic physics; isolated intent. Concurrent actors keep independent native feet. No IgnoreCollision, resizing or teleports during each movement. Full natural AI and Player input are separate acceptance work.";
        public List<Motion> cases = new List<Motion>();
    }
    private sealed class Pawn
    {
        public GameObject root;
        public Rigidbody2D body;
        public Collider2D foot;
        public MonoBehaviour owner;
        public MethodInfo move;
        public bool player;
        public void Drive(Vector2 goal)
        {
            if (player)
                owner.GetType().GetField("moveInput", Private).SetValue(owner,
                    Vector2.Distance(body.position, goal) < .045f ? Vector2.zero : (goal - body.position).normalized);
            else owner.GetType().GetMethod("SetDestination").Invoke(owner, new object[] { goal });
            move.Invoke(owner, null);
        }
    }
    private IEnumerator Fixture()
    {
        string output = Environment.GetEnvironmentVariable("RPG_DEMO_REMEDIATION_EVIDENCE");
        Assert.That(output, Is.Not.Null.And.Not.Empty);
        TowerRunRuntimeEnvironment.ConfigureTestIsolation(Path.Combine(output, "BehaviorSlots"));
        var fixtureScene = SceneManager.CreateScene("OwnedEcologyPhysicalFixture-" + Guid.NewGuid().ToString("N"));
        SceneManager.SetActiveScene(fixtureScene);
        var priorMap = SceneManager.GetSceneByName("SampleScene");
        if (priorMap.IsValid() && priorMap.isLoaded) yield return SceneManager.UnloadSceneAsync(priorMap);
        Assert.That(UnityEngine.Object.FindObjectOfType<MapGenerationController>(), Is.Null, "Physical fixture must not inherit another map's colliders.");
        holder = new GameObject("Owned R4 behavior fixture"); holder.SetActive(false);
        var runtime = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/MapGeneration/RandomMapRuntime.prefab"), holder.transform);
        terrain = runtime.GetComponentInChildren<MapTilemapRenderer>(true);
        decoration = runtime.GetComponentInChildren<MapSimpleDecorationRenderer>(true);
        foreach (var b in runtime.GetComponentsInChildren<MonoBehaviour>(true))
            if (b != terrain && b != decoration) UnityEngine.Object.Destroy(b);
        yield return null;
        foreach (Transform t in runtime.GetComponentsInChildren<Transform>(true)) t.gameObject.SetActive(true);
        holder.SetActive(true); Time.timeScale = 1f;
        settings = Resources.Load<MapSurfaceV4Settings>("Ecology/SurfaceV4/Settings_SurfaceV4");
        Assert.That(settings, Is.Not.Null);
        yield return null;
    }
    private MapData Platform()
    {
        var map = new MapData(24, 24, new Vector2Int(-12, -12));
        for (int x = -5; x <= 5; x++) for (int y = 0; y <= 5; y++) map.SetElevation(new Vector2Int(x, y), 1);
        for (int x = 2; x <= 4; x++) for (int y = 3; y <= 5; y++) map.SetElevation(new Vector2Int(x, y), 2);
        foreach (int x in new[] { 0, 1 }) map.AddStair(new Vector2Int(x, -1), new Vector2Int(x, 0));
        foreach (int x in new[] { 2, 3 }) map.AddStair(new Vector2Int(x, 2), new Vector2Int(x, 3));
        terrain.Render(map, settings); decoration.Render(new MapSimpleDecorationData(), map, settings);
        fixtureMap=map;return map;
    }
    private Pawn Create(string path, Vector2Int cell)
    {
        var inactive = new GameObject("Owned actor preparation"); inactive.transform.SetParent(holder.transform); inactive.SetActive(false);
        var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), inactive.transform);
        foreach (var b in root.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
        root.transform.position = terrain.Coordinates.CellToWorld(cell); root.SetActive(true); inactive.SetActive(true);
        bool player = path.Contains("Players");
        var owner = root.GetComponents<MonoBehaviour>().Single(b => b.GetType().Name == (player ? "PlayerAction" : "MonsterMovementController"));
        return new Pawn { root = inactive, body = root.GetComponent<Rigidbody2D>(), player = player, owner = owner,
            foot = player ? root.transform.Find("MovementFeet").GetComponent<CircleCollider2D>() : root.GetComponentsInChildren<Collider2D>(true).Single(c => !c.isTrigger),
            move = owner.GetType().GetMethod(player ? "Move" : "MoveTowardsDestination", Private) };
    }
    private Collider2D[] Walls() => holder.GetComponentsInChildren<Collider2D>(true)
        .Where(c => c.enabled && !c.isTrigger && c.attachedRigidbody != null && c.attachedRigidbody.bodyType == RigidbodyType2D.Static && c.shapeCount > 0).ToArray();
    private void Sample(Pawn p, Motion row, Collider2D[] walls)
    {
        row.poses.Add(p.body.position); row.physicsSteps++;
        foreach (var wall in walls)
        {
            var gap = p.foot.Distance(wall);
            if (!gap.isValid) continue;
            row.minimumGap = Mathf.Min(row.minimumGap, gap.distance);
            Assert.That(gap.distance, Is.GreaterThanOrEqualTo(-Physics2D.defaultContactOffset), row.actor + "/" + row.scenario + "/" + wall.name);
        }
        SurfaceV4NativeVideo.Record(holder,fixtureMap,terrain,p.body.gameObject,p.foot,Path.GetFileNameWithoutExtension(row.actor)+"_"+row.scenario,row.minimumGap);
    }
    private void Write(string name, Proof proof)
    {
        string output = Environment.GetEnvironmentVariable("RPG_DEMO_REMEDIATION_EVIDENCE");
        Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, name), JsonUtility.ToJson(proof, true));
    }
    [UnityTest] public IEnumerator OriginalFourFamiliesCrossBothStairLanesBothWaysWhileAnotherHeightMoves()
    {
        yield return Fixture(); var map = Platform(); yield return null; Physics2D.SyncTransforms();
        var proof = new Proof();
        try
        {
            foreach (string path in prefabs)
            foreach (int lane in new[] { 0, 1, 2, 3 })
            {
                bool upperStair = lane >= 2;
                Vector2Int lower = new Vector2Int(lane, upperStair ? 1 : -2), upper = new Vector2Int(lane, upperStair ? 4 : 2);
                var main = Create(path, lower);
                var other = Create(path, new Vector2Int(-3, upperStair ? -3 : 2));
                yield return null; Physics2D.SyncTransforms(); var walls = Walls();
                // Preserve the native foot and place the fixture inside the actual
                // two-cell stair corridor. A >1-cell foot cannot start on its rim.
                float corridorLeft = terrain.Coordinates.CellToWorld(new Vector2Int(upperStair ? 2 : 0, lower.y)).x - .5f;
                float offsetX = main.foot.bounds.center.x - main.body.position.x;
                float halfWidth = main.foot.bounds.extents.x;
                float legalX = Mathf.Clamp(main.body.position.x, corridorLeft + halfWidth + .025f - offsetX,
                    corridorLeft + 2f - halfWidth - .025f - offsetX);
                float startY=main.body.position.y, goalY=terrain.Coordinates.CellToWorld(upper).y;
                // The nearby second-height face can protrude beyond its logical
                // tile. Qualify the whole native corridor, not just tile bounds.
                var candidates=new List<float>{legalX};
                float minimumX=corridorLeft+halfWidth+.025f-offsetX, maximumX=corridorLeft+2f-halfWidth-.025f-offsetX;
                for(float x=minimumX;x<=maximumX;x+=.02f)candidates.Add(x);
                bool found=false;
                foreach(float x in candidates.OrderBy(x=>Mathf.Abs(x-legalX)))
                {
                    bool clear=true;
                    for(float y=startY;y<=goalY+.001f;y+=.1f)
                    {
                        main.body.position=new Vector2(x,Mathf.Min(y,goalY));Physics2D.SyncTransforms();
                        foreach(var wall in walls){var gap=main.foot.Distance(wall);if(!gap.isValid || gap.isOverlapped || gap.distance<.004f){clear=false;break;}}
                        if(!clear)break;
                    }
                    if(!clear)continue;
                    legalX=x;found=true;break;
                }
                Assert.That(found,Is.True,"Original complete foot has no native-clear route in declared stair corridor");
                main.body.position = new Vector2(legalX,startY);Physics2D.SyncTransforms();
                foreach (var wall in walls) Assert.That(main.foot.Distance(wall).isOverlapped, Is.False, "Fixture must begin with a legal complete foot");
                foreach (var goalCell in new[] { upper, lower })
                {
                    Vector2 goal = terrain.Coordinates.CellToWorld(goalCell); goal.x = legalX;
                    Vector2 otherGoal = terrain.Coordinates.CellToWorld(new Vector2Int(-3, upperStair ? -1 : 4));
                    var row = new Motion { actor = path, scenario = "lane=" + lane + " goal=" + goalCell,
                        start = main.body.position, goal = goal, startLevel = map.GetElevation(terrain.Coordinates.WorldToCell(main.body.position)) };
                    proof.cases.Add(row);
                    float arrivalTolerance = main.player ? .06f : (float)main.owner.GetType().GetField("stoppingDistance", Private).GetValue(main.owner) + .005f;
                    for (int step = 0; step < 900; step++)
                    {
                        main.Drive(goal); other.Drive(otherGoal); yield return new WaitForFixedUpdate();
                        Sample(main, row, walls);
                        foreach (var wall in walls) Assert.That(other.foot.Distance(wall).distance,
                            Is.GreaterThanOrEqualTo(-Physics2D.defaultContactOffset), "Concurrent other-height actor");
                        if (Vector2.Distance(main.body.position, goal) < arrivalTolerance) break;
                    }
                    row.end = main.body.position; row.endLevel = map.GetElevation(terrain.Coordinates.WorldToCell(row.end));
                    Assert.That(Vector2.Distance(row.end, goal), Is.LessThan(arrivalTolerance), row.scenario);
                    Assert.That(row.endLevel, Is.EqualTo(map.GetElevation(goalCell)));
                    Bounds endFoot = main.foot.bounds;
                    foreach (Vector2 corner in new[] { new Vector2(endFoot.min.x,endFoot.min.y), new Vector2(endFoot.min.x,endFoot.max.y),
                        new Vector2(endFoot.max.x,endFoot.min.y), new Vector2(endFoot.max.x,endFoot.max.y) })
                        Assert.That(map.GetElevation(terrain.Coordinates.WorldToCell(corner)),Is.EqualTo(map.GetElevation(goalCell)),"Complete foot must finish on target elevation");
                }
                UnityEngine.Object.Destroy(main.root); UnityEngine.Object.Destroy(other.root); yield return null;
            }
            Assert.That(proof.cases.Count, Is.EqualTo(32));
        }
        finally { Write("R4-StairsAndConcurrentHeights.json", proof); }
    }
    [UnityTest] public IEnumerator NonStairTopEdgesAndFourDiagonalCornersBlockUnchangedNativeFeet()
    {
        yield return Fixture(); var map = Platform(); yield return null; Physics2D.SyncTransforms();
        var proof = new Proof();
        var starts = new[] { new Vector2Int(-7,-2),new Vector2Int(7,-2),new Vector2Int(-7,7),new Vector2Int(7,7),new Vector2Int(-3,2),new Vector2Int(0,4) };
        var goals = new[] { new Vector2Int(-4,1),new Vector2Int(4,1),new Vector2Int(-4,4),new Vector2Int(4,4),new Vector2Int(-3,-3),new Vector2Int(3,4) };
        try
        {
            foreach (string path in prefabs)
            for (int scenario = 0; scenario < starts.Length; scenario++)
            {
                var pawn = Create(path, starts[scenario]); yield return null; Physics2D.SyncTransforms();
                var walls = Walls(); Vector2 goal = terrain.Coordinates.CellToWorld(goals[scenario]);
                var row = new Motion { actor = path, scenario = "corner/top=" + scenario, start = pawn.body.position,
                    goal = goal, startLevel = map.GetElevation(starts[scenario]) };
                proof.cases.Add(row); int stable = 0;
                for (int step = 0; step < 450; step++)
                {
                    Vector2 before = pawn.body.position; pawn.Drive(goal); yield return new WaitForFixedUpdate(); Sample(pawn,row,walls);
                    stable = Vector2.Distance(before,pawn.body.position) < .00001f ? stable+1 : 0;
                    if (stable >= 12) break;
                }
                row.end = pawn.body.position; row.endLevel = map.GetElevation(terrain.Coordinates.WorldToCell(row.end));
                Assert.That(row.endLevel,Is.EqualTo(row.startLevel),row.actor+"/"+row.scenario);
                UnityEngine.Object.Destroy(pawn.root); yield return null;
            }
            Assert.That(proof.cases.Count,Is.EqualTo(24));
        }
        finally { Write("R4-CornersAndTopEdges.json",proof); }
    }
    [Serializable] private sealed class VisualMaskProof
    {
        public string status="INCOMPLETE", scope="Actual Tilemaps and original source Texture/Sprite rectangles. All 256 eight-neighbor masks; no centre-fill substitute. Source corner sparsity still requires visual review.";
        public int masks, edgePieces, diagonalPieces;
        public bool clearedAfterRebuild;
    }
    [UnityTest] public IEnumerator EveryEightNeighborMaskKeepsFoamOnWaterAndRebuildClearsOwnedDisplay()
    {
        yield return Fixture();var proof=new VisualMaskProof();
        Vector2Int[] neighbours={Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left,
            new Vector2Int(1,1),new Vector2Int(1,-1),new Vector2Int(-1,-1),new Vector2Int(-1,1)};
        try
        {
            for(int mask=0;mask<256;mask++)
            {
                var map=new MapData(12,12,new Vector2Int(-6,-6));
                for(int y=-6;y<6;y++)for(int x=-6;x<6;x++)map.SetTerrain(new Vector2Int(x,y),MapTerrainType.DeepWater);
                for(int i=0;i<8;i++)if((mask&(1<<i))!=0)map.SetTerrain(neighbours[i],MapTerrainType.Grass);
                terrain.Render(map,settings);decoration.Render(new MapSimpleDecorationData(),map,settings);yield return null;
                var visual=holder.GetComponentInChildren<MapSurfaceV4Visuals>(true);
                Assert.That(visual.GetComponentsInChildren<Collider2D>(true),Is.Empty,"Visual owner must never add physics");
                var centre=visual.Placements.Where(p=>p.cell==Vector2Int.zero && p.group=="shore").ToArray();
                for(int i=0;i<4;i++)
                {
                    bool bank=(mask&(1<<i))!=0;
                    Assert.That(centre.Count(p=>p.piece=="edge"+i),Is.EqualTo(bank?1:0),"Missing/wrong water-side edge mask="+mask+" direction="+i);
                    bool diagonal=(mask&(1<<(i+4)))!=0 && (mask&(1<<i))==0 && (mask&(1<<((i+1)%4)))==0;
                    Assert.That(centre.Count(p=>p.piece=="diagonal"+i),Is.EqualTo(diagonal?1:0),"Missing/wrong outer diagonal mask="+mask);
                }
                foreach(var placement in visual.Placements)
                {
                    Assert.That(TerrainTopology.IsWater(map.GetCell(placement.cell).terrainType),Is.True,"Foam or water rock over dry support");
                    if(placement.group!="shore")continue;
                    Assert.That(placement.piece.StartsWith("edge")||placement.piece.StartsWith("diagonal"),Is.True,"No opaque centre-fill piece");
                    var layer=visual.GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>(true).Single(t=>
                        t.name==(placement.piece.StartsWith("edge")?"SurfaceV4Shore"+placement.piece.Substring(4):"SurfaceV4Corner"+placement.piece.Substring(8)));
                    var cell=new Vector3Int(placement.cell.x,placement.cell.y,0);
                    var sprite=layer.GetSprite(cell);
                    Assert.That(sprite.texture,Is.SameAs(settings.shorelineTexture));
                    Assert.That(sprite.rect.width,Is.EqualTo(64));Assert.That(sprite.rect.height,Is.EqualTo(64));
                    Assert.That(layer.GetAnimationFrameCount(cell),Is.EqualTo(16));
                }
                proof.edgePieces+=centre.Count(p=>p.piece.StartsWith("edge"));proof.diagonalPieces+=centre.Count(p=>p.piece.StartsWith("diagonal"));proof.masks++;
            }
            terrain.Clear();yield return null;
            var cleared=holder.GetComponentInChildren<MapSurfaceV4Visuals>(true);
            Assert.That(cleared.Placements,Is.Empty);
            foreach(var layer in cleared.GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>(true))
                Assert.That(layer.GetUsedTilesCount(),Is.Zero,"Old visual generation retained tiles");
            Assert.That(cleared.GetComponentsInChildren<SpriteRenderer>().Length,Is.Zero,"Owned old shadow objects survived clear");
            proof.clearedAfterRebuild=true;proof.status="PASS";
        }
        finally
        {
            string output=Environment.GetEnvironmentVariable("RPG_DEMO_REMEDIATION_EVIDENCE");Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output,"R4-VisualMaskContract.json"),JsonUtility.ToJson(proof,true));
        }
    }

    [UnityTearDown] public IEnumerator Cleanup()
    { SurfaceV4NativeVideo.Flush(holder);if(holder!=null) UnityEngine.Object.Destroy(holder); yield return null; TowerRunRuntimeEnvironment.ResetOverrides(); Time.timeScale=1f; }
}

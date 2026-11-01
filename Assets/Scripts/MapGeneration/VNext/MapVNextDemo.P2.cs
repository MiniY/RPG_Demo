using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MapVNext
{
    public sealed partial class MapVNextDemo
    {
        public bool EnableRandomLayout;
        public bool PlayerSpawnValidatedBeforeActivation {get;private set;}
        public GameObject RandomControlPanel;
        public UnityEngine.UI.Button[] RandomControls;
        public UnityEngine.UI.InputField SeedField;
        public UnityEngine.UI.Button GenerateSeedButton,ReturnRandomButton;
        public string BuildIdentity=>EnableRandomLayout?MapVNextRandomLayout.Identity:MapVNextAssets.Identity;
        public LayoutSummary Layout {get;private set;}
        public bool RandomActive {get;private set;}
        public readonly List<LayoutSummary> Layouts=new List<LayoutSummary>();
        public readonly List<P2PhysicsEvidence> PhysicsEvidence=new List<P2PhysicsEvidence>();
        bool overview,captureCameraFixed;
        int randomSeed=21193;
        string layoutError="",batchPhase;
        int previousColliderPaths=-1;
        readonly List<string> LifecycleTrace=new List<string>{"case,generation,semanticHash,cliffOwners,cliffPaths,navigationCaches,tileCount,resetReason"};
        [Serializable] public sealed class P2PhysicsEvidence
        {public int seed,stairsTotal,stairsWalked,horizontalTrials,horizontalBlocked,cliffTrials,cliffBlocked;public string generation,semanticHash,status;public int[] visitedPlatforms;}
        [Serializable] sealed class P2RunEvidence
        {
            public string identity,phase,status,failure,configurationHash,inputFingerprint,isolatedRoot;
            public int[] frozenSeeds,pilotSeeds,replaySeeds;public LayoutSummary[] layouts;public P2PhysicsEvidence[] physics;
            public CaseEvidence[] baseRegression;public bool playerSpawnValidatedBeforeActivation;public bool formalSaveServiceActive;public float physicsTimeScale,fixedDeltaTime;public string physicalSelection;
        }
        void SetupRandomControls()
        {
            if(!EnableRandomLayout)return;
            Require(RandomControls!=null&&RandomControls.Length==7&&SeedField!=null,"P2_UI_BINDING_MISSING");
            RandomControls[0].onClick.AddListener(()=>LoadRandom(unchecked(randomSeed+104729)));
            RandomControls[1].onClick.AddListener(()=>LoadRandom(randomSeed));
            RandomControls[2].onClick.AddListener(ToggleOverview);
            RandomControls[3].onClick.AddListener(ResetRandom);
            RandomControls[4].onClick.AddListener(()=>Load(0));
            RandomControls[5].onClick.AddListener(ToggleClean);
            RandomControls[6].onClick.AddListener(Quit);
            GenerateSeedButton.onClick.AddListener(GenerateTypedSeed);
            ReturnRandomButton.onClick.AddListener(()=>LoadRandom(randomSeed));
            batchPhase=Arg("--p2-phase")??"full";
        }
        void ShowRandomControls(bool active)
        {
            if(!EnableRandomLayout)return;
            RandomControlPanel.SetActive(active);
            Controls[0].transform.parent.gameObject.SetActive(!active);
            ReturnRandomButton.gameObject.SetActive(!active);
        }
        public void GenerateTypedSeed()
        {
            if(int.TryParse(SeedField.text,out int seed))LoadRandom(seed);
            else layoutError="Seed must be a signed 32-bit integer. Existing map is retained.";
        }
        void HandleRandomKeys()
        {
            var k=Keyboard.current;if(k==null||SeedField.isFocused)return;
            CliffManualKeys(k);
            if(k.escapeKey.wasPressedThisFrame)Quit();
            if(k.nKey.wasPressedThisFrame)LoadRandom(unchecked(randomSeed+104729));
            if(k.gKey.wasPressedThisFrame)LoadRandom(randomSeed);
            if(k.rKey.wasPressedThisFrame)ResetRandom();
            if(k.tabKey.wasPressedThisFrame)ToggleOverview();
            if(k.f1Key.wasPressedThisFrame)ToggleClean();
        }
        public bool LoadRandom(int seed)
        {
            var result=MapVNextRandomLayout.Generate(seed);
            Layouts.Add(result.Summary);
            if(result.Map==null){layoutError="Requested Seed "+seed+" rejected: "+result.Summary.reason;return false;}
            RandomActive=true;gallery=false;Layout=result.Summary;Map=result.Map;randomSeed=seed;layoutError="";
            Current=new StairCase{Id="S-"+seed,Seed=seed,SeedChain=true,Context="ProceduralLayout",Side="Generated",Lower=0};
            Generation=Guid.NewGuid().ToString("N");
            MapVNextProjection.Render(Map,Terrain,settings,Assets,Generation);BindR3();
            var surface=Terrain.GetComponent<MapVNextSurfaceQuery>();var reach=surface.Reachability(Foot.radius+Physics2D.defaultContactOffset);
            Layout.surfaceWalkableCells=reach.legal;Layout.surfaceReachableCells=reach.reachable;Layout.spatialSemanticHash=surface.SpatialHash;
            Require(reach.legal==reach.reachable,"P2_EXCLUSION_DISCONNECTED_PLAYABLE_SURFACES:"+seed+":"+reach.reachable+"/"+reach.legal);
            if(cliffFootVisual!=null)DestroyImmediate(cliffFootVisual);if(cliffDiagnostics!=null)DestroyImmediate(cliffDiagnostics);if(placementProbes!=null)DestroyImmediate(placementProbes);showCliffDiagnostics=false;captureCameraFixed=false;
            PlaceFoot(Center(Map.SpawnCell),"RESET_NEW_RANDOM_MAP_NOT_TRAVERSAL");
            if(SeedField!=null)SeedField.text=seed.ToString();ShowRandomControls(true);
            if(!automatic){overview=false;PositionRandomCamera();}
            RecordLifecycle("RESET_NEW_RANDOM_MAP");
            return true;
        }
        void RecordLifecycle(string reason)
        {
            var colliders=Terrain.WaterBaseTilemap.transform.parent.GetComponentsInChildren<PolygonCollider2D>(true).Where(p=>p.name=="GeneratedCliffEdges").ToArray();
            Require(colliders.Length==1,"P2_CLIFF_OWNER_NOT_SINGLE");
            int tiles=0;foreach(var tm in Terrain.WaterBaseTilemap.transform.parent.GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>(true))foreach(var p in tm.cellBounds.allPositionsWithin)if(tm.HasTile(p))tiles++;
            LifecycleTrace.Add(string.Join(",",Current.Id,Generation,MapVNextRandomLayout.SemanticHash(Map),colliders.Length,colliders[0].pathCount,approachGraphs.Count,tiles,reason));
            Require(approachGraphs.Count==0,"P2_STALE_NAVIGATION_CACHE");previousColliderPaths=colliders[0].pathCount;
        }
        void ResetRandom()
        {
            if(!RandomActive||automatic)return;
            captureCameraFixed=false;
            PlaceFoot(Center(Map.SpawnCell),"USER_RESET_TO_SAFE_SPAWN_NOT_SAVE_RESTORE");
            trajectory.Clear();trajectoryLine.positionCount=0;
        }
        public void ToggleOverview(){overview=!overview;PositionRandomCamera();}
        void PositionRandomCamera()
        {
            if(!RandomActive||View==null)return;
            if(overview)
            {View.orthographicSize=Math.Max(Map.Height*.5f+2,(Map.Width*.5f+2)/View.aspect);View.transform.position=new Vector3(Map.Width*.5f,Map.Height*.5f,-10);}
            else{View.orthographicSize=7.5f;View.transform.position=new Vector3(FootPosition.x,FootPosition.y+1f,-10);}
        }
        void LateUpdate(){if(RandomActive&&Player!=null&&Player.activeInHierarchy&&!overview&&!captureCameraFixed)PositionRandomCamera();}
        string RandomCaption()
        {
            string tested=PhysicsEvidence.Any(p=>p.generation==Generation&&p.status=="PASS")?"logic + automated movement PASS":"logic PASS / manual review pending";
            return $"RANDOM HIGHLANDS / Seed {randomSeed}\n{Layout.platforms.Length} platforms / terrain H0-H{Layout.maxElevation} / {Layout.stairs.Length} stairs / Foot H{ActualElevation}\n"+
                $"{BuildIdentity} / {tested}\nWASD move / Tab overview / R safe reset / N next Seed / G same Seed\n"+layoutError;
        }
        MapVNextStairGeometry Ramp(LayoutStair s)=>ramps.Single(v=>v.Lower==s.lower&&v.Upper==s.upper);
        IEnumerator VisitPlatformTree(int parent,CaseEvidence evidence,List<int> visited)
        {
            foreach(var link in Layout.stairs.Where(s=>s.parentId==parent).OrderBy(s=>s.platformId))
            {
                var ramp=Ramp(link);trialPhase="P2_SAME_LEVEL_APPROACH";
                yield return OnLevelTo(ramp.Entry,evidence);
                bool motion=batchPhase=="pilot"||randomSeed==21193;
                yield return WalkSlope(ramp,true,false,motion,evidence);visited.Add(link.platformId);
                if(!string.IsNullOrEmpty(output))yield return Capture("Motion/"+Current.Id+"-P"+link.platformId+"-upper.png",false);
                yield return VisitPlatformTree(link.platformId,evidence,visited);
                trialPhase="P2_RETURN_APPROACH";yield return OnLevelTo(ramp.Exit,evidence);
                yield return WalkSlope(ramp,false,false,motion,evidence);
            }
        }
        IEnumerator RandomRoundTrip()
        {
            var evidence=NewEvidence();evidence.upper=Layout.maxElevation;var visited=new List<int>();
            Vector2 start=FootPosition;string semantic=MapVNextRandomLayout.SemanticHash(Map);
            yield return VisitPlatformTree(0,evidence,visited);
            yield return OnLevelTo(start,evidence);
            Require(ActualElevation==0&&Vector2.Distance(FootPosition,start)<.22f,"P2_DID_NOT_RETURN_TO_SPAWN");
            Require(visited.Distinct().Count()==Layout.platforms.Length,"P2_UNVISITED_DECLARED_PLATFORM");
            int cliffTests=0;
            foreach(var link in Layout.stairs.OrderBy(s=>s.platformId))
            {
                var ramp=Ramp(link);activeRamp=ramp;
                yield return HorizontalNegatives(ramp,false,false,evidence);
                yield return Release();PlaceFoot(ramp.Point(-.3f,3.5f),"RESET_SEPARATE_P2_CLOSED_CLIFF_NEGATIVE");
                trialPhase="P2_CLOSED_CLIFF_NEGATIVE";int max=ActualElevation;Vector2 before=FootPosition;
                for(int tick=0;tick<40;tick++){Feed(ramp.West?UnityEngine.InputSystem.Key.D:UnityEngine.InputSystem.Key.A);yield return fixedFrame;TraceR3(evidence.ticks++);max=Math.Max(max,ActualElevation);}
                yield return Release();Require(max==ramp.LowerElevation&&Vector2.Distance(before,FootPosition)<1.5f,"P2_CLOSED_CLIFF_BYPASS:"+ramp.Id);cliffTests++;
            }
            Require(semantic==MapVNextRandomLayout.SemanticHash(Map),"P2_MOVEMENT_MUTATED_MAPDATA");
            evidence.cliffBlocked=true;evidence.corridorOrdered=true;evidence.status="PASS";Evidence.Add(evidence);
            PhysicsEvidence.Add(new P2PhysicsEvidence{seed=randomSeed,generation=Generation,semanticHash=semantic,status="PASS",stairsTotal=Layout.stairs.Length,stairsWalked=visited.Count,
                horizontalTrials=evidence.horizontalTrials,horizontalBlocked=evidence.horizontalBlockedCount,cliffTrials=cliffTests,cliffBlocked=cliffTests,visitedPlatforms=visited.ToArray()});
            PlaceFoot(Center(Map.SpawnCell),"RESET_AFTER_NEGATIVES_NOT_TRAVERSAL");
        }
        IEnumerator CaptureRandomMap()
        {
            captureCameraFixed=true;overview=true;PositionRandomCamera();
            yield return Capture("Screenshots/"+Current.Id+"-overview-clean.png",true);
            yield return Capture("Screenshots/"+Current.Id+"-overview-debug.png",false);
            overview=false;
            foreach(var side in new[]{"West","East"})
            {
                var link=Layout.stairs.Where(s=>s.side==side).OrderByDescending(s=>s.upperHeight).ThenBy(s=>s.platformId).FirstOrDefault();
                if(link==null)continue;var p=Ramp(link).Middle;
                View.orthographicSize=7.5f;View.transform.position=new Vector3(p.x,p.y,-10);
                yield return Capture("Screenshots/"+Current.Id+"-close-"+side+"-clean.png",true);
                yield return Capture("Screenshots/"+Current.Id+"-close-"+side+"-debug.png",false);
            }
            captureCameraFixed=false;PositionRandomCamera();
        }
        IEnumerator AcceptP2Steps()
        {
            if(batchPhase=="pilot")
            {
                for(int i=0;i<8;i++)
                {
                    Load(i);yield return null;yield return RoundTrip(i<8,true);
                    yield return Capture("BaseRegression/"+Current.Id+"-clean.png",true);
                }
            }
            int[] seeds=batchPhase=="cliff"?CliffSeeds:batchPhase=="pilot"?MapVNextRandomLayout.PilotSeeds:batchPhase=="determinism"?MapVNextRandomLayout.ReplaySeeds:MapVNextRandomLayout.Seeds;
            int physical=0;
            foreach(int seed in seeds)
            {
                if(!LoadRandom(seed))continue;yield return null;
                // The same input is regenerated in-process to prove complete replacement.
                string expected=Layout.semanticHash;int paths=previousColliderPaths;
                Require(LoadRandom(seed),"P2_SAME_INPUT_SECOND_GENERATION_REJECTED");
                Require(Layout.semanticHash==expected&&previousColliderPaths==paths,"P2_REGENERATION_NOT_EQUIVALENT");
                RecordSurface();
                if(batchPhase=="determinism")continue;
                if(batchPhase=="cliff"){yield return CliffRoundTrip();continue;}
                yield return CaptureRandomMap();
                if(batchPhase=="pilot"||physical<12){yield return RandomRoundTrip();physical++;}
            }
        }
        void WriteP2Evidence(string failure)
        {
            WriteCliffEvidence(failure);
            File.WriteAllLines(Path.Combine(output,"regeneration-lifecycle.csv"),LifecycleTrace);
            bool rejected=Layouts.Any(p=>p.status!="SUCCESS");
            var result=new P2RunEvidence{identity=BuildIdentity,phase=batchPhase,status=failure!=null?"FAIL":rejected?"PARTIAL":"PASS",failure=failure,physicsTimeScale=Time.timeScale,fixedDeltaTime=Time.fixedDeltaTime,configurationHash=new MapVNextLayoutConfig().Digest(),
                inputFingerprint=Assets.InputFingerprint,isolatedRoot=IsolatedRoot,frozenSeeds=MapVNextRandomLayout.Seeds,pilotSeeds=MapVNextRandomLayout.PilotSeeds,replaySeeds=MapVNextRandomLayout.ReplaySeeds,
                layouts=Layouts.ToArray(),physics=PhysicsEvidence.ToArray(),baseRegression=Evidence.Where(e=>e.id.StartsWith("V")).ToArray(),
                playerSpawnValidatedBeforeActivation=PlayerSpawnValidatedBeforeActivation,formalSaveServiceActive=FindObjectsOfType<MonoBehaviour>().Any(m=>m!=null&&(m.GetType().Name=="GameSession"||m.GetType().Name=="TowerRunUiController")),
                physicalSelection="First 12 successful inputs in frozen order; every declared platform, every stair both ways; two horizontal starts and one closed-cliff negative per stair. Explicit reset only for separate negatives."};
            File.WriteAllText(Path.Combine(output,"p2-evidence.json"),JsonUtility.ToJson(result,true));
        }
    }
}

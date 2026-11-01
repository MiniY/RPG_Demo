using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace MapVNext
{
    [DefaultExecutionOrder(-2000)]
    public sealed partial class MapVNextDemo : MonoBehaviour
    {
        public MapVNextAssets Assets;
        public MapTilemapRenderer Terrain;
        public Camera View;
        public Canvas Overlay;
        public Text Caption;
        public UnityEngine.UI.Button[] Controls;
        public MapData Map { get; private set; }
        public GameObject Player { get; private set; }
        public CircleCollider2D Foot { get; private set; }
        public StairCase Current { get; private set; }
        public string Generation { get; private set; }
        public static bool IsolationReady;
        public static string IsolatedRoot;
        public readonly List<string> Trace = new List<string>();
        public readonly List<CaseEvidence> Evidence = new List<CaseEvidence>();
        List<StairCase> cases;
        MapGenerationSettings settings;
        Rigidbody2D body;
        Keyboard automationKeyboard;
        int index;
        bool automatic, gallery;
        string output;

        bool automationPolicySaved;
        InputSettings.BackgroundBehavior originalBackground;
#if UNITY_EDITOR
        InputSettings.EditorInputBehaviorInPlayMode originalEditorInput;
#endif
        readonly WaitForFixedUpdate fixedFrame = new WaitForFixedUpdate();
        public int ActualElevation => Map.GetElevation(Terrain.Coordinates.WorldToCell(Foot.bounds.center));

        [Serializable] public sealed class CaseEvidence
        {
            public string id, context, side, generation, mapDigest, status, clean, debug;
            public int seed, lower, upper, ticks, playerId, footId;
            public float radius;
            public bool uphill, downhill, cliffBlocked;
            public bool horizontalBlocked,pauseStable,reversed,corridorOrdered;
            public int horizontalTrials,horizontalBlockedCount,dualInputSamples;
            public float pauseDrift;public Vector2 upDelta,downDelta;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void ConfigureIsolation()
        {
            IsolationReady = false;
            if (Application.productName != "RPGDemo_MapVNext_Stairs" && Application.productName != "RPGDemo_MapVNext_Random" &&
                Environment.GetEnvironmentVariable("RPG_MAP_VNEXT_ISOLATED") != "1") return;
            IsolatedRoot = Environment.GetEnvironmentVariable("RPG_MAP_VNEXT_RUNTIME_ROOT");
            if (string.IsNullOrEmpty(IsolatedRoot))
                IsolatedRoot = Path.Combine(Application.persistentDataPath, Application.productName=="RPGDemo_MapVNext_Random"?MapVNextRandomLayout.Identity:MapVNextAssets.Identity);
            IsolatedRoot = Path.GetFullPath(IsolatedRoot);
            TowerRunRuntimeEnvironment.ConfigureTestIsolation(IsolatedRoot);
            IsolationReady = true;
        }

        IEnumerator Start()
        {
            if (!IsolationReady) throw new InvalidOperationException("EarliestStartupIsolationMissing");
            if (!TowerRunRuntimeEnvironment.SlotRoot.StartsWith(IsolatedRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("SlotIsolationResetBeforeScene");
            // Assembly-CSharp automatically creates GameSession. It was already isolated before Awake;
            // this standalone fixture needs no session or saving service, so retire that runtime object.
            foreach (MonoBehaviour m in FindObjectsOfType<MonoBehaviour>())
                if (m != null && m.GetType().Name == "GameSession") DestroyImmediate(m.gameObject);
            // Keep this Canvas inactive until the automatic session has retired, so
            // EnsureForScene cannot inject the formal startup/settings/save UI into it.
            Overlay.gameObject.SetActive(true);
            if(Overlay.GetComponents<MonoBehaviour>().Any(m=>m.GetType().Name=="TowerRunUiController"))
                throw new InvalidOperationException("FormalSaveUiInjectedIntoFixture");
            cases = MapVNextFixture.Cases();
            settings = Instantiate(Assets.SourceSettings);
            settings.remediationProjectedSurfaces = false;
            // Instantiate beneath an inactive owner so no entity is published before terrain qualification.
            var preparation=new GameObject("CandidateSpawnPreparation");preparation.SetActive(false);
            Player=Instantiate(Assets.PlayerPrefab,preparation.transform,false);
            Player.SetActive(false);Player.transform.SetParent(null,true);DestroyImmediate(preparation);
            Player.name = "FormalWarrior_MapVNext";
            body = Player.GetComponent<Rigidbody2D>();
            Foot = Player.transform.Find("MovementFeet").GetComponent<CircleCollider2D>();
            Require(Math.Abs(Foot.radius - .30f) < .0001f && !Foot.isTrigger && Player.transform.localScale == Vector3.one, "FormalFootContract");
            output = Arg(EnableRandomLayout?"--map-vnext-p2-accept":"--map-vnext-accept");
            automatic = !string.IsNullOrEmpty(output);
            SetupRandomControls();
            if(EnableRandomLayout)Require(LoadRandom(21193),"InitialP2SeedRejected");else Load(0);
            Trace.Add("trial,tick,key,footX,footY,elevation,generation,playerId,footId,identity,sample,physicsTime,utc,moveX,moveY,cellX,cellY,terrain,rampId,rampPhase,trialPhase,contacts,contactNormals,bodyX,bodyY");
            if(Controls != null && Controls.Length == 7)
            {
                Controls[0].onClick.AddListener(()=>Next(-1)); Controls[1].onClick.AddListener(()=>Next(1));
                Controls[2].onClick.AddListener(PairNext); Controls[3].onClick.AddListener(SwapSide);
                Controls[4].onClick.AddListener(ResetCase); Controls[5].onClick.AddListener(ToggleClean); Controls[6].onClick.AddListener(Quit);
            }
            yield return null;
            if (automatic)
            {
                automationKeyboard = InputSystem.AddDevice<Keyboard>("MapVNextAcceptanceKeyboard");
                InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
                Application.runInBackground = true;
                StartCoroutine(Accept());
            }
        }

        void Update()
        {
            if (Player == null || !Player.activeInHierarchy) return;
            if (!automatic && Keyboard.current != null)
            {
                if(EnableRandomLayout&&RandomActive){HandleRandomKeys();}
                else
                {
                if (Keyboard.current.escapeKey.wasPressedThisFrame) Application.Quit();
                if (Keyboard.current.rKey.wasPressedThisFrame) Load(index);
                if (Keyboard.current.nKey.wasPressedThisFrame) Next(1);
                if (Keyboard.current.pKey.wasPressedThisFrame) Next(-1);
                if (Keyboard.current.f1Key.wasPressedThisFrame) Overlay.enabled = !Overlay.enabled;
                }
            }
            if (!gallery)
            {
                if(!automatic && automationKeyboard==null && Keyboard.current!=null)
                {
                    var keys=new List<string>();var kb=Keyboard.current;
                    if(kb.wKey.isPressed)keys.Add("W");if(kb.sKey.isPressed)keys.Add("S");
                    if(kb.aKey.isPressed)keys.Add("A");if(kb.dKey.isPressed)keys.Add("D");
                    heldKeys=keys.Count==0?"None":string.Join("+",keys);
                    Vector2 foot=FootPosition;trajectory.Add(new Vector3(foot.x,foot.y,-.1f));
                    if(trajectory.Count>4000)trajectory.RemoveAt(0);
                    trajectoryLine.positionCount=trajectory.Count;trajectoryLine.SetPositions(trajectory.ToArray());
                }
                if(EnableRandomLayout&&RandomActive)Caption.text=RandomCaption();
                else Caption.text = $"MAP VNEXT / {Current.Id}\nSeed {Current.Seed}   H{Current.Lower} → H{(Current.SeedChain ? 4 : Current.Lower + 1)}   Foot H{ActualElevation}\n" +
                    $"{BuildIdentity} / {Assets.InputFingerprint.Substring(0,12)}\nWASD move · West W+D / East W+A uphill · N/P case · R reset · F1 clean · Esc exit";
                Caption.text += $"\nKeys {heldKeys} / Move {FinalMoveInput} / {trialPhase} / {activeRamp?.Phase(FootPosition)}";
                if(trajectoryLine!=null)trajectoryLine.enabled=Overlay.enabled;
                int visualH = ActualElevation;
                Vector2Int cell = Terrain.Coordinates.WorldToCell(Foot.bounds.center);
                foreach (var s in Map.Stairs)
                    if (s.LowerCell == cell || s.UpperCell == cell) visualH = Math.Max(visualH, Map.GetElevation(s.UpperCell));
                Player.SendMessage("SetOverrideOrder", visualH * 10 + 5, SendMessageOptions.DontRequireReceiver);
            }
        }

        public void Next(int delta) { if (cases != null && !automatic) Load((index + delta + cases.Count) % cases.Count); }
        public void ResetCase() { if (!automatic) Load(index); }
        public void PairNext() { if (!automatic) Load((index / 4 * 4 + (index + 1) % 4) % 60); }
        public void SwapSide() { if (!automatic && index < 60) Load(index % 8 < 4 ? index + 4 : index - 4); }
        public void ToggleClean() { Overlay.enabled = !Overlay.enabled; }
        public void Quit() { Application.Quit(); }

        public void Load(int selected)
        {
            RandomActive=false;ShowRandomControls(false);
            gallery = false; index = selected; Current = cases[selected];
            Map = MapVNextFixture.Build(Current);
            Generation = Guid.NewGuid().ToString("N");
            MapVNextProjection.Render(Map, Terrain, settings, Assets,Generation);
            BindR3();
            if (Foot != null) PlaceFoot(Current.SeedChain?Center(Map.SpawnCell):ramps.Single().Entry, "RESET_NEW_TRIAL");
            if (View != null)
            {
                View.orthographicSize = Current.SeedChain ? 20f : 7.5f;
                View.transform.position = new Vector3(Current.SeedChain ? 24f : 12f, Current.SeedChain ? 18f : 8.5f, -10f);
            }
        }

        Vector2 Center(Vector2Int c) => Terrain.Coordinates.CellToWorld(c);
        void PlaceFoot(Vector2 point, string reason)
        {
            var surface=Terrain.GetComponent<MapVNextSurfaceQuery>();
            if(!surface.CanOccupy(MapVNextGroundFootprint.Circle(Foot.radius),point,-1,out string rejection))throw new InvalidOperationException("RESET_REJECTED:"+rejection+":"+point);
            if(!Player.activeSelf)
            {
                var footprint=MapVNextGroundFootprint.FromCollider(Foot,out Vector2 groundOffset);
                Require(surface.Ready&&surface.Generation==Generation,"SPAWN_SURFACE_GENERATION_NOT_READY");
                Require(surface.TryPlace(Player,footprint,point,-1,Generation,out string spawnReason,true,groundOffset),"SPAWN_REJECTED:"+spawnReason);
                PlayerSpawnValidatedBeforeActivation=true;
                Physics2D.SyncTransforms();
                Require(Vector2.Distance(Foot.bounds.center,point)<.001f,"SPAWN_GROUND_ANCHOR_MISMATCH");
            }
            Physics2D.SyncTransforms();
            body.position += point - (Vector2)Foot.bounds.center;
            body.velocity = Vector2.zero;
            Physics2D.SyncTransforms();
            Trace.Add("# " + reason + "," + Current.Id + "," + Generation + "," + point);
        }

        public IEnumerator RoundTrip(bool wallProof,bool capture) { return RoundTripR3(wallProof,capture); }
        public IEnumerator SeedRoundTrip() { return SeedRoundTripR3(); }

        void Feed(params Key[] keys)
        {
            // Existing project tests use this policy for unfocused batch Game Views.
            // Only the synthetic automation keyboard is enabled; movement still uses GameInput/PlayerAction.
            Application.runInBackground=true;
            if(!automationPolicySaved)
            {
                originalBackground=InputSystem.settings.backgroundBehavior;
#if UNITY_EDITOR
                originalEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
                InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
                automationPolicySaved=true;
            }
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            if(!automationKeyboard.enabled)InputSystem.EnableDevice(automationKeyboard);
            heldKeys=string.Join("+",keys.Select(k=>k.ToString())); InputSystem.QueueStateEvent(automationKeyboard, new KeyboardState(keys));
        }
        IEnumerator Release()
        {
            heldKeys="None";
            InputSystem.QueueStateEvent(automationKeyboard,new KeyboardState());
            for(int i=0;i<12;i++)
            {
                yield return null;yield return fixedFrame;
                if(FinalMoveInput.sqrMagnitude<.000001f)break;
            }
            yield return fixedFrame;
            Require(FinalMoveInput.sqrMagnitude<.000001f,"SyntheticInputDidNotRelease");
        }
        void PersistFailure(string reason,Vector2 target)
        {
            Directory.CreateDirectory(IsolatedRoot);
            var action=Player.GetComponent("PlayerAction");
            var input=GameObject.Find("GameInput")?.GetComponent("GameInput");
            var contacts=new ContactPoint2D[16];int count=Foot.GetContacts(contacts);
            var data=new FailureEvidence{reason=reason,foot=Foot.bounds.center,target=target,body=body.position,
                level=ActualElevation,focused=Application.isFocused,keyboardEnabled=automationKeyboard!=null&&automationKeyboard.enabled,timeScale=Time.timeScale,
                moveInput=action.GetType().GetProperty("MoveInput").GetValue(action).ToString(),
                gameInput=input?.GetType().GetProperty("MoveInput").GetValue(input).ToString(),
                inputEnabled=input?.GetType().GetProperty("IsGameplayInputEnabled").GetValue(input).ToString(),
                contacts=contacts.Take(count).Select(c=>c.collider.name+":"+c.collider.GetInstanceID()).ToArray()};
            File.WriteAllText(Path.Combine(IsolatedRoot,"failure.json"),JsonUtility.ToJson(data,true));
            File.WriteAllLines(Path.Combine(IsolatedRoot,"partial-movement.csv"),Trace);
            Debug.Log("MAP_VNEXT_FAILURE "+JsonUtility.ToJson(data));
        }
        [Serializable] sealed class FailureEvidence {public string reason,moveInput,gameInput,inputEnabled;public Vector2 foot,target,body;public int level;public bool focused,keyboardEnabled;public float timeScale;public string[] contacts;}

        IEnumerator Capture(string relative, bool clean)
        {
            bool previous=Overlay.enabled;
            Overlay.enabled=!clean;
            if(trajectoryLine!=null)trajectoryLine.enabled=!clean;
            yield return new WaitForEndOfFrame();
            RecordFrame(relative);
            string path=Path.Combine(output,relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Texture2D shot=ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(path,shot.EncodeToPNG()); Destroy(shot);
            Overlay.enabled=previous;
            if(trajectoryLine!=null)trajectoryLine.enabled=previous;
        }

        IEnumerator Accept()
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output,"startup-isolation.json"), "{\"root\":\""+IsolatedRoot.Replace("\\","\\\\")+"\",\"slotRoot\":\""+TowerRunRuntimeEnvironment.SlotRoot.Replace("\\","\\\\")+"\",\"persistentDataPath\":\""+Application.persistentDataPath.Replace("\\","\\\\")+"\",\"earliestPhase\":\"AfterAssembliesLoaded\",\"formalSessionActive\":false}");
            string failure=null;
            var pending = new Stack<IEnumerator>(); pending.Push(EnableRandomLayout?AcceptP2Steps():AcceptSteps());
            while (pending.Count > 0)
            {
                object value=null; bool more=false;
                try { more=pending.Peek().MoveNext(); if(more)value=pending.Peek().Current; }
                catch(Exception ex) {failure=ex.ToString();}
                if(failure!=null)break;
                if(!more){pending.Pop();continue;}
                if(value is IEnumerator nested){pending.Push(nested);continue;}
                yield return value;
            }
            if(EnableRandomLayout)WriteP2Evidence(failure);
            File.WriteAllLines(Path.Combine(output,"movement.csv"),Trace);
            File.WriteAllLines(Path.Combine(output,"frames.csv"),FrameTrace);
            File.WriteAllLines(Path.Combine(output,"approach-plans.csv"),ApproachPlans);
            File.WriteAllText(Path.Combine(output,"player-evidence.json"),JsonUtility.ToJson(new PlayerEvidence {status=failure==null?"PASS":"FAIL",failure=failure,cases=Evidence.ToArray(),identity=BuildIdentity,inputFingerprint=Assets.InputFingerprint},true));
            yield return Release();
            Application.Quit(failure==null?0:2);
        }

        IEnumerator AcceptSteps()
        {
            // Base eight are a gate: no expansion is executed if any of them fails.
            int caseLimit=Environment.GetCommandLineArgs().Contains("--map-vnext-base-only")?8:60;
            for(int i=0;i<caseLimit;i++)
            {
                Load(i);yield return null;
                if(i<8)yield return CaptureR3Diagnostics();
                yield return RoundTrip(i<8,i<8);
                var e=Evidence[Evidence.Count-1];
                e.debug="Screenshots/"+Current.Id+"-debug.png";
                e.clean="Screenshots/"+Current.Id+"-clean.png";
                yield return Capture(e.debug,false);yield return Capture(e.clean,true);
            }
            for(int i=60;caseLimit==60 && i<cases.Count;i++)
            {
                Load(i);yield return null;yield return SeedRoundTrip();
                var e=Evidence[Evidence.Count-1];e.clean="Screenshots/"+Current.Id+"-clean.png";
                yield return Capture(e.clean,true);
            }
            yield return CaptureTrueWaterBoundary();
            yield return MaterialGallery();
        }

        IEnumerator MaterialGallery()
        {
            gallery=true;Player.SetActive(false);
            foreach(var tm in Terrain.WaterBaseTilemap.transform.parent.GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>())tm.ClearAllTiles();
            int[,] pieces={ {1,0,34},{2,0,35},{3,0,36},{0,1,38},{1,1,20},{2,1,21},{3,1,22},{4,1,39},{0,2,32},{1,2,13},{2,2,13},{3,2,13},{4,2,33},{1,3,12},{2,3,13},{3,3,14},{1,4,4},{2,4,5},{3,4,6} };
            var materialMap=Terrain.GrassOverlayTilemap;materialMap.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>().sortingOrder=0;
            for(int h=0;h<5;h++)
            for(int s=0;s<2;s++)
            {
                materialMap.ClearAllTiles();
                for(int i=0;i<pieces.GetLength(0);i++)materialMap.SetTile(new Vector3Int(pieces[i,0],pieces[i,1],0),Assets.Native(h,pieces[i,2]));
                View.orthographicSize=3.4f;View.transform.position=new Vector3(2.5f,2.5f,-10);
                Caption.text=$"SOURCE MATERIAL / H{h} color{MapVNextAssets.Families[h]} / {(s==0?"West 32/38":"East 33/39")}\nShared saved 19-cell template — material only, not a playable claim";
                yield return Capture($"Screenshots/Material-H{h}-{(s==0?"West":"East")}.png",false);
            }
        }

        [Serializable] sealed class PlayerEvidence { public string status,failure,identity,inputFingerprint;public CaseEvidence[] cases; }
        static string Arg(string name) {string[] a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,name);return i>=0&&i+1<a.Length?a[i+1]:null;}
        static void Require(bool condition,string name) {if(!condition)throw new InvalidOperationException(name);}
        void OnDestroy()
        {
            if(automationPolicySaved)
            {
                InputSystem.settings.backgroundBehavior=originalBackground;
#if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode=originalEditorInput;
#endif
            }
            if(automationKeyboard!=null)InputSystem.RemoveDevice(automationKeyboard);
            if(trajectoryMaterial!=null)Destroy(trajectoryMaterial);
            if(cliffFootVisual!=null)Destroy(cliffFootVisual);if(cliffDiagnostics!=null)Destroy(cliffDiagnostics);if(placementProbes!=null)Destroy(placementProbes);if(placementProbeSprite!=null)Destroy(placementProbeSprite);
            if(settings!=null)Destroy(settings);
            if(Player!=null)Destroy(Player);
        }
    }
}

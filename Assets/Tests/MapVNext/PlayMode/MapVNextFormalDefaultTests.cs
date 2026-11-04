using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>Explicit main-only acceptance; it uses the ordinary storage selectors, not fixture overrides.</summary>
public sealed class MainFontSaveAcceptanceTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private MonoBehaviour coordinator;
    private MapGenerationController controller;
    private string Output => Environment.GetEnvironmentVariable("RPG_MAIN_FONT_EVIDENCE");
    private object Call(string name, params object[] args) => coordinator.GetType().GetMethod(name).Invoke(coordinator,args);
    private string State => coordinator.GetType().GetProperty("State").GetValue(coordinator).ToString();
    [Serializable] private sealed class Report
    {
        public string stage, project, persistent, slots, state, profile, content, runId, worldSha, fontSha, exception;
        public string[] initialSlots;
        public bool normalStorage, saveSucceeded, restored;
    }
    private IEnumerator Load(string stage)
    {
        if (Environment.GetEnvironmentVariable("RPG_MAIN_FONT_STAGE") != stage) Assert.Ignore("Explicit main acceptance stage only");
        Assert.That(Path.GetFullPath(Path.Combine(Application.dataPath,"..")), Is.EqualTo(@"D:\UnityProjects\RPG_Demo").IgnoreCase);
        TowerRunRuntimeEnvironment.ResetOverrides();
#if UNITY_EDITOR
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/SampleScene.unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
        Assert.Fail("Editor-only main acceptance");
#endif
        yield return null;
        coordinator=UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true).Single(p=>p.GetType().Name=="TowerRunCoordinator");
        controller=UnityEngine.Object.FindObjectOfType<MapGenerationController>(true);
        Assert.That(State,Is.EqualTo("StartupChoice"));
        Assert.That(Path.GetFullPath(TowerRunRuntimeEnvironment.SlotRoot),Is.EqualTo(Path.GetFullPath(Path.Combine(Application.persistentDataPath,"tower-run-slots"))));
        Assert.That(TowerRunRuntimeEnvironment.Preferences.GetType().Name,Is.EqualTo("TowerRunPlayerPrefsStore"));
        Directory.CreateDirectory(Output);
        if (Environment.GetEnvironmentVariable("RPG_MAIN_FONT_CAPTURE") == "1") yield return CaptureViews("startup");
    }
    private TowerRunSlotReadResult Slot(int i) => (TowerRunSlotReadResult)Call("ReadSlot",i);
    private Report Record(string stage)
    {
        object[] args={null,null}; Assert.That((bool)Call("TryCreateSaveRequest",args),Is.True,args[1]?.ToString());
        var entry=(TowerRunCheckpointRecord)args[0];
        string world=MapHostStateSnapshot.Capture(controller); File.WriteAllText(Path.Combine(Output,stage+"-world.txt"),world);
        return new Report{stage=stage,project=Application.dataPath,persistent=Application.persistentDataPath,slots=TowerRunRuntimeEnvironment.SlotRoot,
            normalStorage=true,state=State,profile=entry.generation.profileId,content=entry.generation.ecology.contentVersion,runId=entry.runId,
            worldSha=MapHostCompatibility.Sha(System.Text.Encoding.UTF8.GetBytes(world)),
            fontSha=MapHostCompatibility.Sha(File.ReadAllBytes(Path.Combine(Application.dataPath,"Font/MyTMP.asset")))};
    }
    [UnityTest] public IEnumerator MainNewAdventureSavesActualSlotAfterCleanup()
    {
        yield return Load("new");
        string[] states=Enumerable.Range(1,3).Select(i=>Slot(i).state.ToString()).ToArray();
        Assert.That(states,Is.EqualTo(new[]{"Empty","Empty","Empty"}));
        var ui=UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true).Single(p=>p.GetType().Name=="TowerRunUiController");
        var button=(Button)ui.GetType().GetField("primaryButton",Private).GetValue(ui);Assert.That(button.interactable,Is.True);button.onClick.Invoke();
        for(int i=0;i<600 && State!="Playing" && State!="Failure";i++)yield return null;
        Assert.That(State,Is.EqualTo("Playing"));
        var row=Record("new");row.initialSlots=states;
        Assert.That(row.profile,Is.EqualTo(EcologyRecipeDefinition.VNextI1ProfileId));Assert.That(row.content,Is.EqualTo(MapHostBuildStamp.NewContentVersion));
        object[] args={null,null};Assert.That((bool)Call("TryCreateSaveRequest",args),Is.True);
        var written=(TowerRunSlotWriteResult)Call("TrySaveEntranceCandidate",1,(TowerRunCheckpointRecord)args[0],false,"empty");
        Assert.That(written.succeeded,Is.True,written.message);row.saveSucceeded=true;
        Assert.That(Slot(1).state,Is.EqualTo(TowerRunSlotState.Valid));
        File.WriteAllText(Path.Combine(Output,"new.json"),JsonUtility.ToJson(row,true));
        File.Copy(Path.Combine(row.slots,"tower-run-slot-1.json"),Path.Combine(Output,"new-checkpoint.json"),false);
    }
    [UnityTest] public IEnumerator MainFreshSessionRestoresActualNewSlot()
    {
        yield return Load("restore");var read=Slot(1);Assert.That(read.state,Is.EqualTo(TowerRunSlotState.Valid));
        string recordBefore=JsonUtility.ToJson(read.record);Call("BeginRestoreSlot",1,read.confirmationToken);
        for(int i=0;i<600 && State!="Playing" && State!="Failure";i++)yield return null;
        Assert.That(State,Is.EqualTo("Playing"));var row=Record("restore");row.restored=true;
        Assert.That(row.content,Is.EqualTo(MapHostBuildStamp.NewContentVersion));
        Assert.That(JsonUtility.ToJson(Slot(1).record),Is.EqualTo(recordBefore));
        File.WriteAllText(Path.Combine(Output,"restore.json"),JsonUtility.ToJson(row,true));
        if (Environment.GetEnvironmentVariable("RPG_MAIN_FONT_CAPTURE") == "1")
        {
            float scale=Time.timeScale;
            try
            {
                Time.timeScale=0;
                yield return CaptureViews("hud");
                var package=UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true).Single(p=>p.GetType().Name=="PackagePanelController");
                package.GetType().GetMethod("OpenPackagePanel").Invoke(package,null);yield return CaptureViews("package");package.GetType().GetMethod("ClosePackagePanel").Invoke(package,null);
                var shop=UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true).SingleOrDefault(p=>p.GetType().Name=="ShopPanelController");
                if (shop != null) {shop.GetType().GetMethod("OpenShopPanel").Invoke(shop,null);yield return CaptureViews("existing-main-shop");shop.GetType().GetMethod("CloseShopPanel").Invoke(shop,null);}
            }
            finally {Time.timeScale=scale;}
        }
    }

    // Captures the real Editor Game View. Custom sizes are removed even when capture fails.
    private IEnumerator CaptureViews(string label)
    {
#if UNITY_EDITOR
        Assert.That(Application.isBatchMode,Is.False,"Real Game View capture requires non-batch Editor");
        var assembly=typeof(UnityEditor.EditorWindow).Assembly;
        var viewType=assembly.GetType("UnityEditor.GameView",true);
        var view=UnityEditor.EditorWindow.GetWindow(viewType);
        var selected=viewType.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        int previous=(int)selected.GetValue(view);
        var sizesType=assembly.GetType("UnityEditor.GameViewSizes",true);
        var singleton=typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes=singleton.GetProperty("instance",BindingFlags.Static|BindingFlags.Public).GetValue(null);
        var groupType=assembly.GetType("UnityEditor.GameViewSizeGroupType",true);
        var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new[]{Enum.Parse(groupType,"Standalone")});
        var sizeType=assembly.GetType("UnityEditor.GameViewSize",true);
        var sizeKind=assembly.GetType("UnityEditor.GameViewSizeType",true);
        var methods=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        var add=group.GetType().GetMethod("AddCustomSize",methods);
        var remove=group.GetType().GetMethod("RemoveCustomSize",methods);
        int added=0,initial=(int)group.GetType().GetMethod("GetCustomCount",methods).Invoke(group,null);
        int builtins=(int)group.GetType().GetMethod("GetBuiltinCount",methods).Invoke(group,null);
        try
        {
            foreach(var resolution in new[]{new[]{1920,1080},new[]{1366,768}})
            {
                var size=Activator.CreateInstance(sizeType,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,
                    new[]{Enum.Parse(sizeKind,"FixedResolution"),(object)resolution[0],resolution[1],"MainFontAcceptance"},null);
                add.Invoke(group,new[]{size});added++;
                selected.SetValue(view,builtins+initial+added-1);view.Focus();view.Repaint();
                yield return new WaitForSecondsRealtime(0.5f);
                Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();
                var image=ScreenCapture.CaptureScreenshotAsTexture();
                Assert.That(image.width,Is.EqualTo(resolution[0]));Assert.That(image.height,Is.EqualTo(resolution[1]));
                string file=label+"-"+resolution[0]+"x"+resolution[1];
                File.WriteAllBytes(Path.Combine(Output,file+".png"),image.EncodeToPNG());UnityEngine.Object.Destroy(image);
                WriteVisibleFontEvidence(Path.Combine(Output,file+"-texts.json"));
            }
        }
        finally
        {
            selected.SetValue(view,previous);
            for(int i=added-1;i>=0;i--)remove.Invoke(group,new object[]{builtins+initial+i});
        }
#else
        Assert.Fail("Editor Game View required");yield break;
#endif
    }
    [Serializable] private sealed class VisibleTextReport {public List<VisibleTextRow> texts=new List<VisibleTextRow>();}
    [Serializable] private sealed class VisibleTextRow {public string name,text,font,material;public bool usesMyTMP;public int missing;}
    private void WriteVisibleFontEvidence(string path)
    {
        var report=new VisibleTextReport();
        foreach(var behaviour in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
        {
            var type=behaviour.GetType();
            if(type.FullName!="TMPro.TextMeshProUGUI" && type.FullName!="TMPro.TextMeshPro")continue;
            var font=type.GetProperty("font").GetValue(behaviour) as UnityEngine.Object;
            string text=type.GetProperty("text").GetValue(behaviour) as string;
            var material=type.GetProperty("fontSharedMaterial").GetValue(behaviour) as UnityEngine.Object;
            var row=new VisibleTextRow{name=behaviour.name,text=text,font=font==null?null:font.name,material=material==null?null:material.name};
#if UNITY_EDITOR
            row.usesMyTMP=UnityEditor.AssetDatabase.GetAssetPath(font)=="Assets/Font/MyTMP.asset";
            if(row.usesMyTMP)
            {
                Assert.That(material,Is.Not.Null);
                var serialized=new UnityEditor.SerializedObject(font);var table=serialized.FindProperty("m_CharacterTable");var codes=new HashSet<int>();
                for(int i=0;i<table.arraySize;i++)codes.Add(table.GetArrayElementAtIndex(i).FindPropertyRelative("m_Unicode").intValue);
                foreach(char c in Regex.Replace(text??"","<[^>]*>",""))if(!char.IsWhiteSpace(c)&&!codes.Contains(c))row.missing++;
                Assert.That(row.missing,Is.Zero,"Visible MyTMP text: "+row.name+" "+row.text);
            }
#endif
            report.texts.Add(row);
        }
        Assert.That(report.texts.Any(t=>t.usesMyTMP),Is.True,"Capture must include visible MyTMP text");
        File.WriteAllText(path,JsonUtility.ToJson(report,true));
    }
}

public sealed partial class MapVNextI1IntegrationTests
{
    private IEnumerator LoadFormalScene(string name)
    {
        if (coordinator != null) Call("ReturnToStartupChoice");
        TowerRunRuntimeEnvironment.ConfigureTestIsolation(Path.Combine(Root, name));
#if UNITY_EDITOR
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
            "Assets/Scenes/SampleScene.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
#endif
        yield return null;
        coordinator = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true).Single(p => p.GetType().Name == "TowerRunCoordinator");
        controller = UnityEngine.Object.FindObjectOfType<MapGenerationController>(true);
        Call("ReturnToStartupChoice");
        Assert.That(TowerRunRuntimeEnvironment.SlotRoot, Does.StartWith(Root));
        Assert.That(UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true).Any(p =>
            p.GetType().Name == "MapVNextI1Bootstrap" || p.GetType().Name == "MapVNextI1Panel"), Is.False);
    }

    [UnityTest]
    public IEnumerator FormalDefaultOrdinaryNewGameButtonUsesVNext()
    {
        yield return LoadFormalScene("FormalButton");
        var ui = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true).Single(p => p.GetType().Name == "TowerRunUiController");
        var button = (Button)ui.GetType().GetField("primaryButton", Private).GetValue(ui);
        Assert.That(button.interactable, Is.True);
        button.onClick.Invoke();
        var snapshot = controller.Orchestrator.Context.CreateDiagnosticSnapshot();
        Assert.That(snapshot.ActiveRequest.ProfileId, Is.EqualTo(EcologyRecipeDefinition.VNextI1ProfileId));
        Debug.Log("FORMAL_ORDINARY_BUTTON profile=" + snapshot.ActiveRequest.ProfileId + " state=" + State);
        Ready(true);
        SaveAndReopen(true, "FormalOrdinaryButton");
    }

    [UnityTest]
    public IEnumerator FormalDefaultSeededRunSavesReopensAndPreservesRecordedOldProfile()
    {
        yield return LoadFormalScene("FormalRestore");
        Call("BeginNewRunWithSeed", 21193);
        Ready(true);
        SaveAndReopen(true, "FormalDefault");
        VerifySameSceneOldRoute(controller.Orchestrator.Context);
        var oldRecordField = coordinator.GetType().GetField("currentRecord", Private);
        var oldRecord = (TowerRunCheckpointRecord)oldRecordField.GetValue(coordinator);
        var legacyRecord = oldRecord.DeepClone();
        legacyRecord.schemaVersion = TowerRunCheckpointRecord.CurrentSchemaVersion;
        legacyRecord.generation.profileId = "default";
        try
        {
            oldRecordField.SetValue(coordinator, legacyRecord);
            var next = (TowerRunGenerationRecord)coordinator.GetType().GetMethod("CreateNextFloorGenerationRecord", Private).Invoke(coordinator, null);
            Assert.That(next.profileId, Is.EqualTo(EcologyRecipeDefinition.MainV1ProfileId), "Legacy-schema floor changes retain their pre-promotion default.");
        }
        finally { oldRecordField.SetValue(coordinator, oldRecord); }
        Call("ReturnToStartupChoice");
        Call("BeginNewRunWithSeed", 21193);
        Ready(true);
        Assert.That(Entrance().generation.profileId, Is.EqualTo(EcologyRecipeDefinition.VNextI1ProfileId));
    }

    [UnityTest]
    public IEnumerator FormalDefaultExplicitRejectedSeedStaysStrictAndRandomOperationRetries()
    {
        yield return LoadFormalScene("FormalRandomRetry");
        const int rejectedSeed = -1254467573;
        LogAssert.Expect(LogType.Error, PlanningRejectionPattern());
        Call("BeginNewRunWithSeed", rejectedSeed);
        Assert.That(State, Is.EqualTo("Failure"), "The recorded rejected seed must remain a strict input.");
        var rejected = controller.Orchestrator.Context.CreateDiagnosticSnapshot();
        Assert.That(rejected.ActiveRequest.RequestedSeed, Is.EqualTo(rejectedSeed));
        Assert.That(rejected.AttemptChain.Count, Is.EqualTo(1));
        var record = ((TowerRunCheckpointRecord)coordinator.GetType().GetField("stagedRecord", Private).GetValue(coordinator)).DeepClone();
        var kind = coordinator.GetType().GetField("operationKind", Private).GetValue(coordinator);
        var previousFailure = controller.Orchestrator.Context.Failure;
        var settingsField = typeof(MapGenerationController).GetField("settings", Private);
        var originalSettings = settingsField.GetValue(controller);
        try
        {
            settingsField.SetValue(controller, null);
            LogAssert.Expect(LogType.Error, "MapGenerationController is missing MapGenerationSettings.");
            coordinator.GetType().GetMethod("BeginRandomOperation", Private).Invoke(coordinator, new[] { kind, record });
            Assert.That(State, Is.EqualTo("Failure"));
            Assert.That(controller.Orchestrator.Context.Failure, Is.SameAs(previousFailure), "An early configuration return retains the prior diagnostic and must not trigger random retries.");
        }
        finally { settingsField.SetValue(controller, originalSettings); }
        Call("ReturnToStartupChoice");
        coordinator.GetType().GetMethod("BeginRandomOperation", Private).Invoke(coordinator, new[] { kind, record });
        Ready(true);
        Assert.That(controller.Orchestrator.Context.CreateDiagnosticSnapshot().ActiveRequest.RequestedSeed, Is.Not.EqualTo(rejectedSeed));
        SaveAndReopen(true, "FormalRandomRetry");
    }

    [UnityTest]
    public IEnumerator FormalRandomRetriesKeepRejectedCandidatesOutOfErrorLog()
    {
        yield return LoadFormalScene("FormalRetryLogs");
        Call("BeginNewRunWithSeed", 21193);
        Ready(true);
        // Replay the user's four rejected inputs and successful fifth input through the real retry loop.
        BeginRecordedRandomCandidates(-367763121, -1644553391, -93201598, 887863686, 666268681);
        Ready(true);
        Assert.That(controller.Orchestrator.Context.CreateDiagnosticSnapshot().ActiveRequest.RequestedSeed,
            Is.EqualTo(666268681));
        SaveAndReopen(true, "FormalRetryLogs");
    }

    [UnityTest]
    public IEnumerator FormalRandomRetryExhaustionReportsOneErrorAndExplicitRetryRemainsStrict()
    {
        yield return LoadFormalScene("FormalRetryExhausted");
        Call("BeginNewRunWithSeed", 21193);
        Ready(true);
        const int rejectedSeed = -1254467573;
        LogAssert.Expect(LogType.Error, new Regex(@"^Random map generation failed after 12 candidate inputs \[(VNextFormalLayoutRejected|EcologyBudgetExhaustedOnFixedTerrain)\]:"));
        BeginRecordedRandomCandidates(Enumerable.Repeat(rejectedSeed, 12).ToArray());
        Assert.That(State, Is.EqualTo("Failure"));
        Assert.That(controller.Orchestrator.Context.IsReady, Is.False);
        Assert.That(controller.Orchestrator.Context.Failure, Is.Not.Null);
        var queue = (Queue<int>)coordinator.GetType().GetField("nextFloorInputs", Private).GetValue(coordinator);
        Assert.That(queue.Count, Is.Zero, "Exactly twelve candidate inputs must exhaust the existing budget.");

        Call("ReturnToStartupChoice");
        LogAssert.Expect(LogType.Error, PlanningRejectionPattern());
        Call("BeginNewRunWithSeed", rejectedSeed);
        Assert.That(State, Is.EqualTo("Failure"));
        Assert.That(controller.Orchestrator.Context.CreateDiagnosticSnapshot().ActiveRequest.RequestedSeed,
            Is.EqualTo(rejectedSeed));
    }

    private void BeginRecordedRandomCandidates(params int[] seeds)
    {
        var record = Entrance().DeepClone();
        record.schemaVersion = TowerRunCheckpointRecord.CurrentSchemaVersion;
        record.generation.requestedSeed = seeds[0];
        record.generation.attemptIndex = 0;
        record.generation.staticBaselineSignature = string.Empty;
        record.generation.ecology = null;
        var queueField = coordinator.GetType().GetField("nextFloorInputs", Private);
        // Fixture injection only: production fixed-input queues keep their existing four-input limit and route.
        queueField.SetValue(coordinator, new Queue<int>(seeds.Skip(1)));
        var kindField = coordinator.GetType().GetField("operationKind", Private);
        var kind = Enum.Parse(kindField.FieldType, "FloorTransition");
        coordinator.GetType().GetMethod("BeginRandomOperation", Private).Invoke(coordinator, new[] { kind, record });
    }

    private static Regex PlanningRejectionPattern()
    {
        return new Regex(@"^Map generation failed \[(VNextFormalLayoutRejected|EcologyBudgetExhaustedOnFixedTerrain)\]:");
    }
}


/// <summary>Explicit integration test in the ordinary scene; the runner uses a private baked Editor namespace.</summary>
public sealed class ShopIntegrationPlayTests
{
    [UnityTest] public IEnumerator FormalSceneMerchantTradeAndPresentation()
    {
        string root=Environment.GetEnvironmentVariable("RPG_I1_RUNTIME_ROOT");Assert.That(root,Is.Not.Null.And.Not.Empty);
        Assert.That(Application.companyName,Is.EqualTo("CodexShopEditorAcceptance"),"Do not run this fixture against main preferences");
        TowerRunRuntimeEnvironment.ConfigureTestIsolation(Path.Combine(root,"ShopFlow"));
#if UNITY_EDITOR
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/SampleScene.unity",new LoadSceneParameters(LoadSceneMode.Single));
#endif
        yield return null;
        Type type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MapVNextI1Acceptance")).First(t=>t!=null);
        var actor=new GameObject("ExplicitShopIntegrationTest");var probe=actor.AddComponent(type);
        try
        {
            var sequence=(IEnumerator)type.GetMethod("RunShopEditorTest").Invoke(probe,new object[]{Environment.GetEnvironmentVariable("RPG_I1_EVIDENCE")});
            yield return sequence;
        }
        finally {UnityEngine.Object.Destroy(actor);}
    }
}

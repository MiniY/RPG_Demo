using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using MapVNext;

public sealed partial class MapVNextI1IntegrationTests
{
    [Serializable] private sealed class LegacyInputRow {public string file,kind,status,baseline;}
    [Serializable] private sealed class LegacyInputReport {public string scope="THREE_PRIMARY_TWO_BACKUP_QUALIFICATIONS_ONE_LEGACY_NON_CHECKPOINT";public List<LegacyInputRow> rows=new List<LegacyInputRow>();}
    [UnityTest] public IEnumerator HistoricalDefaultBackupsAndLegacyDataUseOriginalPolicy()
    {
        string copies=Path.Combine(Application.dataPath,"../Artifacts/MapAcceptance/MapVNext-Integration-I1-20261029/Authoring/OldSaveCopies");
        string[] inputs=Directory.GetFiles(Path.Combine(copies,"tower-run-slots"),"tower-run-slot-*.json*").OrderBy(p=>p,StringComparer.Ordinal).ToArray();
        Assert.That(inputs.Length,Is.EqualTo(5));var report=new LegacyInputReport();
        for(int i=0;i<inputs.Length;i++)
        {
            yield return Load("Historical-"+i);
            byte[] original=File.ReadAllBytes(inputs[i]);var expected=JsonUtility.FromJson<TowerRunCheckpointRecord>(System.Text.Encoding.UTF8.GetString(original));
            Directory.CreateDirectory(TowerRunRuntimeEnvironment.SlotRoot);File.WriteAllBytes(Path.Combine(TowerRunRuntimeEnvironment.SlotRoot,"tower-run-slot-1.json"),original);
            Restore(1);Ready(false);
            Assert.That(JsonUtility.ToJson(Entrance().generation),Is.EqualTo(JsonUtility.ToJson(expected.generation)));
            SaveAndReopen(false,"Historical-"+i);
            Assert.That(File.ReadAllBytes(inputs[i]),Is.EqualTo(original));
            report.rows.Add(new LegacyInputRow {file=Path.GetFileName(inputs[i]),kind=inputs[i].EndsWith(".backup",StringComparison.Ordinal)?"BACKUP_QUALIFICATION_NOT_ADDITIONAL_PRIMARY":"PRIMARY_CHECKPOINT",status="ACTUAL_ORIGINAL_READY_SAVED_REOPENED",baseline=expected.generation.staticBaselineSignature});
            File.WriteAllText(Path.Combine(Output,"historical-inputs.json"),JsonUtility.ToJson(report,true));
        }
        yield return Load("Legacy-non-checkpoint");
        string legacy=Path.Combine(copies,"save.json");byte[] legacyBytes=File.ReadAllBytes(legacy);
        Directory.CreateDirectory(Path.GetDirectoryName(TowerRunRuntimeEnvironment.LegacySavePath));File.WriteAllBytes(TowerRunRuntimeEnvironment.LegacySavePath,legacyBytes);
        var detection=new LegacySaveDetector(new LegacySavePhysicalFileProbe(),TowerRunRuntimeEnvironment.LegacySavePath).Detect();
        Assert.That(detection.state,Is.EqualTo(LegacySaveState.Detected));Assert.That(detection.isBlocking,Is.False);
        Directory.CreateDirectory(TowerRunRuntimeEnvironment.SlotRoot);File.WriteAllBytes(Path.Combine(TowerRunRuntimeEnvironment.SlotRoot,"tower-run-slot-1.json"),legacyBytes);
        var rejected=(TowerRunSlotReadResult)Call("ReadSlot",1);// Original Evaluate rejects schemaVersion=0 as Incompatible before persisted-field checks.
        Assert.That(rejected.state,Is.EqualTo(TowerRunSlotState.Incompatible));
        Assert.That(State,Is.EqualTo("StartupChoice"));Assert.That(controller.Orchestrator.Context.IsReady,Is.False);
        Assert.That(File.ReadAllBytes(legacy),Is.EqualTo(legacyBytes));
        report.rows.Add(new LegacyInputRow {file="save.json",kind="LEGACY_GAME_DATA_NOT_TOWER_CHECKPOINT",status="DETECTED_ORIGINAL_NONMIGRATING_POLICY_AND_ORIGINAL_INCOMPATIBLE_AS_SLOT_NOT_RESTORED"});
        File.WriteAllText(Path.Combine(Output,"historical-inputs.json"),JsonUtility.ToJson(report,true));
    }
    [UnityTest] public IEnumerator MixedContentIsRejectedBeforeGenerationAndUnknownProfileCannotFallBack()
    {
        yield return Load("Mixed-content");
        var goodPath=Path.Combine(Application.dataPath,"../Artifacts/MapAcceptance/MapVNext-Integration-I1-20261029/VersionedRuns/i1-formal-pilot-05/VNext-21193-checkpoint.json");
        var record=JsonUtility.FromJson<TowerRunCheckpointRecord>(File.ReadAllText(goodPath));
        record.generation.ecology.contentVersion=MapHostBuildStamp.OldContentVersion;
        int projected=0,ready=0;controller.MapGenerated+=_=>projected++;controller.Orchestrator.GameplayReady+=_=>ready++;
        LogAssert.Expect(LogType.Error,new Regex(@"^Map integration failed \[(ExactRetainedEcologyRecipeUnavailable|RetainedEcologyContentMismatch)\]:"));
        bool accepted=controller.TryGenerateMap(new MapGenerationRequest(Guid.NewGuid(),record.generation.requestedSeed,record.generation.profileId,ecologyEntry:record.generation.ecology.ToIdentity()));
        Assert.That(accepted,Is.False);Assert.That(projected,Is.Zero);Assert.That(ready,Is.Zero);
        Assert.That(controller.Orchestrator.Context.ActiveRegistry,Is.Null);Assert.That(controller.Orchestrator.Context.IsReady,Is.False);
        yield return Load("Unknown-profile");record.generation.profileId="unknown-I1-version";
        Directory.CreateDirectory(TowerRunRuntimeEnvironment.SlotRoot);File.WriteAllText(Path.Combine(TowerRunRuntimeEnvironment.SlotRoot,"tower-run-slot-1.json"),JsonUtility.ToJson(record));
        Assert.That(((TowerRunSlotReadResult)Call("ReadSlot",1)).state,Is.EqualTo(TowerRunSlotState.Incompatible));
        File.WriteAllText(Path.Combine(Output,"wrong-content.txt"),"Mixed MainV1 content with VNext profile rejected before projection/Registry/Ready. Unknown profile slot rejected without fallback. No source file was changed for negative testing.");
    }
    private Application.LogCallback consumerExpectedBoundedRetry;
    private MonoBehaviour consumerProbe;
    private IEnumerator consumerRoutine;
    private void CleanupConsumerTestProbe()
    {
        Exception first=null;
        var handler=consumerExpectedBoundedRetry;consumerExpectedBoundedRetry=null;
        var routine=consumerRoutine;consumerRoutine=null;
        var probe=consumerProbe;consumerProbe=null;
        try {if(handler!=null)Application.logMessageReceived-=handler;}catch(Exception error) {first=error;}
        try {(routine as IDisposable)?.Dispose();}catch(Exception error) {if(first==null)first=error;else Debug.LogException(error);}
        try {if(probe!=null)UnityEngine.Object.Destroy(probe);}catch(Exception error) {if(first==null)first=error;else Debug.LogException(error);}
        if(first!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
    }
    // The pinned runner can stop a test before its next MoveNext after an async Error.
    [UnityTearDown] public IEnumerator RemoveConsumerListenerEvenAfterFrameworkAbort()
    {CleanupConsumerTestProbe();yield return null;}
    [UnityTest] public IEnumerator RealConsumersPortalTransitionDeathAndRestoreUseFormalOwners()
    {
        yield return Load("Real-consumers");
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MapVNextI1Acceptance")).First(t=>t!=null);
        int fixtureSeed=(int)type.GetField("ConsumerFixtureSeed").GetRawConstantValue();
        Assert.AreEqual(7727,fixtureSeed,"Positive input must be predeclared, never a runtime fallback.");
        Call("BeginNewRunWithSeed",fixtureSeed);Ready(true);
        consumerProbe=(MonoBehaviour)controller.gameObject.AddComponent(type);
        consumerExpectedBoundedRetry=(message,stack,kind)=>
        {
            if(kind==LogType.Error&&(message.StartsWith("Map generation failed [VNextFormalLayoutRejected]:",StringComparison.Ordinal)||message.StartsWith("Map generation failed [EcologyBudgetExhaustedOnFixedTerrain]:",StringComparison.Ordinal)))
                LogAssert.Expect(LogType.Error,message);
        };
        Application.logMessageReceived+=consumerExpectedBoundedRetry;
        try
        {
            consumerRoutine=(IEnumerator)type.GetMethod("RunConsumerChecksForTests").Invoke(consumerProbe,null);
            while(consumerRoutine.MoveNext())yield return consumerRoutine.Current;
            Ready(true);
        }
        finally {CleanupConsumerTestProbe();}
    }
    [UnityTest] public IEnumerator ActualRoleGroundCannotBePlannedOnRockAndCannotActivateThere()
    {
        yield return Load("Precommit-rock-negative");Call("BeginNewRunWithSeed",21193);Ready(true);
        var context=controller.Orchestrator.Context;int originalRegistryCount=context.ActiveRegistry.Count;
        var row=context.ActiveRegistry.Entries.First(r=>r.Role==MerchantPlacementService.MerchantRole&&r.Instance is Component);
        Transform realSource=((Component)row.Instance).transform;Vector3 originalPose=realSource.position;
        var faces=MapVNextCliffExclusion.Derive(context.ActiveMap,controller.TilemapRenderer.Coordinates).Regions;
        Vector2Int illegal=faces.Select(f=>controller.TilemapRenderer.Coordinates.WorldToCell(f.vertices.Aggregate(Vector2.zero,(a,b)=>a+b)/f.vertices.Length))
            .First(cell=>context.ActiveMap.IsInside(cell)&&!context.ActiveSpatialGeometry.IsPotentialPoseClear(realSource,controller.TilemapRenderer.Coordinates.CellToWorld(cell)));
        var candidate=new MapPlacementPlan(context.ActiveGenerationId.Value);
        Assert.That(candidate.TryAdd(new MapPlacementReservation("negative.real-merchant",row.Role,illegal,new[]{illegal},MapPlacementOwnership.SceneBound),out _),Is.True);
        var sources=new Dictionary<string,Transform> {{"negative.real-merchant",realSource}};
        Assert.Throws<MapPlanningException>(()=>MapSpatialPlacement.ValidatePlannedRoles(context.ActiveSpatialGeometry,candidate,sources,controller.TilemapRenderer.Coordinates));
        Assert.That(context.ActiveRegistry.Count,Is.EqualTo(originalRegistryCount));Assert.That(realSource.position,Is.EqualTo(originalPose));
        Assert.That(MapSpatialPlacement.Prepare(context,context.ActiveGenerationId.Value,realSource.gameObject,controller.TilemapRenderer.Coordinates.CellToWorld(illegal),out _),Is.False);
        Assert.That(realSource.position,Is.EqualTo(originalPose));
        File.WriteAllText(Path.Combine(Output,"precommit-rock-negative.txt"),"Actual Merchant source footprint rejected by the same planned-role validator called by JointPlanner; no new Registry/activation/position write. Prepare independently rejects same destination. This negative validator check supplements actual production role materialization, not natural gameplay.");
    }
    [UnityTest] public IEnumerator VersionedStairConflictUsesOriginalFiniteEcologyBudgetBeforePublication()
    {
        yield return Load("Stair-candidate-rejection");Call("BeginNewRunWithSeed",21193);Ready(true);
        var context=controller.Orchestrator.Context;var basis=context.ActiveSpatialGeometry;
        var foot=((IEcologyMapSession)coordinator).PlayerMovementFoot;Guid generation=context.ActiveGenerationId.Value;
        string terrain=MapVNextRandomLayout.SemanticHash(context.ActiveMap);int originalCount=context.ActiveRegistry.Count;
        basis.ValidateVersionedStairAccess(basis,foot,context.ActiveMap.SpawnCell);
        var blocker=new GameObject("Inactive_Independent_Stair_Conflict_Negative");blocker.SetActive(false);
        try
        {
            blocker.AddComponent<BoxCollider2D>().size=Vector2.one;
            Vector2 position=context.ActiveSpatialContext.NavigationLinks[0].Points[2];
            var blocked=basis.WithPlannedSolids(new[]{MapPlannedSolidGeometry.Capture("negative.stair-solid",blocker.transform,position,Quaternion.identity)});
            var exception=Assert.Throws<MapPlanningException>(()=>basis.ValidateVersionedStairAccess(blocked,foot,context.ActiveMap.SpawnCell));
            Assert.That(exception.Code,Is.EqualTo("CriticalRouteBlocked"));
            int attempts=0;
            var exhausted=EcologyPlanningAttempts.Run(context.ActiveMap,basis,_=>
            {
                attempts++;basis.ValidateVersionedStairAccess(blocked,foot,context.ActiveMap.SpawnCell);
                Assert.Fail("Blocked whole-foot stair must never publish a plan.");return null;
            });
            Assert.That(exhausted.Succeeded,Is.False);Assert.That(attempts,Is.EqualTo(EcologyPlanningAttempts.MaximumAttempts));
            Assert.That(exhausted.Failures.Count,Is.EqualTo(8));Assert.That(exhausted.Failures.All(x=>x.Contains(":CriticalRouteBlocked:")),Is.True);
            Assert.That(MapVNextRandomLayout.SemanticHash(context.ActiveMap),Is.EqualTo(terrain));
            Assert.That(context.ActiveGenerationId,Is.EqualTo(generation));Assert.That(context.ActiveRegistry.Count,Is.EqualTo(originalCount));
            File.WriteAllText(Path.Combine(Output,"stair-candidate-rejection.json"),JsonUtility.ToJson(new StairRejection {attempts=attempts,failures=exhausted.Failures.ToArray(),terrainUnchanged=true,registryUnchanged=true},true));
        }
        finally {UnityEngine.Object.Destroy(blocker);}
    }
    [Serializable] private sealed class StairRejection {public int attempts;public string[] failures;public bool terrainUnchanged,registryUnchanged;}

}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class MapVNextI1IntegrationTests
{
    [UnityTest] public IEnumerator FormalStairsNativeMotionDiagnosticPreservesActualFailure()
    {
        yield return Load("Stair-native-diagnostic");Call("BeginNewRunWithSeed",21193);Ready(true);
        SaveAndReopen(true,"Diagnostic-21193");Ready(true);yield return null;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MapVNextI1Acceptance")).First(t=>t!=null);
        var probe=(MonoBehaviour)controller.gameObject.AddComponent(type);
        try {yield return (IEnumerator)type.GetMethod("RunStairDiagnosticForTests").Invoke(probe,null);Ready(true);}
        finally {UnityEngine.Object.Destroy(probe);}
    }
    private MonoBehaviour recordedNavigationProbe;
    private Application.LogCallback recordedNavigationExpectedBoundedRetry;
    private Stack<IEnumerator> recordedNavigationStack;
    private Exception recordedNavigationFailure;
    private bool recordedNavigationFailureReported;
    private readonly List<string> recordedNavigationSecondary=new List<string>();
    private void RetainRecordedNavigationFailure(Exception error,string stage)
    {if(recordedNavigationFailure==null)recordedNavigationFailure=error;else recordedNavigationSecondary.Add(stage+": "+error);}
    private void CleanupRecordedNavigationProbe()
    {
        var handler=recordedNavigationExpectedBoundedRetry;recordedNavigationExpectedBoundedRetry=null;
        try {if(handler!=null)Application.logMessageReceived-=handler;}catch(Exception error) {RetainRecordedNavigationFailure(error,"detach exact bounded-rejection listener");}
        var pending=recordedNavigationStack;recordedNavigationStack=null;
        while(pending!=null&&pending.Count>0)
        {try {(pending.Pop() as IDisposable)?.Dispose();}catch(Exception error) {RetainRecordedNavigationFailure(error,"dispose diagnostic");}}
        var probe=recordedNavigationProbe;recordedNavigationProbe=null;
        try {if(probe!=null)UnityEngine.Object.Destroy(probe);}catch(Exception error) {RetainRecordedNavigationFailure(error,"destroy diagnostic probe");}
    }
    // NUnit can abort after an async Error without another MoveNext/Dispose on the test.
    [UnityTearDown] public IEnumerator RemoveRecordedNavigationProbeAfterFrameworkAbort()
    {
        CleanupRecordedNavigationProbe();yield return null;
        foreach(string detail in recordedNavigationSecondary)Debug.LogWarning("Secondary recorded navigation cleanup: "+detail);
        var failure=recordedNavigationFailure;bool reported=recordedNavigationFailureReported;
        recordedNavigationFailure=null;recordedNavigationFailureReported=false;recordedNavigationSecondary.Clear();
        if(!reported&&failure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
    [UnityTest] public IEnumerator Recorded8675309ResidentNavigationFaultPreservesActualFailure()
        => RunRecordedNavigationTest("Recorded-8675309-navigation","RunRecorded8675309NavigationDiagnosticForTests",8675309);
    [UnityTest] public IEnumerator RecordedFullSequenceResidentNavigationFaultPreservesActualFailure()
        => RunRecordedNavigationTest("Recorded-full-sequence-navigation","RunRecordedFullNavigationForTests");
    private IEnumerator RunRecordedNavigationTest(string name,string method,int? seed=null)
    {
        yield return Load(name);
        if(seed.HasValue) {Call("BeginNewRunWithSeed",seed.Value);Ready(true);SaveAndReopen(true,"Diagnostic-"+seed.Value);Ready(true);yield return null;}
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MapVNextI1Acceptance")).First(t=>t!=null);
        recordedNavigationFailure=null;recordedNavigationFailureReported=false;recordedNavigationSecondary.Clear();
        recordedNavigationStack=new Stack<IEnumerator>();recordedNavigationProbe=(MonoBehaviour)controller.gameObject.AddComponent(type);
        if(!seed.HasValue)
        {
            recordedNavigationExpectedBoundedRetry=(message,stack,kind)=>
            {
                if(kind==LogType.Error&&(message.StartsWith("Map generation failed [VNextFormalLayoutRejected]:",StringComparison.Ordinal)||message.StartsWith("Map generation failed [EcologyBudgetExhaustedOnFixedTerrain]:",StringComparison.Ordinal)))LogAssert.Expect(LogType.Error,message);
            };
            Application.logMessageReceived+=recordedNavigationExpectedBoundedRetry;
        }
        try
        {
            try {recordedNavigationStack.Push((IEnumerator)type.GetMethod(method).Invoke(recordedNavigationProbe,null));}
            catch(Exception error) {RetainRecordedNavigationFailure(error,"create diagnostic routine");}
            while(recordedNavigationFailure==null&&recordedNavigationStack.Count>0)
            {
                bool moved=false;object current=null;
                try {moved=recordedNavigationStack.Peek().MoveNext();if(moved)current=recordedNavigationStack.Peek().Current;}
                catch(Exception error) {RetainRecordedNavigationFailure(error,"diagnostic execution");break;}
                if(!moved)
                {try {(recordedNavigationStack.Pop() as IDisposable)?.Dispose();}catch(Exception error) {RetainRecordedNavigationFailure(error,"completed diagnostic");}continue;}
                if(current is IEnumerator nested)recordedNavigationStack.Push(nested);else yield return current;
            }
            if(recordedNavigationFailure==null&&seed.HasValue) {try {Ready(true);}catch(Exception error) {RetainRecordedNavigationFailure(error,"final ready");}}
        }
        finally {CleanupRecordedNavigationProbe();}
        recordedNavigationFailureReported=true;
        if(recordedNavigationFailure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(recordedNavigationFailure).Throw();
    }
    [Serializable] private sealed class DiagnosticCompletionStatus {public string status,failure;}
    [Test] public void FullSequenceDiagnosticDisposeDoesNotClaimCompletion()
    {
        string previous=Environment.GetEnvironmentVariable("RPG_I1_EVIDENCE");
        string root=Path.Combine(Output,"FullDiagnosticAbort-"+Guid.NewGuid().ToString("N"));
        GameObject actor=null;IEnumerator routine=null;Exception first=null;var secondary=new List<string>();
        void Retain(Exception error,string stage) {if(first==null)first=error;else secondary.Add(stage+": "+error);}
        try
        {
            Environment.SetEnvironmentVariable("RPG_I1_EVIDENCE",root);
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MapVNextI1Acceptance")).First(t=>t!=null);
            actor=new GameObject("I1FullDiagnosticAbortFixture");var probe=(MonoBehaviour)actor.AddComponent(type);
            routine=(IEnumerator)type.GetMethod("RunRecordedFullNavigationForTests").Invoke(probe,null);
            Assert.That(routine.MoveNext(),Is.True,"Original Run must be pending at its first yield.");
            (routine as IDisposable).Dispose();routine=null;
            var record=JsonUtility.FromJson<DiagnosticCompletionStatus>(File.ReadAllText(Path.Combine(root,"i1-player-evidence.json")));
            Assert.That(record.status,Is.EqualTo("INCOMPLETE_SEE_XML"));
            Assert.That(string.IsNullOrEmpty(record.failure),Is.True,"Dispose must not invent an uncaptured original exception.");
        }
        catch(Exception error) {Retain(error,"early dispose negative");}
        finally
        {
            try {(routine as IDisposable)?.Dispose();}catch(Exception error) {Retain(error,"dispose pending test routine");}
            try {if(actor!=null)UnityEngine.Object.DestroyImmediate(actor);}catch(Exception error) {Retain(error,"destroy abort probe");}
            try {Environment.SetEnvironmentVariable("RPG_I1_EVIDENCE",previous);}catch(Exception error) {Retain(error,"restore evidence environment");}
            foreach(string detail in secondary)Debug.LogWarning("Secondary full abort fixture cleanup: "+detail);
        }
        if(first!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
    }
    [TestCase(false)] [TestCase(true)]
    public void TerminalEvidenceLockedMovementRetainsFirstAndWritesFailure(bool preexistingFailure)
    {
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MapVNextI1Acceptance")).First(t=>t!=null);
        var actor=new GameObject("I1TerminalEvidenceIOFixture");var probe=(MonoBehaviour)actor.AddComponent(type);
        string root=Path.Combine(Output,"TermIO-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var record=Activator.CreateInstance(type.GetNestedType("Evidence",BindingFlags.NonPublic),true);
        type.GetField("output",flags).SetValue(probe,root);type.GetField("evidence",flags).SetValue(probe,record);
        Exception original=preexistingFailure?new InvalidOperationException("OriginalFailureMustRemainFirst"):null;
        var secondary=new List<string>();Exception result;
        try
        {
            using(var locked=new FileStream(Path.Combine(root,"movement.csv"),FileMode.CreateNew,FileAccess.Write,FileShare.None))
                result=(Exception)type.GetMethod("PersistTerminalEvidence",flags).Invoke(probe,new object[]{original,secondary,true,"TEST_FIXTURE_COMPLETE","INCOMPLETE"});
            Assert.That(result,Is.Not.Null);
            if(preexistingFailure) {Assert.That(ReferenceEquals(result,original),Is.True);Assert.That(secondary.Any(x=>x.Contains("final movement")),Is.True);}
            else Assert.That(result,Is.InstanceOf<IOException>());
            var terminal=JsonUtility.FromJson<DiagnosticCompletionStatus>(File.ReadAllText(Path.Combine(root,"i1-player-evidence.json")));
            Assert.That(terminal.status,Is.EqualTo("FAIL"));Assert.That(terminal.failure,Does.Contain(preexistingFailure?"OriginalFailureMustRemainFirst":"IOException"));
            Assert.That(File.Exists(Path.Combine(root,"frames.csv")),Is.True,"The second trace write must still run.");
        }
        finally {UnityEngine.Object.DestroyImmediate(actor);}
    }
    [Test] public void TerminalEvidenceLockedJsonCreatesExplicitUnconfirmedFailureSidecar()
    {
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MapVNextI1Acceptance")).First(t=>t!=null);
        var actor=new GameObject("I1TerminalJsonIOFixture");var probe=(MonoBehaviour)actor.AddComponent(type);
        string root=Path.Combine(Output,"TermJSON-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var record=Activator.CreateInstance(type.GetNestedType("Evidence",BindingFlags.NonPublic),true);
        type.GetField("output",flags).SetValue(probe,root);type.GetField("evidence",flags).SetValue(probe,record);
        string canonical=Path.Combine(root,"i1-player-evidence.json");File.WriteAllText(canonical,"{\"status\":\"RUNNING\"}");
        try
        {
            Exception result;
            using(var locked=new FileStream(canonical,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
                result=(Exception)type.GetMethod("PersistTerminalEvidence",flags).Invoke(probe,new object[]{null,new List<string>(),true,"TEST_FIXTURE_COMPLETE","INCOMPLETE"});
            Assert.That(result,Is.InstanceOf<IOException>());
            var terminal=JsonUtility.FromJson<DiagnosticCompletionStatus>(File.ReadAllText(Path.Combine(root,"i1-terminal-failure.json")));
            Assert.That(terminal.status,Is.EqualTo("FAIL"));Assert.That(terminal.failure,Does.Contain("IOException"));
            Assert.That(File.ReadAllText(Path.Combine(root,"i1-terminal-failure.json")),Does.Contain("TERMINAL_CANONICAL_JSON_UNCONFIRMED"));
            Assert.That(JsonUtility.FromJson<DiagnosticCompletionStatus>(File.ReadAllText(canonical)).status,Is.EqualTo("RUNNING"),"The failure sidecar must not claim the locked canonical file was rewritten.");
        }
        finally {UnityEngine.Object.DestroyImmediate(actor);}
    }
    [Test] public void DestroyCleanupLockedConsumerTraceRetainsActualOriginalFailure()
    {
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MapVNextI1Acceptance")).First(t=>t!=null);
        var actor=new GameObject("I1DestroyFirstFailureIOFixture");var probe=(MonoBehaviour)actor.AddComponent(type);
        string root=Path.Combine(Output,"DestroyIO-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var record=Activator.CreateInstance(type.GetNestedType("Evidence",BindingFlags.NonPublic),true);
        type.GetField("output",flags).SetValue(probe,root);type.GetField("evidence",flags).SetValue(probe,record);
        var original=new InvalidOperationException("ActualOriginalMustSurviveDestroyCleanup");
        var persist=type.GetMethod("PersistTerminalEvidence",flags);var destroy=type.GetMethod("OnDestroy",flags);
        try
        {
            Assert.That(persist.Invoke(probe,new object[]{original,new List<string>(),true,"UNUSED_COMPLETE","INCOMPLETE"}),Is.SameAs(original));
            string canonical=Path.Combine(root,"i1-player-evidence.json");
            string originalFailure=JsonUtility.FromJson<DiagnosticCompletionStatus>(File.ReadAllText(canonical)).failure;
            Assert.That(type.GetField("consumerTraceWrittenPath",flags).GetValue(probe),Is.Null,"Force the actual first write rather than a no-new-rows fast return.");
            using(var locked=new FileStream(Path.Combine(root,"real-consumers.csv"),FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None))
            {
                var failure=Assert.Throws<TargetInvocationException>(()=>destroy.Invoke(probe,null));
                Assert.That(failure.InnerException,Is.SameAs(original));
                var terminal=JsonUtility.FromJson<DiagnosticCompletionStatus>(File.ReadAllText(canonical));
                Assert.That(terminal.status,Is.EqualTo("FAIL"));Assert.That(terminal.failure,Is.EqualTo(originalFailure));
                string json=File.ReadAllText(canonical);
                Assert.That(json,Does.Contain("SECONDARY_DESTROY_CLEANUP: flush consumer trace"));Assert.That(json,Does.Contain("IOException"));
            }
            Assert.DoesNotThrow(()=>destroy.Invoke(probe,null),"A subsequent successful cleanup must not rethrow the old failure.");
        }
        finally {UnityEngine.Object.DestroyImmediate(actor);}
    }
    [Test] public void DiagnosticBufferedBatchesRetainLockedFirstAndWriteSecondWithoutRetry()
    {
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MapVNextI1Acceptance")).First(t=>t!=null);
        var actor=new GameObject("I1DiagnosticBatchFixture");var probe=(MonoBehaviour)actor.AddComponent(type);
        string root=Path.Combine(Output,"DiagnosticBatchIO-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        void Set(string name,object value)=>type.GetField(name,flags).SetValue(probe,value);
        object Get(string name)=>type.GetField(name,flags).GetValue(probe);
        var flush=type.GetMethod("FlushBufferedConsumerObservation",flags);
        try
        {
            Set("output",root);Set("bufferedConsumerLifecycle",new List<string>{"life-exact"});Set("bufferedConsumerNative",new List<string>{"native-exact"});
            flush.Invoke(probe,null);
            Assert.That(File.ReadAllLines(Path.Combine(root,"consumer-lifecycle.jsonl")),Is.EqualTo(new[]{"life-exact"}));
            Assert.That(File.ReadAllLines(Path.Combine(root,"consumer-native-motion.csv")),Is.EqualTo(new[]{"native-exact"}));
            Assert.That(Get("bufferedConsumerLifecycle"),Is.Null);Assert.That(Get("bufferedConsumerNative"),Is.Null);
            string negative=Path.Combine(root,"locked-first");Directory.CreateDirectory(negative);Set("output",negative);Set("bufferedFlushAttempted",false);
            Set("bufferedConsumerLifecycle",new List<string>{"unconfirmed-life-exact"});Set("bufferedConsumerNative",new List<string>{"second-independent-exact"});
            using(var locked=new FileStream(Path.Combine(negative,"consumer-lifecycle.jsonl"),FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {
                var failure=Assert.Throws<TargetInvocationException>(()=>flush.Invoke(probe,null));Assert.That(failure.InnerException,Is.TypeOf<AggregateException>());
                Assert.That(File.ReadAllLines(Path.Combine(negative,"consumer-native-motion.csv")),Is.EqualTo(new[]{"second-independent-exact"}));
                Assert.That(File.ReadAllLines(Path.Combine(negative,"consumer-lifecycle.jsonl.unconfirmed-batch")),Is.EqualTo(new[]{"unconfirmed-life-exact"}));
                Assert.That(Get("bufferedConsumerLifecycle"),Is.EqualTo(new[]{"unconfirmed-life-exact"}));Assert.That(Get("bufferedConsumerNative"),Is.Null);
                var retry=Assert.Throws<TargetInvocationException>(()=>flush.Invoke(probe,null));Assert.That(retry.InnerException,Is.TypeOf<InvalidOperationException>());
                Assert.That(File.ReadAllLines(Path.Combine(negative,"consumer-native-motion.csv")),Has.Length.EqualTo(1));
            }
        }
        finally {UnityEngine.Object.DestroyImmediate(actor);}
    }
    [UnityTest] public IEnumerator Recorded7727UnsafeHandoffRequiresActualInputAckAndLawfulCorrection()
    {
        yield return Load("Recorded-7727-handoff");Call("BeginNewRunWithSeed",7727);Ready(true);yield return null;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MapVNextI1Acceptance")).First(t=>t!=null);
        var probe=(MonoBehaviour)controller.gameObject.AddComponent(type);
        IEnumerator routine=null;Exception firstFailure=null;
        void Retain(Exception error) {if(firstFailure==null)firstFailure=error;else Debug.LogWarning("Secondary recorded-regression cleanup failure: "+error);}
        try
        {
            try {routine=(IEnumerator)type.GetMethod("RunRecordedHandoffForTests").Invoke(probe,null);}
            catch(Exception error) {Retain(error);}
            while(routine!=null&&firstFailure==null)
            {
                bool moved=false;object current=null;
                try {moved=routine.MoveNext();if(moved)current=routine.Current;}
                catch(Exception error) {Retain(error);break;}
                if(!moved)break;yield return current;
            }
            if(firstFailure==null) {try {Ready(true);}catch(Exception error) {Retain(error);}}
        }
        finally
        {
            try {(routine as IDisposable)?.Dispose();}catch(Exception error) {Retain(error);}
            try {UnityEngine.Object.Destroy(probe);}catch(Exception error) {Retain(error);}
        }
        if(firstFailure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstFailure).Throw();
    }
}

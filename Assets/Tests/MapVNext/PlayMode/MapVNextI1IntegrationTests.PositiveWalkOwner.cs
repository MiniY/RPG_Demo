using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class MapVNextI1IntegrationTests
{
    [Serializable] private sealed class PositiveWalkDeathProof
    {
        public string scope = "CONTROLLED_ORIGINAL_DEATH_RECEIVER_NEGATIVE_NOT_TRAVERSAL_OR_NATURAL_COMBAT";
        public string buildId, originalGeneration, state, explicitFailure;
        public long originalLease, retiredLease;
        public bool barrierRetired, originalFailurePreserved, noRestoration;
    }
    [UnityTest] public IEnumerator OriginalPlayerDeathRejectsPositiveWalkBeforeNullBarrierRead()
    {
        yield return Load("Positive-walk-death-owner"); Call("BeginNewRunWithSeed", 21193); Ready(true);
        var foot = ((IEcologyMapSession)coordinator).PlayerMovementFoot as CircleCollider2D;
        Assert.That(foot, Is.Not.Null); var body = foot.attachedRigidbody;
        var binding = body.GetComponent<EcologyCombatBinding>(); long originalLease = binding.Lease;
        Guid generation = controller.Orchestrator.Context.ActiveGenerationId.Value;
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("MapVNextI1Acceptance")).First(t => t != null);
        var probe = (MonoBehaviour)controller.gameObject.AddComponent(type);
        void Set(string name, object value) => type.GetField(name, Private).SetValue(probe, value);
        Set("map", controller); Set("session", coordinator); Set("body", body); Set("foot", foot);
        Set("action", body.GetComponents<MonoBehaviour>().Single(p => p.GetType().Name == "PlayerAction"));
        // The probe cleanup writes consumer traces; isolate them from other cases in this Run.
        string probeOutput = Path.Combine(Output, "PositiveWalkOwnerDriver"); Directory.CreateDirectory(probeOutput);
        Set("trial", "Controlled-original-death-before-positive-walk"); Set("output", probeOutput);
        IEnumerator routine = null;
        try
        {
            var damage = body.GetComponents<MonoBehaviour>().Single(p => p.GetType().Name == "PlayerDamageController");
            damage.GetType().GetMethod("TakeDamage", new[] { typeof(float), typeof(Transform) }).Invoke(damage, new object[] { float.MaxValue, null });
            Assert.That(State, Is.EqualTo("DeathChoice")); Assert.That(binding.Barrier, Is.Null);
            Assert.That(binding.Lease, Is.Not.EqualTo(originalLease));
            routine = (IEnumerator)type.GetMethod("MoveBodyTo", Private).Invoke(probe, new object[] { body.position + Vector2.right, false, null });
            var error = Assert.Throws<InvalidOperationException>(() => routine.MoveNext());
            Assert.That(error.Message, Does.StartWith("OriginalPlayerDiedDuringPositiveWalk:"));
            Assert.That(error.Message, Does.Contain("health=0")); Assert.That(error.Message, Does.Contain("barrierNull=True"));
            Assert.That(State, Is.EqualTo("DeathChoice"));
            Assert.That(controller.Orchestrator.Context.ActiveGenerationId.Value, Is.EqualTo(generation));
            File.WriteAllText(Path.Combine(Output, "positive-walk-death-owner.json"), JsonUtility.ToJson(new PositiveWalkDeathProof {
                buildId = MapHostBuildStamp.BuildId, originalGeneration = generation.ToString("D"), state = State,
                originalLease = originalLease, retiredLease = binding.Lease, explicitFailure = error.ToString(),
                barrierRetired = binding.Barrier == null, originalFailurePreserved = true, noRestoration = true }, true));
        }
        finally
        {
            (routine as IDisposable)?.Dispose(); UnityEngine.Object.Destroy(probe);
        }
        yield return null;
    }
}

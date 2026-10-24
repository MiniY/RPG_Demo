#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class EcologyPreparedPoolProbe : MonoBehaviour
{
    public static int SourceId;
    public bool Prepared;
    public int Lease;
    [NonSerialized] public int AwakeCount, InvalidAwake, InvalidEnable;
    public readonly List<int> ActivationLeases = new List<int>();
    private void Awake()
    {
        if (gameObject.GetInstanceID() == SourceId) return;
        AwakeCount++; if (!Prepared) InvalidAwake++;
    }
    private void OnEnable()
    {
        if (gameObject.GetInstanceID() == SourceId) return;
        if (!Prepared || Lease < 1) InvalidEnable++;
        ActivationLeases.Add(Lease);
    }
    private void OnDisable() { Prepared = false; }
}

public sealed class EcologyPreparedPoolPlayModeTests
{
    private readonly HashSet<GameObject> owned = new HashSet<GameObject>();
    private GameObject ownedManager;
    private float previousTimeScale;
    [SetUp] public void CaptureTime() { previousTimeScale = Time.timeScale; }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        foreach (GameObject actor in owned) if (actor != null) UnityEngine.Object.Destroy(actor);
        owned.Clear();
        if (ownedManager != null) UnityEngine.Object.Destroy(ownedManager);
        yield return null; EcologyPreparedPoolProbe.SourceId = 0; Time.timeScale = previousTimeScale;
    }
    private static object Call(Type pool, string method, params object[] arguments)
    {
        try { return pool.GetMethod(method, BindingFlags.Static | BindingFlags.Public).Invoke(null, arguments); }
        catch (TargetInvocationException exception) { ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
    }

    [UnityTest]
    public IEnumerator LegacySpawnSkipsAnActuallyDestroyedQueuedObjectAndKeepsItsSourceIdentity()
    {
        Type pool = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("ObjectPoolManager")).First(t => t != null);
        var before = (Component)pool.GetProperty("Instance").GetValue(null);
        var source = new GameObject("Owned minimal stale queue source"); source.SetActive(false); owned.Add(source);
        var first = (GameObject)Call(pool, "Spawn", source, Vector3.zero, Quaternion.identity); owned.Add(first);
        if (before == null) ownedManager = ((Component)pool.GetProperty("Instance").GetValue(null)).gameObject;
        Call(pool, "ReturnOrDeactivate", first);
        UnityEngine.Object.Destroy(first); yield return null;
        Assert.That(first == null, Is.True, "The counterexample requires a genuinely destroyed queued Unity object.");
        var recovered = (GameObject)Call(pool, "Spawn", source, new Vector3(7f, 8f), Quaternion.identity); owned.Add(recovered);
        Assert.That(recovered, Is.Not.Null); Assert.That(recovered.activeInHierarchy, Is.True);
        Assert.That(recovered.transform.position, Is.EqualTo(new Vector3(7f, 8f)));
        var pooled = recovered.GetComponents<Component>().Single(c => c != null && c.GetType().Name == "PooledObject");
        Assert.That(pooled.GetType().GetProperty("SourcePrefab").GetValue(pooled), Is.SameAs(source));
    }

    [UnityTest]
    public IEnumerator OriginalPoolPreparesBeforeFirstAwakeAndEveryEnableAndRecoversFailedPreparation()
    {
        Type pool = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("ObjectPoolManager")).FirstOrDefault(t => t != null);
        Assert.That(pool, Is.Not.Null);
        Assert.That(pool.GetMethod("SpawnPrepared", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
        Component managerBefore = pool.GetProperty("Instance").GetValue(null) as Component;
        var source = new GameObject("Owned active source for prepared pool lifecycle, no physics or gameplay");
        source.SetActive(false); owned.Add(source);
        source.AddComponent<EcologyPreparedPoolProbe>(); EcologyPreparedPoolProbe.SourceId = source.GetInstanceID();
        source.SetActive(true); yield return null;
        int lease = 0;
        Action<GameObject> bind = actor =>
        {
            owned.Add(actor); Assert.That(actor.activeInHierarchy, Is.False);
            var probe = actor.GetComponent<EcologyPreparedPoolProbe>();
            probe.Prepared = true; probe.Lease = ++lease;
        };
        GameObject first;
        try { first = (GameObject)Call(pool, "SpawnPrepared", source, new Vector3(4f, 5f), Quaternion.identity, bind); }
        finally
        {
            Component now = pool.GetProperty("Instance").GetValue(null) as Component;
            if (managerBefore == null && now != null) ownedManager = now.gameObject;
        }
        Assert.That(first.activeInHierarchy, Is.True);
        var state = first.GetComponent<EcologyPreparedPoolProbe>();
        Assert.That(state.AwakeCount, Is.EqualTo(1)); Assert.That(state.InvalidAwake, Is.Zero); Assert.That(state.InvalidEnable, Is.Zero);
        Assert.That(state.ActivationLeases, Is.EqualTo(new[] { 1 }));
        Assert.That(first.transform.position, Is.EqualTo(new Vector3(4f, 5f)));
        Component pooled = first.GetComponents<Component>().Single(c => c.GetType().Name == "PooledObject");
        Assert.That(pooled.GetType().GetProperty("SourcePrefab").GetValue(pooled), Is.SameAs(source));
        Call(pool, "ReturnOrDeactivate", first);
        Assert.That(first.activeInHierarchy, Is.False); Assert.That(state.Prepared, Is.False);
        GameObject second = (GameObject)Call(pool, "SpawnPrepared", source, new Vector3(6f, 7f), Quaternion.identity, bind);
        Assert.That(second, Is.SameAs(first)); Assert.That(state.ActivationLeases, Is.EqualTo(new[] { 1, 2 }));
        Assert.That(state.AwakeCount, Is.EqualTo(1)); Assert.That(state.InvalidEnable, Is.Zero);
        Call(pool, "ReturnOrDeactivate", second);
        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() => Call(pool, "SpawnPrepared", source,
            Vector3.zero, Quaternion.identity, (Action<GameObject>)(actor =>
            { owned.Add(actor); Assert.That(actor.activeInHierarchy, Is.False); throw new InvalidOperationException("Owned preparation fault injection."); })));
        Assert.That(failure.Message, Does.Contain("fault injection"));
        Assert.That(first.activeInHierarchy, Is.False); Assert.That(state.ActivationLeases.Count, Is.EqualTo(2));
        GameObject recovered = (GameObject)Call(pool, "SpawnPrepared", source, Vector3.one, Quaternion.identity, bind);
        Assert.That(recovered, Is.SameAs(first)); Assert.That(state.ActivationLeases, Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(state.InvalidAwake, Is.Zero); Assert.That(state.InvalidEnable, Is.Zero);
        Call(pool, "ReturnOrDeactivate", recovered);
        Debug.Log("ECOLOGY_PREPARED_POOL firstAwakeAlreadyBound=True activationLeases=1,2,3 sameInstance=True failedPreparationNeverEnabled=True sourcePrefabPreserved=True");
    }

    [UnityTest]
    public IEnumerator TwelveRetainedNativeFeetPrepareInactiveIdentityThenObserveAfterRealActivationAndReuse()
    {
        Type pool = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("ObjectPoolManager")).First(t => t != null);
        Component managerBefore = pool.GetProperty("Instance").GetValue(null) as Component;
        Time.timeScale = 0f;
        var barrierObject = new GameObject("Owned prepared motion barrier"); owned.Add(barrierObject);
        var barrier = barrierObject.AddComponent<EcologyDamageBarrier>(); barrier.ConfigureProtection(Array.Empty<Rect>());
        string[] paths = Resources.Load<EcologyRecipeDefinition>(EcologyRecipeDefinition.ResourcePathFor(EcologyRecipeDefinition.MainV1ProfileId)).ResolveTypes()
            .Select(t => AssetDatabase.GetAssetPath(t.Prefab)).ToArray();
        Assert.That(paths.Length, Is.EqualTo(12), "Retired purchased fixtures remain in the private historical archive; current reuse covers twelve actual retained native types.");
        int actualActivations = 0;
        for (int i = 0; i < paths.Length; i++)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]); Assert.That(source, Is.Not.Null);
            GameObject previous = null; long previousLease = 0;
            for (int cycle = 0; cycle < 2; cycle++)
            {
                EcologyMotionBody motion = null; EcologyCombatBinding binding = null;
                Action<GameObject> prepare = actor =>
                {
                    owned.Add(actor); Assert.That(actor.activeInHierarchy, Is.False);
                    MonoBehaviour health = actor.GetComponents<MonoBehaviour>().Single(b => b.GetType().Name == "BaseMonster");
                    PropertyInfo hp = health.GetType().GetProperty("CurrentHealth");
                    binding = actor.GetComponent<EcologyCombatBinding>() ?? actor.AddComponent<EcologyCombatBinding>();
                    binding.Bind(barrier, 1, i + 2, () => health != null && (float)hp.GetValue(health) > 0f);
                    Assert.That(binding.IsLive, Is.False);
                    motion = actor.GetComponent<EcologyMotionBody>() ?? actor.AddComponent<EcologyMotionBody>();
                    Collider2D foot = actor.GetComponentsInChildren<Collider2D>(true).Single(c => c.enabled && !c.isTrigger);
                    MonoBehaviour movement = source.GetComponents<MonoBehaviour>().Single(b => b.GetType().Name == "MonsterMovementController");
                    var settings = new SerializedObject(movement);
                    motion.ConfigurePrepared(foot, settings.FindProperty("obstacleLayers").intValue, settings.FindProperty("collisionSkinWidth").floatValue);
                    Assert.That(motion.IsReady, Is.False, "An inactive configured shape has no native protection observation yet.");
                    var probe = actor.GetComponent<EcologyPreparedPoolProbe>() ?? actor.AddComponent<EcologyPreparedPoolProbe>();
                    probe.Prepared = true; probe.Lease = cycle + 1;
                };
                GameObject instance;
                try { instance = (GameObject)Call(pool, "SpawnPrepared", source, new Vector3(500f + i * 4f, 500f), Quaternion.identity, prepare); }
                finally
                {
                    Component now = pool.GetProperty("Instance").GetValue(null) as Component;
                    if (managerBefore == null && now != null) ownedManager = now.gameObject;
                }
                Assert.That(binding.IsLive, Is.True); Assert.That(motion.IsReady, Is.False);
                Physics2D.SyncTransforms(); motion.CompletePreparedConfiguration();
                Assert.That(motion.IsReady, Is.True); Assert.That(binding.MotionFaultRevision, Is.Zero);
                if (previous != null) { Assert.That(instance, Is.SameAs(previous)); Assert.That(binding.Lease, Is.GreaterThan(previousLease)); }
                var observed = instance.GetComponent<EcologyPreparedPoolProbe>();
                Assert.That(observed.InvalidAwake, Is.Zero); Assert.That(observed.InvalidEnable, Is.Zero);
                Assert.That(observed.AwakeCount, Is.EqualTo(1)); Assert.That(observed.ActivationLeases.Count, Is.EqualTo(cycle + 1));
                Time.timeScale = 1f; yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate(); Time.timeScale = 0f;
                Assert.That(binding.MotionFaultRevision, Is.Zero, "The real observer must survive actual movement steps after activation.");
                previous = instance; previousLease = binding.Lease; actualActivations++;
                Call(pool, "ReturnOrDeactivate", instance); Assert.That(motion.IsReady, Is.False);
            }
        }
        Assert.That(actualActivations, Is.EqualTo(24));
        Debug.Log($"ECOLOGY_PREPARED_ACTUAL_MOTION actualActivations={actualActivations} nativeTypes=12 sameInstanceReuse=True preAwakeIdentity=True " +
            "nativeBaselineOnlyAfterEnable=True realFixedSteps=True homeAndProfileIntegration=NOT_YET");
    }
}
#endif

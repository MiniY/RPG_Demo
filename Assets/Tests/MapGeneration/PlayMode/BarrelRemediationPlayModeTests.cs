using System.Linq;
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class BarrelRemediationPlayModeTests
{
    [UnityTest] public IEnumerator NewBarrelsKeepOriginalGeometryAndUseOneThreeSecondThreeFrameAttack()
    {
        yield return null;
        foreach (string color in new[] { "Blue", "Purple", "Red", "Yellow" })
        {
            var original = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Monsters/Barrel/Barrel_" + color + ".prefab");
            var prefab = Resources.Load<GameObject>("Ecology/RemediationV3/Barrel_" + color);
            Assert.That(prefab, Is.Not.Null, "The new version must be explicit; no in-place rewrite of retained legacy Barrels.");
            Assert.That(prefab.GetComponent("BarrelExplosionAttack"), Is.Not.Null);
            Assert.That(prefab.GetComponents<Component>().Count(c => c != null && c.GetType().Name == "MonsterAIController"), Is.EqualTo(1));
            Assert.That(prefab.transform.localScale, Is.EqualTo(original.transform.localScale));
            Assert.That(prefab.GetComponent<BoxCollider2D>().size, Is.EqualTo(original.GetComponent<BoxCollider2D>().size));
            Assert.That(prefab.GetComponent<BoxCollider2D>().offset, Is.EqualTo(original.GetComponent<BoxCollider2D>().offset));
            Assert.That(prefab.GetComponent("MonsterPopulationTargetBridge"), Is.Not.Null);
            var animator = prefab.GetComponentInChildren<Animator>(true);
            foreach (var clip in animator.runtimeAnimatorController.animationClips.Where(c => c.name.Contains("Attack_")).Distinct())
            {
                var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single(b => b.propertyName == "m_Sprite");
                var frames = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                Assert.That(frames.Length, Is.EqualTo(3), clip.name);
                Assert.That(frames.Select(f => f.value.name).ToArray(), Is.EqualTo(new[] { "Barrel_" + color + "_5_0", "Barrel_" + color + "_5_1", "Barrel_" + color + "_5_2" }));
                Assert.That(clip.length, Is.EqualTo(3f).Within(.001f), clip.name);
                Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime, Is.False);
                Assert.That(AnimationUtility.GetAnimationEvents(clip), Is.Empty, "No second animation-event damage clock.");
            }
        }
    }
    private readonly System.Collections.Generic.List<GameObject> owned = new System.Collections.Generic.List<GameObject>();
    private Component stats, playerDamage, barrelDamage, strategy;
    private object previousStats;
    private GameObject player, barrel, newPrefab;
    private EcologyDamageBarrier barrier;
    private string slots;
    private float previousTime;

    private IEnumerator ArrangeBehavior()
    {
        previousTime = Time.timeScale; Time.timeScale = 1f;
        slots = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "BarrelR3-" + System.Guid.NewGuid().ToString("N"));
        TowerRunRuntimeEnvironment.ConfigureTestIsolation(slots);
        Component session = (Component)FindType("GameSession").GetProperty("Instance").GetValue(null);
        stats = (Component)Get(session, "PlayerStats"); previousStats = Call(stats, "CaptureSnapshot");
        Call(stats, "SetCurrentHealth", 100f);
        Assert.That(Object.FindObjectOfType<MapGenerationController>(), Is.Null, "Standalone same-level fixture must not replace a live map.");
        var input = new GameObject("Barrel-Test-Input"); owned.Add(input); input.AddComponent(FindType("GameInput"));
        Call(input.GetComponent("GameInput"), "SetGameplayInputEnabled", true);
        player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Players/Warrior_Blue.prefab"), new Vector3(200f, 200f), Quaternion.identity);
        owned.Add(player); playerDamage = player.GetComponent("PlayerDamageController");
        Call(playerDamage, "SynchronizeAfterRestore");
        newPrefab = Resources.Load<GameObject>("Ecology/RemediationV3/Barrel_Blue");
        var pool = FindType("ObjectPoolManager"); bool ownPool = pool.GetProperty("Instance").GetValue(null) == null;
        barrel = (GameObject)pool.GetMethod("Spawn").Invoke(null, new object[] { newPrefab, new Vector3(201.3f, 200f), Quaternion.identity });
        owned.Add(barrel);
        if (ownPool) owned.Add(((Component)pool.GetProperty("Instance").GetValue(null)).gameObject);
        barrelDamage = barrel.GetComponent("BaseMonster"); strategy = barrel.GetComponent("BarrelExplosionAttack");
        var holder = new GameObject("Barrel-Test-Barrier"); owned.Add(holder); barrier = holder.AddComponent<EcologyDamageBarrier>();
        player.AddComponent<EcologyCombatBinding>().Bind(barrier, 1, 1, () => !(bool)Get(playerDamage, "IsDefeated"));
        barrel.AddComponent<EcologyCombatBinding>().Bind(barrier, 1, 2, () => !(bool)Get(barrelDamage, "IsDefeated"));
        player.AddComponent<EcologyMotionBody>().Configure(player.transform.Find("MovementFeet").GetComponent<Collider2D>(), Physics2D.AllLayers, .02f);
        barrel.AddComponent<EcologyMotionBody>().Configure(barrel.GetComponent<Collider2D>(), Physics2D.AllLayers, .02f);
        // The real map integration initializes home after final placement. Plain Spawn's first clone
        // awakens at the prefab's authored position before Spawn moves it; use the original map hook.
        Call(barrel.GetComponent("MonsterAIController"), "ReinitializeForMap");
        barrier.OnTrace += (request, phase) => Debug.Log($"BARREL_B1 phase={phase} status={request.Status} required={request.RequiredCompletedStep} completed={barrier.CompletedStep} world={Time.timeAsDouble:R} applied={request.DamageApplied}");
        Component ai = barrel.GetComponent("MonsterAIController");
        Subscribe(ai, "OnStateChanged", () => Debug.Log($"BARREL_FSM state={Get(ai, "CurrentStateType")} target={Get(ai, "CurrentTarget")} canTrigger={Call(strategy, "CanTrigger", player.transform)} scale={Time.timeScale}"));
        Debug.Log($"BARREL_INITIAL aiEnabled={((Behaviour)ai).enabled} player={player.transform.position} barrel={barrel.transform.position} canTrigger={Call(strategy, "CanTrigger", player.transform)} sourceLive={barrel.GetComponent<EcologyCombatBinding>().IsLive} targetLive={player.GetComponent<EcologyCombatBinding>().IsLive} scale={Time.timeScale}");
        float deadline = Time.realtimeSinceStartup + 5f;
        while (!(bool)Get(strategy, "IsLatched") && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That((bool)Get(strategy, "IsLatched"), Is.True, $"AI={Get(ai, "CurrentStateType")} target={Get(ai, "CurrentTarget")} player={player.transform.position} barrel={barrel.transform.position} visible={Get(barrel.GetComponent("MonsterPerceptionController"), "CurrentVisibleTarget")} canTrigger={Call(strategy, "CanTrigger", player.transform)} scale={Time.timeScale}");
        Debug.Log($"BARREL_ARMED world={Get(strategy, "ArmedAt")} hp={Get(playerDamage, "CurrentHealth")} FSM={Get(barrel.GetComponent("MonsterAIController"), "CurrentStateType")}");
    }

    [UnityTest] public IEnumerator RealAiFuseRunsThreeSecondsHitsOnceAndUsesOriginalDeathPool()
    {
        yield return ArrangeBehavior();
        int deaths = 0, hits = 0;
        Subscribe(barrelDamage, "OnDefeated", () => deaths++);
        Subscribe(playerDamage, "OnDamaged", () => hits++);
        double start = (double)Get(strategy, "ArmedAt");
        Call(barrelDamage, "TakeDamage", 1f, player.transform);
        yield return null;
        Assert.That((bool)Get(strategy, "IsLatched"), Is.True, "Nonlethal damage must not restart or cancel an armed fuse.");
        yield return Until(() => Time.timeAsDouble >= start + 2.9d, 4f);
        Assert.That((float)Get(playerDamage, "CurrentHealth"), Is.EqualTo(100f));
        Assert.That(deaths, Is.Zero);
        yield return Until(() => deaths == 1, 2f);
        Assert.That(Time.timeAsDouble - start, Is.GreaterThanOrEqualTo(3d));
        Assert.That(Time.timeAsDouble - start, Is.LessThan(3.4d));
        Assert.That((float)Get(playerDamage, "CurrentHealth"), Is.EqualTo(90f));
        Assert.That(hits, Is.EqualTo(1));
        Assert.That((int)Get(strategy, "ExplosionCount"), Is.EqualTo(1));
        Call(barrelDamage, "TakeDamage", 999f, player.transform);
        Assert.That(deaths, Is.EqualTo(1));
        yield return Until(() => !barrel.activeSelf, 2f);
        Assert.That(Get(barrel.GetComponent("PooledObject"), "SourcePrefab"), Is.EqualTo(newPrefab));
        Debug.Log($"BARREL_REAL_AI_SINGLE_SETTLEMENT fuse={Time.timeAsDouble-start:R} hits={hits} deaths={deaths} pooled={!barrel.activeSelf}");
    }

    [UnityTest] public IEnumerator EarlyRealKillCancelsExplosionAndOriginalDeathHappensOnce()
    {
        yield return ArrangeBehavior(); int deaths = 0;
        Subscribe(barrelDamage, "OnDefeated", () => deaths++);
        Call(barrelDamage, "TakeDamage", 999f, player.transform);
        yield return new WaitForSeconds(3.3f);
        Assert.That(deaths, Is.EqualTo(1));
        Assert.That((float)Get(playerDamage, "CurrentHealth"), Is.EqualTo(100f));
        Assert.That((int)Get(strategy, "ExplosionCount"), Is.Zero);
        Assert.That(barrel.activeSelf, Is.False);
    }

    [UnityTest] public IEnumerator ActualStaticWallBlocksBlastWhileOriginalDeathStillSettlesOnce()
    {
        yield return ArrangeBehavior();
        var blocker = new GameObject("Actual static blast wall"); owned.Add(blocker);
        int mask = (int)Get(barrel.GetComponent("MonsterMovementController"), "MovementBlockingLayers");
        int layer = Enumerable.Range(0, 32).First(i => (mask & (1 << i)) != 0); blocker.layer = layer;
        Vector2 source = barrel.GetComponent<Collider2D>().bounds.center;
        Vector2 target = player.transform.Find("MovementFeet").GetComponent<Collider2D>().bounds.center;
        blocker.transform.position = (source + target) * .5f;
        blocker.AddComponent<BoxCollider2D>().size = new Vector2(.08f, 2f);
        blocker.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        Physics2D.SyncTransforms();
        int deaths = 0; Subscribe(barrelDamage, "OnDefeated", () => deaths++);
        yield return Until(() => deaths == 1, 5f);
        Assert.That((int)Get(strategy, "ExplosionCount"), Is.EqualTo(1));
        Assert.That((int)Get(strategy, "DamageCount"), Is.Zero);
        Assert.That((float)Get(playerDamage, "CurrentHealth"), Is.EqualTo(100f));
        Assert.That(deaths, Is.EqualTo(1));
    }

    [UnityTest] public IEnumerator ArmedTargetLeavingBlastRadiusDoesNotReceiveDamageOrPreventSingleSelfDeath()
    {
        yield return ArrangeBehavior();
        var producer = player.AddComponent<EcologyMotionIntentProducer>();
        Vector2 destination = player.GetComponent<Rigidbody2D>().position + Vector2.left * 3f;
        producer.Submission = () => player.GetComponent<EcologyMotionBody>().Request(destination, EcologyMotionKind.Normal);
        yield return Until(() => Vector2.Distance(player.GetComponent<Rigidbody2D>().position, destination) < .1f, 2f);
        int deaths = 0; Subscribe(barrelDamage, "OnDefeated", () => deaths++);
        yield return Until(() => deaths == 1, 5f);
        Assert.That((int)Get(strategy, "ExplosionCount"), Is.EqualTo(1));
        Assert.That((int)Get(strategy, "DamageCount"), Is.Zero);
        Assert.That((float)Get(playerDamage, "CurrentHealth"), Is.EqualTo(100f));
    }

    [UnityTest] public IEnumerator ObservedFullFootProtectionCancelsOldFuseEvenAfterExit()
    {
        yield return ArrangeBehavior();
        Collider2D foot = player.transform.Find("MovementFeet").GetComponent<Collider2D>();
        Bounds actual = foot.bounds;
        barrier.ConfigureProtection(new[] { new Rect((Vector2)actual.min - Vector2.one * .02f, (Vector2)actual.size + Vector2.one * .04f) });
        yield return new WaitForFixedUpdate();
        Assert.That(player.GetComponent<EcologyCombatBinding>().ProtectionNow, Is.True, "Actual observed full-foot containment, not a requested position.");
        Vector2 destination = player.GetComponent<Rigidbody2D>().position + Vector2.down * .7f;
        var producer = player.AddComponent<EcologyMotionIntentProducer>();
        producer.Submission = () => player.GetComponent<EcologyMotionBody>().Request(destination, EcologyMotionKind.Normal);
        yield return Until(() => !player.GetComponent<EcologyCombatBinding>().ProtectionNow, 2f);
        Assert.That(Vector2.Distance(foot.bounds.center, destination), Is.LessThan(1f), "Observe actual solved movement, not a submitted destination.");
        Assert.That(Vector2.Distance(foot.ClosestPoint(barrel.GetComponent<Collider2D>().bounds.center), barrel.GetComponent<Collider2D>().bounds.center), Is.LessThan(1.5f), "Keep the victim within blast radius so distance cannot explain the rejected damage.");
        yield return Until(() => (bool)Get(barrelDamage, "IsDefeated"), 5f);
        Assert.That((float)Get(playerDamage, "CurrentHealth"), Is.EqualTo(100f));
        Assert.That((int)Get(strategy, "ExplosionCount"), Is.EqualTo(1));
    }

    [UnityTearDown] public IEnumerator CleanupBehavior()
    {
        if (stats == null) yield break;
        for (int index = owned.Count - 1; index >= 0; index--) if (owned[index] != null) Object.Destroy(owned[index]);
        owned.Clear(); yield return null;
        Call(stats, "RestoreSnapshot", previousStats, false); stats = null;
        Time.timeScale = previousTime; TowerRunRuntimeEnvironment.ResetOverrides();
        if (slots != null && System.IO.Directory.Exists(slots)) System.IO.Directory.Delete(slots, true);
    }

    private static System.Type FindType(string name) => System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Get(object source, string name) => source.GetType().GetProperty(name).GetValue(source);
    private static object Call(object source, string name, params object[] args) => source.GetType().GetMethod(name, args.Select(a => a.GetType()).ToArray()).Invoke(source, args);
    private static IEnumerator Until(System.Func<bool> condition, float seconds)
    {
        float deadline = Time.realtimeSinceStartup + seconds;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(condition(), Is.True, "Barrel directed behavior deadline.");
    }
    private static void Subscribe(Component source, string name, System.Action callback)
    {
        var info = source.GetType().GetEvent(name);
        var parameters = info.EventHandlerType.GetMethod("Invoke").GetParameters().Select(p => System.Linq.Expressions.Expression.Parameter(p.ParameterType)).ToArray();
        var body = System.Linq.Expressions.Expression.Call(System.Linq.Expressions.Expression.Constant(callback), typeof(System.Action).GetMethod("Invoke"));
        info.AddEventHandler(source, System.Linq.Expressions.Expression.Lambda(info.EventHandlerType, body, parameters).Compile());
    }

}

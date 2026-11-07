using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class ConsumablesPlayTests
{
    private ConsumableFixture fixture;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        TowerRunRuntimeEnvironment.ConfigureTestIsolation(ConsumableReflection.PrivateRoot());
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        yield return null;
        fixture = new ConsumableFixture();
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            fixture?.Dispose();
        }
        finally
        {
            fixture = null;
            TowerRunRuntimeEnvironment.ResetOverrides();
        }
    }

    [UnityTest]
    public IEnumerator OnlyTheMatchingCurrentStatDeterminesRecoveryOrInventory()
    {
        fixture.SetStats(20f, 100f);
        fixture.Fire(fixture.Drop("PlantCream_Small", 1));
        Assert.That(fixture.Amount("PlantCream_Small"), Is.EqualTo(1));
        Assert.That(fixture.Current("Health"), Is.EqualTo(20f));
        fixture.SetStats(100f, 20f);
        fixture.Fire(fixture.Drop("Meat", 1));
        Assert.That(fixture.Amount("Meat"), Is.EqualTo(1));
        Assert.That(fixture.Current("Mana"), Is.EqualTo(20f));
        fixture.SetStats(70f, 60f);
        fixture.Fire(fixture.Drop("Meat", 1));
        Assert.That(fixture.Current("Health"), Is.EqualTo(95f));
        fixture.Fire(fixture.Drop("PlantCream_Small", 1));
        Assert.That(fixture.Current("Mana"), Is.EqualTo(70f));
        fixture.Fire(fixture.Drop("PlantCream_Big", 1));
        Assert.That(fixture.Current("Mana"), Is.EqualTo(95f));
        Assert.That(fixture.Maximum("Health"), Is.EqualTo(100));
        Assert.That(fixture.Maximum("Mana"), Is.EqualTo(100));
        yield return null;
    }

    [UnityTest]
    public IEnumerator WholeUnitOverflowIsDiscardedWithoutAddingInventory()
    {
        fixture.SetStats(95f, 99f);
        fixture.Fire(fixture.Drop("Meat", 1));
        fixture.Fire(fixture.Drop("PlantCream_Big", 1));
        Assert.That(fixture.Current("Health"), Is.EqualTo(100f));
        Assert.That(fixture.Current("Mana"), Is.EqualTo(100f));
        Assert.That(fixture.Amount("Meat") + fixture.Amount("PlantCream_Big"), Is.Zero);
        Assert.That(fixture.MemoryCurrent("Health"), Is.EqualTo(100f));
        Assert.That(fixture.MemoryCurrent("Mana"), Is.EqualTo(100f));
        yield return null;
    }

    [UnityTest]
    public IEnumerator MultipleUnitsReevaluateAfterEachCommittedUnit()
    {
        fixture.SetStats(100f, 85f);
        var kinds = new List<string>();
        fixture.ObserveStatic("RewardPickup", "OnRewardProcessed", args => kinds.Add(ConsumableReflection.Read(args[1], "Kind").ToString()));
        Component drop = fixture.Drop("PlantCream_Small", 3);
        fixture.Fire(drop);
        Assert.That(kinds, Is.EqualTo(new[] { "Mana", "Mana", "Inventory" }));
        Assert.That(fixture.Current("Mana"), Is.EqualTo(100f));
        Assert.That(fixture.Amount("PlantCream_Small"), Is.EqualTo(1));
        Assert.That(drop.gameObject.activeSelf, Is.False);
        Assert.That((int)ConsumableReflection.Read(drop, "RemainingAmount"), Is.Zero);
        Assert.That(2 + fixture.Amount("PlantCream_Small"), Is.EqualTo(3), "Consumed units plus owned units equal the original ground quantity.");
        yield return null;
    }

    [UnityTest]
    public IEnumerator OverflowingInventoryKeepsTheUnprocessedGroundRemainder()
    {
        fixture.SetStats(100f, 95f);
        fixture.Add("PlantCream_Small", int.MaxValue);
        Component drop = fixture.Drop("PlantCream_Small", 3);
        fixture.Fire(drop);
        Assert.That(fixture.Current("Mana"), Is.EqualTo(100f));
        Assert.That(fixture.MemoryCurrent("Mana"), Is.EqualTo(100f));
        Assert.That(fixture.Amount("PlantCream_Small"), Is.EqualTo(int.MaxValue));
        Assert.That(drop.gameObject.activeSelf, Is.True);
        Assert.That((int)ConsumableReflection.Read(drop, "RemainingAmount"), Is.EqualTo(2));
        Assert.That((bool)ConsumableReflection.Read(drop, "hasBeenPickedUp"), Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ARejectedSecondCommitKeepsTheFirstAndRestoresOnlyTheFailedUnit()
    {
        fixture.SetStats(100f, 85f);
        int commits = 0;
        fixture.BindCommit(candidate => ++commits == 2 ? false : (bool)ConsumableReflection.CallStatic("GameSaveService", "TryCommitRuntimeData", candidate));
        Component drop = fixture.Drop("PlantCream_Small", 3);
        fixture.Fire(drop);
        Assert.That(commits, Is.EqualTo(2));
        Assert.That(fixture.Current("Mana"), Is.EqualTo(95f));
        Assert.That(fixture.MemoryCurrent("Mana"), Is.EqualTo(95f));
        Assert.That(fixture.Amount("PlantCream_Small"), Is.Zero);
        Assert.That((int)ConsumableReflection.Read(drop, "RemainingAmount"), Is.EqualTo(2));
        Assert.That(drop.gameObject.activeSelf, Is.True);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ARejectedFirstCommitLeavesStatsInventoryAndGroundUntouched()
    {
        fixture.SetStats(40f, 50f);
        int commits = 0;
        fixture.BindCommit(_ => { commits++; return false; });
        Component drop = fixture.Drop("Meat", 1);
        fixture.Fire(drop);
        Assert.That(fixture.Current("Health"), Is.EqualTo(40f));
        Assert.That(commits, Is.EqualTo(1));
        Assert.That(fixture.MemoryCurrent("Health"), Is.EqualTo(40f));
        Assert.That(fixture.Amount("Meat"), Is.Zero);
        Assert.That((int)ConsumableReflection.Read(drop, "RemainingAmount"), Is.EqualTo(1));
        Assert.That(drop.gameObject.activeSelf, Is.True);
        yield return null;
    }

    [UnityTest]
    public IEnumerator RepeatedCollidersAndObserverReentryCannotResettleTheSameUnit()
    {
        fixture.SetStats(95f, 100f);
        Component drop = fixture.Drop("Meat", 1);
        int notices = 0;
        fixture.ObserveStatic("RewardPickup", "OnRewardProcessed", _ => { notices++; fixture.Fire(drop); });
        fixture.Fire(drop); fixture.Fire(drop);
        Assert.That(notices, Is.EqualTo(1));
        Assert.That(fixture.Current("Health"), Is.EqualTo(100f));
        Assert.That(fixture.Amount("Meat"), Is.Zero);
        yield return null;
    }

    [UnityTest]
    public IEnumerator MissingAndMismatchedReceiversKeepTheRewardAvailableForRetry()
    {
        fixture.SetStats(50f, 100f);
        var owner = (Behaviour)fixture.Inventory;
        var other = (Behaviour)fixture.OriginalInventory;
        ScriptableObject meat = fixture.Reward("Meat");
        int otherAmount = Convert.ToInt32(ConsumableReflection.Call(other, "GetItemAmount", meat));
        Component drop = fixture.Drop("Meat", 1);
        Type pickupType = ConsumableReflection.Type("RewardPickup");

        owner.enabled = false;
        try
        {
            Assert.That(ConsumableReflection.Read(pickupType, "OnRewardPickupRequested"), Is.Null);
            fixture.Fire(drop);
            Assert.That(drop.gameObject.activeSelf, Is.True);
            Assert.That(ConsumableReflection.Read(drop, "RemainingAmount"), Is.EqualTo(1));

            other.enabled = true;
            var requests = (Delegate)ConsumableReflection.Read(pickupType, "OnRewardPickupRequested");
            Assert.That(requests.GetInvocationList().Single().Target, Is.SameAs(other));
            fixture.Fire(drop);
            Assert.That(drop.gameObject.activeSelf, Is.True);
            Assert.That(ConsumableReflection.Read(drop, "RemainingAmount"), Is.EqualTo(1));
            Assert.That(Convert.ToInt32(ConsumableReflection.Call(other, "GetItemAmount", meat)), Is.EqualTo(otherAmount));
            Assert.That(fixture.Current("Health"), Is.EqualTo(50f));
            Assert.That(fixture.MemoryCurrent("Health"), Is.EqualTo(50f));
        }
        finally
        {
            other.enabled = false;
            owner.enabled = true;
        }

        fixture.Fire(drop);
        Assert.That(fixture.Current("Health"), Is.EqualTo(75f));
        Assert.That(fixture.Amount("Meat"), Is.Zero);
        Assert.That(drop.gameObject.activeSelf, Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ASecondReceiverAppearingMidStackStopsBeforeTheNextMutation()
    {
        fixture.SetStats(95f, 100f);
        Component original = fixture.OriginalInventory;
        int before = Convert.ToInt32(ConsumableReflection.Call(original, "GetItemAmount", fixture.Reward("Meat")));
        fixture.Observe(fixture.Stats, "OnStatsChanged", _ => ((Behaviour)original).enabled = true);
        Component drop = fixture.Drop("Meat", 3); fixture.Fire(drop);
        Assert.That(fixture.Current("Health"), Is.EqualTo(100f));
        Assert.That((int)ConsumableReflection.Read(drop, "RemainingAmount"), Is.EqualTo(2));
        Assert.That(drop.gameObject.activeSelf, Is.True);
        Assert.That(fixture.Amount("Meat"), Is.Zero);
        Assert.That(Convert.ToInt32(ConsumableReflection.Call(original, "GetItemAmount", fixture.Reward("Meat"))), Is.EqualTo(before));
        yield return null;
    }

    [UnityTest]
    public IEnumerator StatsAndProcessedObserverExceptionsCannotUndoTheCommit()
    {
        fixture.SetStats(50f, 100f);
        int later = 0;
        fixture.Observe(fixture.Stats, "OnStatsChanged", _ => throw new InvalidOperationException("CONSUMABLES_TEST_STATS_OBSERVER"));
        fixture.Observe(fixture.Stats, "OnStatsChanged", _ => later++);
        fixture.ObserveStatic("RewardPickup", "OnRewardProcessed", _ => throw new InvalidOperationException("CONSUMABLES_TEST_RESULT_OBSERVER"));
        fixture.ObserveStatic("RewardPickup", "OnRewardProcessed", _ => later++);
        LogAssert.Expect(LogType.Error, new Regex(@"\[Consumables\] Stats committed;.*CONSUMABLES_TEST_STATS_OBSERVER", RegexOptions.Singleline));
        LogAssert.Expect(LogType.Error, new Regex(@"\[Consumables\] Settlement committed;.*CONSUMABLES_TEST_RESULT_OBSERVER", RegexOptions.Singleline));
        Component drop = fixture.Drop("Meat", 1); fixture.Fire(drop); fixture.Fire(drop);
        Assert.That(fixture.Current("Health"), Is.EqualTo(75f));
        Assert.That(fixture.MemoryCurrent("Health"), Is.EqualTo(75f));
        Assert.That(later, Is.EqualTo(2));
        Assert.That(drop.gameObject.activeSelf, Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator InvalidRecoveryConfigurationIsRejectedWithoutFallingBackToInventory()
    {
        var definition = UnityEngine.Object.Instantiate(fixture.Reward("Meat"));
        fixture.Own(definition);
        ConsumableReflection.Set(definition, "recoveryAmount", float.NaN);
        fixture.SetStats(50f, 100f);
        LogAssert.Expect(LogType.Warning, new Regex(@"\[Consumables\] Invalid recovery configuration: reward=meat; kind=Health; amount=NaN"));
        Component drop = fixture.Drop(definition, 1); fixture.Fire(drop);
        Assert.That(fixture.Current("Health"), Is.EqualTo(50f));
        Assert.That(fixture.Amount("Meat"), Is.Zero);
        Assert.That(drop.gameObject.activeSelf, Is.True);
        yield return null;
    }

    [UnityTest]
    public IEnumerator OwnedUnitsBypassTheOriginalDropMultiplier()
    {
        var definition = UnityEngine.Object.Instantiate(fixture.Reward("PlantCream_Small"));
        fixture.Own(definition);
        ConsumableReflection.Set(definition, "pickupAmountMultiplier", 4);
        fixture.SetStats(100f, 100f);
        Component raw = fixture.Drop(definition, 1); fixture.Fire(raw);
        Assert.That(fixture.Amount(definition), Is.EqualTo(4));
        Assert.That((bool)ConsumableReflection.Call(fixture.Inventory, "TryRemoveItem", definition, 1L), Is.True);
        Component owned = fixture.Drop(definition, 1, ownedUnits: true); fixture.Fire(owned);
        Assert.That(fixture.Amount(definition), Is.EqualTo(4), "One returned owned unit must not become four units.");
        yield return null;
    }

    [UnityTest]
    public IEnumerator InventoryHopKeepsTheRootAndColliderStillAndWorksWhilePaused()
    {
        fixture.SetStats(100f, 100f);
        Component drop = fixture.Drop(fixture.Reward("Meat"), 1, visual: true);
        Vector3 root = drop.transform.position;
        Bounds bounds = drop.GetComponent<Collider2D>().bounds;
        fixture.Fire(drop); fixture.Fire(drop);
        Assert.That(fixture.Amount("Meat"), Is.EqualTo(1), "Commit once before the presentation begins.");
        Assert.That(drop.gameObject.activeSelf, Is.True);
        yield return new WaitForSecondsRealtime(.12f);
        Assert.That(drop.transform.GetChild(0).localPosition.y, Is.GreaterThan(.05f));
        Assert.That(drop.transform.position, Is.EqualTo(root));
        Assert.That(drop.GetComponent<Collider2D>().bounds, Is.EqualTo(bounds));
        yield return new WaitForSecondsRealtime(.35f);
        Assert.That(drop.gameObject.activeSelf, Is.False);
        Assert.That(fixture.Amount("Meat"), Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator DestroyingTheVisualDuringPresentationStillCompletesRetirement()
    {
        Component drop = fixture.Drop(fixture.Reward("Meat"), 1, visual: true);
        fixture.Fire(drop);
        UnityEngine.Object.Destroy(drop.transform.GetChild(0).gameObject);
        yield return new WaitForSecondsRealtime(.45f);
        Assert.That(drop.gameObject.activeSelf, Is.False);
        Assert.That(fixture.Amount("Meat"), Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator ADisabledAndReusedLeaseIgnoresTheOldPresentation()
    {
        Component drop = fixture.Drop(fixture.Reward("Meat"), 1, visual: true);
        fixture.Fire(drop);
        drop.gameObject.SetActive(false);
        ConsumableReflection.Call(drop, "InitializeOwnedUnits", fixture.Reward("Meat"), 2);
        drop.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(.45f);
        Assert.That(drop.gameObject.activeSelf, Is.True);
        Assert.That((int)ConsumableReflection.Read(drop, "RemainingAmount"), Is.EqualTo(2));
        Assert.That(drop.transform.GetChild(0).localPosition, Is.EqualTo(Vector3.zero));
        Assert.That(fixture.Amount("Meat"), Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator TheActualPoolReusesTheLeaseWithoutAnOldCompletionCallback()
    {
        GameObject prefab = fixture.Drop(fixture.Reward("Meat"), 1, visual: true).gameObject;
        prefab.SetActive(false);
        Action<GameObject> prepare = go => ConsumableReflection.Call(go.GetComponent(ConsumableReflection.Type("RewardPickup")),
            "InitializeOwnedUnits", fixture.Reward("Meat"), 1);
        var first = (GameObject)ConsumableReflection.CallStatic("ObjectPoolManager", "SpawnPrepared", prefab,
            new Vector3(12000f, 12000f, 0f), Quaternion.identity, prepare);
        fixture.Own(first);
        fixture.Fire(first.GetComponent(ConsumableReflection.Type("RewardPickup")));
        ConsumableReflection.CallStatic("ObjectPoolManager", "ReturnOrDeactivate", first);
        Action<GameObject> prepareNext = go => ConsumableReflection.Call(go.GetComponent(ConsumableReflection.Type("RewardPickup")),
            "InitializeOwnedUnits", fixture.Reward("Meat"), 2);
        var second = (GameObject)ConsumableReflection.CallStatic("ObjectPoolManager", "SpawnPrepared", prefab,
            new Vector3(12002f, 12000f, 0f), Quaternion.identity, prepareNext);
        Assert.That(second, Is.SameAs(first));
        yield return new WaitForSecondsRealtime(.45f);
        Assert.That(second.activeSelf, Is.True);
        Assert.That((int)ConsumableReflection.Read(second.GetComponent(ConsumableReflection.Type("RewardPickup")), "RemainingAmount"), Is.EqualTo(2));
        Assert.That(second.transform.GetChild(0).localPosition, Is.EqualTo(Vector3.zero));
        Assert.That(fixture.Amount("Meat"), Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator ComponentOnlyDisableRetiresTheCommitAndPreparedPoolReuseEnablesTheReceiver()
    {
        GameObject prefab = fixture.Drop(fixture.Reward("Meat"), 1, visual: true).gameObject;
        prefab.SetActive(false);
        Action<GameObject> prepare = go => ConsumableReflection.Call(go.GetComponent(ConsumableReflection.Type("RewardPickup")),
            "InitializeOwnedUnits", fixture.Reward("Meat"), 1);
        var first = (GameObject)ConsumableReflection.CallStatic("ObjectPoolManager", "SpawnPrepared", prefab,
            new Vector3(13000f, 13000f, 0f), Quaternion.identity, prepare);
        fixture.Own(first);
        var pickup = (Behaviour)first.GetComponent(ConsumableReflection.Type("RewardPickup"));
        fixture.Fire(pickup);
        Assert.That(first.activeSelf, Is.True);
        pickup.enabled = false;
        Assert.That(first.activeSelf, Is.False, "Component-level cancellation must finish retirement after the commit.");
        var second = (GameObject)ConsumableReflection.CallStatic("ObjectPoolManager", "SpawnPrepared", prefab,
            new Vector3(13002f, 13000f, 0f), Quaternion.identity, prepare);
        Assert.That(second, Is.SameAs(first));
        Assert.That(pickup.isActiveAndEnabled, Is.True);
        fixture.Fire(pickup);
        yield return new WaitForSecondsRealtime(.45f);
        Assert.That(second.activeSelf, Is.False);
        Assert.That(fixture.Amount("Meat"), Is.EqualTo(2));
    }

    [UnityTest]
    public IEnumerator LargeStacksYieldAndEventuallyConserveEveryUnit()
    {
        fixture.SetStats(100f, 100f);
        Component drop = fixture.Drop("PlantCream_Small", 97);
        fixture.Fire(drop);
        Assert.That(drop.gameObject.activeSelf, Is.True, "Processing a large stack must leave the frame responsive.");
        float deadline = Time.realtimeSinceStartup + 3f;
        while (drop.gameObject.activeSelf && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(drop.gameObject.activeSelf, Is.False);
        Assert.That(fixture.Amount("PlantCream_Small"), Is.EqualTo(97));
    }

    [UnityTest]
    public IEnumerator MissingWorldOrBindingCannotDropOrRestoreAnything()
    {
        fixture.Add("Meat", 2); fixture.SetStats(50f, 100f);
        object[] args = { fixture.Reward("Meat"), null };
        Assert.That((bool)ConsumableReflection.Call(fixture.Inventory, "TryDropOne", args), Is.False);
        Assert.That(fixture.Amount("Meat"), Is.EqualTo(2));
        Assert.That(fixture.Current("Health"), Is.EqualTo(50f));
        ConsumableReflection.Call(fixture.Inventory, "BindRewardService", new object[] { null });
        Component drop = fixture.Drop("Meat", 1); fixture.Fire(drop);
        Assert.That(drop.gameObject.activeSelf, Is.True);
        Assert.That(fixture.Amount("Meat"), Is.EqualTo(2));
        Assert.That(fixture.Current("Health"), Is.EqualTo(50f));
        yield return null;
    }
}

internal static class ConsumableReflection
{
    internal const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    internal static Type Type(string name) => System.Type.GetType(name + ", Assembly-CSharp", true);
    internal static object Read(object target, string name)
    {
        var type = target as Type ?? target.GetType();
        return type.GetProperty(name, All)?.GetValue(target is Type ? null : target) ?? type.GetField(name, All)?.GetValue(target is Type ? null : target);
    }
    internal static void Set(object target, string name, object value) => target.GetType().GetField(name, All).SetValue(target, value);
    internal static object Call(object target, string name, params object[] args)
    {
        var type = target as Type ?? target.GetType();
        return type.GetMethods(All).Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(target is Type ? null : target, args);
    }
    internal static object CallStatic(string type, string name, params object[] args) => Call(Type(type), name, args);
    internal static Component Session => (Component)Read(Type("GameSession"), "Instance");
    internal static ScriptableObject Reward(string name) => AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/ScriptObjects/RewardSOs/" + name + ".asset");
    internal static string PrivateRoot()
    {
        string evidence = Environment.GetEnvironmentVariable("ADR03_EVIDENCE") ?? Path.Combine(Path.GetTempPath(), "RPG_Demo-Consumables");
        return Path.Combine(evidence, "PrivateCases", Guid.NewGuid().ToString("N"));
    }
    internal static Delegate Forward(Type delegateType, Action<object[]> callback)
    {
        var parameters = delegateType.GetMethod("Invoke").GetParameters().Select(p => Expression.Parameter(p.ParameterType, p.Name)).ToArray();
        return Expression.Lambda(delegateType, Expression.Invoke(Expression.Constant(callback),
            Expression.NewArrayInit(typeof(object), parameters.Select(p => Expression.Convert(p, typeof(object))))), parameters).Compile();
    }
    internal static void BindRewards(Component inventory, Component stats, MapGenerationController map, Func<object, bool> callback)
    {
        var constructor = Type("PlayerRewardService").GetConstructors(All).Single();
        Delegate commit = null;
        if (callback != null)
        {
            Type type = constructor.GetParameters()[4].ParameterType;
            var parameter = Expression.Parameter(type.GetMethod("Invoke").GetParameters()[0].ParameterType, "candidate");
            commit = Expression.Lambda(type, Expression.Invoke(Expression.Constant(callback), Expression.Convert(parameter, typeof(object))), parameter).Compile();
        }
        object service = constructor.Invoke(new object[] { inventory, stats, map, (Func<bool>)(() => true), commit });
        Call(inventory, "BindRewardService", service);
    }
}

internal sealed class ConsumableFixture : IDisposable
{
    private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
    private readonly List<Action> unsubscribe = new List<Action>();
    private readonly Component[] originals;
    private readonly object memoryBefore;
    internal readonly Component Inventory;
    internal readonly Component Stats;
    internal readonly Collider2D PlayerCollider;
    internal Component OriginalInventory => originals[0];

    internal ConsumableFixture()
    {
        ConsumableReflection.Call(ConsumableReflection.Read(ConsumableReflection.Session, "TowerRun"), "ReturnToStartupChoice");
        originals = UnityEngine.Object.FindObjectsOfType(ConsumableReflection.Type("PlayerInventory")).Cast<Component>().Where(c => ((Behaviour)c).enabled).ToArray();
        memoryBefore = ConsumableReflection.CallStatic("GameSaveService", "Clone", ConsumableReflection.CallStatic("GameSaveService", "GetOrCreateData"));
        foreach (Component original in originals) ((Behaviour)original).enabled = false;
        Assert.That(ConsumableReflection.Read(ConsumableReflection.Type("RewardPickup"), "OnRewardPickupRequested"), Is.Null);
        ConsumableReflection.CallStatic("GameSaveService", "TryReplaceRuntimeData", ConsumableReflection.CallStatic("GameSaveService", "CreateNewData"));
        var player = new GameObject("ConsumablesFixturePlayer"); Own(player); player.SetActive(false); player.tag = "Player";
        player.transform.position = new Vector3(10000f, 10000f, 0f);
        PlayerCollider = player.AddComponent<CircleCollider2D>(); PlayerCollider.isTrigger = true;
        Inventory = player.AddComponent(ConsumableReflection.Type("PlayerInventory"));
        ConsumableReflection.Set(Inventory, "goldReward", Reward("Gold"));
        ConsumableReflection.Set(Inventory, "rewardRegistry", AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/ScriptObjects/RewardRegistry.asset"));
        player.SetActive(true);
        var statsObject = new GameObject("ConsumablesFixtureStats"); Own(statsObject);
        Stats = statsObject.AddComponent(ConsumableReflection.Type("PlayerStatsRuntime"));
        BindCommit(null);
    }

    internal ScriptableObject Reward(string name) => ConsumableReflection.Reward(name);
    internal void Own(UnityEngine.Object item) => owned.Add(item);
    internal int Amount(string name) => Amount(Reward(name));
    internal int Amount(ScriptableObject reward) => Convert.ToInt32(ConsumableReflection.Call(Inventory, "GetItemAmount", reward));
    internal void Add(string name, long amount) => Assert.That((bool)ConsumableReflection.Call(Inventory, "TryAddItem", Reward(name), amount), Is.True);
    internal float Current(string stat) => Convert.ToSingle(ConsumableReflection.Call(Stats, "GetCurrent", Enum.Parse(ConsumableReflection.Type("PlayerStatType"), stat)));
    internal int Maximum(string stat) => Convert.ToInt32(ConsumableReflection.Call(Stats, "GetMaximum", Enum.Parse(ConsumableReflection.Type("PlayerStatType"), stat)));
    internal float MemoryCurrent(string stat) => Convert.ToSingle(ConsumableReflection.Read(ConsumableReflection.Read(ConsumableReflection.CallStatic("GameSaveService", "GetOrCreateData"), "playerStats"), "current" + stat));
    internal void SetStats(float health, float mana)
    {
        ConsumableReflection.Call(Stats, "SetCurrentHealth", health);
        ConsumableReflection.Call(Stats, "SetCurrentMana", mana);
        object candidate = ConsumableReflection.CallStatic("GameSaveService", "Clone", ConsumableReflection.CallStatic("GameSaveService", "GetOrCreateData"));
        ConsumableReflection.Call(Inventory, "WriteToSaveData", candidate);
        ConsumableReflection.Call(Stats, "WriteToSaveData", candidate);
        Assert.That((bool)ConsumableReflection.CallStatic("GameSaveService", "TryCommitRuntimeData", candidate), Is.True);
    }
    internal void BindCommit(Func<object, bool> callback)
    {
        ConsumableReflection.BindRewards(Inventory, Stats, null, callback);
    }
    internal Component Drop(string name, int amount) => Drop(Reward(name), amount);
    internal Component Drop(ScriptableObject reward, int amount, bool visual = false, bool ownedUnits = false)
    {
        var go = new GameObject("ConsumablesFixtureGround"); Own(go); go.SetActive(false);
        go.transform.position = new Vector3(11000f, 11000f, 0f);
        if (visual)
        {
            var child = new GameObject("Visual"); child.transform.SetParent(go.transform, false);
            child.AddComponent(ConsumableReflection.Type("RewardVisualController"));
        }
        Component pickup = go.AddComponent(ConsumableReflection.Type("RewardPickup"));
        ConsumableReflection.Call(pickup, ownedUnits ? "InitializeOwnedUnits" : "Initialize", reward, amount);
        go.SetActive(true);
        Physics2D.SyncTransforms();
        return pickup;
    }
    internal void Fire(Component pickup) => ConsumableReflection.Call(pickup, "OnTriggerEnter2D", PlayerCollider);
    internal void Observe(object target, string name, Action<object[]> callback)
    {
        Type type = target as Type ?? target.GetType(); var info = type.GetEvent(name, ConsumableReflection.All);
        Delegate handler = ConsumableReflection.Forward(info.EventHandlerType, callback);
        object instance = target is Type ? null : target;
        info.AddEventHandler(instance, handler); unsubscribe.Add(() => info.RemoveEventHandler(instance, handler));
    }
    internal void ObserveStatic(string type, string name, Action<object[]> callback) => Observe(ConsumableReflection.Type(type), name, callback);
    public void Dispose()
    {
        foreach (Action remove in unsubscribe) remove();
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) UnityEngine.Object.DestroyImmediate(owned[i]);
        ConsumableReflection.CallStatic("GameSaveService", "TryReplaceRuntimeData", memoryBefore);
        foreach (Component original in originals) if (original != null) ((Behaviour)original).enabled = true;
    }
}

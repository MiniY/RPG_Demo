using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// 验证正式 SampleScene / RandomGenerated 路径中的 Damageable 奖励闭环。
/// </summary>
public sealed class DamageableRewardLoopPlayModeTests
{
    private const string TorchBlueDamageableId = "Torch_Blue";

    [UnityTest]
    public IEnumerator TorchBlueCompletesRewardLoopOncePerLifeAndAcrossRegeneration()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        yield return null;

        Scene scene = SceneManager.GetActiveScene();
        Assert.That(scene.path, Is.EqualTo("Assets/Scenes/SampleScene.unity"));

        GameObject runtimeRoot = FindRootObject(scene, "RandomMapRuntime");
        Assert.That(runtimeRoot, Is.Not.Null);

        MapGenerationController controller =
            runtimeRoot.GetComponentInChildren<MapGenerationController>(true);
        MonsterPlacementService monsterService =
            runtimeRoot.GetComponent<MonsterPlacementService>();
        Assert.That(controller, Is.Not.Null);
        Assert.That(monsterService, Is.Not.Null);
        Assert.That(monsterService.MonsterTarget, Is.Not.Null);
        Assert.That(monsterService.ReinitializationTarget, Is.Not.Null);

        Component damageable = FindComponentInHierarchy(
            monsterService.ReinitializationTarget,
            "BaseDamageable");
        Assert.That(damageable, Is.Not.Null);

        object reward = AssertFormalTorchBlueRewardMapping(damageable, controller.Player);
        Component inventory = controller.Player.GetComponent("PlayerInventory");
        Assert.That(inventory, Is.Not.Null);
        int inventoryBefore = GetInventoryAmount(inventory, reward);

        InvokeTakeDamage(damageable, float.MaxValue);
        InvokeTakeDamage(damageable, float.MaxValue);
        yield return WaitForActiveRewardCount(1);

        List<Component> firstLifeRewards = FindActiveRewardPickups();
        Assert.That(firstLifeRewards, Has.Count.EqualTo(1),
            "同一次生命中的重复致命伤害只能生成一个奖励实例。");
        Component firstReward = firstLifeRewards[0];

        yield return PickUpThroughPhysics(firstReward, controller.Player);
        Assert.That(GetInventoryAmount(inventory, reward), Is.EqualTo(inventoryBefore + 1));
        Assert.That(firstReward.gameObject.activeSelf, Is.False,
            "成功拾取后奖励应按现有对象池流程退出场景。");

        InvokePickupCallback(firstReward, controller.Player);
        Assert.That(GetInventoryAmount(inventory, reward), Is.EqualTo(inventoryBefore + 1),
            "同一个奖励实例不能因重复触发而重复入账。");

        yield return WaitForInactive(monsterService.ReinitializationTarget);
        controller.RegenerateMap();
        yield return null;

        Assert.That(monsterService.ReinitializationTarget.activeInHierarchy, Is.True);
        Assert.That(GetDamageableFlag(damageable, "IsDefeated"), Is.False,
            "正式地图再生成应通过既有激活生命周期开始下一次有效生命。");

        InvokeTakeDamage(damageable, float.MaxValue);
        InvokeTakeDamage(damageable, float.MaxValue);
        yield return WaitForActiveRewardCount(1);

        List<Component> secondLifeRewards = FindActiveRewardPickups();
        Assert.That(secondLifeRewards, Has.Count.EqualTo(1),
            "下一次有效生命应恢复一次且仅一次的死亡结算能力。");
        Component uncollectedReward = secondLifeRewards[0];

        yield return WaitForInactive(monsterService.ReinitializationTarget);
        controller.RegenerateMap();
        yield return null;

        Assert.That(FindActiveRewardPickups(), Is.Empty,
            "地图再生成必须回收上一代未拾取的 Reward。");
        Assert.That(uncollectedReward.gameObject.activeSelf, Is.False);
        Assert.That(GetInventoryAmount(inventory, reward), Is.EqualTo(inventoryBefore + 1),
            "地图清理不能把未拾取奖励误计入背包。");

        InvokeTakeDamage(damageable, float.MaxValue);
        InvokeTakeDamage(damageable, float.MaxValue);
        yield return WaitForActiveRewardCount(1);

        List<Component> thirdLifeRewards = FindActiveRewardPickups();
        Assert.That(thirdLifeRewards, Has.Count.EqualTo(1));
        Assert.That(thirdLifeRewards[0], Is.SameAs(uncollectedReward),
            "地图清理后的下一次掉落应复用现有对象池实例。");

        yield return PickUpThroughPhysics(thirdLifeRewards[0], controller.Player);
        Assert.That(GetInventoryAmount(inventory, reward), Is.EqualTo(inventoryBefore + 2));
    }

    [UnityTest]
    public IEnumerator GeneratedDestructibleStillDropsOnceAndCleansRewardOnRegeneration()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        yield return null;

        Scene scene = SceneManager.GetActiveScene();
        GameObject runtimeRoot = FindRootObject(scene, "RandomMapRuntime");
        MapGenerationController controller =
            runtimeRoot.GetComponentInChildren<MapGenerationController>(true);
        DestructiblePlacementService destructibleService =
            runtimeRoot.GetComponent<DestructiblePlacementService>();
        Assert.That(controller, Is.Not.Null);
        Assert.That(destructibleService, Is.Not.Null);
        Assert.That(destructibleService.LastPopulationPlan.Placements, Is.Not.Empty);

        DestructiblePlacementResult placement =
            destructibleService.LastPopulationPlan.Placements[0];
        Assert.That(destructibleService.TryGetActiveInstance(
            placement.LogicalObjectId,
            out Object instance), Is.True);

        Component damageable = FindComponentInHierarchy(
            ((Component)instance).gameObject,
            "BaseDamageable");
        Assert.That(damageable, Is.Not.Null);
        Assert.That(FindActiveRewardPickups(), Is.Empty);

        InvokeTakeDamage(damageable, float.MaxValue);
        yield return null;
        int rewardCountAfterFirstDefeat = FindActiveRewardPickups().Count;
        Assert.That(rewardCountAfterFirstDefeat, Is.GreaterThanOrEqualTo(1),
            "现有 Destructible 的必掉规则仍应生成奖励。");

        InvokeTakeDamage(damageable, float.MaxValue);
        yield return null;
        Assert.That(FindActiveRewardPickups(), Has.Count.EqualTo(rewardCountAfterFirstDefeat),
            "公共 Damageable 修改不能让 Destructible 重复结算死亡奖励。");

        controller.RegenerateMap();
        yield return null;
        Assert.That(FindActiveRewardPickups(), Is.Empty,
            "地图再生成应回收 Destructible 留下的未拾取奖励。");
    }

    private static object AssertFormalTorchBlueRewardMapping(
        Component damageable,
        Transform player)
    {
        System.Type baseDamageableType = FindTypeInHierarchy(
            damageable.GetType(),
            "BaseDamageable");
        Assert.That(baseDamageableType, Is.Not.Null);

        FieldInfo damageableIdField = baseDamageableType.GetField(
            "damageableId",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo rewardListField = baseDamageableType.GetField(
            "rewardList",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(damageableIdField, Is.Not.Null);
        Assert.That(rewardListField, Is.Not.Null);

        string damageableId = damageableIdField.GetValue(damageable) as string;
        object rewardList = rewardListField.GetValue(damageable);
        Assert.That(damageableId, Is.EqualTo(TorchBlueDamageableId));
        Assert.That(damageableId, Is.Not.EqualTo(MonsterPlacementService.MainMonsterLogicalObjectId),
            "Damageable 奖励键与地图 Logical ID 承担不同职责，不能互相替代。");
        Assert.That(rewardList, Is.Not.Null);

        MethodInfo tryGetRewardTable = rewardList.GetType().GetMethod("TryGetRewardTable");
        Assert.That(tryGetRewardTable, Is.Not.Null);
        object[] lookupArguments = { damageableId, null };
        Assert.That((bool)tryGetRewardTable.Invoke(rewardList, lookupArguments), Is.True,
            "正式 DamageableDieRewardList 必须能够解析 Torch_Blue。");

        object rewardTable = lookupArguments[1];
        Assert.That(rewardTable, Is.Not.Null);
        FieldInfo tableIdField = rewardTable.GetType().GetField("damageableId");
        FieldInfo rulesField = rewardTable.GetType().GetField("dieReward");
        Assert.That(tableIdField.GetValue(rewardTable), Is.EqualTo(TorchBlueDamageableId));

        IList rules = rulesField.GetValue(rewardTable) as IList;
        Assert.That(rules, Is.Not.Null);
        Assert.That(rules, Has.Count.EqualTo(1));
        object rule = rules[0];
        object reward = rule.GetType().GetField("reward").GetValue(rule);
        Assert.That(reward, Is.Not.Null);
        Assert.That((float)rule.GetType().GetField("dropChance").GetValue(rule), Is.EqualTo(1f));
        Assert.That((int)rule.GetType().GetField("minAmount").GetValue(rule), Is.EqualTo(1));
        Assert.That((int)rule.GetType().GetField("maxAmount").GetValue(rule), Is.EqualTo(1));

        PropertyInfo rewardIdProperty = reward.GetType().GetProperty("RewardId");
        FieldInfo rewardPrefabField = reward.GetType().GetField("rewardPrefab");
        string rewardId = rewardIdProperty.GetValue(reward) as string;
        Assert.That(rewardId, Is.EqualTo("branch"));
        Assert.That(rewardPrefabField.GetValue(reward), Is.Not.Null);

        Component inventory = player.GetComponent("PlayerInventory");
        Assert.That(inventory, Is.Not.Null);
        FieldInfo rewardRegistryField = inventory.GetType().GetField(
            "rewardRegistry",
            BindingFlags.Instance | BindingFlags.NonPublic);
        object registry = rewardRegistryField.GetValue(inventory);
        Assert.That(registry, Is.Not.Null);
        MethodInfo findById = registry.GetType().GetMethod("FindById");
        Assert.That(findById.Invoke(registry, new object[] { rewardId }), Is.SameAs(reward),
            "掉落奖励必须已经注册到正式 RewardRegistry，才能进入存档/背包解析链。");

        return reward;
    }

    private static IEnumerator PickUpThroughPhysics(Component rewardPickup, Transform player)
    {
        Collider2D playerCollider = player.GetComponentInChildren<Collider2D>();
        Assert.That(playerCollider, Is.Not.Null);

        rewardPickup.transform.position = playerCollider.bounds.center;
        Physics2D.SyncTransforms();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.That(rewardPickup.gameObject.activeSelf, Is.False,
            "奖励与玩家触发器接触后应通过 RewardPickup 完成拾取。");
    }

    private static IEnumerator WaitForActiveRewardCount(int expectedCount)
    {
        const int maxFrames = 120;

        for (int frame = 0; frame < maxFrames; frame++)
        {
            if (FindActiveRewardPickups().Count == expectedCount)
                yield break;

            yield return null;
        }

        Assert.That(FindActiveRewardPickups(), Has.Count.EqualTo(expectedCount));
    }

    private static IEnumerator WaitForInactive(GameObject target)
    {
        yield return new WaitForSeconds(0.6f);

        const int maxFrames = 120;

        for (int frame = 0; frame < maxFrames && target.activeSelf; frame++)
            yield return null;

        Assert.That(target.activeSelf, Is.False,
            "被击败的 Torch_Blue 应完成现有延迟回收流程。");
    }

    private static List<Component> FindActiveRewardPickups()
    {
        List<Component> rewards = new List<Component>();

        foreach (MonoBehaviour behaviour in Object.FindObjectsOfType<MonoBehaviour>())
        {
            if (behaviour.GetType().Name == "RewardPickup" &&
                behaviour.gameObject.activeInHierarchy)
            {
                rewards.Add(behaviour);
            }
        }

        return rewards;
    }

    private static Component FindComponentInHierarchy(GameObject target, string baseTypeName)
    {
        foreach (MonoBehaviour behaviour in target.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (FindTypeInHierarchy(behaviour.GetType(), baseTypeName) != null)
                return behaviour;
        }

        return null;
    }

    private static System.Type FindTypeInHierarchy(System.Type type, string typeName)
    {
        for (System.Type current = type; current != null; current = current.BaseType)
        {
            if (current.Name == typeName)
                return current;
        }

        return null;
    }

    private static void InvokeTakeDamage(Component damageable, float damage)
    {
        MethodInfo takeDamage = damageable.GetType().GetMethod(
            "TakeDamage",
            new[] { typeof(float) });
        Assert.That(takeDamage, Is.Not.Null);
        takeDamage.Invoke(damageable, new object[] { damage });
    }

    private static void InvokePickupCallback(Component rewardPickup, Transform player)
    {
        Collider2D playerCollider = player.GetComponentInChildren<Collider2D>();
        MethodInfo callback = rewardPickup.GetType().GetMethod(
            "OnTriggerEnter2D",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(callback, Is.Not.Null);
        callback.Invoke(rewardPickup, new object[] { playerCollider });
    }

    private static int GetInventoryAmount(Component inventory, object reward)
    {
        MethodInfo getItemAmount = inventory.GetType().GetMethod("GetItemAmount");
        Assert.That(getItemAmount, Is.Not.Null);
        return (int)getItemAmount.Invoke(inventory, new[] { reward });
    }

    private static bool GetDamageableFlag(Component damageable, string propertyName)
    {
        PropertyInfo property = damageable.GetType().GetProperty(propertyName);
        Assert.That(property, Is.Not.Null);
        return (bool)property.GetValue(damageable);
    }

    private static GameObject FindRootObject(Scene scene, string objectName)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            if (rootObject.name == objectName)
                return rootObject;
        }

        return null;
    }
}

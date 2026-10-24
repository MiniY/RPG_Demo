#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class EcologyPresentationPlayModeTests
{
    private readonly List<GameObject> owned = new List<GameObject>();
    private float previousTimeScale;

    [SetUp]
    public void SetUp() { previousTimeScale = Time.timeScale; Time.timeScale = 1f; }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (GameObject actor in owned) if (actor != null) UnityEngine.Object.Destroy(actor);
        owned.Clear();
        yield return null;
        Time.timeScale = previousTimeScale;
    }

    [UnityTest]
    public IEnumerator LegacyTorchKeepsItsGenericDeathFade()
    {
        GameObject actor = Clone("Assets/Prefabs/Monsters/Torch/Torch_Blue.prefab");
        Component damage = actor.GetComponent("BaseMonster");
        Animator animator = actor.GetComponentInChildren<Animator>();
        damage.GetType().GetMethod("TakeDamage", new[] { typeof(float) }).Invoke(damage, new object[] { float.MaxValue });
        Assert.That(animator.enabled, Is.False);
        yield return new WaitForSeconds(0.15f);
        Assert.That(animator.GetComponent<SpriteRenderer>().color.a, Is.LessThan(0.9f));
    }

    private GameObject Clone(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null, path);
        GameObject actor = UnityEngine.Object.Instantiate(prefab, new Vector3(220f, 200f), Quaternion.identity);
        owned.Add(actor);
        ((Behaviour)actor.GetComponent("MonsterAIController")).enabled = false;
        // Feedback-only fixture: suppress unrelated loot and delayed pool destruction, not HP/events.
        Component damage = actor.GetComponent("BaseMonster");
        Type baseType = damage.GetType().BaseType;
        baseType.GetField("rewardList", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(damage, null);
        baseType.GetField("returnToPoolDelay", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(damage, 10f);
        return actor;
    }
}
#endif

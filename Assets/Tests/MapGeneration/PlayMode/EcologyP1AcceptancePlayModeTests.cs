#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class EcologyP1AcceptancePlayModeTests
{
    private readonly List<GameObject> owned = new List<GameObject>();
    private float previousTimeScale;
    private string output;

    [Serializable] private sealed class UnitsRow
    {
        public string typeId, prefab, prefabGuid, sprite, spriteSource, spriteGuid, animator, footShape;
        public string[] animationClips;
        public Vector2 footSize, footOffset;
        public int instance, deaths, actualReuses;
        public bool shadowPresent, healthBarPresent;
    }
    [Serializable] private sealed class UnitsReport
    {
        public string scope = "Twelve retained imported identities and actual original damage/death/pool cycles. AI disabled in isolated native fixture; not natural gameplay or human art approval.";
        public List<UnitsRow> types = new List<UnitsRow>();
    }

    [UnityTest] public IEnumerator TwelveUnitsCandidateTypesKeepExactImportedColourAndActualDamageDeathPoolReuse()
    {
        var recipe = Resources.Load<EcologyRecipeDefinition>(EcologyRecipeDefinition.ResourcePathFor(EcologyRecipeDefinition.MainV1ProfileId));
        Assert.That(recipe, Is.Not.Null);
        var types = recipe.ResolveTypes();
        Assert.That(types.Length, Is.EqualTo(12)); Assert.That(types.Any(t => t.IsBoss), Is.False);
        Type pool = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("ObjectPoolManager")).First(t => t != null);
        MethodInfo spawn = pool.GetMethod("Spawn", BindingFlags.Public | BindingFlags.Static);
        var report = new UnitsReport();
        foreach (var type in types)
        {
            GameObject prefab = type.Prefab;
            bool newManager = (UnityEngine.Object)pool.GetProperty("Instance").GetValue(null) == null;
            GameObject actor = (GameObject)spawn.Invoke(null, new object[] { prefab, new Vector3(250, 200), Quaternion.identity });
            Own(actor); if (newManager) Own(((Component)pool.GetProperty("Instance").GetValue(null)).gameObject);
            DisableAI(actor); yield return null;
            Component damage = actor.GetComponent("BaseMonster"); Assert.That(damage, Is.Not.Null);
            Type baseType = damage.GetType().BaseType;
            baseType.GetField("rewardList", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(damage, null);
            Animator animator = actor.GetComponentInChildren<Animator>(); Assert.That(animator, Is.Not.Null);
            SpriteRenderer renderer = animator.GetComponent<SpriteRenderer>(); Assert.That(renderer.sprite, Is.Not.Null);
            Assert.That(renderer.sprite.name, Does.StartWith(prefab.name + "_"), "Imported type/colour must match the actual retained Prefab.");
            string spriteSource = AssetDatabase.GetAssetPath(renderer.sprite);
            Collider2D foot = type.ResolveMovementFoot(actor);
            var row = new UnitsRow { typeId = type.TypeId, prefab = AssetDatabase.GetAssetPath(prefab),
                prefabGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)), sprite = renderer.sprite.name,
                spriteSource = spriteSource, spriteGuid = AssetDatabase.AssetPathToGUID(spriteSource),
                animator = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController),
                animationClips = animator.runtimeAnimatorController.animationClips.Select(c => AssetDatabase.GetAssetPath(c)).Distinct().OrderBy(s => s).ToArray(),
                footShape = foot.GetType().Name, footSize = foot.bounds.size, footOffset = foot.offset, instance = actor.GetInstanceID(),
                shadowPresent = animator.transform.Find("Shadow") != null, healthBarPresent = actor.transform.Find("HealthBar/Fill") != null };
            // The existing Units Prefabs do not use XiaoSu's added Shadow/HealthBar hierarchy.
            // Record that baseline instead of adding duplicate visuals to satisfy a fixture assumption.
            Assert.That(foot.enabled && !foot.isTrigger, Is.True, prefab.name + " retained solid movement foot");
            MethodInfo takeDamage = damage.GetType().GetMethod("TakeDamage", new[] { typeof(float) });
            float max = Convert.ToSingle(damage.GetType().GetProperty("MaxHealth").GetValue(damage));
            for (int cycle = 0; cycle < 2; cycle++)
            {
                takeDamage.Invoke(damage, new object[] { max * .5f });
                Assert.That(Convert.ToSingle(damage.GetType().GetProperty("CurrentHealth").GetValue(damage)), Is.EqualTo(max * .5f));
                takeDamage.Invoke(damage, new object[] { float.MaxValue });
                Assert.That((bool)damage.GetType().GetProperty("IsDefeated").GetValue(damage), Is.True); row.deaths++;
                Assert.That(actor.GetComponentsInChildren<Collider2D>(true).All(c => !c.enabled), Is.True);
                float deadline = Time.realtimeSinceStartup + 4f;
                while (actor.activeSelf && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(actor.activeSelf, Is.False, prefab.name + " original death must return to the actual pool.");
                GameObject next = (GameObject)spawn.Invoke(null, new object[] { prefab, new Vector3(250, 200), Quaternion.identity });
                Assert.That(next.GetInstanceID(), Is.EqualTo(row.instance)); row.actualReuses++;
                DisableAI(next); yield return null;
                Assert.That(Convert.ToSingle(damage.GetType().GetProperty("CurrentHealth").GetValue(damage)), Is.EqualTo(max));
                Assert.That((bool)damage.GetType().GetProperty("IsDefeated").GetValue(damage), Is.False);
                Assert.That(type.ResolveMovementFoot(next).enabled, Is.True);
                Component pooled = actor.GetComponent("PooledObject");
                Assert.That(pooled.GetType().GetProperty("SourcePrefab").GetValue(pooled), Is.SameAs(prefab));
                Assert.That(renderer.sprite.name, Does.StartWith(prefab.name + "_"), "Reused animation must retain the exact colour.");
            }
            report.types.Add(row);
            Debug.Log($"UNITS_CANDIDATE_NATIVE type={type.TypeId} prefab={prefab.name} sprite={row.sprite} deaths={row.deaths} actualReuses={row.actualReuses} instance={row.instance}");
        }
        string path = Path.Combine(output, "UnitsCandidateActualTypesAndPool.json");
        Assert.That(File.Exists(path), Is.False); File.WriteAllText(path, JsonUtility.ToJson(report, true));
    }

    [Serializable] private sealed class Figure
    {
        public string name, prefab, spriteGuid;
        public long spriteFileId;
        public Vector3 rootScale, visualScale, rootPosition;
        public Vector2 footSize, footOffset;
        public bool shadowPresent, healthBarPresent;
    }
    [Serializable] private sealed class Comparison
    {
        public string unity, kind = "isolated native test-camera render; not natural gameplay or human approval";
        public float cameraSize;
        public Vector3 cameraPosition;
        public List<Figure> figures = new List<Figure>();
    }

    [SetUp] public void SetUp()
    {
        previousTimeScale = Time.timeScale;
        Time.timeScale = 1f;
        string label = Environment.GetEnvironmentVariable("RPG_ECOLOGY_EVIDENCE_LABEL");
        if (string.IsNullOrWhiteSpace(label)) label = "Local-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff");
        Assert.That(label.All(c => char.IsLetterOrDigit(c) || c == '-'), Is.True);
        output = Path.Combine("Artifacts/MapAcceptance/EcologyP1-20261021/Screenshots", label);
        Directory.CreateDirectory(output);
    }

    [UnityTearDown] public IEnumerator TearDown()
    {
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) UnityEngine.Object.Destroy(owned[i]);
        owned.Clear();
        yield return null;
        Time.timeScale = previousTimeScale;
    }

    private GameObject Own(GameObject value) { owned.Add(value); return value; }
    private GameObject Clone(string path, Vector3 position)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null, path);
        GameObject actor = Own(UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity));
        DisableAI(actor);
        return actor;
    }
    private static void DisableAI(GameObject actor)
    {
        Behaviour ai=actor.GetComponent("MonsterAIController") as Behaviour;
        if(ai!=null)ai.enabled=false;
    }
    private static void Capture(Camera camera, string path)
    {
        Assert.That(File.Exists(path), Is.False, "Do not replace an earlier capture.");
        RenderTexture target = RenderTexture.GetTemporary(1920, 720, 24);
        RenderTexture previous = RenderTexture.active;
        Texture2D pixels = new Texture2D(1920, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1920, 720), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.Destroy(pixels);
        }
    }
}
#endif

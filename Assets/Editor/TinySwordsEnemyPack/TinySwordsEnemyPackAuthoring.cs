using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

// Explicit batch entry points. Never runs on asset import or touches gameplay registries.
public static class TinySwordsEnemyPackAuthoring
{
    [Serializable] public sealed class Plan
    {
        public string unityVersion, sourceFingerprint, artRoot, visualChildName;
        public Art[] assets;
        public Entity[] prefabs;
    }
    [Serializable] public sealed class Art
    {
        public string source, path, sha256, role;
        public int width, height, frameWidth, frameHeight, columns, rows, frameCount, ppu;
        public float pivotX, pivotY;
    }
    [Serializable] public sealed class Entity
    {
        public string id, type, variant, category, direction, prefabPath;
        public State[] states;
        public string[] missingSourceAnimations;
    }
    [Serializable] public sealed class State
    {
        public string name, artPath, timingEvidence;
        public int[] frameIndices, durationsMs;
        public bool loop;
    }
    [Serializable] public sealed class SpriteIdentity
    {
        public string name, spriteId;
        public long fileId;
        public Rect rect;
        public Vector2 pivot;
    }
    [Serializable] public sealed class ArtIdentity
    {
        public string path, guid, metaSha256, sourceSha256;
        public int width, height, ppu;
        public SpriteIdentity[] sprites;
    }
    [Serializable] public sealed class ReferenceIdentity
    {
        public string guid;
        public long fileId;
        public float time;
    }
    [Serializable] public sealed class ClipIdentity
    {
        public string path, guid, name;
        public bool loop;
        public float sourceDuration, clipDuration, startTime, stopTime;
        public ReferenceIdentity[] keys;
    }
    [Serializable] public sealed class PrefabIdentity
    {
        public string path, guid, controllerGuid, status;
        public ClipIdentity[] clips;
        public string[] dependencies;
    }
    [Serializable] public sealed class Evidence
    {
        public int schemaVersion = 2;
        public string status, unity, sourceFingerprint, error;
        public ArtIdentity[] assets;
        public PrefabIdentity[] prefabs;
        public string[] warnings;
    }

    const string AnimRoot = "Assets/Animations/Monsters/Tiny Swords Enemy Pack";
    public const string PreviewScene = "Assets/Tests/PrefabPreview/TinySwordsEnemyPackPreview.unity";
    static readonly List<string> Warnings = new List<string>();
    static string Argument(string key, bool required = true)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == key) return args[i + 1];
        if (required) throw new InvalidOperationException("Missing argument " + key);
        return null;
    }
    static string Sha(string path)
    {
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
    }
    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    static Plan ReadPlan()
    {
        var plan = JsonUtility.FromJson<Plan>(File.ReadAllText(Argument("-enemyPlan")));
        Require(plan.unityVersion == Application.unityVersion, "Editor version differs from frozen plan");
        Require(plan.artRoot == "Assets/_Resources/Tiny Swords Enemy Pack" && plan.visualChildName == "Visual", "Unexpected library authority");
        foreach (var art in plan.assets)
        {
            Require(art.path.StartsWith(plan.artRoot + "/", StringComparison.Ordinal), "Art path outside library");
            Require(File.Exists(art.path) && Sha(art.path) == art.sha256, "Source image SHA differs: " + art.path);
        }
        return plan;
    }
    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        Require(!File.Exists(path + ".meta"), "Orphan folder metadata; restore before opening Unity: " + path);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    static Sprite[] Sprites(Art art)
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath(art.path).OfType<Sprite>().OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
        Require(sprites.Length == art.frameCount, "Slice count differs: " + art.path);
        return sprites;
    }
    static void Configure(Art art)
    {
        var importer = AssetImporter.GetAtPath(art.path) as TextureImporter;
        Require(importer != null, "TextureImporter unavailable: " + art.path);
        if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Multiple)
        {
            // Existing frozen slices are verified, never regenerated.
            Require(Sprites(art).Length == art.frameCount, "Existing slices conflict");
            ValidateImporter(importer, art);
            return;
        }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = art.ppu;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 8192;
        importer.alphaIsTransparency = true;
        importer.isReadable = false;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        Require(provider != null, "Sprite Data Provider unavailable");
        provider.InitSpriteEditorDataProvider();
        // Sprite package 1.0.0 (Unity 2022) exposes this capability seam. The Unity 6
        // ISpriteFrameEditCapability interface is absent in this installed package.
        Require(provider.targetObject == importer && provider.HasDataProvider(typeof(ISpriteNameFileIdDataProvider)),
            "Importer cannot preserve Sprite name/ID pairs; refusing slicing");
        var ids = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        Require(ids != null, "Sprite name/ID provider missing");
        var rects = new SpriteRect[art.frameCount];
        for (int i = 0; i < rects.Length; i++)
        {
            int x = i % art.columns, y = i / art.columns;
            rects[i] = new SpriteRect
            {
                name = Path.GetFileNameWithoutExtension(art.path) + "_" + i.ToString("D3"),
                rect = new Rect(x * art.frameWidth, art.height - (y + 1) * art.frameHeight, art.frameWidth, art.frameHeight),
                alignment = SpriteAlignment.Custom, pivot = new Vector2(art.pivotX, art.pivotY), spriteID = GUID.Generate()
            };
        }
        provider.SetSpriteRects(rects);
        ids.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
        ValidateImporter(importer, art);
        Sprites(art);
    }
    static void ValidateImporter(TextureImporter importer, Art art)
    {
        Require(importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Multiple &&
            importer.filterMode == FilterMode.Point && !importer.mipmapEnabled &&
            importer.textureCompression == TextureImporterCompression.Uncompressed &&
            Mathf.Approximately(importer.spritePixelsPerUnit, art.ppu), "Import settings differ: " + art.path);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(art.path);
        Require(texture && texture.width == art.width && texture.height == art.height, "Texture was downscaled: " + art.path);
        var sprites = Sprites(art);
        for (int i = 0; i < sprites.Length; i++)
        {
            var s = sprites[i];
            Require(Mathf.Approximately(s.rect.width, art.frameWidth) && Mathf.Approximately(s.rect.height, art.frameHeight), "Sprite rect size differs");
            Require(Vector2.Distance(s.pivot, new Vector2(art.frameWidth * art.pivotX, art.frameHeight * art.pivotY)) < 0.01f, "Sprite pivot differs");
        }
    }
    static string ClipPath(Entity entity, State state) => AnimRoot + "/" + entity.id + "/" + entity.id + "_" + state.name + ".anim";
    static string ControllerPath(Entity entity) => AnimRoot + "/" + entity.id + "/" + entity.id + ".controller";
    static void GenerateEntity(Plan plan, Entity entity)
    {
        Require(entity.prefabPath == "Assets/Prefabs/Monsters/Tiny Swords Enemy Pack/" + entity.id + ".prefab", "Unexpected prefab path");
        EnsureFolder(AnimRoot + "/" + entity.id);
        EnsureFolder(Path.GetDirectoryName(entity.prefabPath).Replace('\\', '/'));
        foreach (var state in entity.states)
        {
            string path = ClipPath(entity, state);
            bool exists = File.Exists(path);
            var art = plan.assets.Single(a => a.path == state.artPath);
            var sprites = Sprites(art);
            var clip = exists ? AssetDatabase.LoadAssetAtPath<AnimationClip>(path) :
                new AnimationClip { name = Path.GetFileNameWithoutExtension(path) };
            Require(clip, "Clip is unreadable");
            // Aseprite durations are integer milliseconds. Unity appends one sample
            // to an object-reference curve; reserve exactly that final millisecond.
            clip.frameRate = 1000;
            var keys = new ObjectReferenceKeyframe[state.frameIndices.Length + 1];
            int timeMs = 0;
            for (int i = 0; i < state.frameIndices.Length; i++)
            {
                Require(state.durationsMs[i] > 0, "Invalid source duration");
                keys[i] = new ObjectReferenceKeyframe { time = timeMs / 1000f, value = sprites[state.frameIndices[i]] };
                timeMs += state.durationsMs[i];
            }
            Require(state.durationsMs.Last() > 1, "Final source frame is shorter than the reserved sample");
            keys[keys.Length - 1] = new ObjectReferenceKeyframe { time = (timeMs - 1) / 1000f,
                value = keys[keys.Length - 2].value };
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            SetClipTiming(clip, state);
            if (exists) AssetDatabase.SaveAssetIfDirty(clip);
            else AssetDatabase.CreateAsset(clip, path);
        }
        string controllerPath = ControllerPath(entity);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (!controller)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (var state in entity.states)
            {
                var node = machine.AddState(state.name);
                node.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(entity, state));
                node.writeDefaultValues = false;
                if (state.name == "Idle" || machine.defaultState == null) machine.defaultState = node;
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
        }
        if (File.Exists(entity.prefabPath)) return;
        var root = new GameObject(entity.id);
        try
        {
            var visual = new GameObject(plan.visualChildName);
            visual.transform.SetParent(root.transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            var first = entity.states[0];
            renderer.sprite = Sprites(plan.assets.Single(a => a.path == first.artPath))[first.frameIndices[0]];
            renderer.sortingOrder = 5;
            var animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            PrefabUtility.SaveAsPrefabAsset(root, entity.prefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    static void SetClipTiming(AnimationClip clip, State state)
    {
        // Source durations also own the Inspector's playback range. The final
        // reference key already accounts for Unity's implicit sample-length tail.
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.startTime = 0;
        settings.stopTime = state.durationsMs.Sum() / 1000f;
        settings.loopTime = state.loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }
    static void CheckReferences(UnityEngine.Object asset)
    {
        var serialized = new SerializedObject(asset);
        var property = serialized.GetIterator();
        while (property.Next(true))
            if (property.propertyType == SerializedPropertyType.ObjectReference)
                Require(property.objectReferenceValue != null || property.objectReferenceInstanceIDValue == 0,
                    "Missing object reference: " + asset.name + "/" + property.propertyPath);
    }
    static Evidence Inspect(Plan plan, Art[] arts, Entity[] entities)
    {
        var evidence = new Evidence { unity = Application.unityVersion, sourceFingerprint = plan.sourceFingerprint };
        evidence.assets = arts.Select(art =>
        {
            var importer = AssetImporter.GetAtPath(art.path) as TextureImporter;
            Require(importer, "Missing importer");
            ValidateImporter(importer, art);
            var factory = new SpriteDataProviderFactories(); factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var ids = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID.ToString());
            return new ArtIdentity { path = art.path, guid = AssetDatabase.AssetPathToGUID(art.path),
                metaSha256 = Sha(art.path + ".meta"), sourceSha256 = Sha(art.path), width = art.width, height = art.height, ppu = art.ppu,
                sprites = Sprites(art).Select(s =>
                {
                    Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(s, out string guid, out long id), "Missing Sprite fileID");
                    return new SpriteIdentity { name = s.name, fileId = id, spriteId = ids[s.name], rect = s.rect, pivot = s.pivot };
                }).ToArray() };
        }).ToArray();
        evidence.prefabs = entities.Select(entity =>
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entity.prefabPath);
            Require(prefab && prefab.name == entity.id, "Missing/misnamed prefab: " + entity.id);
            var visual = prefab.transform.Find(plan.visualChildName);
            Require(visual && visual.GetComponent<SpriteRenderer>() && visual.GetComponent<Animator>(), "Missing visual components");
            foreach (var component in prefab.GetComponentsInChildren<Component>(true))
            {
                Require(component, "Missing script: " + entity.id);
                Require(component is Transform || component is SpriteRenderer || component is Animator, "Gameplay component must not be wired");
                CheckReferences(component);
            }
            var animator = visual.GetComponent<Animator>();
            var controller = animator.runtimeAnimatorController as AnimatorController;
            Require(controller && controller.name == entity.id, "Controller missing/misnamed");
            var states = controller.layers[0].stateMachine.states.Select(x => x.state).ToArray();
            Require(states.Length == entity.states.Length, "Animator state coverage differs");
            var clips = entity.states.Select(state =>
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(entity, state));
                Require(clip && clip.name == Path.GetFileNameWithoutExtension(ClipPath(entity, state)), "Clip missing/misnamed");
                Require(states.Single(s => s.name == state.name).motion == clip, "State motion reference differs");
                var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                Require(bindings.Length == 1 && bindings[0].path == "" && bindings[0].type == typeof(SpriteRenderer), "Unexpected clip binding");
                var curve = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
                Require(curve.Length == state.frameIndices.Length + 1, "Clip frame count differs");
                var expected = Sprites(plan.assets.Single(a => a.path == state.artPath));
                float time = 0;
                for (int i = 0; i < state.frameIndices.Length; i++)
                {
                    Require(curve[i].value == expected[state.frameIndices[i]] && Mathf.Abs(curve[i].time - time) < .001f, "Frame order/timing differs");
                    time += state.durationsMs[i] / 1000f;
                }
                Require(Mathf.Abs(curve.Last().time - (time-.001f)) < .0001f &&
                    curve.Last().value == curve[curve.Length-2].value && clip.frameRate == 1000, "Terminal frame hold differs");
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                bool loop = settings.loopTime;
                Require(loop == state.loop, "Loop policy differs");
                float sourceDuration = state.durationsMs.Sum() / 1000f;
                Require(Mathf.Abs(settings.startTime) < .0001f && Mathf.Abs(settings.stopTime-sourceDuration) < .001f &&
                    Mathf.Abs(clip.length-sourceDuration) < .0001f, "Effective clip duration differs: " + entity.id + "/" + state.name +
                    " source=" + sourceDuration + " actual=" + clip.length + " stop=" + settings.stopTime);
                CheckReferences(clip);
                return new ClipIdentity { path = ClipPath(entity, state), guid = AssetDatabase.AssetPathToGUID(ClipPath(entity, state)),
                    name = state.name, loop = loop, sourceDuration = sourceDuration, clipDuration = clip.length,
                    startTime = settings.startTime, stopTime = settings.stopTime, keys = curve.Select(k =>
                    {
                        Require(k.value && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(k.value, out string guid, out long id), "Clip Missing Sprite");
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(k.value, out string g, out long f);
                        return new ReferenceIdentity { guid = g, fileId = f, time = k.time };
                    }).ToArray() };
            }).ToArray();
            return new PrefabIdentity { path = entity.prefabPath, guid = AssetDatabase.AssetPathToGUID(entity.prefabPath),
                controllerGuid = AssetDatabase.AssetPathToGUID(ControllerPath(entity)), status = "VISUAL_READY_GAMEPLAY_NOT_WIRED", clips = clips,
                dependencies = AssetDatabase.GetDependencies(entity.prefabPath, true).OrderBy(x => x, StringComparer.Ordinal).ToArray() };
        }).ToArray();
        evidence.status = "PASS";
        return evidence;
    }
    static void CreatePreview(Plan plan)
    {
        EnsureFolder("Assets/Tests/PrefabPreview");
        if (File.Exists(PreviewScene))
        {
            EditorSceneManager.OpenScene(PreviewScene, OpenSceneMode.Single);
            var existing = UnityEngine.Object.FindObjectOfType<TinySwordsEnemyPackPreview>();
            Require(existing && existing.entries.Length == plan.prefabs.Length, "Existing Preview coverage differs");
            for (int i = 0; i < plan.prefabs.Length; i++)
                Require(existing.entries[i].id == plan.prefabs[i].id && existing.entries[i].prefab == AssetDatabase.LoadAssetAtPath<GameObject>(plan.prefabs[i].prefabPath)
                    && existing.entries[i].states.Length == plan.prefabs[i].states.Length, "Existing Preview references differ");
            return;
        }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 5;
        camera.transform.position = new Vector3(0, 1, -10);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.075f, .09f, .12f);
        var preview = new GameObject("EnemyPackPreview").AddComponent<TinySwordsEnemyPackPreview>();
        preview.entries = plan.prefabs.Select(entity => new TinySwordsEnemyPackPreview.Entry
        {
            id = entity.id, category = entity.category, variant = entity.variant, direction = entity.direction,
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entity.prefabPath),
            states = entity.states.Select(state => new TinySwordsEnemyPackPreview.ClipEntry
            {
                name = state.name, clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(entity, state)),
                durationsMs = state.durationsMs, loop = state.loop,
                frames = state.frameIndices.Select(i => Sprites(plan.assets.Single(a => a.path == state.artPath))[i]).ToArray()
            }).ToArray()
        }).ToArray();
        EditorSceneManager.MarkSceneDirty(scene);
        Require(EditorSceneManager.SaveScene(scene, PreviewScene), "Preview save failed");
    }
    static void Execute(bool generate, bool pilot)
    {
        string output = Argument("-enemyReport");
        Require(!File.Exists(output), "Evidence output already exists");
        Application.logMessageReceived += Capture;
        Evidence report = null;
        try
        {
            var plan = ReadPlan();
            var entities = pilot ? plan.prefabs.Where(p => p.id == "Bear").ToArray() : plan.prefabs;
            var paths = new HashSet<string>(entities.SelectMany(e => e.states).Select(s => s.artPath));
            var arts = pilot ? plan.assets.Where(a => paths.Contains(a.path)).ToArray() : plan.assets;
            if (generate)
            {
                foreach (var art in arts) Configure(art);
                foreach (var entity in entities) GenerateEntity(plan, entity);
                if (!pilot) CreatePreview(plan);
            }
            else foreach (var art in arts) AssetDatabase.ImportAsset(art.path, ImportAssetOptions.ForceUpdate);
            report = Inspect(plan, arts, entities);
            var goldenPath = Argument("-enemyGolden", false);
            if (!string.IsNullOrEmpty(goldenPath))
            {
                var golden = JsonUtility.FromJson<Evidence>(File.ReadAllText(goldenPath));
                Require(golden.schemaVersion == 2, "Golden schema differs; use the final source-timing recovery kit");
                // Compare typed elements explicitly; JsonUtility does not serialize top-level arrays.
                Require(golden.assets.Length == report.assets.Length && golden.prefabs.Length == report.prefabs.Length, "Golden coverage differs");
                for (int i = 0; i < golden.assets.Length; i++) Require(JsonUtility.ToJson(golden.assets[i]) == JsonUtility.ToJson(report.assets[i]), "Golden Sprite differs: " + report.assets[i].path);
                for (int i = 0; i < golden.prefabs.Length; i++) ComparePrefabIdentity(golden.prefabs[i], report.prefabs[i]);
            }
            Require(Warnings.Count == 0, "New scoped warnings/errors: " + string.Join("; ", Warnings));
        }
        catch (Exception error)
        {
            if (report == null) report = new Evidence { unity = Application.unityVersion };
            report.status = "FAIL";
            report.error = error.ToString();
            Debug.LogError(error);
        }
        report.warnings = Warnings.ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        File.WriteAllText(output, JsonUtility.ToJson(report, true));
        Application.logMessageReceived -= Capture;
        EditorApplication.Exit(report.status == "PASS" ? 0 : 2);
    }
    static void ComparePrefabIdentity(PrefabIdentity expected, PrefabIdentity actual)
    {
        Require(expected.path == actual.path && expected.guid == actual.guid && expected.controllerGuid == actual.controllerGuid &&
            expected.status == actual.status && expected.dependencies.SequenceEqual(actual.dependencies) && expected.clips.Length == actual.clips.Length,
            "Golden Prefab identity differs: " + actual.path);
        for (int c = 0; c < expected.clips.Length; c++)
        {
            var a = expected.clips[c]; var b = actual.clips[c];
            Require(a.path == b.path && a.guid == b.guid && a.name == b.name && a.loop == b.loop && a.keys.Length == b.keys.Length,
                "Golden clip identity differs: " + b.path);
            // Unity recalculates native clip length after reload. Permit float roundoff
            // below 0.1 ms; GUIDs, fileIDs, key order and asset metadata remain exact.
            Require(Mathf.Abs(a.sourceDuration-b.sourceDuration) < .0001f && Mathf.Abs(a.clipDuration-b.clipDuration) < .0001f &&
                Mathf.Abs(a.startTime-b.startTime) < .0001f && Mathf.Abs(a.stopTime-b.stopTime) < .0001f, "Golden clip duration differs: " + b.path);
            for (int k = 0; k < a.keys.Length; k++)
                Require(a.keys[k].guid == b.keys[k].guid && a.keys[k].fileId == b.keys[k].fileId && Mathf.Abs(a.keys[k].time-b.keys[k].time) < .000001f,
                    "Golden Sprite reference/time differs: " + b.path + "/" + k);
        }
    }
    static void Capture(string text, string trace, LogType type)
    {
        if (type == LogType.Warning || type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            Warnings.Add(type + ": " + text);
    }
    public static void Pilot() => Execute(true, true);
    public static void Generate() => Execute(true, false);
    public static void Verify() => Execute(false, false);
}

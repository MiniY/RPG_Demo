using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class TowerRunIndependentProcessPlayModeTests
{
    private const string RootEnvironmentVariable = "RPG_DEMO_TOWER_RUN_PERSISTENCE_ROOT";
    private const string PhaseEnvironmentVariable = "RPG_DEMO_TOWER_RUN_PERSISTENCE_PHASE";
    private const string WriterPhase = "write";
    private const string ReaderPhase = "read";
    private const string PreferenceFileName = "isolated-preferences.json";
    private const string ManifestFileName = "writer-manifest.json";

    private string testRoot;
    private string phase;

    [SetUp]
    public void SetUp()
    {
        testRoot = Environment.GetEnvironmentVariable(RootEnvironmentVariable);
        phase = Environment.GetEnvironmentVariable(PhaseEnvironmentVariable);

        Assert.That(testRoot, Is.Not.Null.And.Not.Empty,
            $"{RootEnvironmentVariable} must name the isolated cross-process data root.");
        Assert.That(Path.IsPathRooted(testRoot), Is.True,
            "The cross-process data root must be an absolute path.");
        Assert.That(phase, Is.EqualTo(WriterPhase).Or.EqualTo(ReaderPhase),
            $"{PhaseEnvironmentVariable} must be '{WriterPhase}' or '{ReaderPhase}'.");

        TowerRunRuntimeEnvironment.ConfigureTestIsolation(
            testRoot,
            new JsonFilePreferenceStore(Path.Combine(testRoot, PreferenceFileName)));
    }

    [TearDown]
    public void TearDown()
    {
        TowerRunRuntimeEnvironment.ResetOverrides();
    }

    [UnityTest]
    public IEnumerator WriterThenReaderUseTheSameDiskCheckpointAndAudioPreferences()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        yield return null;

        if (phase == WriterPhase)
            yield return RunWriterPhase();
        else
            yield return RunReaderPhase();
    }

    private IEnumerator RunWriterPhase()
    {
        MonoBehaviour coordinator = FindBehaviour("TowerRunCoordinator");
        MonoBehaviour runUi = FindBehaviour("TowerRunUiController");
        Assert.That(coordinator, Is.Not.Null);
        Assert.That(runUi, Is.Not.Null);

        Invoke(coordinator, "ReturnToStartupChoice");
        Invoke(coordinator, "BeginNewRun");
        yield return null;
        AssertPlaying(coordinator, "新手关卡");

        Slider volumeSlider = GetPrivateField<Slider>(runUi, "volumeSlider");
        Toggle muteToggle = GetPrivateField<Toggle>(runUi, "muteToggle");
        volumeSlider.value = 0.37f;
        muteToggle.isOn = true;
        Assert.That(AudioListener.volume, Is.Zero);

        Invoke(runUi, "OpenSettings");
        InvokePrivate(runUi, "ShowSavePage");
        InvokePrivate(runUi, "BeginSaveRequest");
        InvokePrivate(runUi, "SelectSaveSlot", 1);

        TowerRunSlotReadResult saved =
            (TowerRunSlotReadResult)Invoke(coordinator, "ReadSlot", 1);
        Assert.That(saved.state, Is.EqualTo(TowerRunSlotState.Valid), saved.message);
        Assert.That(saved.record, Is.Not.Null);
        Assert.That(saved.record.generation.staticBaselineSignature, Is.Not.Empty,
            "The writer must persist the generated entrance baseline before the reader starts.");

        WriterManifest manifest = new WriterManifest
        {
            runId = saved.record.runId,
            floorDisplayName = saved.record.floor.displayName,
            staticBaselineSignature = saved.record.generation.staticBaselineSignature,
            volume = 0.37f,
            muted = true
        };
        Directory.CreateDirectory(testRoot);
        File.WriteAllText(
            Path.Combine(testRoot, ManifestFileName),
            JsonUtility.ToJson(manifest, true));

        Assert.That(File.Exists(Path.Combine(testRoot, PreferenceFileName)), Is.True,
            "The writer must flush isolated audio preferences to disk.");
        Debug.Log(
            $"CROSS_PROCESS_WRITER_EVIDENCE|RunId={manifest.runId}" +
            $"|Floor={manifest.floorDisplayName}" +
            $"|Baseline={manifest.staticBaselineSignature}|Volume={manifest.volume}|Muted=True");
    }

    private IEnumerator RunReaderPhase()
    {
        string manifestPath = Path.Combine(testRoot, ManifestFileName);
        Assert.That(File.Exists(manifestPath), Is.True,
            "The reader must consume the manifest produced by the earlier Unity process.");
        WriterManifest expected =
            JsonUtility.FromJson<WriterManifest>(File.ReadAllText(manifestPath));
        Assert.That(expected, Is.Not.Null);
        Assert.That(expected.staticBaselineSignature, Is.Not.Empty);

        MonoBehaviour coordinator = FindBehaviour("TowerRunCoordinator");
        MonoBehaviour runUi = FindBehaviour("TowerRunUiController");
        Assert.That(coordinator, Is.Not.Null);
        Assert.That(runUi, Is.Not.Null);
        Assert.That(GetProperty(coordinator, "State").ToString(), Is.EqualTo("StartupChoice"));

        Slider volumeSlider = GetPrivateField<Slider>(runUi, "volumeSlider");
        Toggle muteToggle = GetPrivateField<Toggle>(runUi, "muteToggle");
        Assert.That(volumeSlider.value, Is.EqualTo(expected.volume).Within(0.001f));
        Assert.That(muteToggle.isOn, Is.EqualTo(expected.muted));
        Assert.That(AudioListener.volume, Is.Zero,
            "The startup UI must apply the muted preference loaded from the earlier process.");

        TowerRunSlotReadResult saved =
            (TowerRunSlotReadResult)Invoke(coordinator, "ReadSlot", 1);
        Assert.That(saved.state, Is.EqualTo(TowerRunSlotState.Valid), saved.message);
        Assert.That(saved.record.runId, Is.EqualTo(expected.runId));
        Assert.That(saved.record.floor.displayName, Is.EqualTo(expected.floorDisplayName));
        Assert.That(saved.record.generation.staticBaselineSignature,
            Is.EqualTo(expected.staticBaselineSignature),
            "The reader must retain the baseline persisted by the writer process.");

        Invoke(coordinator, "BeginRestoreSlot", 1, saved.confirmationToken);

        for (int frame = 0; frame < 60 &&
             GetProperty(coordinator, "State").ToString() != "Playing"; frame++)
        {
            yield return null;
        }

        AssertPlaying(coordinator, expected.floorDisplayName);
        MapRuntimeBootstrap bootstrap = FindObject<MapRuntimeBootstrap>();
        MapGenerationController mapController = FindObject<MapGenerationController>();
        Assert.That(bootstrap, Is.Not.Null);
        Assert.That(mapController, Is.Not.Null);
        string actualBaseline = TowerRunMapBaseline.Compute(
            bootstrap.Context.ActiveMap,
            bootstrap.Context.ActivePlacementPlan,
            mapController.LastGeneratedSimpleDecorations);
        Assert.That(actualBaseline, Is.EqualTo(expected.staticBaselineSignature),
            "Restore must compare against the writer's saved baseline, not replace it first.");

        TowerRunSlotReadResult unchanged =
            (TowerRunSlotReadResult)Invoke(coordinator, "ReadSlot", 1);
        Assert.That(unchanged.record.generation.staticBaselineSignature,
            Is.EqualTo(expected.staticBaselineSignature));
        Debug.Log(
            $"CROSS_PROCESS_READER_EVIDENCE|RunId={expected.runId}" +
            $"|Floor={expected.floorDisplayName}|SavedBaseline={expected.staticBaselineSignature}" +
            $"|ActualBaseline={actualBaseline}|Volume={volumeSlider.value}|Muted={muteToggle.isOn}");
    }

    private static MonoBehaviour FindBehaviour(string typeName)
    {
        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null && behaviours[i].GetType().Name == typeName)
                return behaviours[i];
        }

        return null;
    }

    private static T FindObject<T>() where T : UnityEngine.Object
    {
        T[] objects = UnityEngine.Object.FindObjectsOfType<T>(true);
        return objects.Length == 0 ? null : objects[0];
    }

    private static void AssertPlaying(MonoBehaviour coordinator, string expectedFloor)
    {
        Assert.That(GetProperty(coordinator, "State").ToString(), Is.EqualTo("Playing"));
        Assert.That((string)GetProperty(coordinator, "FloorDisplayName"),
            Is.EqualTo(expectedFloor));
    }

    private static object GetProperty(object target, string name)
    {
        PropertyInfo property = target.GetType().GetProperty(
            name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(property, Is.Not.Null, $"Missing property {target.GetType().Name}.{name}");
        return property.GetValue(target);
    }

    private static T GetPrivateField<T>(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field {target.GetType().Name}.{name}");
        return (T)field.GetValue(target);
    }

    private static object Invoke(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null,
            $"Missing method {target.GetType().Name}.{methodName}");
        return method.Invoke(target, arguments);
    }

    private static object InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null,
            $"Missing private method {target.GetType().Name}.{methodName}");
        return method.Invoke(target, arguments);
    }

    [Serializable]
    private sealed class WriterManifest
    {
        public string runId;
        public string floorDisplayName;
        public string staticBaselineSignature;
        public float volume;
        public bool muted;
    }

    private sealed class JsonFilePreferenceStore : ITowerRunPreferenceStore
    {
        private readonly string path;
        private readonly Dictionary<string, int> integers =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> floats =
            new Dictionary<string, float>(StringComparer.Ordinal);

        public JsonFilePreferenceStore(string path)
        {
            this.path = path;

            if (!File.Exists(path))
                return;

            PreferenceSnapshot snapshot =
                JsonUtility.FromJson<PreferenceSnapshot>(File.ReadAllText(path));

            if (snapshot == null)
                return;

            for (int i = 0; snapshot.integers != null && i < snapshot.integers.Count; i++)
                integers[snapshot.integers[i].key] = snapshot.integers[i].value;

            for (int i = 0; snapshot.floats != null && i < snapshot.floats.Count; i++)
                floats[snapshot.floats[i].key] = snapshot.floats[i].value;
        }

        public int GetInt(string key, int defaultValue) =>
            integers.TryGetValue(key, out int value) ? value : defaultValue;

        public float GetFloat(string key, float defaultValue) =>
            floats.TryGetValue(key, out float value) ? value : defaultValue;

        public void SetInt(string key, int value) => integers[key] = value;
        public void SetFloat(string key, float value) => floats[key] = value;

        public void Save()
        {
            PreferenceSnapshot snapshot = new PreferenceSnapshot();

            foreach (KeyValuePair<string, int> pair in integers)
                snapshot.integers.Add(new IntEntry { key = pair.Key, value = pair.Value });

            foreach (KeyValuePair<string, float> pair in floats)
                snapshot.floats.Add(new FloatEntry { key = pair.Key, value = pair.Value });

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string pendingPath = path + ".pending";
            File.WriteAllText(pendingPath, JsonUtility.ToJson(snapshot, true));
            File.Copy(pendingPath, path, true);
            File.Delete(pendingPath);
        }
    }

    [Serializable]
    private sealed class PreferenceSnapshot
    {
        public List<IntEntry> integers = new List<IntEntry>();
        public List<FloatEntry> floats = new List<FloatEntry>();
    }

    [Serializable]
    private sealed class IntEntry
    {
        public string key;
        public int value;
    }

    [Serializable]
    private sealed class FloatEntry
    {
        public string key;
        public float value;
    }
}

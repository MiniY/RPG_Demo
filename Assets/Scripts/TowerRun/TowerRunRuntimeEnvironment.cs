using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public interface ITowerRunPreferenceStore
{
    int GetInt(string key, int defaultValue);
    float GetFloat(string key, float defaultValue);
    void SetInt(string key, int value);
    void SetFloat(string key, float value);
    void Save();
}

public sealed class TowerRunPlayerPrefsStore : ITowerRunPreferenceStore
{
    public int GetInt(string key, int defaultValue) => PlayerPrefs.GetInt(key, defaultValue);
    public float GetFloat(string key, float defaultValue) => PlayerPrefs.GetFloat(key, defaultValue);
    public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
    public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);
    public void Save() => PlayerPrefs.Save();
}

public sealed class TowerRunMemoryPreferenceStore : ITowerRunPreferenceStore
{
    private readonly Dictionary<string, int> integers =
        new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly Dictionary<string, float> floats =
        new Dictionary<string, float>(StringComparer.Ordinal);

    public int GetInt(string key, int defaultValue) =>
        integers.TryGetValue(key, out int value) ? value : defaultValue;

    public float GetFloat(string key, float defaultValue) =>
        floats.TryGetValue(key, out float value) ? value : defaultValue;

    public void SetInt(string key, int value) => integers[key] = value;
    public void SetFloat(string key, float value) => floats[key] = value;
    public void Save() { }
}

public static class TowerRunRuntimeEnvironment
{
    private static readonly ITowerRunPreferenceStore playerPreferences =
        new TowerRunPlayerPrefsStore();
    private static string slotRootOverride;
    private static string legacySavePathOverride;
    private static ITowerRunPreferenceStore preferenceOverride;

    public static string SlotRoot => slotRootOverride ??
        Path.Combine(Application.persistentDataPath, "tower-run-slots");

    public static string LegacySavePath => legacySavePathOverride ??
        Path.Combine(Application.persistentDataPath, "save.json");

    public static ITowerRunPreferenceStore Preferences =>
        preferenceOverride ?? playerPreferences;

    public static void ConfigureTestIsolation(string rootDirectory)
    {
        ConfigureTestIsolation(rootDirectory, new TowerRunMemoryPreferenceStore());
    }

    public static void ConfigureTestIsolation(
        string rootDirectory,
        ITowerRunPreferenceStore isolatedPreferences)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
            throw new ArgumentException("A test isolation root is required.", nameof(rootDirectory));

        if (isolatedPreferences == null)
        {
            throw new ArgumentNullException(
                nameof(isolatedPreferences),
                "A test preference store is required.");
        }

        slotRootOverride = Path.Combine(rootDirectory, "tower-run-slots");
        legacySavePathOverride = Path.Combine(rootDirectory, "save.json");
        preferenceOverride = isolatedPreferences;
    }

    public static void ResetOverrides()
    {
        slotRootOverride = null;
        legacySavePathOverride = null;
        preferenceOverride = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnSubsystemRegistration()
    {
        ResetOverrides();
    }
}

using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

public class DataTestMapProbeTests
{
    [Serializable]
    private sealed class ProbeResult
    {
        public string mode;
        public int baseSeed;
        public int loops;
        public int width;
        public int height;
        public double meanMilliseconds;
        public double p95Milliseconds;
        public double p99Milliseconds;
        public double maxMilliseconds;
        public double[] generationMilliseconds;
        public long[] settledManagedBytes;
        public long managedBeforeBytes;
        public long managedAfterBytes;
        public long unityAllocatedBeforeBytes;
        public long unityAllocatedAfterBytes;
        public long unityReservedBeforeBytes;
        public long unityReservedAfterBytes;
        public long processWorkingSetBeforeBytes;
        public long processWorkingSetAfterBytes;
        public int gc0Before;
        public int gc0After;
        public int editorGameObjectsBefore;
        public int editorGameObjectsAfter;
        public string unityVersion;
        public string environment = "Unity Editor; instrumented map-data generation";
    }

    [Test]
    public void GenerateMapAndCaptureEditorMetrics()
    {
        string mode = Environment.GetEnvironmentVariable("DATATEST_PROBE_MODE");
        if (mode != "Performance" && mode != "Memory" && mode != "Stress" && mode != "Soak")
            Assert.Ignore("DataTest probe only runs in an explicit metrics profile.");

        string path = Environment.GetEnvironmentVariable("DATATEST_METRICS_PATH");
        Assert.That(path, Is.Not.Null.And.Not.Empty, "Runner must set an isolated metrics path.");
        int baseSeed = int.Parse(Environment.GetEnvironmentVariable("DATATEST_PROBE_SEED"));
        int loops = int.Parse(Environment.GetEnvironmentVariable("DATATEST_PROBE_LOOPS"));
        Assert.That(loops, Is.InRange(1, 10000));

        MapGenerationSettings settings = ScriptableObject.CreateInstance<MapGenerationSettings>();
        try
        {
            settings.mapWidth = 64;
            settings.mapHeight = 64;
            settings.mapOrigin = Vector2Int.zero;
            settings.borderSize = 1;
            settings.noiseScale = 0.08f;
            settings.waterThreshold = 0.32f;
            settings.shallowWaterThreshold = 0.42f;
            settings.sandHeightThreshold = 0.48f;
            settings.mountainHeightThreshold = 0.72f;
            settings.minimumNaturalRegionSize = 3;
            settings.spawnProtectionRadius = 5;
            settings.roadWidth = 2;
            settings.roadTurnChance = 0.3f;

            for (int i = 0; i < 3; i++)
            {
                settings.seed = unchecked(baseSeed + i);
                AssertValid(RandomMapGenerator.Generate(settings));
            }

            bool settleEachCycle = mode == "Memory" || mode == "Soak";
            ProbeResult result = new ProbeResult
            {
                mode = mode,
                baseSeed = baseSeed,
                loops = loops,
                width = settings.mapWidth,
                height = settings.mapHeight,
                generationMilliseconds = new double[loops],
                settledManagedBytes = settleEachCycle ? new long[loops] : new long[0],
                managedBeforeBytes = GC.GetTotalMemory(true),
                unityAllocatedBeforeBytes = Profiler.GetTotalAllocatedMemoryLong(),
                unityReservedBeforeBytes = Profiler.GetTotalReservedMemoryLong(),
                processWorkingSetBeforeBytes = Process.GetCurrentProcess().WorkingSet64,
                gc0Before = GC.CollectionCount(0),
                editorGameObjectsBefore = Resources.FindObjectsOfTypeAll<GameObject>().Length,
                unityVersion = Application.unityVersion
            };

            for (int i = 0; i < loops; i++)
            {
                settings.seed = unchecked(baseSeed + i);
                long start = Stopwatch.GetTimestamp();
                MapData map = RandomMapGenerator.Generate(settings);
                long end = Stopwatch.GetTimestamp();
                AssertValid(map);
                result.generationMilliseconds[i] = (end - start) * 1000.0 / Stopwatch.Frequency;
                map = null;
                if (settleEachCycle)
                    result.settledManagedBytes[i] = GC.GetTotalMemory(true);
            }

            result.managedAfterBytes = GC.GetTotalMemory(true);
            result.unityAllocatedAfterBytes = Profiler.GetTotalAllocatedMemoryLong();
            result.unityReservedAfterBytes = Profiler.GetTotalReservedMemoryLong();
            result.processWorkingSetAfterBytes = Process.GetCurrentProcess().WorkingSet64;
            result.gc0After = GC.CollectionCount(0);
            result.editorGameObjectsAfter = Resources.FindObjectsOfTypeAll<GameObject>().Length;

            double[] sorted = (double[])result.generationMilliseconds.Clone();
            Array.Sort(sorted);
            double total = 0;
            foreach (double duration in sorted) total += duration;
            result.meanMilliseconds = total / sorted.Length;
            result.p95Milliseconds = sorted[(int)Math.Ceiling(sorted.Length * 0.95) - 1];
            result.p99Milliseconds = sorted[(int)Math.Ceiling(sorted.Length * 0.99) - 1];
            result.maxMilliseconds = sorted[sorted.Length - 1];

            File.WriteAllText(path, JsonUtility.ToJson(result, true));
            Debug.Log("DataTest map probe metrics: " + path);
        }
        finally
        {
            Object.DestroyImmediate(settings);
        }
    }

    private static void AssertValid(MapData map)
    {
        Assert.That(map, Is.Not.Null);
        Assert.That(map.IsWalkable(map.SpawnCell), Is.True, "Spawn must be walkable.");
        Assert.That(map.IsInside(map.ExitCell), Is.True, "Exit must be inside the map.");
    }
}

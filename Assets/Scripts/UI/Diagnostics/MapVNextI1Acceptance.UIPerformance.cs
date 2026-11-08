#if UNITY_EDITOR || RPG_SHOP_INTEGRATION_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;

/// <summary>Identical before/after measurement code; uses only the pre-existing public UI and gate APIs.</summary>
public sealed partial class MapVNextI1Acceptance
{
    [Serializable] private sealed class UIPerformanceRow
    {
        public string operation, unit, allocationCounter;
        public int samples, gcCollections;
        public double mean, p95;
        public long totalAllocatedBytes, monoUsedDeltaBytes;
    }
    [Serializable] private sealed class UIPerformanceReport
    {
        public string buildId, sourceFingerprint, unity, cpu, gpu, operatingSystem, note;
        public int width, height, packageSlots, canvases, renderTextures;
        public long threadAllocationCalibrationBytes, monoUsedDeltaDuringCalibrationBytes;
        public List<UIPerformanceRow> measurements = new List<UIPerformanceRow>();
        public List<string> unavailable = new List<string>();
    }
    private static UIPerformanceRow UIStatistics(string operation, string unit, double[] values, long allocated = 0)
    {
        double sum = values.Sum(); Array.Sort(values);
        return new UIPerformanceRow { operation = operation, unit = unit, samples = values.Length,
            mean = sum / values.Length, p95 = values[Math.Min(values.Length - 1, (int)Math.Ceiling(values.Length * .95) - 1)],
            totalAllocatedBytes = allocated };
    }
    private static UIPerformanceRow UIMeasure(string name, Action action, bool allocationCounterSupported)
    {
        for (int i = 0; i < 10; i++) action();
        var values = new double[100];
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        long monoUsed = Profiler.GetMonoUsedSizeLong();
        int collections = GC.CollectionCount(0);
        for (int i = 0; i < values.Length; i++)
        {
            long started = Stopwatch.GetTimestamp(); action();
            values[i] = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
        }
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        long heapDelta = Profiler.GetMonoUsedSizeLong() - monoUsed;
        int collectionDelta = GC.CollectionCount(0) - collections;
        var row = UIStatistics(name, "ms", values, allocationCounterSupported ? allocated : -1);
        row.monoUsedDeltaBytes = heapDelta; row.gcCollections = collectionDelta;
        row.allocationCounter = allocationCounterSupported ? "CALIBRATED_CURRENT_THREAD_BYTES" : "UNAVAILABLE_NOT_ZERO_ALLOCATION";
        return row;
    }
    private IEnumerator ProveUIPerformance()
    {
        var package = FindObjectOfType<PackagePanelController>(true);
        var grid = FindObjectOfType<PackageGridUI>(true);
        var settings = FindObjectOfType<TowerRunUiController>();
        var report = new UIPerformanceReport { buildId = MapHostBuildStamp.BuildId,
            sourceFingerprint = Environment.GetEnvironmentVariable("RPG_I1_SOURCE_FINGERPRINT"), unity = Application.unityVersion,
            cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName, operatingSystem = SystemInfo.operatingSystem,
            width = Screen.width, height = Screen.height,
            note = "Same original merchant/trade fixture and identical probe SHA on both revisions. 10 warmups, 100 synchronous samples; rendered counters sampled for 120 consecutive frames each. A known 65536-byte allocation calibrates memory APIs before reporting allocations. Unsupported thread allocation count is -1, never zero. Managed-used delta includes live and uncollected objects on all threads; collection counts are recorded per timed batch and a collection may make heap delta negative. Screenshot capture is outside measurement. Original GateHub benchmark pause lease; all original cameras temporarily disabled and restored before UI cleanup in finally; ScreenSpaceOverlay UI excludes random-map rendering. Hardware/OS noise remains. Unsupported counters are explicit." };
        long allocationStart = GC.GetAllocatedBytesForCurrentThread();
        long monoStart = Profiler.GetMonoUsedSizeLong();
        var calibration = new byte[65536]; calibration[0] = 1;
        report.threadAllocationCalibrationBytes = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
        report.monoUsedDeltaDuringCalibrationBytes = Profiler.GetMonoUsedSizeLong() - monoStart;
        GC.KeepAlive(calibration);
        bool allocationCounterSupported = report.threadAllocationCalibrationBytes >= 65536;
        if (!allocationCounterSupported) report.unavailable.Add("GC.GetAllocatedBytesForCurrentThread failed known-allocation calibration; do not interpret prior zeros as allocation-free");
        Require(grid.GetComponentInParent<Canvas>(true).rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay,
            "Performance isolation requires the original overlay Canvas");
        Camera[] cameras = FindObjectsOfType<Camera>(true);
        bool[] cameraEnabled = cameras.Select(camera => camera.enabled).ToArray();
        try
        {
        foreach (Camera camera in cameras) camera.enabled = false;
        using (var pause = TowerRunGateHub.AcquireWorldPause("ui.performance.measurement"))
        {
            report.measurements.Add(UIMeasure("Package.OpenClose", () => { package.OpenPackagePanel(); package.ClosePackagePanel(); }, allocationCounterSupported));
            report.measurements.Add(UIMeasure("Settings.OpenClose", () => { settings.OpenSettings(); settings.CloseSettings(); }, allocationCounterSupported));
            package.OpenPackagePanel();
            Require(package.IsPackagePanelVisible, "Performance package did not open");
            report.packageSlots = FindObjectsOfType<PackageSlotUI>().Length;
            report.measurements.Add(UIMeasure("Package.Refresh", grid.Refresh, allocationCounterSupported));
            report.measurements.Add(UIMeasure("Package.RefreshAndCanvas", () => { grid.Refresh(); Canvas.ForceUpdateCanvases(); }, allocationCounterSupported));
            report.canvases = FindObjectsOfType<Canvas>(true).Length;
            report.renderTextures = Resources.FindObjectsOfTypeAll<RenderTexture>().Length;
            var handles = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
            var wanted = new[] { "Batches Count", "SetPass Calls Count", "Vertices Count", "GC Allocated In Frame", "Canvas.BuildBatch", "Canvas.SendWillRenderCanvases", "Layout.Rebuild" };
            foreach (string name in wanted)
            {
                var handle = handles.FirstOrDefault(h => ProfilerRecorderHandle.GetDescription(h).Name == name);
                if (!handle.Valid) { report.unavailable.Add(name); continue; }
                var description = ProfilerRecorderHandle.GetDescription(handle);
                using (var recorder = ProfilerRecorder.StartNew(description.Category, name, 1))
                {
                    var values = new double[120];
                    for (int i = 0; i < values.Length; i++) { yield return null; values[i] = recorder.LastValue; }
                    report.measurements.Add(UIStatistics(name, description.UnitType.ToString(), values));
                }
            }
            package.ClosePackagePanel();
        }
        }
        finally
        {
            for (int i = 0; i < cameras.Length; i++) if (cameras[i] != null) cameras[i].enabled = cameraEnabled[i];
            try { package.ClosePackagePanel(); }
            finally { settings.CloseSettings(); }
        }
        File.WriteAllText(Path.Combine(output, "ui-performance.json"), JsonUtility.ToJson(report, true));
    }
}
#endif

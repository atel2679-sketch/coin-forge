#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Globalization;
using Unity.Profiling;
using UnityEngine;

/// <summary>
/// IC-04 frame-time probe (dev builds only). Records every frame into preallocated buffers for N seconds,
/// then logs one "IC04_PERF" line: average FPS, P50/P95/P99 frame time, GC collections, GC alloc per frame,
/// and CPU/GPU frame time from FrameTimingManager (needs Player Settings > Frame Timing Stats).
/// Start from the debug panel, or launch with: adb shell am start ... -e unity "-perfprobe 60"
/// </summary>
public sealed class CoinForgePerfProbe : MonoBehaviour
{
    const int MaxFrames = 8192;
    readonly float[] frameMs = new float[MaxFrames];
    readonly float[] cpuMs = new float[MaxFrames];
    readonly float[] gpuMs = new float[MaxFrames];
    readonly long[] allocBytes = new long[MaxFrames];
    readonly FrameTiming[] timing = new FrameTiming[1];
    ProfilerRecorder gcAlloc;
    // Per-frame cost of the usual UI suspects (ns). Missing markers simply report -1.
    static readonly (ProfilerCategory cat, string name)[] Markers =
    {
        (ProfilerCategory.Internal, "Main Thread"),
        (ProfilerCategory.Scripts, "Update.ScriptRunBehaviourUpdate"),
        (ProfilerCategory.Scripts, "PreLateUpdate.ScriptRunBehaviourLateUpdate"),
        (ProfilerCategory.Gui, "Canvas.SendWillRenderCanvases"),
        (ProfilerCategory.Gui, "Canvas.BuildBatch"),
        (ProfilerCategory.Gui, "UGUI.Rendering.UpdateBatches"),
        (ProfilerCategory.Gui, "Layout"),
        (ProfilerCategory.Gui, "TextRendering.Cleanup"),
        (ProfilerCategory.Render, "Gfx.WaitForPresentOnGfxThread"),
        (ProfilerCategory.Render, "Gfx.PresentFrame"),
        (ProfilerCategory.Internal, "WaitForTargetFPS"),
        (ProfilerCategory.Internal, "GC.Collect"),
    };
    ProfilerRecorder[] markers;
    float[][] markerMs;
    readonly int[] spikeFrames = new int[MaxFrames];
    readonly float[] frameAt = new float[MaxFrames];
    int spikes;
    int frames, cpuCount, gpuCount, gcAtStart;
    float duration, elapsed, warmup;
    string label;

    public static void Begin(float seconds, string label = "manual", float warmupSeconds = 1f)
    {
        var go = new GameObject("IC-04 Perf Probe");
        DontDestroyOnLoad(go);
        var probe = go.AddComponent<CoinForgePerfProbe>();
        probe.duration = seconds;
        probe.label = label;
        probe.warmup = warmupSeconds;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void FromCommandLine()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] != "-perfprobe") continue;
            float seconds = 60;
            if (i + 1 < args.Length) float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out seconds);
            Begin(seconds, "cmdline", 10f);    // let startup, ads and the first layout settle
            return;
        }
    }

    void OnEnable()
    {
        gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        markers = new ProfilerRecorder[Markers.Length];
        markerMs = new float[Markers.Length][];
        for (int i = 0; i < Markers.Length; i++)
        {
            markers[i] = ProfilerRecorder.StartNew(Markers[i].cat, Markers[i].name);
            markerMs[i] = new float[MaxFrames];
        }
    }

    void OnDisable()
    {
        gcAlloc.Dispose();
        foreach (var m in markers) m.Dispose();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (warmup > 0)
        {
            warmup -= dt;
            if (warmup <= 0)
            {
                gcAtStart = GC.CollectionCount(0);
                Debug.Log($"IC04_PERF_START tag={label} seconds={duration} targetFrameRate={Application.targetFrameRate} vSync={QualitySettings.vSyncCount} res={Screen.width}x{Screen.height}");
            }
            return;
        }

        if (frames < MaxFrames)
        {
            frameMs[frames] = dt * 1000f;
            frameAt[frames] = elapsed;
            if (dt > 0.034f) spikeFrames[spikes++] = frames;
            for (int i = 0; i < markers.Length; i++)
                markerMs[i][frames] = markers[i].Valid ? markers[i].LastValue / 1e6f : -1;
            allocBytes[frames] = gcAlloc.Valid ? gcAlloc.LastValue : -1;
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timing) == 1)
            {
                if (timing[0].cpuFrameTime > 0) cpuMs[cpuCount++] = (float)timing[0].cpuFrameTime;
                if (timing[0].gpuFrameTime > 0) gpuMs[gpuCount++] = (float)timing[0].gpuFrameTime;
            }
            frames++;
        }

        elapsed += dt;
        if (elapsed < duration) return;
        ReportMarkers();   // before Report(): Report sorts the shared frame buffer
        Report();
        Destroy(gameObject);
    }

    void Report()
    {
        int gc = GC.CollectionCount(0) - gcAtStart;
        float avgFps = frames / elapsed;
        long maxAlloc = 0, totalAlloc = 0;
        for (int i = 0; i < frames; i++) { maxAlloc = Math.Max(maxAlloc, allocBytes[i]); totalAlloc += Math.Max(0, allocBytes[i]); }
        long[] allocSorted = new long[frames];
        Array.Copy(allocBytes, allocSorted, frames);
        Array.Sort(allocSorted);
        Array.Sort(frameMs, 0, frames);
        Array.Sort(cpuMs, 0, cpuCount);
        Array.Sort(gpuMs, 0, gpuCount);
        int over20 = 0, over34 = 0;
        for (int i = 0; i < frames; i++) { if (frameMs[i] > 20f) over20++; if (frameMs[i] > 34f) over34++; }

        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "IC04_PERF tag={0} frames={1} seconds={2:0.0} avgFps={3:0.00} p50={4:0.00}ms p95={5:0.00}ms p99={6:0.00}ms max={7:0.00}ms " +
            "over20ms={8} over34ms={9} gcCollections={10} gcAllocTotal={11}B gcAllocP95={12}B gcAllocMax={13}B " +
            "cpuP50={14:0.00}ms cpuP95={15:0.00}ms gpuP50={16:0.00}ms gpuP95={17:0.00}ms",
            this.label, frames, elapsed, avgFps, P(frameMs, frames, 0.5f), P(frameMs, frames, 0.95f), P(frameMs, frames, 0.99f),
            frames > 0 ? frameMs[frames - 1] : 0, over20, over34, gc, totalAlloc, frames > 0 ? allocSorted[(int)((frames - 1) * 0.95f)] : 0,
            maxAlloc, P(cpuMs, cpuCount, 0.5f), P(cpuMs, cpuCount, 0.95f), P(gpuMs, gpuCount, 0.5f), P(gpuMs, gpuCount, 0.95f)));
    }

    void ReportMarkers()
    {
        var sb = new System.Text.StringBuilder("IC04_PERF_MARKERS");
        for (int i = 0; i < Markers.Length; i++)
        {
            float[] v = markerMs[i];
            float sum = 0; for (int f = 0; f < frames; f++) sum += v[f];
            // cost on spike frames vs. all frames
            float spikeSum = 0; for (int k = 0; k < spikes; k++) spikeSum += v[spikeFrames[k]];
            Array.Sort(v, 0, frames);
            sb.AppendFormat(CultureInfo.InvariantCulture, " | {0}: avg={1:0.00} p95={2:0.00} spikeAvg={3:0.00}",
                Markers[i].name, frames > 0 ? sum / frames : -1, P(v, frames, 0.95f), spikes > 0 ? spikeSum / spikes : -1);
        }
        // Spike periodicity: median gap between frames over 34 ms.
        if (spikes > 2)
        {
            var gaps = new float[spikes - 1];
            for (int k = 1; k < spikes; k++) gaps[k - 1] = frameAt[spikeFrames[k]] - frameAt[spikeFrames[k - 1]];
            Array.Sort(gaps);
            sb.AppendFormat(CultureInfo.InvariantCulture, " | spikeGapMedian={0:0.000}s spikeGapP25={1:0.000}s", gaps[gaps.Length / 2], gaps[gaps.Length / 4]);
        }
        Debug.Log(sb.ToString());
    }

    static float P(float[] sorted, int count, float q) => count == 0 ? -1 : sorted[(int)((count - 1) * q)];
}
#endif

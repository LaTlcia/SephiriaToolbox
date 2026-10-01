using System;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

public partial class SephiriaToolbox
{
    enum PerfPart { Sample, Track, Loot, Arrange, Other, Gui, Count }
    static readonly string[] PerfNames = { "采样", "追踪", "评估", "整理", "其他", "界面" };
    const double SlowFrameMs = 8;
    const float PerfReportSeconds = 60f;

    readonly long[] perfTicks = new long[(int)PerfPart.Count];
    readonly long[] perfWorst = new long[(int)PerfPart.Count];
    long perfWorstTotal;
    int perfFrames, perfSlow;
    double perfSumMs;
    float nextPerfReport = PerfReportSeconds;

    static long PerfStart() => Stopwatch.GetTimestamp();
    void PerfAdd(PerfPart p, long t0) => perfTicks[(int)p] += Stopwatch.GetTimestamp() - t0;
    static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    void PerfFrame()
    {
        long total = 0;
        for (int i = 0; i < perfTicks.Length; i++) total += perfTicks[i];
        perfFrames++;
        perfSumMs += Ms(total);
        if (Ms(total) > SlowFrameMs)
        {
            perfSlow++;
            if (total > perfWorstTotal) { perfWorstTotal = total; Array.Copy(perfTicks, perfWorst, perfTicks.Length); }
        }
        Array.Clear(perfTicks, 0, perfTicks.Length);
        if (Time.unscaledTime < nextPerfReport) return;
        nextPerfReport = Time.unscaledTime + PerfReportSeconds;
        if (perfSlow > 0)
        {
            string parts = string.Join("，", Enumerable.Range(0, (int)PerfPart.Count)
                .Where(i => Ms(perfWorst[i]) >= 0.1).Select(i => $"{PerfNames[i]} {Ms(perfWorst[i]):0.0}"));
            Debug.Log($"[SephiriaToolbox] 性能：最近 {PerfReportSeconds:0} 秒 {perfFrames} 帧，模组平均每帧 {perfSumMs / Math.Max(1, perfFrames):0.00} ms，" +
                      $"{perfSlow} 帧超过 {SlowFrameMs:0} ms，最慢一帧 {Ms(perfWorstTotal):0.0} ms（{parts}）");
        }
        perfFrames = perfSlow = 0;
        perfSumMs = 0;
        perfWorstTotal = 0;
        Array.Clear(perfWorst, 0, perfWorst.Length);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using ThreadPriority = System.Threading.ThreadPriority;

public partial class SephiriaToolbox
{
    static int WorkerThreads => Math.Max(1, Math.Min(4, Environment.ProcessorCount / 2));

    static void LowPriority(Action work)
    {
        var th = Thread.CurrentThread;
        ThreadPriority old = ThreadPriority.Normal;
        bool changed = false;
        try { old = th.Priority; th.Priority = ThreadPriority.BelowNormal; changed = true; } catch { }
        try { work(); }
        finally
        {
            if (changed) try { th.Priority = old; } catch { }
        }
    }

    static T LowPriority<T>(Func<T> work)
    {
        T result = default;
        LowPriority(() => { result = work(); });
        return result;
    }

    static readonly object writeLock = new();
    static Task writeChain = Task.CompletedTask;
    static readonly Queue<string> writeErrors = new();

    static void WriteFileInBackground(string path, Func<string> content, Action done = null)
    {
        lock (writeLock)
            writeChain = writeChain.ContinueWith(_ => LowPriority(() =>
            {
                try
                {
                    string text = content();
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllText(path, text, new UTF8Encoding(false));
                    done?.Invoke();
                }
                catch (Exception e) { lock (writeErrors) writeErrors.Enqueue(Path.GetFileName(path) + ": " + e.Message); }
            }), TaskScheduler.Default);
    }

    static void FlushBackgroundWrites()
    {
        Task t;
        lock (writeLock) t = writeChain;
        try { t.Wait(5000); } catch { }
    }

    void ReportBackgroundWrites()
    {
        lock (writeErrors)
            while (writeErrors.Count > 0) WarnOnce("写文件", new IOException(writeErrors.Dequeue()));
    }
}

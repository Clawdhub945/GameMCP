using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace GameMCP;

/// <summary>
/// 主线程调度器 — HTTP handler 不能直接访问 Unity 对象,
/// 必须通过此组件在主线程上执行。
/// </summary>
public class MainThreadDispatcher : MonoBehaviour
{
    private static MainThreadDispatcher? _instance;
    private static readonly ConcurrentQueue<Action> _queue = new();
    private static readonly ConcurrentDictionary<int, object?> _results = new();
    private static int _nextId;

    public MainThreadDispatcher(IntPtr ptr) : base(ptr) { }

    private void Awake()
    {
        _instance = this;
    }

    private void Update()
    {
        while (_queue.TryDequeue(out var action))
        {
            try { action(); }
            catch (Exception ex)
            {
                GameMCPPlugin.LogError($"主线程任务异常: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 在主线程上执行 func 并同步等待结果。
    /// 从 HTTP 线程调用，阻塞直到主线程执行完毕。
    /// </summary>
    public static string ExecuteOnMainThread(Func<string> func)
    {
        if (_instance == null)
            return HttpServer.Error("MainThreadDispatcher 未初始化");

        // 如果已经在主线程，直接执行
        if (Thread.CurrentThread.ManagedThreadId == _instance.gameObject?.GetHashCode())
        {
            try { return func(); }
            catch (Exception ex) { return HttpServer.Error(ex.Message); }
        }

        int id = Interlocked.Increment(ref _nextId);
        var done = new ManualResetEventSlim(false);
        string? result = null;

        _queue.Enqueue(() =>
        {
            try { result = func(); }
            catch (Exception ex) { result = HttpServer.Error(ex.Message); }
            finally { done.Set(); }
        });

        // 等待主线程执行完毕，超时 5 秒
        if (!done.Wait(5000))
            return HttpServer.Error("主线程执行超时 (5s)");

        return result ?? HttpServer.Error("无返回结果");
    }
}

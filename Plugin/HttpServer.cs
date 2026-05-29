using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using GameMCP.Handlers;

namespace GameMCP;

public class HttpServer
{
    private readonly HttpListener _listener;
    private Thread? _thread;
    private volatile bool _running;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public HttpServer(int port)
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
    }

    public void Start()
    {
        _running = true;
        _listener.Start();
        _thread = new Thread(ListenLoop) { IsBackground = true, Name = "GameMCP_HTTP" };
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        try { _listener.Stop(); } catch { }
    }

    private void ListenLoop()
    {
        while (_running)
        {
            try
            {
                var context = _listener.GetContext();
                ThreadPool.QueueUserWorkItem(_ => HandleRequest(context));
            }
            catch (HttpListenerException) { break; }
            catch (Exception ex)
            {
                GameMCPPlugin.LogError($"HTTP 监听异常: {ex.Message}");
            }
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        var req = context.Request;
        var resp = context.Response;

        try
        {
            string path = req.Url?.AbsolutePath?.ToLower() ?? "";
            string method = req.HttpMethod.ToUpper();
            string? body = null;

            if (method == "POST" && req.HasEntityBody)
            {
                using var reader = new System.IO.StreamReader(req.InputStream, req.ContentEncoding);
                body = reader.ReadToEnd();
            }

            string json = Route(method, path, body);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            resp.ContentType = "application/json; charset=utf-8";
            resp.ContentLength64 = buffer.Length;
            resp.OutputStream.Write(buffer, 0, buffer.Length);
        }
        catch (Exception ex)
        {
            GameMCPPlugin.LogError($"请求处理异常: {ex.Message}");
            try
            {
                string err = JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
                byte[] buf = Encoding.UTF8.GetBytes(err);
                resp.StatusCode = 500;
                resp.ContentType = "application/json; charset=utf-8";
                resp.ContentLength64 = buf.Length;
                resp.OutputStream.Write(buf, 0, buf.Length);
            }
            catch { }
        }
        finally
        {
            try { resp.Close(); } catch { }
        }
    }

    private string Route(string method, string path, string? body)
    {
        // 所有游戏数据查询必须在主线程执行
        if (path == "/api/status")
            return MainThreadDispatcher.ExecuteOnMainThread(() => StatusHandler.GetStatus());

        if (path == "/api/registry/items")
            return MainThreadDispatcher.ExecuteOnMainThread(() => RegistryHandler.ListItems());

        if (path == "/api/registry/buildings")
            return MainThreadDispatcher.ExecuteOnMainThread(() => RegistryHandler.ListBuildings());

        if (path == "/api/registry/recipes")
            return MainThreadDispatcher.ExecuteOnMainThread(() => RegistryHandler.ListRecipes());

        if (path.StartsWith("/api/registry/item/"))
        {
            string idStr = path.Substring("/api/registry/item/".Length);
            if (int.TryParse(idStr, out int itemId))
                return MainThreadDispatcher.ExecuteOnMainThread(() => RegistryHandler.GetItemDetail(itemId));
            return Error("无效的 item id");
        }

        if (path.StartsWith("/api/registry/building/"))
        {
            string idStr = path.Substring("/api/registry/building/".Length);
            if (int.TryParse(idStr, out int buildingId))
                return MainThreadDispatcher.ExecuteOnMainThread(() => RegistryHandler.GetBuildingDetail(buildingId));
            return Error("无效的 building id");
        }

        if (path == "/api/world/facilities")
            return MainThreadDispatcher.ExecuteOnMainThread(() => WorldHandler.ListFacilities());

        if (path == "/api/world/npcs")
            return MainThreadDispatcher.ExecuteOnMainThread(() => WorldHandler.ListNpcs());

        if (path == "/api/world/animals")
            return MainThreadDispatcher.ExecuteOnMainThread(() => WorldHandler.ListAnimals());

        if (path == "/api/world/player")
            return MainThreadDispatcher.ExecuteOnMainThread(() => WorldHandler.GetPlayer());

        if (path.StartsWith("/api/inspect/"))
        {
            string typeName = path.Substring("/api/inspect/".Length);
            return MainThreadDispatcher.ExecuteOnMainThread(() => InspectHandler.InspectType(typeName));
        }

        if (method == "POST" && path == "/api/command")
            return MainThreadDispatcher.ExecuteOnMainThread(() => CommandHandler.Execute(body));

        if (path == "/api/saves")
            return MainThreadDispatcher.ExecuteOnMainThread(() => SaveHandler.ListSaves());

        if (method == "POST" && path == "/api/saves/load_latest")
            return MainThreadDispatcher.ExecuteOnMainThread(() => SaveHandler.LoadLatest());

        if (method == "POST" && path.StartsWith("/api/saves/load/"))
        {
            string saveName = path.Substring("/api/saves/load/".Length);
            if (string.IsNullOrWhiteSpace(saveName))
                return Error("存档名不能为空");
            return MainThreadDispatcher.ExecuteOnMainThread(() => SaveHandler.LoadSave(saveName));
        }

        if (method == "POST" && path.StartsWith("/api/saves/auto_load/"))
        {
            string flag = path.Substring("/api/saves/auto_load/".Length);
            bool enabled = flag == "true" || flag == "1";
            return MainThreadDispatcher.ExecuteOnMainThread(() => SaveHandler.ToggleAutoLoad(enabled));
        }

        if (path == "/api/mod/verify")
            return MainThreadDispatcher.ExecuteOnMainThread(() => ModVerifyHandler.Verify());

        if (path == "/api/debug/d")
            return MainThreadDispatcher.ExecuteOnMainThread(() => DebugHandler.InspectD());

        if (path == "/api/debug/gamew")
            return MainThreadDispatcher.ExecuteOnMainThread(() => DebugHandler.InspectGameW());

        if (path == "/api/debug/gamew2")
            return MainThreadDispatcher.ExecuteOnMainThread(() => DebugHandler.InspectGameWProperties());

        return Error($"未知路径: {method} {path}");
    }

    internal static string Error(string message)
    {
        return JsonSerializer.Serialize(new { error = message }, JsonOptions);
    }

    internal static string Ok(object data)
    {
        try
        {
            return JsonSerializer.Serialize(data, JsonOptions);
        }
        catch
        {
            // IL2CPP 对象含不可序列化字段，降级处理
            try
            {
                var safe = SanitizeForJson(data);
                return JsonSerializer.Serialize(safe, JsonOptions);
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = $"序列化失败: {ex.Message}" }, JsonOptions);
            }
        }
    }

    private static object? SanitizeForJson(object? obj)
    {
        if (obj == null) return null;
        var type = obj.GetType();
        if (type.IsPrimitive || type == typeof(string)) return obj;
        if (type == typeof(IntPtr)) return $"[IntPtr]";
        if (type == typeof(UIntPtr)) return $"[UIntPtr]";

        if (obj is System.Collections.IDictionary dict)
        {
            var result = new System.Collections.Generic.Dictionary<string, object?>();
            foreach (System.Collections.DictionaryEntry entry in dict)
            {
                result[entry.Key?.ToString() ?? "null"] = SanitizeForJson(entry.Value);
            }
            return result;
        }
        if (obj is System.Collections.IEnumerable enumerable && obj is not string)
        {
            var list = new System.Collections.Generic.List<object?>();
            foreach (var item in enumerable) list.Add(SanitizeForJson(item));
            return list;
        }
        return obj.ToString();
    }
}

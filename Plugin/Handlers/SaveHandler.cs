using System;
using System.IO;
using System.Linq;
using System.Reflection;
using GameMCP.Patches;
using HarmonyLib;

namespace GameMCP.Handlers;

/// <summary>
/// 存档管理 — 列举、加载存档
/// </summary>
public static class SaveHandler
{
    public static string ListSaves()
    {
        try
        {
            var root = AutoLoadSavePatch.GetSaveRoot();
            if (root == null || !Directory.Exists(root))
                return HttpServer.Error("存档目录不存在");

            var dirs = Directory.GetDirectories(root)
                .OrderByDescending(d => Directory.GetLastWriteTime(d))
                .Take(50)
                .Select(d =>
                {
                    var name = Path.GetFileName(d);
                    var summaryFile = Path.Combine(d, "summary.sav");
                    var hasSummary = File.Exists(summaryFile);
                    return new
                    {
                        folder_name = name,
                        last_modified = Directory.GetLastWriteTime(d).ToString("yyyy-MM-dd HH:mm:ss"),
                        has_summary = hasSummary,
                    };
                })
                .ToArray();

            return HttpServer.Ok(new { total = dirs.Length, saves = dirs });
        }
        catch (Exception ex)
        {
            return HttpServer.Error($"列举存档失败: {ex.Message}");
        }
    }

    public static string LoadSave(string folderName)
    {
        try
        {
            var csharpAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (csharpAsm == null) return HttpServer.Error("Assembly-CSharp 不可用");

            // 通过 UI.ExitAndLoadGame 加载
            // (UI.ExitGame 已被补丁, 标题画面会跳过 OnGameExit 直接重载场景)
            var loaded = TryUILoadSave(csharpAsm, folderName);
            if (loaded)
            {
                return HttpServer.Ok(new
                {
                    status = "loading",
                    save = folderName,
                    message = "正在加载存档..."
                });
            }

            return HttpServer.Error("UI 加载存档失败");
        }
        catch (Exception ex)
        {
            return HttpServer.Error($"加载存档失败: {ex.Message}");
        }
    }

    public static string LoadLatest()
    {
        var root = AutoLoadSavePatch.GetSaveRoot();
        if (root == null || !Directory.Exists(root))
            return HttpServer.Error("存档目录不存在");

        var latest = Directory.GetDirectories(root)
            .OrderByDescending(d => Directory.GetLastWriteTime(d))
            .FirstOrDefault();

        if (latest == null)
            return HttpServer.Error("没有找到任何存档");

        var folderName = Path.GetFileName(latest);
        return LoadSave(folderName);
    }

    public static string ToggleAutoLoad(bool enabled)
    {
        AutoLoadSavePatch.SetEnabled(enabled);
        return HttpServer.Ok(new
        {
            auto_load = enabled,
            message = enabled ? "已启用自动加载存档" : "已禁用自动加载存档"
        });
    }

    private static bool TryUILoadSave(Assembly csharpAsm, string folderName)
    {
        try
        {
            Type? uiType = null;
            try { uiType = csharpAsm.GetTypes().FirstOrDefault(t => t.Name == "UI"); }
            catch (ReflectionTypeLoadException ex) { uiType = ex.Types?.FirstOrDefault(t => t?.Name == "UI"); }
            if (uiType == null) { GameMCPPlugin.LogError("TryUI: UI 类型不存在"); return false; }

            // 找 UI.Ins
            object? uiIns = null;

            var insProp = uiType.GetProperty("Ins", BindingFlags.Public | BindingFlags.Static);
            if (insProp != null) uiIns = insProp.GetValue(null);

            if (uiIns == null)
            {
                var insField = uiType.GetField("Ins", BindingFlags.Public | BindingFlags.Static);
                if (insField != null) uiIns = insField.GetValue(null);
            }

            if (uiIns == null) { GameMCPPlugin.LogError("TryUI: UI.Ins 不可用"); return false; }

            // 调用 UI.ExitAndLoadGame(archive_folder_name, exit_msg)
            var exitAndLoad = uiType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "ExitAndLoadGame");
            if (exitAndLoad != null)
            {
                try
                {
                    exitAndLoad.Invoke(uiIns, new object[] { folderName, "" });
                    GameMCPPlugin.LogInfo($"UI.ExitAndLoadGame({folderName}) 调用成功");
                    return true;
                }
                catch (Exception ex2)
                {
                    GameMCPPlugin.LogError($"ExitAndLoadGame 失败: {ex2.Message} | Inner: {ex2.InnerException?.Message}");
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            GameMCPPlugin.LogError($"TryUILoadSave 失败: {ex.Message}");
            return false;
        }
    }
}

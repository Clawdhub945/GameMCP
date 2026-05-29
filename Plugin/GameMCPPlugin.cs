using System;
using System.Text;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace GameMCP;

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class GameMCPPlugin : BasePlugin
{
    public const string PLUGIN_GUID = "claude.gamemcp";
    public const string PLUGIN_NAME = "GameMCP";
    public const string PLUGIN_VERSION = "1.1.0";

    internal static BepInEx.Logging.ManualLogSource Logger = null!;
    private HttpServer? _httpServer;

    public override void Load()
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }
        Logger = Log;

        // 注册主线程调度器
        ClassInjector.RegisterTypeInIl2Cpp<MainThreadDispatcher>();
        var go = new GameObject("GameMCP_Dispatcher");
        GameObject.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<MainThreadDispatcher>();

        // 注册 Harmony 补丁 (自动加载存档)
        var harmony = new Harmony(PLUGIN_GUID);
        Patches.AutoLoadSavePatch.Register(harmony);

        // 启动 HTTP 服务器
        _httpServer = new HttpServer(23333);
        _httpServer.Start();

        LogInfo($"{PLUGIN_NAME} v{PLUGIN_VERSION} 已加载! HTTP @ http://localhost:23333");
    }

    public override bool Unload()
    {
        _httpServer?.Stop();
        LogInfo($"{PLUGIN_NAME} 已卸载");
        return true;
    }

    internal static void LogInfo(string msg) => Logger.LogInfo(msg);
    internal static void LogError(string msg) => Logger.LogError(msg);
}

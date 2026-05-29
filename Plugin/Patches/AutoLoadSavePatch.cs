using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;

namespace GameMCP.Patches;

/// <summary>
/// 自动加载存档
/// - 补丁 UI.ExitGame: 标题画面跳过 OnGameExit NRE,直接重载场景
/// - 补丁 UI.Start: 首次启动时自动加载最新存档
/// </summary>
public static class AutoLoadSavePatch
{
    private static bool _autoLoadEnabled = true;
    private static IntPtr _autoLoadFieldPtr;
    private static MethodInfo? _setFieldMethod;
    private static MethodInfo? _managedStringToIl2Cpp;

    public static string? PendingSaveFolder { get; set; }

    public static void SetEnabled(bool enabled) => _autoLoadEnabled = enabled;

    public static void Register(Harmony harmony)
    {
        var csharpAsm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
        if (csharpAsm == null) { GameMCPPlugin.LogError("Assembly-CSharp 不可用"); return; }

        Type? uiType = null;
        try { uiType = csharpAsm.GetTypes().FirstOrDefault(t => t.Name == "UI"); }
        catch (ReflectionTypeLoadException ex) { uiType = ex.Types?.FirstOrDefault(t => t?.Name == "UI"); }
        if (uiType == null) { GameMCPPlugin.LogError("UI 类型不存在"); return; }

        // 初始化 IL2CPP API
        InitIl2CppApi();

        // 补丁 UI.ExitGame — 标题画面跳过 OnGameExit
        var exitGame = uiType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "ExitGame");
        if (exitGame != null)
        {
            harmony.Patch(exitGame,
                prefix: new HarmonyMethod(typeof(AutoLoadSavePatch).GetMethod(nameof(ExitGamePrefix))));
            GameMCPPlugin.LogInfo("已补丁 UI.ExitGame");
        }

        // 补丁 UI.Start — 首次启动自动加载
        var startMethod = uiType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "Start");
        if (startMethod != null)
        {
            harmony.Patch(startMethod,
                prefix: new HarmonyMethod(typeof(AutoLoadSavePatch).GetMethod(nameof(UIStartPrefix))));
            GameMCPPlugin.LogInfo("已补丁 UI.Start");
        }
    }

    private static void InitIl2CppApi()
    {
        try
        {
            var mainSceneType = AccessTools.TypeByName("MainScene");
            if (mainSceneType == null) return;

            // 触发 MainScene IL2CPP 类型初始化 (cctor)
            // 通过访问 static property 来触发 il2cpp_runtime_class_init
            try
            {
                var prop = mainSceneType.GetProperty("auto_load_archive_folder_name",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                if (prop != null)
                {
                    var _ = prop.GetValue(null); // 触发 cctor
                    GameMCPPlugin.LogInfo("InitIl2CppApi: 已通过 property 访问触发 MainScene cctor");
                }
                else
                {
                    // 备选: 访问任意 static 字段
                    var anyField = mainSceneType.GetFields(BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(f => !f.Name.StartsWith("Native"));
                    if (anyField != null)
                    {
                        var _ = anyField.GetValue(null);
                        GameMCPPlugin.LogInfo($"InitIl2CppApi: 已通过 {anyField.Name} 访问触发 MainScene cctor");
                    }
                }
            }
            catch { }

            var autoLoadField = mainSceneType.GetField("NativeFieldInfoPtr_auto_load_archive_folder_name",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
            if (autoLoadField != null)
                _autoLoadFieldPtr = (IntPtr)autoLoadField.GetValue(null)!;

            var il2cppType = typeof(Il2CppInterop.Runtime.IL2CPP);
            _setFieldMethod = il2cppType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "il2cpp_field_static_set_value" && m.GetParameters().Length == 2);
            _managedStringToIl2Cpp = il2cppType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "ManagedStringToIl2Cpp");

            GameMCPPlugin.LogInfo($"InitIl2CppApi: autoLoadPtr={_autoLoadFieldPtr}, setField={_setFieldMethod != null}, strToIl2Cpp={_managedStringToIl2Cpp != null}");
        }
        catch (Exception ex)
        {
            GameMCPPlugin.LogError($"InitIl2CppApi 失败: {ex.Message}");
        }
    }

    /// <summary>
    /// UI.Start 前缀 — 设置 auto_load_archive_folder_name 让游戏自动加载
    /// </summary>
    public static bool UIStartPrefix(object __instance)
    {
        try
        {
            string? folderName = null;

            if (!string.IsNullOrEmpty(PendingSaveFolder))
            {
                folderName = PendingSaveFolder;
                GameMCPPlugin.LogInfo($"UIStart: 使用待加载存档: {folderName}");
            }
            else if (_autoLoadEnabled)
            {
                var saveRoot = GetSaveRoot();
                if (saveRoot != null)
                {
                    var latest = Directory.GetDirectories(saveRoot)
                        .OrderByDescending(d => Directory.GetLastWriteTime(d))
                        .FirstOrDefault();
                    if (latest != null)
                    {
                        folderName = Path.GetFileName(latest);
                        GameMCPPlugin.LogInfo($"UIStart: 自动加载最新存档: {folderName}");
                    }
                }
            }

            if (string.IsNullOrEmpty(folderName))
            {
                PendingSaveFolder = null;
                return true;
            }

            // 设置 IL2CPP 字段
            SetIl2CppStringField(_autoLoadFieldPtr, folderName!);

            // 直接调用 UI.StartGame (如果有这个方法)
            var uiType = __instance.GetType();
            var startGame = uiType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "StartGame");
            if (startGame != null)
            {
                try
                {
                    var parms = startGame.GetParameters();
                    if (parms.Length == 1 && parms[0].ParameterType == typeof(string))
                    {
                        startGame.Invoke(__instance, new object[] { folderName! });
                        GameMCPPlugin.LogInfo($"UIStart: 直接调用 UI.StartGame({folderName})");
                    }
                    else if (parms.Length == 0)
                    {
                        startGame.Invoke(__instance, null);
                        GameMCPPlugin.LogInfo("UIStart: 直接调用 UI.StartGame()");
                    }
                    else if (parms.Length == 2)
                    {
                        // UI.StartGame(string archive_folder_name, EmpireVillageProgram program)
                        startGame.Invoke(__instance, new object[] { folderName!, null! });
                        GameMCPPlugin.LogInfo($"UIStart: 调用 UI.StartGame({folderName}, null)");
                    }
                    else
                    {
                        GameMCPPlugin.LogInfo($"UIStart: UI.StartGame 参数: {string.Join(",", parms.Select(p => p.ParameterType.Name))}");
                    }
                }
                catch (Exception ex)
                {
                    GameMCPPlugin.LogError($"UIStart: UI.StartGame 调用失败: {ex.Message} | {ex.InnerException?.Message}");
                }
            }
            else
            {
                GameMCPPlugin.LogInfo("UIStart: UI.StartGame 方法不存在, 设置字段等待 UI.Start 自动处理");
            }

            PendingSaveFolder = null;
        }
        catch (Exception ex)
        {
            GameMCPPlugin.LogError($"UIStart 失败: {ex.Message}");
        }

        return true;
    }

    /// <summary>
    /// UI.ExitGame 前缀 — 标题画面跳过 OnGameExit,重载场景
    /// </summary>
    public static bool ExitGamePrefix(object __instance)
    {
        try
        {
            var csharpAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (csharpAsm == null) return true;

            Type? gameType = null;
            try { gameType = csharpAsm.GetTypes().FirstOrDefault(t => t.Name == "Game"); }
            catch (ReflectionTypeLoadException ex) { gameType = ex.Types?.FirstOrDefault(t => t?.Name == "Game"); }
            if (gameType == null) return true;

            var getMainScene = gameType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "get_main_scene");
            if (getMainScene != null)
            {
                var val = getMainScene.Invoke(null, null);
                if (val == null)
                {
                    // 标题画面 — 读取 exit_and_load_game_info
                    var uiType = __instance.GetType();
                    var infoField = uiType.GetField("exit_and_load_game_info",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var info = infoField?.GetValue(__instance);

                    if (info != null)
                    {
                        var folderField = info.GetType().GetField("load_game_archive_folder_name",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        var folderName = folderField?.GetValue(info)?.ToString();
                        if (!string.IsNullOrEmpty(folderName))
                        {
                            GameMCPPlugin.LogInfo($"ExitGame: 标题画面, 待加载存档: {folderName}");
                            PendingSaveFolder = folderName;
                            SetIl2CppStringField(_autoLoadFieldPtr, folderName);
                        }
                    }
                    else
                    {
                        GameMCPPlugin.LogInfo("ExitGame: 标题画面, 无存档信息");
                    }

                    // 重载 UIScene
                    ReloadUIScene();
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            GameMCPPlugin.LogError($"ExitGamePrefix 失败: {ex.Message}");
        }
        return true;
    }

    private static void SetIl2CppStringField(IntPtr fieldPtr, string value)
    {
        if (fieldPtr == IntPtr.Zero || _setFieldMethod == null || _managedStringToIl2Cpp == null) return;
        try
        {
            var il2cppStr = (IntPtr)_managedStringToIl2Cpp.Invoke(null, new object[] { value })!;
            _setFieldMethod.Invoke(null, new object[] { fieldPtr, il2cppStr });
            GameMCPPlugin.LogInfo($"SetIl2CppStringField: {value} (il2cppStr={il2cppStr})");

            // 验证: 用 IL2CPP API 读回
            var il2cppType = typeof(Il2CppInterop.Runtime.IL2CPP);
            var getFieldMethod = il2cppType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "il2cpp_field_static_get_value" && m.GetParameters().Length == 2);
            if (getFieldMethod != null)
            {
                var resultPtr = IntPtr.Zero;
                // il2cpp_field_static_get_value(fieldInfo, out value) — 需要传指针
                var buf = Marshal.AllocHGlobal(IntPtr.Size);
                try
                {
                    Marshal.WriteIntPtr(buf, IntPtr.Zero);
                    getFieldMethod.Invoke(null, new object[] { fieldPtr, buf });
                    var readBackPtr = Marshal.ReadIntPtr(buf);
                    if (readBackPtr != IntPtr.Zero)
                    {
                        // 尝试用 IL2CPP API 读取字符串
                        var strConvert = il2cppType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                            .FirstOrDefault(m => m.Name == "Il2CppStringToManaged");
                        string? managedStr = strConvert != null
                            ? (string?)strConvert.Invoke(null, new object[] { readBackPtr })
                            : Marshal.PtrToStringUTF8(readBackPtr + 4); // fallback
                        GameMCPPlugin.LogInfo($"SetIl2CppStringField 验证 (IL2CPP API 读回): {managedStr}");
                    }
                    else
                    {
                        GameMCPPlugin.LogInfo("SetIl2CppStringField 验证: 读回为 null");
                    }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }

            // 验证: 用 property 读回
            var mainSceneType = AccessTools.TypeByName("MainScene");
            if (mainSceneType != null)
            {
                var prop = mainSceneType.GetProperty("auto_load_archive_folder_name",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                if (prop != null)
                {
                    var propVal = prop.GetValue(null)?.ToString();
                    GameMCPPlugin.LogInfo($"SetIl2CppStringField 验证 (property 读回): {propVal}");
                }
            }
        }
        catch (Exception ex)
        {
            GameMCPPlugin.LogError($"SetIl2CppStringField 失败: {ex.Message} | {ex.InnerException?.Message}");
        }
    }

    private static void ReloadUIScene()
    {
        try
        {
            Type? sceneManagerType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    sceneManagerType = asm.GetTypes().FirstOrDefault(t =>
                        t.Name == "SceneManager" &&
                        t.Namespace?.Contains("SceneManagement") == true);
                }
                catch (ReflectionTypeLoadException ex)
                {
                    sceneManagerType = ex.Types?.FirstOrDefault(t =>
                        t?.Name == "SceneManager" &&
                        t.Namespace?.Contains("SceneManagement") == true);
                }
                if (sceneManagerType != null) break;
            }
            if (sceneManagerType == null) return;

            var loadScene = sceneManagerType.GetMethod("LoadScene", new[] { typeof(string) });
            if (loadScene != null)
            {
                GameMCPPlugin.LogInfo("ReloadUIScene: 重新加载 UIScene");
                loadScene.Invoke(null, new object[] { "UIScene" });
            }
        }
        catch (Exception ex)
        {
            GameMCPPlugin.LogError($"ReloadUIScene 失败: {ex.Message}");
        }
    }

    internal static string? GetSaveRoot()
    {
        try
        {
            var csharpAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (csharpAsm != null)
            {
                Type? amType = null;
                try { amType = csharpAsm.GetTypes().FirstOrDefault(t => t.Name == "ArchiveManager"); }
                catch (ReflectionTypeLoadException ex) { amType = ex.Types?.FirstOrDefault(t => t?.Name == "ArchiveManager"); }

                if (amType != null)
                {
                    var getDirPath = amType.GetMethod("get_dir_path",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (getDirPath != null)
                    {
                        var result = getDirPath.Invoke(null, null);
                        if (result is string path && Directory.Exists(path))
                            return path;
                    }
                }
            }
        }
        catch { }

        var persistentDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            .Replace("\\Roaming", "\\LocalLow");
        var candidates = new[]
        {
            Path.Combine(persistentDataPath, "Looming", "Territory", "Save"),
            Path.Combine(persistentDataPath, "Looming", "Territory", "TerritoryArchive", "UserData"),
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }
}

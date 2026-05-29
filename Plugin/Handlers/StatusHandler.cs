using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace GameMCP.Handlers;

public static class StatusHandler
{
    public static string GetStatus()
    {
        try
        {
            var csharpAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            bool gameLoaded = csharpAsm != null;

            // 检查游戏状态
            bool inGame = false;
            string sceneName = "";
            try
            {
                sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

                // 通过 Game.main_scene 判断是否在游戏中 (比场景名更准确)
                var gameType = HarmonyLib.AccessTools.TypeByName("Game");
                if (gameType != null)
                {
                    var getMainScene = gameType.GetMethod("get_main_scene",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (getMainScene != null)
                        inGame = getMainScene.Invoke(null, null) != null;
                }

                // fallback: 场景名判断
                if (!inGame)
                    inGame = !string.IsNullOrEmpty(sceneName) && sceneName != "InitScene" && sceneName != "TitleScene";
            }
            catch { }

            // 统计 mod 注册项
            int itemCount = 0, buildingCount = 0, recipeCount = 0;
            try
            {
                var d = ReflectionHelper.GetDInstance();
                if (d != null)
                {
                    var stuffDic = ReflectionHelper.GetProperty(d, "stuff_dic");
                    if (stuffDic != null) itemCount = ReflectionHelper.GetDictionaryCount(stuffDic);

                    var buildDic = ReflectionHelper.GetProperty(d, "build_dic");
                    if (buildDic != null) buildingCount = ReflectionHelper.GetDictionaryCount(buildDic);
                }
            }
            catch { }

            // 判断游戏阶段 (更明确的状态)
            string gameState = "unknown";
            if (!gameLoaded)
                gameState = "not_loaded";
            else if (!inGame)
                gameState = "title_screen";
            else if (itemCount > 0 && buildingCount > 0)
                gameState = "world_active";
            else
                gameState = "loading";

            return HttpServer.Ok(new
            {
                status = "ok",
                plugin_version = GameMCPPlugin.PLUGIN_VERSION,
                game_state = gameState,
                in_game = inGame,
                scene = sceneName,
                registry = new
                {
                    items = itemCount,
                    buildings = buildingCount,
                    recipes = recipeCount,
                },
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            });
        }
        catch (Exception ex)
        {
            return HttpServer.Error($"获取状态失败: {ex.Message}");
        }
    }
}

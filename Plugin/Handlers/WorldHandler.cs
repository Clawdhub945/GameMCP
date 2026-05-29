using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GameMCP.Handlers;

/// <summary>
/// 运行时世界数据查询 — 设施、NPC、动物、玩家
/// </summary>
public static class WorldHandler
{
    public static string ListFacilities()
    {
        try
        {
            var facilityDic = GetFacilityDic();
            if (facilityDic == null)
                return HttpServer.Error("facility_dic 不可用，需要先进入游戏");

            int total = ReflectionHelper.GetDictionaryCount(facilityDic);
            var entries = ReflectionHelper.EnumerateDictionary(facilityDic, 200);
            var items = new List<Dictionary<string, object?>>();
            foreach (var (key, facility) in entries)
            {
                if (facility == null) continue;

                var info = new Dictionary<string, object?>
                {
                    ["guid"] = ReflectionHelper.GetProperty(facility, "Guid")?.ToString()
                               ?? ReflectionHelper.GetProperty(facility, "guid")?.ToString(),
                    ["stuff_id"] = ReflectionHelper.GetProperty(facility, "stuff_id")?.ToString(),
                    ["name"] = ReflectionHelper.GetProperty(facility, "stuff_name_with_id_index")?.ToString()
                               ?? $"设施#{ReflectionHelper.GetProperty(facility, "stuff_id")}",
                    ["pos"] = FormatPosition(facility),
                    ["is_broken"] = ReflectionHelper.GetProperty(facility, "is_broken")?.ToString(),
                };
                items.Add(info);
            }

            return HttpServer.Ok(new { total, returned = items.Count, facilities = items });
        }
        catch (Exception ex)
        {
            return HttpServer.Error($"列举设施失败: {ex.Message}");
        }
    }

    public static string ListNpcs()
    {
        try
        {
            var npcDic = GetNpcDic();
            if (npcDic == null)
                return HttpServer.Error("npc_dic 不可用，需要先进入游戏");

            int total = ReflectionHelper.GetDictionaryCount(npcDic);
            var entries = ReflectionHelper.EnumerateDictionary(npcDic, 200);
            var items = new List<Dictionary<string, object?>>();
            foreach (var (key, npc) in entries)
            {
                if (npc == null) continue;

                var info = new Dictionary<string, object?>
                {
                    ["guid"] = ReflectionHelper.GetProperty(npc, "Guid")?.ToString()
                               ?? ReflectionHelper.GetProperty(npc, "guid")?.ToString(),
                    ["npc_id"] = ReflectionHelper.GetProperty(npc, "npc_id")?.ToString(),
                    ["npc_name"] = ReflectionHelper.GetProperty(npc, "npc_name")?.ToString(),
                    ["pos"] = FormatPosition(npc),
                    ["hp"] = ReflectionHelper.GetProperty(npc, "hp")?.ToString(),
                    ["hp_total"] = ReflectionHelper.GetProperty(npc, "hp_total")?.ToString(),
                    ["is_dead"] = ReflectionHelper.GetProperty(npc, "IsDead")?.ToString(),
                };
                items.Add(info);
            }

            return HttpServer.Ok(new { total, returned = items.Count, npcs = items });
        }
        catch (Exception ex)
        {
            return HttpServer.Error($"列举NPC失败: {ex.Message}");
        }
    }

    public static string ListAnimals()
    {
        try
        {
            var animalList = GetAnimalDic();
            if (animalList == null)
                return HttpServer.Error("animal_list 不可用，需要先进入游戏");

            // animal_list 是 List<Animal>，用 Count + 索引器遍历
            var countProp = animalList.GetType().GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
            int total = countProp?.GetValue(animalList) is int c ? c : -1;

            var items = new List<Dictionary<string, object?>>();
            int max = Math.Min(total, 200);
            for (int i = 0; i < max; i++)
            {
                var animal = ReflectionHelper.GetListItem(animalList, i);
                if (animal == null) continue;

                var info = new Dictionary<string, object?>
                {
                    ["guid"] = ReflectionHelper.GetProperty(animal, "Guid")?.ToString()
                               ?? ReflectionHelper.GetProperty(animal, "guid")?.ToString(),
                    ["stuff_id"] = ReflectionHelper.GetProperty(animal, "stuff_id")?.ToString(),
                    ["name"] = ReflectionHelper.GetProperty(animal, "gameObject") is UnityEngine.GameObject go
                               ? go.name : "",
                    ["pos"] = FormatPosition(animal),
                    ["hp"] = ReflectionHelper.GetProperty(animal, "hp")?.ToString(),
                };
                items.Add(info);
            }

            return HttpServer.Ok(new { total, returned = items.Count, animals = items });
        }
        catch (Exception ex)
        {
            return HttpServer.Error($"列举动物失败: {ex.Message}");
        }
    }

    public static string GetPlayer()
    {
        try
        {
            var npcDic = GetNpcDic();
            if (npcDic == null)
                return HttpServer.Error("npc_dic 不可用，需要先进入游戏");

            var entries = ReflectionHelper.EnumerateDictionary(npcDic, 200);

            // 遍历 NPC 找玩家角色
            foreach (var (key, npc) in entries)
            {
                if (npc == null) continue;

                var isPlayer = ReflectionHelper.GetProperty(npc, "is_player");
                if (isPlayer is bool b && b)
                {
                    return HttpServer.Ok(new
                    {
                        guid = ReflectionHelper.GetProperty(npc, "Guid")?.ToString(),
                        npc_name = ReflectionHelper.GetProperty(npc, "npc_name")?.ToString(),
                        pos = FormatPosition(npc),
                        hp = ReflectionHelper.GetProperty(npc, "hp")?.ToString(),
                        hp_total = ReflectionHelper.GetProperty(npc, "hp_total")?.ToString(),
                    });
                }
            }

            // 如果没找到 player 标志，返回第一个 NPC
            foreach (var (key, npc) in entries)
            {
                if (npc == null) continue;

                return HttpServer.Ok(new
                {
                    guid = ReflectionHelper.GetProperty(npc, "Guid")?.ToString(),
                    npc_name = ReflectionHelper.GetProperty(npc, "npc_name")?.ToString(),
                    pos = FormatPosition(npc),
                    note = "未找到明确的玩家标志，返回第一个NPC",
                });
            }

            return HttpServer.Error("未找到玩家或NPC");
        }
        catch (Exception ex)
        {
            return HttpServer.Error($"获取玩家失败: {ex.Message}");
        }
    }

    // === 工具方法 ===

    private static object? GetCachedTerritory()
    {
        try
        {
            // Game.main_scene.area_map.my_territory
            var csharpAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (csharpAsm == null) return null;

            var gameType = csharpAsm.GetTypes().FirstOrDefault(t => t.Name == "Game");
            if (gameType == null) return null;

            var mainSceneProp = gameType.GetProperty("main_scene", BindingFlags.Public | BindingFlags.Static);
            if (mainSceneProp == null) return null;

            var mainScene = mainSceneProp.GetValue(null);
            if (mainScene == null) return null;

            var areaMap = ReflectionHelper.GetProperty(mainScene, "area_map");
            if (areaMap == null) return null;

            var myTerritory = ReflectionHelper.GetProperty(areaMap, "my_territory");
            return myTerritory;
        }
        catch { }

        return null;
    }

    private static object? GetNpcDic()
    {
        var territory = GetCachedTerritory();
        if (territory == null) return null;
        var helper = ReflectionHelper.GetProperty(territory, "npc_helper");
        if (helper == null) return null;
        return ReflectionHelper.GetProperty(helper, "npc_dic");
    }

    private static object? GetFacilityDic()
    {
        var territory = GetCachedTerritory();
        if (territory == null) return null;
        var helper = ReflectionHelper.GetProperty(territory, "facility_helper");
        if (helper == null) return null;
        return ReflectionHelper.GetProperty(helper, "facility_dic");
    }

    private static object? GetAnimalDic()
    {
        var territory = GetCachedTerritory();
        if (territory == null) return null;
        var helper = ReflectionHelper.GetProperty(territory, "animal_helper");
        if (helper == null) return null;
        return ReflectionHelper.GetProperty(helper, "animal_list");
    }

    private static string FormatPosition(object obj)
    {
        try
        {
            var pos = ReflectionHelper.GetProperty(obj, "Position")
                   ?? ReflectionHelper.GetProperty(obj, "position")
                   ?? ReflectionHelper.GetProperty(obj, "pos");
            if (pos == null) return "unknown";

            // Vector2 或 Vector3
            var x = ReflectionHelper.GetProperty(pos, "x");
            var y = ReflectionHelper.GetProperty(pos, "y");
            if (x != null && y != null)
                return $"{x},{y}";

            return pos.ToString() ?? "unknown";
        }
        catch { return "unknown"; }
    }
}

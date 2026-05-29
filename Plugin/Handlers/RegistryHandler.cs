using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace GameMCP.Handlers;

/// <summary>
/// 读取游戏注册表 — D.Ins 中的各种字典
/// </summary>
public static class RegistryHandler
{
    public static string ListItems()
    {
        return ListFromDictionary("stuff_dic", "items", new[]
        {
            "stuff_id", "stuff_name", "stuff_type", "IsDomesticAnimal",
            "IsMonsterType", "prefab", "desc"
        });
    }

    public static string ListBuildings()
    {
        try
        {
            var d = ReflectionHelper.GetDInstance();
            if (d == null) return HttpServer.Error("D.Ins 不可用");

            var buildDic = ReflectionHelper.GetProperty(d, "build_dic");
            if (buildDic == null) return HttpServer.Error("build_dic 不可用");

            var stuffDic = ReflectionHelper.GetProperty(d, "stuff_dic");

            int total = ReflectionHelper.GetDictionaryCount(buildDic);
            var entries = ReflectionHelper.EnumerateDictionary(buildDic, 500);

            var items = new List<Dictionary<string, object?>>();
            foreach (var (key, value) in entries)
            {
                var item = new Dictionary<string, object?>
                {
                    ["key"] = key?.ToString()
                };
                if (value != null)
                {
                    // 从 stuff_dic 查名称
                    if (stuffDic != null && key is int id)
                    {
                        var stuffInfo = ReflectionHelper.GetDictionaryValue(stuffDic, id);
                        if (stuffInfo != null)
                        {
                            item["name"] = ReflectionHelper.GetProperty(stuffInfo, "stuff_name")?.ToString();
                            item["desc"] = ReflectionHelper.GetProperty(stuffInfo, "desc")?.ToString();
                        }
                    }
                    foreach (var field in new[] { "cellw", "cellh", "hp", "IsRepairByWorker",
                        "IsHoldResRange", "IsObstruct", "IsCannotDismantle" })
                    {
                        var val = ReflectionHelper.GetProperty(value, field);
                        item[field] = val?.ToString();
                    }
                }
                items.Add(item);
            }

            return HttpServer.Ok(new { total, returned = items.Count, items });
        }
        catch (Exception ex)
        {
            return HttpServer.Error($"列举建筑失败: {ex.Message}");
        }
    }

    public static string ListRecipes()
    {
        return ListFromDictionary("recipe_dic", "recipes", new[]
        {
            "recipe_id", "recipe_name", "result_stuff_id", "result_count",
            "facility_stuff_id", "craft_time"
        });
    }

    public static string GetItemDetail(int itemId)
    {
        try
        {
            var d = ReflectionHelper.GetDInstance();
            if (d == null) return HttpServer.Error("D.Ins 不可用");

            var stuffDic = ReflectionHelper.GetProperty(d, "stuff_dic");
            if (stuffDic == null) return HttpServer.Error("stuff_dic 不可用");

            var item = ReflectionHelper.GetDictionaryValue(stuffDic, itemId);
            if (item == null) return HttpServer.Error($"物品 {itemId} 不存在");

            var result = new Dictionary<string, object?>();
            result["type"] = item.GetType().FullName;
            foreach (var field in new[] { "stuff_id", "stuff_name", "stuff_type", "prefab", "desc",
                "IsDomesticAnimal", "IsMonsterType", "hp", "max_hp", "attack", "defense",
                "move_speed", "attack_speed", "price", "sell_price" })
            {
                var val = ReflectionHelper.GetProperty(item, field);
                if (val != null) result[field] = val.ToString();
            }
            return HttpServer.Ok(new { stuff_id = itemId, data = result });
        }
        catch (Exception ex)
        {
            return HttpServer.Error($"查询物品失败: {ex.Message}");
        }
    }

    public static string GetBuildingDetail(int buildingId)
    {
        try
        {
            var d = ReflectionHelper.GetDInstance();
            if (d == null) return HttpServer.Error("D.Ins 不可用");

            var buildDic = ReflectionHelper.GetProperty(d, "build_dic");
            if (buildDic == null)
            {
                buildDic = ReflectionHelper.GetProperty(d, "facility_build_info_dic");
            }
            if (buildDic == null) return HttpServer.Error("建筑注册表不可用");

            var item = ReflectionHelper.GetDictionaryValue(buildDic, buildingId);
            if (item == null) return HttpServer.Error($"建筑 {buildingId} 不存在");

            // 直接读取已知字段，避免 DumpObject 的 IL2CPP 序列化问题
            var result = new Dictionary<string, object?>();
            result["type"] = item.GetType().FullName;
            foreach (var field in new[] { "stuff_id", "facility_name", "cellw", "cellh", "w", "h",
                "hp", "max_hp", "IsRepairByWorker", "IsHoldResRange", "IsObstruct", "desc",
                "prefab", "stuff_type", "IsCannotDismantle" })
            {
                var val = ReflectionHelper.GetProperty(item, field);
                if (val != null) result[field] = val.ToString();
            }
            return HttpServer.Ok(new { stuff_id = buildingId, data = result });
        }
        catch (Exception ex)
        {
            return HttpServer.Error($"查询建筑失败: {ex.Message}");
        }
    }

    private static string ListFromDictionary(string dictName, string resultKey, string[] fields)
    {
        try
        {
            var d = ReflectionHelper.GetDInstance();
            if (d == null) return HttpServer.Error("D.Ins 不可用");

            var dict = ReflectionHelper.GetProperty(d, dictName);
            if (dict == null) return HttpServer.Error($"{dictName} 不可用");

            int total = ReflectionHelper.GetDictionaryCount(dict);
            var entries = ReflectionHelper.EnumerateDictionary(dict, 500);

            var items = new List<Dictionary<string, object?>>();
            foreach (var (key, value) in entries)
            {
                var item = new Dictionary<string, object?>
                {
                    ["key"] = key?.ToString()
                };
                if (value != null)
                {
                    foreach (var field in fields)
                    {
                        var val = ReflectionHelper.GetProperty(value, field);
                        item[field] = val?.ToString();
                    }
                }
                items.Add(item);
            }

            return HttpServer.Ok(new
            {
                total,
                returned = items.Count,
                items
            });
        }
        catch (Exception ex)
        {
            return HttpServer.Error($"列举失败: {ex.Message}");
        }
    }
}

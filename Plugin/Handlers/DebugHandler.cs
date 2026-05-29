using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GameMCP.Handlers;

public static class DebugHandler
{
    private const BindingFlags StaticBF = BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic;
    private const BindingFlags InstanceBF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public static string InspectD()
    {
        try
        {
            var d = ReflectionHelper.GetDInstance();
            if (d == null)
                return HttpServer.Ok(new { d_ins = "null", message = "D.Ins 不可用" });

            var result = new Dictionary<string, object?>
            {
                ["d_type"] = d.GetType().FullName,
                ["d_ins_available"] = true,
            };

            // 检查 stuff_dic
            InspectDictProperty(d, "stuff_dic", result);
            InspectDictProperty(d, "animal_dic", result);
            InspectDictProperty(d, "monster_dic", result);
            InspectDictProperty(d, "build_dic", result);
            InspectDictProperty(d, "tech_dic_by_facility", result);
            InspectDictProperty(d, "recipe_dic", result);

            return HttpServer.Ok(result);
        }
        catch (Exception ex)
        {
            return HttpServer.Error($"检查 D 类失败: {ex.Message}");
        }
    }

    public static string InspectGameW()
    {
        try
        {
            var csharpAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (csharpAsm == null) return HttpServer.Error("Assembly-CSharp 不可用");

            var gameType = csharpAsm.GetTypes().FirstOrDefault(t => t.Name == "Game");
            if (gameType == null) return HttpServer.Error("Game 类型不存在");

            var result = new Dictionary<string, object?>();
            result["game_type"] = gameType.FullName;

            // 检查 Game 的所有静态属性
            foreach (var prop in gameType.GetProperties(BindingFlags.Public | BindingFlags.Static))
            {
                try
                {
                    var val = prop.GetValue(null);
                    if (val == null)
                    {
                        result[$"Game.{prop.Name}"] = "null";
                        continue;
                    }
                    result[$"Game.{prop.Name}"] = new
                    {
                        type = val.GetType().FullName,
                        is_mono = val is UnityEngine.Object,
                    };
                }
                catch (Exception ex)
                {
                    result[$"Game.{prop.Name}"] = $"error: {ex.Message}";
                }
            }

            // 检查 Game 的所有静态字段
            foreach (var field in gameType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                try
                {
                    var val = field.GetValue(null);
                    if (val == null)
                    {
                        result[$"Game.{field.Name}"] = "null";
                        continue;
                    }
                    result[$"Game.{field.Name}"] = new
                    {
                        type = val.GetType().FullName,
                        is_mono = val is UnityEngine.Object,
                    };
                }
                catch (Exception ex)
                {
                    result[$"Game.{field.Name}"] = $"error: {ex.Message}";
                }
            }

            return HttpServer.Ok(result);
        }
        catch (Exception ex)
        {
            return HttpServer.Error($"检查 Game 失败: {ex.Message}");
        }
    }

    public static string InspectGameWProperties()
    {
        try
        {
            var csharpAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (csharpAsm == null) return HttpServer.Error("Assembly-CSharp 不可用");

            var gameType = csharpAsm.GetTypes().FirstOrDefault(t => t.Name == "Game");
            if (gameType == null) return HttpServer.Error("Game 类型不存在");

            var wProp = gameType.GetProperty("w", BindingFlags.Public | BindingFlags.Static);
            if (wProp == null) return HttpServer.Error("Game.w 属性不存在");

            var w = wProp.GetValue(null);
            if (w == null) return HttpServer.Ok(new { Game_w = "null", message = "Game.w 返回 null，可能还没进入存档" });

            var wType = w.GetType();
            var result = new Dictionary<string, object?>
            {
                ["w_type"] = wType.FullName,
                ["w_is_mono"] = w is UnityEngine.Object,
            };

            // 遍历 W 的所有属性
            foreach (var prop in wType.GetProperties(InstanceBF))
            {
                try
                {
                    var val = prop.GetValue(w);
                    if (val == null)
                    {
                        result[prop.Name] = "null";
                        continue;
                    }
                    var typeName = val.GetType().FullName;
                    bool isDict = val is IDictionary;
                    int count = -1;
                    if (val is IDictionary d) count = d.Count;
                    else if (val is ICollection c) count = c.Count;
                    result[prop.Name] = new
                    {
                        type = typeName,
                        is_dict = isDict,
                        count,
                    };
                }
                catch (Exception ex)
                {
                    result[prop.Name] = $"error: {ex.Message}";
                }
            }

            // 遍历 W 的所有字段
            foreach (var field in wType.GetFields(InstanceBF))
            {
                try
                {
                    var val = field.GetValue(w);
                    if (val == null)
                    {
                        result[$"field_{field.Name}"] = "null";
                        continue;
                    }
                    var typeName = val.GetType().FullName;
                    bool isDict = val is IDictionary;
                    int count = -1;
                    if (val is IDictionary d) count = d.Count;
                    else if (val is ICollection c) count = c.Count;
                    result[$"field_{field.Name}"] = new
                    {
                        type = typeName,
                        is_dict = isDict,
                        count,
                    };
                }
                catch (Exception ex)
                {
                    result[$"field_{field.Name}"] = $"error: {ex.Message}";
                }
            }

            return HttpServer.Ok(result);
        }
        catch (Exception ex)
        {
            return HttpServer.Error($"检查 Game.w 失败: {ex.Message}");
        }
    }

    private static void InspectDictProperty(object obj, string propName, Dictionary<string, object?> result)
    {
        try
        {
            var prop = obj.GetType().GetProperty(propName, InstanceBF);
            if (prop == null)
            {
                result[propName] = "property_not_found";
                return;
            }

            var val = prop.GetValue(obj);
            if (val == null)
            {
                result[propName] = "null";
                return;
            }

            var typeName = val.GetType().FullName ?? val.GetType().Name;
            bool isDict = val is IDictionary;
            bool isEnumerable = val is IEnumerable;

            int count = -1;
            if (val is IDictionary dict) count = dict.Count;
            else if (val is ICollection col) count = col.Count;

            result[propName] = new
            {
                type = typeName,
                is_dict = isDict,
                is_enumerable = isEnumerable,
                count = count,
                base_type = val.GetType().BaseType?.FullName,
                interfaces = val.GetType().GetInterfaces().Select(i => i.Name).Take(10).ToArray(),
            };
        }
        catch (Exception ex)
        {
            result[propName] = $"error: {ex.Message}";
        }
    }
}

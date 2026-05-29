using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace GameMCP.Handlers;

/// <summary>
/// Mod 验证 — 检查注册项完整性（贴图、引用等）
/// </summary>
public static class ModVerifyHandler
{
    public static string Verify()
    {
        try
        {
            var d = ReflectionHelper.GetDInstance();
            if (d == null) return HttpServer.Error("D.Ins 不可用，游戏可能未加载");

            var issues = new List<string>();
            var ok = new List<string>();

            // 检查 stuff_dic 中的物品
            var stuffDic = ReflectionHelper.GetProperty(d, "stuff_dic");
            if (stuffDic is IDictionary dict)
            {
                int checked_count = 0;
                foreach (DictionaryEntry entry in dict)
                {
                    var item = entry.Value;
                    if (item == null) continue;

                    string name = ReflectionHelper.GetProperty(item, "stuff_name")?.ToString() ?? entry.Key?.ToString() ?? "?";

                    // 检查 prefab 是否为空
                    var prefab = ReflectionHelper.GetProperty(item, "prefab");
                    if (prefab == null || (prefab is string s && string.IsNullOrEmpty(s)))
                        issues.Add($"物品 {name} (id={entry.Key}): prefab 为空");

                    checked_count++;
                    if (checked_count >= 500) break;
                }

                ok.Add($"检查了 {checked_count} 个物品定义");
            }

            return HttpServer.Ok(new
            {
                status = issues.Count == 0 ? "all_ok" : "has_issues",
                issues_count = issues.Count,
                issues = issues.Take(50).ToList(),
                ok = ok,
            });
        }
        catch (Exception ex)
        {
            return HttpServer.Error($"验证失败: {ex.Message}");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GameMCP.Handlers;

/// <summary>
/// 通用类型检查器 — 按类名搜索并 dump 对象
/// </summary>
public static class InspectHandler
{
    private const string OutputDir = @"C:\AI\MOD数据";

    public static string InspectType(string typeName)
    {
        try
        {
            var csharpAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

            if (csharpAsm == null)
                return HttpServer.Error("Assembly-CSharp 不可用");

            Type[] types;
            try { types = csharpAsm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray()!; }

            // 精确匹配优先，模糊匹配其次
            var matchedType = types.FirstOrDefault(t => t != null && t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase))
                           ?? types.FirstOrDefault(t => t != null && t.Name.Contains(typeName, StringComparison.OrdinalIgnoreCase));

            if (matchedType == null)
                return HttpServer.Error($"未找到类型: {typeName}");

            // 查找实例
            var instances = ReflectionHelper.FindInstances(matchedType, 10);
            if (instances.Count == 0)
                return HttpServer.Ok(new
                {
                    type = matchedType.FullName,
                    instance_count = 0,
                    message = "未找到实例"
                });

            var dumps = new List<object?>();
            for (int i = 0; i < Math.Min(instances.Count, 5); i++)
            {
                dumps.Add(ReflectionHelper.DumpObject(instances[i], 3));
            }

            return HttpServer.Ok(new
            {
                type = matchedType.FullName,
                instance_count = instances.Count,
                returned = dumps.Count,
                instances = dumps
            });
        }
        catch (Exception ex)
        {
            return HttpServer.Error($"检查类型失败: {ex.Message}");
        }
    }
}

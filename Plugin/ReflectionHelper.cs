using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GameMCP;

/// <summary>
/// IL2CPP 反射工具 — 用于读取游戏内部状态。
/// 基于 ObjectDumper 的反射模式，适配 D.Ins 等游戏 API。
/// </summary>
public static class ReflectionHelper
{
    private const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public
        | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

    // === D.Ins 访问 ===

    public static object? GetDInstance()
    {
        try
        {
            var csharpAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (csharpAsm == null) return null;

            var dType = csharpAsm.GetTypes().FirstOrDefault(t => t.Name == "D");
            if (dType == null) return null;

            // D.Ins 是静态属性或字段
            var insProp = dType.GetProperty("Ins", BindingFlags.Public | BindingFlags.Static);
            if (insProp != null) return insProp.GetValue(null);

            var insField = dType.GetField("Ins", BindingFlags.Public | BindingFlags.Static);
            if (insField != null) return insField.GetValue(null);

            return null;
        }
        catch { return null; }
    }

    // === 属性/字段读取 ===

    public static object? GetProperty(object obj, string name)
    {
        if (obj == null) return null;
        try
        {
            var t = obj.GetType();
            while (t != null && t != typeof(object))
            {
                var prop = t.GetProperty(name, BF);
                if (prop != null)
                {
                    try { return prop.GetValue(obj); }
                    catch { return null; }
                }
                var field = t.GetField(name, BF);
                if (field != null)
                {
                    try { return field.GetValue(obj); }
                    catch { return null; }
                }
                t = t.BaseType;
            }
        }
        catch { }
        return null;
    }

    public static int GetInt(object obj, string name)
    {
        var val = GetProperty(obj, name);
        if (val is int i) return i;
        if (val != null && int.TryParse(val.ToString(), out int parsed)) return parsed;
        return 0;
    }

    public static string GetString(object obj, string name)
    {
        return GetProperty(obj, name)?.ToString() ?? "";
    }

    // === 字典访问 (支持 IL2CPP 和 .NET 字典) ===

    public static int GetDictionaryCount(object dict)
    {
        if (dict is IDictionary idict) return idict.Count;

        // IL2CPP 字典: 通过 Count 属性
        try
        {
            var countProp = dict.GetType().GetProperty("Count", BF);
            if (countProp != null)
            {
                var val = countProp.GetValue(dict);
                if (val is int i) return i;
            }
        }
        catch { }
        return -1;
    }

    public static object? GetDictionaryValue(object dict, int key)
    {
        // 标准 .NET 字典
        if (dict is IDictionary idict)
        {
            foreach (DictionaryEntry entry in idict)
            {
                if (entry.Key is int k && k == key) return entry.Value;
                if (entry.Key?.ToString() == key.ToString()) return entry.Value;
            }
        }

        // IL2CPP 字典: 用 ContainsKey + TryGetValue
        try
        {
            var type = dict.GetType();
            var containsKey = type.GetMethod("ContainsKey", BF);
            var tryGetValue = type.GetMethod("TryGetValue", BF);
            if (containsKey != null && tryGetValue != null)
            {
                bool exists = (bool)(containsKey.Invoke(dict, new object[] { key }) ?? false);
                if (!exists) return null;

                var args = new object?[] { key, null };
                tryGetValue.Invoke(dict, args);
                return args[1];
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// 遍历 IL2CPP 字典，返回 key-value 列表
    /// </summary>
    public static List<(object key, object? value)> EnumerateDictionary(object dict, int maxCount = 100)
    {
        var result = new List<(object, object?)>();

        // 标准 .NET 字典
        if (dict is IDictionary idict)
        {
            int count = 0;
            foreach (DictionaryEntry entry in idict)
            {
                if (count >= maxCount) break;
                result.Add((entry.Key, entry.Value));
                count++;
            }
            return result;
        }

        // IL2CPP 字典: 用 GetEnumerator
        try
        {
            var type = dict.GetType();
            var getEnum = type.GetMethod("GetEnumerator", BF);
            if (getEnum == null) return result;

            var enumerator = getEnum.Invoke(dict, null);
            if (enumerator == null) return result;

            var moveNext = enumerator.GetType().GetMethod("MoveNext", BF);
            var current = enumerator.GetType().GetProperty("Current", BF);
            if (moveNext == null || current == null) return result;

            int count = 0;
            while ((bool)(moveNext.Invoke(enumerator, null) ?? false))
            {
                if (count >= maxCount) break;
                var kv = current.GetValue(enumerator);
                if (kv == null) continue;

                // KeyValuePair<TKey, TValue> — 通过 Key/Value 属性读取
                var key = GetProperty(kv, "Key");
                var value = GetProperty(kv, "Value");
                result.Add((key ?? "null", value));
                count++;
            }
        }
        catch { }

        return result;
    }

    /// <summary>
    /// 获取 IL2CPP List 的第 index 个元素
    /// </summary>
    public static object? GetListItem(object list, int index)
    {
        try
        {
            var type = list.GetType();
            var getItem = type.GetMethod("get_Item", BF);
            if (getItem != null)
                return getItem.Invoke(list, new object[] { index });
        }
        catch { }
        return null;
    }

    // === 对象 Dump ===

    public static object? DumpObject(object? obj, int maxDepth, int currentDepth = 0)
    {
        if (obj == null) return null;
        if (currentDepth >= maxDepth) return obj.ToString();

        var type = obj.GetType();

        // 简单类型
        if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal))
            return obj;

        // IntPtr / UIntPtr — IL2CPP 内部指针，无法序列化
        if (type == typeof(IntPtr) || type == typeof(UIntPtr))
            return $"[{type.Name}]";

        // Unity 基础类型跳过
        if (type.FullName != null && _skipTypes.Contains(type.FullName))
        {
            if (obj is UnityEngine.Object uObj)
                return $"[{type.Name}] \"{uObj.name}\"";
            return $"[{type.Name}]";
        }

        // 字典
        if (obj is IDictionary dict)
        {
            var result = new Dictionary<string, object?>();
            int count = 0;
            foreach (DictionaryEntry entry in dict)
            {
                if (count >= 30) { result["__truncated__"] = $"{dict.Count - count} more"; break; }
                string key = entry.Key?.ToString() ?? "null";
                result[key] = DumpObject(entry.Value, maxDepth, currentDepth + 1);
                count++;
            }
            return result;
        }

        // 集合
        if (obj is IEnumerable enumerable && obj is not string)
        {
            var list = new List<object?>();
            int count = 0;
            foreach (var item in enumerable)
            {
                if (count >= 30) break;
                list.Add(DumpObject(item, maxDepth, currentDepth + 1));
                count++;
            }
            return list;
        }

        // 复杂对象 — 遍历属性和字段
        var objDict = new Dictionary<string, object?>();
        var t = type;
        while (t != null && t != typeof(object) && !_stopTypes.Contains(t.FullName ?? ""))
        {
            foreach (var prop in t.GetProperties(BindingFlags.Instance | BindingFlags.Public
                | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (prop.GetIndexParameters().Length > 0) continue;
                try
                {
                    var val = prop.GetValue(obj);
                    objDict[$"{t.Name}.{prop.Name}"] = DumpObject(val, maxDepth, currentDepth + 1);
                }
                catch { }
            }

            foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.Public
                | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (field.FieldType == typeof(IntPtr) || field.FieldType == typeof(UIntPtr)) continue;
                try
                {
                    var val = field.GetValue(obj);
                    objDict[$"{t.Name}.{field.Name}"] = DumpObject(val, maxDepth, currentDepth + 1);
                }
                catch { }
            }

            t = t.BaseType;
        }
        return objDict;
    }

    // === 实例查找 ===

    public static List<object> FindInstances(Type type, int maxCount = 10)
    {
        var result = new List<object>();
        try
        {
            bool isUnityObject = typeof(UnityEngine.Object).IsAssignableFrom(type);
            if (isUnityObject)
            {
                // IL2CPP 中用 Resources.FindObjectsOfTypeAll(Type) 重载
                var method = typeof(UnityEngine.Resources).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "FindObjectsOfTypeAll"
                        && m.GetParameters().Length == 1
                        && m.GetParameters()[0].ParameterType == typeof(Type));

                if (method != null)
                {
                    var array = method.Invoke(null, new object[] { type }) as Array;
                    if (array != null)
                    {
                        int count = 0;
                        foreach (var obj in array)
                        {
                            if (obj != null && count < maxCount)
                            {
                                result.Add(obj);
                                count++;
                            }
                        }
                    }
                }
            }
        }
        catch { }
        return result;
    }

    // === 类型过滤 ===

    private static readonly HashSet<string> _skipTypes = new()
    {
        "UnityEngine.Transform", "UnityEngine.GameObject", "UnityEngine.Material",
        "UnityEngine.Mesh", "UnityEngine.Shader", "UnityEngine.Texture",
        "UnityEngine.Animator", "UnityEngine.Animation", "UnityEngine.Camera",
        "UnityEngine.Collider", "UnityEngine.Rigidbody", "UnityEngine.RectTransform",
        "UnityEngine.CanvasRenderer", "UnityEngine.Skybox", "UnityEngine.Light",
        "UnityEngine.Renderer", "UnityEngine.SpriteRenderer", "UnityEngine.Sprite",
        "UnityEngine.AudioSource", "UnityEngine.AudioClip",
    };

    private static readonly HashSet<string> _stopTypes = new()
    {
        "UnityEngine.MonoBehaviour", "UnityEngine.Behaviour",
        "UnityEngine.Component", "UnityEngine.Object",
        "UnityEngine.ScriptableObject", "UnityEngine.EventSystems.UIBehaviour",
    };
}

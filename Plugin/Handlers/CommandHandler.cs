using System;
using System.Text.Json;

namespace GameMCP.Handlers;

/// <summary>
/// 执行游戏命令 — 传送、给物品等调试操作
/// </summary>
public static class CommandHandler
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static string Execute(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return HttpServer.Error("请求体为空");

        CommandRequest? cmd;
        try
        {
            cmd = JsonSerializer.Deserialize<CommandRequest>(body, _jsonOptions);
        }
        catch (Exception ex)
        {
            return HttpServer.Error($"JSON 解析失败: {ex.Message}");
        }

        if (cmd == null || string.IsNullOrWhiteSpace(cmd.Command))
            return HttpServer.Error("命令不能为空");

        try
        {
            return cmd.Command.ToLower() switch
            {
                "give_item" => GiveItem(cmd),
                "teleport" => Teleport(cmd),
                "heal" => Heal(cmd),
                "spawn" => Spawn(cmd),
                _ => HttpServer.Error($"未知命令: {cmd.Command}"),
            };
        }
        catch (Exception ex)
        {
            return HttpServer.Error($"命令执行失败: {ex.Message}");
        }
    }

    private static string GiveItem(CommandRequest cmd)
    {
        // 预留 — 需要找到游戏的背包系统 API
        return HttpServer.Ok(new
        {
            command = "give_item",
            status = "not_implemented",
            message = "需要接入背包系统 API，请提供相关代码位置"
        });
    }

    private static string Teleport(CommandRequest cmd)
    {
        return HttpServer.Ok(new
        {
            command = "teleport",
            status = "not_implemented",
            message = "需要接入移动系统 API"
        });
    }

    private static string Heal(CommandRequest cmd)
    {
        return HttpServer.Ok(new
        {
            command = "heal",
            status = "not_implemented",
            message = "需要接入生命值系统 API"
        });
    }

    private static string Spawn(CommandRequest cmd)
    {
        return HttpServer.Ok(new
        {
            command = "spawn",
            status = "not_implemented",
            message = "需要接入实体生成 API"
        });
    }

    private class CommandRequest
    {
        public string Command { get; set; } = "";
        public int? StuffId { get; set; }
        public int? Count { get; set; }
        public float? X { get; set; }
        public float? Y { get; set; }
        public string? Target { get; set; }
    }
}

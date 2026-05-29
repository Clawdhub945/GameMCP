#!/usr/bin/env node

import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import * as game from "./game-client.js";

const server = new McpServer({
  name: "game-mcp",
  version: "1.0.0",
  description: "领地：种田与征战 — Mod 开发辅助 MCP",
});

// ===== 游戏状态 =====

server.tool(
  "game_status",
  "查看游戏运行状态：是否在游戏内、当前场景、mod 加载状态、注册表统计",
  {},
  async () => {
    const { data, error } = await game.getStatus();
    if (error) return { content: [{ type: "text", text: `错误: ${error}` }] };
    return { content: [{ type: "text", text: JSON.stringify(data, null, 2) }] };
  }
);

// ===== 注册表查询 =====

server.tool(
  "list_items",
  "列举所有注册的物品定义 (StuffDef)，返回物品 ID、名称、类型等基础信息",
  {},
  async () => {
    const { data, error } = await game.listItems();
    if (error) return { content: [{ type: "text", text: `错误: ${error}` }] };
    return { content: [{ type: "text", text: JSON.stringify(data, null, 2) }] };
  }
);

server.tool(
  "list_buildings",
  "列举所有注册的建筑定义 (FacilityDef)，返回建筑 ID、名称、尺寸等信息",
  {},
  async () => {
    const { data, error } = await game.listBuildings();
    if (error) return { content: [{ type: "text", text: `错误: ${error}` }] };
    return { content: [{ type: "text", text: JSON.stringify(data, null, 2) }] };
  }
);

server.tool(
  "list_recipes",
  "列举所有注册的配方 (RecipeDef)，返回配方 ID、输入/输出物品、制作时间等",
  {},
  async () => {
    const { data, error } = await game.listRecipes();
    if (error) return { content: [{ type: "text", text: `错误: ${error}` }] };
    return { content: [{ type: "text", text: JSON.stringify(data, null, 2) }] };
  }
);

server.tool(
  "get_item_detail",
  "查询指定物品的全部属性，用于验证 mod 注册的物品是否正确",
  { stuff_id: z.number().describe("物品 stuff_id") },
  async ({ stuff_id }) => {
    const { data, error } = await game.getItemDetail(stuff_id);
    if (error) return { content: [{ type: "text", text: `错误: ${error}` }] };
    return { content: [{ type: "text", text: JSON.stringify(data, null, 2) }] };
  }
);

server.tool(
  "get_building_detail",
  "查询指定建筑的全部属性，用于验证 mod 注册的建筑定义是否正确",
  { stuff_id: z.number().describe("建筑 stuff_id") },
  async ({ stuff_id }) => {
    const { data, error } = await game.getBuildingDetail(stuff_id);
    if (error) return { content: [{ type: "text", text: `错误: ${error}` }] };
    return { content: [{ type: "text", text: JSON.stringify(data, null, 2) }] };
  }
);

// ===== 运行时世界查询 =====

server.tool(
  "list_facilities",
  "列举当前游戏中所有运行时设施实例，返回位置、状态、库存等信息。需要先进入游戏。",
  {},
  async () => {
    const { data, error } = await game.listFacilities();
    if (error) return { content: [{ type: "text", text: `错误: ${error}` }] };
    return { content: [{ type: "text", text: JSON.stringify(data, null, 2) }] };
  }
);

server.tool(
  "list_npcs",
  "列举当前游戏中所有 NPC，返回名称、位置、血量、状态等。需要先进入游戏。",
  {},
  async () => {
    const { data, error } = await game.listNpcs();
    if (error) return { content: [{ type: "text", text: `错误: ${error}` }] };
    return { content: [{ type: "text", text: JSON.stringify(data, null, 2) }] };
  }
);

server.tool(
  "list_animals",
  "列举当前游戏中所有动物，返回种类、位置、血量等。需要先进入游戏。",
  {},
  async () => {
    const { data, error } = await game.listAnimals();
    if (error) return { content: [{ type: "text", text: `错误: ${error}` }] };
    return { content: [{ type: "text", text: JSON.stringify(data, null, 2) }] };
  }
);

server.tool(
  "get_player",
  "获取玩家角色信息，包括位置、血量、背包等。需要先进入游戏。",
  {},
  async () => {
    const { data, error } = await game.getPlayer();
    if (error) return { content: [{ type: "text", text: `错误: ${error}` }] };
    return { content: [{ type: "text", text: JSON.stringify(data, null, 2) }] };
  }
);

// ===== 对象检查 =====

server.tool(
  "inspect_type",
  "通用类型检查器：按类名搜索游戏内对象并 dump 其字段。可用于检查任意游戏类的实例状态。",
  {
    type_name: z
      .string()
      .describe("要检查的类名，如 Facility、Npc、Animal、StuffInfo 等"),
  },
  async ({ type_name }) => {
    const { data, error } = await game.inspectType(type_name);
    if (error) return { content: [{ type: "text", text: `错误: ${error}` }] };
    return { content: [{ type: "text", text: JSON.stringify(data, null, 2) }] };
  }
);

// ===== Mod 验证 =====

server.tool(
  "verify_mod",
  "验证 mod 注册项完整性：检查物品定义是否有效（prefab、贴图引用等）",
  {},
  async () => {
    const { data, error } = await game.verifyMod();
    if (error) return { content: [{ type: "text", text: `错误: ${error}` }] };
    return { content: [{ type: "text", text: JSON.stringify(data, null, 2) }] };
  }
);

// ===== 调试命令 =====

server.tool(
  "execute_command",
  "执行游戏调试命令（传送、给物品等）。目前为预留接口，需要接入对应游戏 API。",
  {
    command: z
      .enum(["give_item", "teleport", "heal", "spawn"])
      .describe("命令类型"),
    stuff_id: z.number().optional().describe("物品/实体 ID"),
    count: z.number().optional().describe("数量"),
    x: z.number().optional().describe("X 坐标"),
    y: z.number().optional().describe("Y 坐标"),
    target: z.string().optional().describe("目标名称"),
  },
  async (args) => {
    const { data, error } = await game.executeCommand(args);
    if (error) return { content: [{ type: "text", text: `错误: ${error}` }] };
    return { content: [{ type: "text", text: JSON.stringify(data, null, 2) }] };
  }
);

// ===== 启动 =====

async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
  console.error("GameMCP Server 已启动");
}

main().catch((err) => {
  console.error("启动失败:", err);
  process.exit(1);
});

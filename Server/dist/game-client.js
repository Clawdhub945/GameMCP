const BASE_URL = process.env.GAME_MCP_URL || "http://localhost:23333";
async function request(path, options) {
    try {
        const url = `${BASE_URL}${path}`;
        const resp = await fetch(url, {
            ...options,
            signal: AbortSignal.timeout(10_000),
        });
        const text = await resp.text();
        let json;
        try {
            json = JSON.parse(text);
        }
        catch {
            return { error: `非 JSON 响应: ${text.slice(0, 200)}` };
        }
        if (json.error)
            return { error: json.error };
        return { data: json };
    }
    catch (err) {
        if (err.name === "TimeoutError" || err.code === "ETIMEDOUT") {
            return { error: "连接游戏超时 (10s)，游戏是否在运行？" };
        }
        if (err.code === "ECONNREFUSED") {
            return {
                error: "无法连接游戏 (localhost:23333)，请确认 GameMCP 插件已加载",
            };
        }
        return { error: `请求失败: ${err.message}` };
    }
}
// === Status ===
export async function getStatus() {
    return request("/api/status");
}
// === Registry ===
export async function listItems() {
    return request("/api/registry/items");
}
export async function listBuildings() {
    return request("/api/registry/buildings");
}
export async function listRecipes() {
    return request("/api/registry/recipes");
}
export async function getItemDetail(id) {
    return request(`/api/registry/item/${id}`);
}
export async function getBuildingDetail(id) {
    return request(`/api/registry/building/${id}`);
}
// === World ===
export async function listFacilities() {
    return request("/api/world/facilities");
}
export async function listNpcs() {
    return request("/api/world/npcs");
}
export async function listAnimals() {
    return request("/api/world/animals");
}
export async function getPlayer() {
    return request("/api/world/player");
}
// === Inspect ===
export async function inspectType(typeName) {
    return request(`/api/inspect/${encodeURIComponent(typeName)}`);
}
// === Command ===
export async function executeCommand(cmd) {
    return request("/api/command", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(cmd),
    });
}
// === Mod Verify ===
export async function verifyMod() {
    return request("/api/mod/verify");
}
//# sourceMappingURL=game-client.js.map
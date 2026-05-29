export interface ApiResponse<T = any> {
    data?: T;
    error?: string;
}
export declare function getStatus(): Promise<ApiResponse<any>>;
export declare function listItems(): Promise<ApiResponse<any>>;
export declare function listBuildings(): Promise<ApiResponse<any>>;
export declare function listRecipes(): Promise<ApiResponse<any>>;
export declare function getItemDetail(id: number): Promise<ApiResponse<any>>;
export declare function getBuildingDetail(id: number): Promise<ApiResponse<any>>;
export declare function listFacilities(): Promise<ApiResponse<any>>;
export declare function listNpcs(): Promise<ApiResponse<any>>;
export declare function listAnimals(): Promise<ApiResponse<any>>;
export declare function getPlayer(): Promise<ApiResponse<any>>;
export declare function inspectType(typeName: string): Promise<ApiResponse<any>>;
export declare function executeCommand(cmd: Record<string, any>): Promise<ApiResponse<any>>;
export declare function verifyMod(): Promise<ApiResponse<any>>;
//# sourceMappingURL=game-client.d.ts.map
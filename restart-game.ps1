# GameMCP 快速重启脚本
# 用法: powershell -ExecutionPolicy Bypass -File C:\AI\mod\GameMCP\restart-game.ps1

$GameExe = "C:\Program Files (x86)\Steam\steamapps\common\Territory\Territory.exe"
$PluginDll = "C:\AI\mod\GameMCP\Plugin\bin\Release\net6.0\GameMCPPlugin.dll"
$DeployDir = "C:\TerritoryModTest\GameMCP"

# 1. 编译
Write-Host "[1/4] 编译插件..." -ForegroundColor Cyan
$buildResult = dotnet build "C:\AI\mod\GameMCP\Plugin" -c Release --nologo -v q 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "编译失败:" -ForegroundColor Red
    Write-Host $buildResult
    exit 1
}
Write-Host "  编译成功" -ForegroundColor Green

# 2. 关闭游戏
Write-Host "[2/4] 关闭游戏..." -ForegroundColor Cyan
$proc = Get-Process -Name "Territory" -ErrorAction SilentlyContinue
if ($proc) {
    Stop-Process -Name "Territory" -Force
    Start-Sleep -Seconds 2
    Write-Host "  游戏已关闭" -ForegroundColor Green
} else {
    Write-Host "  游戏未在运行" -ForegroundColor Yellow
}

# 3. 部署 DLL
Write-Host "[3/4] 部署 DLL..." -ForegroundColor Cyan
New-Item -ItemType Directory -Path $DeployDir -Force | Out-Null
Copy-Item $PluginDll $DeployDir -Force
Write-Host "  已部署到 $DeployDir" -ForegroundColor Green

# 4. 启动游戏
Write-Host "[4/4] 启动游戏..." -ForegroundColor Cyan
Start-Process $GameExe
Write-Host "  游戏启动中，等待加载..." -ForegroundColor Green

# 等待游戏窗口出现
$timeout = 60
$elapsed = 0
while ($elapsed -lt $timeout) {
    Start-Sleep -Seconds 2
    $elapsed += 2
    $gameProc = Get-Process -Name "Territory" -ErrorAction SilentlyContinue
    if ($gameProc -and $gameProc.MainWindowTitle) {
        Write-Host "  游戏已启动 (PID: $($gameProc.Id))" -ForegroundColor Green
        Write-Host ""
        Write-Host "完成! 等待游戏加载到主界面后即可使用 MCP" -ForegroundColor Cyan
        exit 0
    }
}
Write-Host "  游戏启动超时，请手动检查" -ForegroundColor Yellow

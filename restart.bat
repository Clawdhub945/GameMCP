@echo off
chcp 65001 >nul 2>&1
echo [1/5] Building plugin...
cd /d "C:\AI\mod\GameMCP\Plugin"
dotnet build -c Release --nologo -v q
if errorlevel 1 (
    echo Build failed!
    pause
    exit /b 1
)
echo   Build OK

echo [2/5] Closing game...
taskkill /IM Territory.exe /F >nul 2>&1
if errorlevel 1 (
    echo   Game not running
) else (
    echo   Game closed
    timeout /t 3 /nobreak >nul
)

echo [3/5] Deploying DLL...
copy /Y "C:\AI\mod\GameMCP\Plugin\bin\Release\net6.0\GameMCPPlugin.dll" "C:\TerritoryModTest\GameMCP\" >nul
echo   Deployed to TerritoryModTest

echo [4/5] Clearing BepInEx cache...
del /Q "C:\Program Files (x86)\Steam\steamapps\common\Territory\BepInEx\cache\chainloader_typeloader.dat" >nul 2>&1
echo   Cache cleared

echo [5/5] Starting game...
cd /d "C:\Program Files (x86)\Steam\steamapps\common\Territory"
start "" Territory.exe
echo   Game starting...
echo.
echo Done! Wait for game to load, enter a save, then use MCP.
timeout /t 3

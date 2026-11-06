@echo off
echo Starting FurnaceMonitor Backend API...
start cmd /k "cd backend\FurnaceMonitor\src\FurnaceMonitor.Api && dotnet run"

echo Starting FurnaceMonitor Frontend UI (TV Compatible Mode)...
start cmd /k "cd frontend\furnace-ui && npm run build && npm run preview -- --host --port 5173"

echo.
echo =======================================================
echo Both servers are starting in separate windows.
echo The Backend API will be available at: http://localhost:5005
echo.
echo The Frontend UI will be available on THIS computer at: 
echo   http://localhost:5173
echo.
echo To view the display on your TV, open the TV's web browser and go to:
echo   http://192.168.1.15:5173
echo.
echo Note: If a Windows Firewall popup appears, click "Allow access".
echo =======================================================
echo.
pause

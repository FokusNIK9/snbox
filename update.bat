@echo off
cd /d "%~dp0"
echo Pulling latest updates from GitHub...
git pull origin main
if errorlevel 1 (
    echo.
    echo [ERROR] Pull failed! Check your connection or conflicts.
    pause
    exit /b 1
)
echo.
echo Done! 
timeout /t 5

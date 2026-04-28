@echo off
cd /d "%~dp0"
echo Pulling latest updates...
git pull origin main
echo.
echo Done! Press any key to close.
pause >nul

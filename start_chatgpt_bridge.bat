@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion
cd /d "%~dp0"

echo ============================================================
echo   Ozmium MCP Bridge for ChatGPT
echo ============================================================
echo.

REM Check if Python is installed
python --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Python not found. Please install Python from https://python.org/
    pause
    exit /b 1
)

REM Check if dependencies are already installed by trying to import one
python -c "import fastapi" >nul 2>&1
if errorlevel 1 (
    echo [INFO] Installing dependencies...
    pip install fastapi uvicorn requests --quiet
    if errorlevel 1 (
        echo [ERROR] Failed to install dependencies.
        pause
        exit /b 1
    )
) else (
    echo [INFO] Dependencies are already satisfied.
)

echo.
echo    Make sure s&box is running and Ozmium MCP Server is active!
echo.

if "%~1"=="" (
    echo Usage with ngrok URL:
    echo        start_chatgpt_bridge.bat https://your-ngrok-url.ngrok-free.app
    echo.
    echo Running without external URL...
    echo ============================================================
    python AI_Tools\chatgpt_mcp_proxy.py
) else (
    echo Server URL: %~1
    echo Import in ChatGPT Actions: %~1/openapi.json
    echo ============================================================
    python AI_Tools\chatgpt_mcp_proxy.py --server-url %~1
)
pause

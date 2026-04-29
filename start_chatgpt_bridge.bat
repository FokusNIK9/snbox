@echo off
chcp 65001 >nul
echo ============================================================
echo   Ozmium MCP Bridge for ChatGPT
echo ============================================================
echo.

REM Check if Python is installed
python --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Python not found. Install Python: https://python.org/downloads
    pause
    exit /b 1
)

REM Install dependencies
echo [1/3] Installing dependencies...
pip install fastapi uvicorn requests --quiet
if errorlevel 1 (
    echo [ERROR] Failed to install dependencies
    pause
    exit /b 1
)

echo [2/3] Starting ChatGPT - MCP bridge...
echo.
echo    Make sure s-box is running and Ozmium MCP Server is active!
echo.

if "%~1"=="" (
    echo [3/3] Usage with ngrok URL:
    echo        start_chatgpt_bridge.bat https://your-ngrok-url.ngrok-free.app
    echo.
    echo    Or without URL ^(add servers manually in ChatGPT^):
    echo        start_chatgpt_bridge.bat
    echo.
    echo ============================================================
    python chatgpt_mcp_proxy.py
) else (
    echo [3/3] Server URL: %~1
    echo    Import in ChatGPT Actions: %~1/openapi.json
    echo.
    echo ============================================================
    python chatgpt_mcp_proxy.py --server-url %~1
)
pause

@echo off
chcp 65001 >nul
echo ============================================================
echo   Ozmium MCP Bridge for ChatGPT
echo ============================================================
echo.

REM Check if Python is installed
python --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Python не найден. Установите Python: https://python.org/downloads
    pause
    exit /b 1
)

REM Install dependencies
echo [1/3] Устанавливаю зависимости...
pip install fastapi uvicorn requests --quiet
if errorlevel 1 (
    echo [ERROR] Не удалось установить зависимости
    pause
    exit /b 1
)

echo [2/3] Запускаю мост ChatGPT - MCP...
echo.
echo    Убедитесь что s&box запущен и Ozmium MCP Server активен!
echo.
echo [3/3] После запуска откройте второй терминал и выполните:
echo        ngrok http 8001
echo    Затем импортируйте URL в ChatGPT Actions:
echo        https://ваш-ngrok-url/openapi.json
echo.
echo ============================================================

python chatgpt_mcp_proxy.py
pause

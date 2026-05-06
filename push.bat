@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"

echo.
echo === Pushing changes to GitHub ===
echo.

git add -A
git status

echo.
set /p MSG="Commit message (or press Enter for default 'update from local agent'): "
if "!MSG!"=="" set MSG=update from local agent

git commit -m "!MSG!"
if errorlevel 1 (
    echo.
    echo [INFO] No changes to commit or commit failed.
) else (
    echo.
    echo === Pushing to origin main ===
    git push origin main
    if errorlevel 1 (
        echo.
        echo [ERROR] Push failed! Check your connection or token.
        pause
        exit /b 1
    )
)

echo.
echo Done! Changes pushed to GitHub.
echo.
timeout /t 5

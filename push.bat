@echo off
cd /d "%~dp0"
echo.
echo === Pushing changes to GitHub ===
echo.
git add -A
git status
echo.
set /p MSG="Commit message (or press Enter for default): "
if "%MSG%"=="" set MSG=update from local agent
git commit -m "%MSG%"
git push origin main
echo.
echo Done! Changes pushed to GitHub.
echo Now tell Devin to review: paste the review prompt.
echo.
pause >nul

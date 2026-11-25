@echo off
chcp 65001 >nul
title Gunplay Siege Defense - Local Preview
rem ============================================================
rem  本地预览网页版（docs 目录）
rem  Unity WebGL 不能用 file:// 直接打开，必须通过 http 服务访问
rem ============================================================

set "PORT=8766"
cd /d "%~dp0docs"

if not exist "index.html" (
    echo [ERROR] docs index.html not found. Run Unity menu: Gunplay / 6. Export GitHub Pages Site
    pause
    exit /b 1
)

set "PY="
where python >nul 2>nul && set "PY=python"
if not defined PY where py >nul 2>nul && set "PY=py -3"
if not defined PY set "PY=C:\Users\Administrator\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe"

echo.
echo   游戏地址: http://127.0.0.1:%PORT%/index.html
echo   关闭此窗口即停止服务
echo.

start "" "http://127.0.0.1:%PORT%/index.html"

%PY% -m http.server %PORT% --bind 127.0.0.1

pause
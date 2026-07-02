@echo off
setlocal
cd /d "%~dp0"
powershell -ExecutionPolicy Bypass -File "%~dp0deploy_2026.ps1" %*
if errorlevel 1 pause

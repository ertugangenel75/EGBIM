@echo off
setlocal
cd /d "%~dp0"
dotnet build "src\EGBIMOTO.Addin\EGBIMOTO.Addin.csproj" -c Release -p:RevitVersion=2026 -p:Platform=x64
if errorlevel 1 pause

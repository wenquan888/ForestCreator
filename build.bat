@echo off
setlocal
rem One-click build for ForestCreator (Thunderstore).
rem Compiles the mod and copies the resulting dll into ThunderStore\Forest Creator.
cd /d "%~dp0"

rem dotnet is not on PATH on this machine; fall back to the full install path.
set "DOTNET=dotnet"
where dotnet >nul 2>nul
if errorlevel 1 set "DOTNET=C:\Program Files\dotnet\dotnet.exe"

echo === Building ForestCreator (Release) ===
"%DOTNET%" build "ForestCreator.csproj" -c Release -v minimal
if errorlevel 1 (
    echo BUILD FAILED
    exit /b 1
)

set "SRC=bin\Release\netstandard2.0\ForestCreator.dll"
set "DSTDIR=ThunderStore\Forest Creator"
if not exist "%DSTDIR%" mkdir "%DSTDIR%"
copy /Y "%SRC%" "%DSTDIR%\ForestCreator.dll" >nul
if errorlevel 1 (
    echo COPY FAILED
    exit /b 1
)

echo === Deployed: %DSTDIR%\ForestCreator.dll ===
echo Done.
endlocal

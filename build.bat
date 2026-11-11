@echo off
setlocal
rem One-click build for ForestCreator (Thunderstore).
rem Compiles the mod and stages everything Thunderstore needs inside ThunderStore\Forest Creator.
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

set "OUT=bin\Release\netstandard2.0"
set "DSTDIR=ThunderStore\Forest Creator"
if not exist "%DSTDIR%" mkdir "%DSTDIR%"

copy /Y "%OUT%\ForestCreator.dll" "%DSTDIR%\ForestCreator.dll" >nul
if errorlevel 1 (
    echo COPY DLL FAILED
    exit /b 1
)

rem Every image the mod draws lives here: cornerstone icons, Town Hall frame,
rem button, card face, card header, avatar ring and the 9-slice border sidecars.
if exist "%DSTDIR%\Assets" rmdir /S /Q "%DSTDIR%\Assets"
xcopy /E /I /Y "%OUT%\Assets" "%DSTDIR%\Assets" >nul
if errorlevel 1 (
    echo COPY ASSETS FAILED
    exit /b 1
)

rem README lives at the project root and is mirrored into the package; CHANGELOG already sits there.
copy /Y "README.md" "%DSTDIR%\README.md" >nul

echo === Deployed: %DSTDIR%\ForestCreator.dll ===
echo Done.
endlocal

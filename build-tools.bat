@echo off
rem ============================================================================
rem build-tools.bat - one-click build of the official Luban toolchain.
rem
rem Builds:  Tools\Luban  (main generator)
rem           Tools\Luban.Agent  (agent CLI: validate / list-tables / describe / schema)
rem           Tools\Luban.Mcp    (stdio MCP server)
rem
rem Note: Git Bash / minimal shells strip or empty standard Windows variables
rem (ProgramFiles(x86), ALLUSERSPROFILE, ...). NuGet's machine-wide settings
rem lookup then crashes with "Value cannot be null (Parameter 'path1')" during
rem restore. This script fills any missing/empty ones before invoking dotnet.
rem ============================================================================
setlocal
cd /d "%~dp0"

if not defined SystemRoot set "SystemRoot=C:\Windows"
if "%SystemRoot%"=="" set "SystemRoot=C:\Windows"
if not defined windir set "windir=C:\Windows"
if "%windir%"=="" set "windir=C:\Windows"
if not defined SystemDrive set "SystemDrive=C:"
if "%SystemDrive%"=="" set "SystemDrive=C:"
if not defined OS set "OS=Windows_NT"
if not defined ProgramData set "ProgramData=C:\ProgramData"
if "%ProgramData%"=="" set "ProgramData=C:\ProgramData"
if not defined ALLUSERSPROFILE set "ALLUSERSPROFILE=C:\ProgramData"
if "%ALLUSERSPROFILE%"=="" set "ALLUSERSPROFILE=C:\ProgramData"
if not defined ProgramFiles set "ProgramFiles=C:\Program Files"
if "%ProgramFiles%"=="" set "ProgramFiles=C:\Program Files"
if "%ProgramFiles(x86)%"=="" set "ProgramFiles(x86)=C:\Program Files (x86)"
if not defined ProgramW6432 set "ProgramW6432=C:\Program Files"
if "%ProgramW6432%"=="" set "ProgramW6432=C:\Program Files"
if not defined CommonProgramFiles set "CommonProgramFiles=C:\Program Files\Common Files"
if "%CommonProgramFiles%"=="" set "CommonProgramFiles=C:\Program Files\Common Files"
if "%CommonProgramFiles(x86)%"=="" set "CommonProgramFiles(x86)=C:\Program Files (x86)\Common Files"

set "DOTNET="
where dotnet >nul 2>nul
if not errorlevel 1 set "DOTNET=dotnet"
if not defined DOTNET if exist "C:\Program Files\dotnet\dotnet.exe" set "DOTNET=C:\Program Files\dotnet\dotnet.exe"
if not defined DOTNET (
  echo [build-tools] dotnet SDK not found. Install .NET 8 SDK ^(or newer^) or add dotnet to PATH.
  exit /b 1
)

echo [build-tools] Using dotnet: %DOTNET%
"%DOTNET%" --version
if errorlevel 1 exit /b 1

set FAILED=0

echo [build-tools] Building src\Luban ...
"%DOTNET%" build src\Luban\Luban.csproj -c Release -o Tools\Luban
if errorlevel 1 set FAILED=1

echo [build-tools] Building src\Luban.Agent ...
"%DOTNET%" build src\Luban.Agent\Luban.Agent.csproj -c Release -o Tools\Luban.Agent
if errorlevel 1 set FAILED=1

echo [build-tools] Building src\Luban.Mcp ...
"%DOTNET%" build src\Luban.Mcp\Luban.Mcp.csproj -c Release -o Tools\Luban.Mcp
if errorlevel 1 set FAILED=1

if "%FAILED%"=="1" (
  echo [build-tools] FAILED - see messages above.
  exit /b 1
)

echo [build-tools] SUCCESS. Outputs under %cd%\Tools:
echo   Tools\Luban        main generator ^(Luban.dll^)
echo   Tools\Luban.Agent  agent CLI ^(validate / list-tables / describe / schema / capabilities^)
echo   Tools\Luban.Mcp    stdio MCP server
exit /b 0

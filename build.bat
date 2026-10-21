@echo off
setlocal
cd /d "%~dp0"

set APP=src\IoTCommTool.App\IoTCommTool.App.csproj
set RIDS=%*
if "%RIDS%"=="" set RIDS=win-x64 linux-x64 osx-x64

for %%r in (%RIDS%) do (
  echo ==^> Publishing %%r
  dotnet publish %APP% -c Release -r %%r --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\%%r || exit /b 1
)

echo Done. Output under publish\
endlocal

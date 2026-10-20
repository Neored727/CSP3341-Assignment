@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo A .NET 10 SDK is required. Open this solution in Visual Studio with .NET desktop development installed.
  pause
  exit /b 1
)
dotnet publish "src\StudentRecordSystem.App\StudentRecordSystem.App.csproj" -c Release -r win-x64 --self-contained true -o "WindowsApp"
if errorlevel 1 (
  echo Publish failed. Read the build messages above.
  pause
  exit /b 1
)
echo Ready: WindowsApp\StudentRecordSystem.exe
echo Keep the entire WindowsApp folder together when sharing it.
pause

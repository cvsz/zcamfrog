@echo off
setlocal
cd /d "%~dp0"
set "EXE=%~dp0src\CamfrogMultiID.App\bin\Release\net8.0-windows\win-x64\publish\CamfrogMultiID.exe"
echo ==============================================
echo Camfrog Multi-ID UI Diagnostic Launcher V11
echo ==============================================
if not exist "%EXE%" (
  echo ERROR: Published executable was not found:
  echo %EXE%
  exit /b 2
)
start "" "%EXE%"
timeout /t 3 /nobreak >nul
tasklist /fi "IMAGENAME eq CamfrogMultiID.exe" | find /i "CamfrogMultiID.exe" >nul
if errorlevel 1 (
  echo PROCESS EXITED.
  echo Check:
  echo %LOCALAPPDATA%\CamfrogMultiID\startup-error.log
  echo %LOCALAPPDATA%\CamfrogMultiID\logs\app.log
  exit /b 1
)
echo PROCESS IS RUNNING.
echo.
echo If no window is visible, check the startup log:
echo %LOCALAPPDATA%\CamfrogMultiID\startup-error.log
echo.
pause
exit /b 0

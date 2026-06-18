@echo off
setlocal

set "PROJECT_DIR=%~dp0"
set "PUBLISH_DIR=%PROJECT_DIR%bin\Release\net8.0-windows10.0.19041.0\publish"
set "EXE_NAME=BluetoothAudioReceiver.exe"

echo ========================================
echo   Bluetooth Audio Receiver - Build
echo ========================================
echo.

echo [1/2] Publishing...
dotnet publish "%PROJECT_DIR%BluetoothAudioReceiver.csproj" -c Release -p:PublishSingleFile=true -o "%PUBLISH_DIR%"
if %ERRORLEVEL% NEQ 0 (
    echo Build failed!
    exit /b 1
)

if not exist "%PUBLISH_DIR%\%EXE_NAME%" (
    echo EXE not found at "%PUBLISH_DIR%\%EXE_NAME%"
    exit /b 1
)

echo.
echo [2/2] Copying to project root...
copy /Y "%PUBLISH_DIR%\%EXE_NAME%" "%PROJECT_DIR%\%EXE_NAME%" >nul
if %ERRORLEVEL% NEQ 0 (
    echo Copy failed!
    exit /b 1
)

echo.
echo ========================================
echo   Done: %PROJECT_DIR%%EXE_NAME%
echo ========================================
endlocal

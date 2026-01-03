@echo off
echo ========================================
echo  KICK Life Support - Deployment Script
echo ========================================
echo.

REM Build the project in Release mode
echo Building project...
dotnet build -c Release
if %ERRORLEVEL% NEQ 0 (
    echo Build failed!
    pause
    exit /b 1
)
echo Build successful!
echo.

REM Define paths
set SOURCE_DLL=bin\Release\net472\KickLifeSupport.dll
set SOURCE_VERS=GameData\KickLifeSupport\KickLifeSupport.version
set SOURCE_CFG=GameData\KickLifeSupport\KickLifeSupport.cfg
set SOURCE_SETTINGS_CFG=GameData\KickLifeSupport\Settings.cfg
set SOURCE_RADIATORS_CFG=GameData\KickLifeSupport\Radiators.cfg
set SOURCE_RESOURCE=GameData\KickLifeSupport\Resources\LithiumHydroxide.cfg
set SOURCE_PARTS=GameData\KickLifeSupport\Parts
set SOURCE_PATCHES=GameData\KickLifeSupport\Patches
set DEST_DIR=H:\SteamLibrary\steamapps\common\Kerbal Space Program\GameData\KickLifeSupport
set DEST_RESOURCES=%DEST_DIR%\Resources
set DEST_PARTS=%DEST_DIR%\Parts
set DEST_PATCHES=%DEST_DIR%\Patches

REM Create destination directories if they don't exist
if not exist "%DEST_DIR%" mkdir "%DEST_DIR%"
if not exist "%DEST_RESOURCES%" mkdir "%DEST_RESOURCES%"
if not exist "%DEST_PARTS%" mkdir "%DEST_PARTS%"
if not exist "%DEST_PATCHES%" mkdir "%DEST_PATCHES%"

REM Copy files
echo Deploying files to KSP...
copy /Y "%SOURCE_DLL%" "%DEST_DIR%\KickLifeSupport.dll"
copy /Y "%SOURCE_CFG%" "%DEST_DIR%\KickLifeSupport.cfg"
copy /Y "%SOURCE_VERS%" "%DEST_DIR%\KickLifeSupport.version"
copy /Y "%SOURCE_SETTINGS_CFG%" "%DEST_DIR%\Settings.cfg"
copy /Y "%SOURCE_RADIATORS_CFG%" "%DEST_DIR%\Radiators.cfg"
copy /Y "%SOURCE_RESOURCE%" "%DEST_RESOURCES%\LithiumHydroxide.cfg"
copy /Y "%SOURCE_PARTS%\LIOHCartridge.cfg" "%DEST_PARTS%\LIOHCartridge.cfg"
copy /Y "%SOURCE_PARTS%\LIOHCartridge.mu" "%DEST_PARTS%\LIOHCartridge.mu"
copy /Y "%SOURCE_PARTS%\LiOHCartridge.png" "%DEST_PARTS%\LiOHCartridge.png"
copy /Y "%SOURCE_PATCHES%\US2.cfg" "%DEST_PATCHES%\US2.cfg"


echo.
echo ========================================
echo  Deployment complete!
echo ========================================
echo.

H:
cd "H:\SteamLibrary\steamapps\common\Kerbal Space Program"
explorer .
start KSP_x64.exe
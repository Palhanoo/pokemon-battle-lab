@echo off
cd /d "%~dp0"
if not exist "Builds\DirectControl\PokemonBattleLab.exe" (
 echo Build missing. Open this folder in Unity 6000.6.3f1 and use Battle Lab / Build Windows review.
 pause
 exit /b 1
)
start "Pokemon Battle Lab - Direct Control" "Builds\DirectControl\PokemonBattleLab.exe" -screen-fullscreen 0 -screen-width 1440 -screen-height 900

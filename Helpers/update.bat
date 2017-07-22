@echo off
@title Update HomeAssistant
echo Stopping HomeAssistant
call "stop.bat"
echo Starting update
pip install --upgrade homeassistant
set INPUT=
set /P INPUT=Should HomeAssistant be restarted? (y/n): %=%
If /I "%INPUT%"=="y" goto yes 
If /I "%INPUT%"=="n" goto no
:yes
call "start.bat"
:no
pause
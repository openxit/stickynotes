@echo off
setlocal
title StickyNotes Uninstaller

echo.
echo  StickyNotes Uninstaller
echo  =======================
echo  This will:
echo   - Stop StickyNotes.exe if running
echo   - Remove StickyNotes from Windows startup
echo   - Delete installed StickyNotes.exe (%LOCALAPPDATA%\StickyNotes)
echo   - Delete all saved sticky notes and settings (%APPDATA%\StickyNotes)
echo.

set /p CONFIRM="Are you sure you want to uninstall StickyNotes and delete all notes? (Y/N): "
if /i not "%CONFIRM%"=="Y" (
    echo Cancelled.
    pause
    exit /b 0
)

echo.
echo Stopping StickyNotes...
taskkill /F /IM StickyNotes.exe >nul 2>&1
timeout /t 1 /nobreak >nul

echo Removing Windows autostart entry...
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "StickyNotesApp" /f >nul 2>&1

echo Removing installed executable...
if exist "%LOCALAPPDATA%\StickyNotes" (
    rmdir /S /Q "%LOCALAPPDATA%\StickyNotes"
)

echo Removing saved notes and settings...
if exist "%APPDATA%\StickyNotes" (
    rmdir /S /Q "%APPDATA%\StickyNotes"
)

if exist "%~dp0StickyNotes.exe" (
    echo Removing StickyNotes.exe from current folder...
    del /F /Q "%~dp0StickyNotes.exe" >nul 2>&1
)

echo.
echo StickyNotes has been completely uninstalled.
pause

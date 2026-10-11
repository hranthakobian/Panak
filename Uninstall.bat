@echo off
title Panak Uninstaller
echo Starting Panak Uninstaller Wizard...
if exist "publish\Setup.exe" (
    start "" "publish\Setup.exe" --uninstall
) else if exist "%LocalAppData%\Programs\Panak\Setup.exe" (
    start "" "%LocalAppData%\Programs\Panak\Setup.exe" --uninstall
) else if exist "Setup\bin\Release\net8.0-windows10.0.19041.0\win-x64\Setup.exe" (
    start "" "Setup\bin\Release\net8.0-windows10.0.19041.0\win-x64\Setup.exe" --uninstall
) else if exist "Setup\bin\Debug\net8.0-windows10.0.19041.0\Setup.exe" (
    start "" "Setup\bin\Debug\net8.0-windows10.0.19041.0\Setup.exe" --uninstall
) else if exist "%LocalAppData%\Programs\Panak\Panak.exe" (
    start "" "%LocalAppData%\Programs\Panak\Panak.exe" --uninstall
) else if exist "publish\Panak.exe" (
    start "" "publish\Panak.exe" --uninstall
) else (
    echo Panak executable or uninstaller not found.
    pause
)

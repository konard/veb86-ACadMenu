@echo off
chcp 65001 >nul
setlocal

rem BobrovMenu: per-user installation, administrator rights are not required.
rem Copies AppData\Roaming from the archive into %APPDATA% of the current user.
rem The bundle holds the Bobrov partial CUIX and three SmartLeader menu DLLs.

set "SOURCE=%~dp0AppData\Roaming\Autodesk\ApplicationPlugins\BobrovMenu.bundle"
set "TARGET=%APPDATA%\Autodesk\ApplicationPlugins\BobrovMenu.bundle"
if not exist "%SOURCE%\PackageContents.xml" (
    echo Не найдена папка AppData рядом с install.cmd. Распакуйте архив полностью.
    goto :fail
)

echo Установка BobrovMenu в %TARGET% ...
rem /MIR replaces the previous version of the bundle and removes its stale files; other plugins are not touched.
robocopy "%SOURCE%" "%TARGET%" /MIR /R:1 /W:1 /NFL /NDL /NJH /NJS /NP
if errorlevel 8 (
    echo Ошибка копирования. Закройте AutoCAD и запустите install.cmd снова.
    goto :fail
)
rem Files extracted from a downloaded ZIP carry the "from the internet" mark (Zone.Identifier); remove it.
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -LiteralPath $env:TARGET -Recurse -File | Unblock-File" >nul 2>&1

echo.
echo Готово.
echo   %TARGET%
echo AutoCAD 2020-2024: вкладка Bobrov содержит кнопку «Палитра XDATA».
echo Кнопка вызывает команду XDATAPALETTE плагина acadPaletteSetXDATA, который устанавливается отдельно.
echo AutoCAD 2020 и новее: откройте DWG и выберите вкладку ACadSmartLeader.
echo Для выносок установите ACadSmartLeader 0.1.4 или новее отдельно.
echo Подробности - в файле INSTALL.md.
pause
exit /b 0

:fail
pause
exit /b 1

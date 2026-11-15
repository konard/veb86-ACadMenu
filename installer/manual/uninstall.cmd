@echo off
chcp 65001 >nul
setlocal

rem BobrovMenu: removes the per-user installation made by install.cmd.
rem Only BobrovMenu.bundle is removed; acadPaletteSetXDATA.bundle and other plugins are not touched.

set "TARGET=%APPDATA%\Autodesk\ApplicationPlugins\BobrovMenu.bundle"
if not exist "%TARGET%" (
    echo BobrovMenu не установлен: %TARGET%
    pause
    exit /b 0
)

rmdir /s /q "%TARGET%"
if exist "%TARGET%" (
    echo Не удалось удалить %TARGET%. Закройте AutoCAD и запустите uninstall.cmd снова.
    pause
    exit /b 1
)

echo Удалено: %TARGET%
echo При следующем запуске AutoCAD меню и вкладка Bobrov исчезнут.
echo Плагин acadPaletteSetXDATA и команда XDATAPALETTE продолжают работать.
pause
exit /b 0

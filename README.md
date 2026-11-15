# ACadMenu

Меню и ribbon для моих модулей AutoCAD.

Модуль **BobrovMenu** (задача [#1](https://github.com/veb86/ACadMenu/issues/1), ТЗ —
[menu-module-spec.md](menu-module-spec.md)) добавляет в AutoCAD 2020–2024 x64 кнопку-переключатель
**«Палитра XDATA»**, которая вызывает команду `XDATAPALETTE` плагина
[acadPaletteSetXDATA](https://github.com/veb86/acadPaletteSetXDATA):

| Где | Что |
| --- | --- |
| Строка меню (`MENUBAR` = 1) | выпадающее меню **Bobrov** → **Палитра XDATA** |
| Лента | вкладка **Bobrov** → панель **Палитра XDATA** → большая кнопка **Палитра XDATA** |
| Подсказка | «Показать или скрыть панель «Свойства XDATA» (XDATAPALETTE)» |
| Макрос | `$M=$(if,$(getvar,cmdactive),^C^C)_XDATAPALETTE ` (отменяет только активную команду, выбор объектов сохраняется) |
| Значки | 16×16 и 32×32, нейтральные, без состояния «включено/выключено» |

![Значки 32×32 и 16×16 на светлом и тёмном фоне (увеличено)](docs/screenshots/icons-preview.png)

Реализация — рекомендуемый вариант п. 4 ТЗ: частичный CUIX `BobrovMenu.cuix` в пакете
автозагрузки `BobrovMenu.bundle`, **без .NET и без LISP**. Меню и вкладку загружает сам AutoCAD,
поэтому модуль ничего не пишет в командную строку, не запускает команд при запуске и не меняет
`MENUBAR`, `WSCURRENT`, рабочие пространства и основной CUIX пользователя.

## Установка

Скачайте `BobrovMenu-manual-install-v<версия>.zip` из
[Releases](https://github.com/veb86/ACadMenu/releases) (или артефакт `BobrovMenu-release` сборки
CI), распакуйте и запустите `install.cmd`. Подробная инструкция по установке, обновлению и
удалению — [installer/manual/INSTALL.md](installer/manual/INSTALL.md).

Плагин acadPaletteSetXDATA устанавливается отдельно.

## Структура репозитория

| Путь | Назначение |
| --- | --- |
| `installer/BobrovMenu.bundle/` | пакет как есть: `PackageContents.xml` и `Contents/BobrovMenu.cuix` (готовый CUIX хранится в репозитории) |
| `src/BobrovMenu.cuix/` | исходники CUIX — распакованные части (XML `.cui`, значки PNG), из которых собирается `BobrovMenu.cuix` |
| `installer/manual/` | `install.cmd`, `uninstall.cmd`, `INSTALL.md` архива ручной установки |
| `scripts/build_cuix.py` | собирает `BobrovMenu.cuix` из `src/BobrovMenu.cuix/`, проверяет (`--check`), распаковывает CUIX в исходники (`--unpack`) |
| `scripts/make_icons.py` | рисует значки 16×16 и 32×32 |
| `scripts/package.py` | собирает архив `BobrovMenu-manual-install-v<версия>.zip` |
| `tests/` | автоматические проверки (Python `unittest`) |
| `experiments/` | проверка `install.cmd`/`uninstall.cmd` в Windows, превью значков |
| `docs/acceptance-protocol.md` | протокол приёмки по п. 8 ТЗ |

Нужен только Python 3.8+ (стандартная библиотека).

## Сборка и проверка

```bash
python3 scripts/make_icons.py --check     # значки совпадают с make_icons.py
python3 scripts/build_cuix.py --check     # BobrovMenu.cuix совпадает с src/BobrovMenu.cuix
python3 -m unittest discover -s tests -v  # структура CUIX, команда, меню, лента, пакет, архив
python3 scripts/package.py --version v0.0.1 --commit "$(git rev-parse HEAD)"  # artifacts/release/*.zip
```

CI (`.github/workflows/ci.yml`) выполняет эти проверки, собирает архив, проверяет `install.cmd` и
`uninstall.cmd` в Windows на временном `%APPDATA%`, а при push в `main` публикует релиз
`v0.0.<номер сборки>` с архивом ручной установки.

## Как изменить меню

**В редакторе CUI AutoCAD.** Откройте `installer/BobrovMenu.bundle/Contents/BobrovMenu.cuix`
командой `CUI` в AutoCAD 2020 (самой старой целевой версии), измените и сохраните, затем
синхронизируйте исходники:

```bash
python3 scripts/build_cuix.py --unpack installer/BobrovMenu.bundle/Contents/BobrovMenu.cuix
```

Если значки менялись в редакторе, перенесите изменения в `scripts/make_icons.py` (иначе
`make_icons.py --check` не пройдёт).

**В исходниках.** Измените файлы в `src/BobrovMenu.cuix/` (новые части и значки нужно указать в
`_rels/.rels` и `Menu_Package_Info.xml`, в последнем — обновить `PartData_Modified` изменённых
частей), затем пересоберите `python3 scripts/build_cuix.py` и закоммитьте и исходники, и
`BobrovMenu.cuix`.

Идентификаторы модуля: группа меню `BOBROVMENU`, команда `ID_BOBROV_XDATAPALETTE`, меню
`ID_BOBROV_POPMENU` (псевдонимы `POP1`, `BOBROV`), вкладка `ID_BOBROV_TAB`
(`DefaultDisplay="AddToWorkspace"`, `WorkspaceBehavior="MergeOrAddTab"`, псевдоним `ID_BOBROVTAB`),
панель `ID_BOBROV_PANEL_XDATA`. GUID пакета: `ProductCode` `{21c5b8be-7ef2-4d0b-9796-84bd8bef7117}`,
`UpgradeCode` `{d20d824c-4f5e-40b1-a857-e8a3bc01bdb4}` (не совпадают с плагином).

# CMS18 Mod Menu

### Your mods, explained. Your Workshop, within reach.

**CMS18 Mod Menu** brings a dedicated mod browser to the Car Mechanic Simulator 2018 main menu. Explore loaded BepInEx plugins, learn what they do, and check your Steam Workshop subscriptions from a panel styled to fit the game.

The supplied source declares version **1.2.1**, plugin ID `local.cms18.modmenu`, and the target process `cms2018.exe`.

## Features

- **A familiar entry point:** adds **Mods** beside **Exit**, preferring the left side and checking alternate positions when needed. It reuses the native menu font and selection underline.
- **BepInEx plugin browser:** select a loaded plugin to see its name, version, DLL filename, description, and available setting descriptions.
- **Useful description fallbacks:** reads nearby documentation first, then built-in summaries for recognized plugin IDs, then DLL description metadata. If no usable description exists, the panel says so.
- **Workshop overview:** lists subscribed items with their Workshop IDs and Steam-reported states: Subscribed, Installed, Downloading, Download pending, or Update pending.
- **Quick Workshop access:** open an item's page or browse the CMS18 Workshop through the Steam overlay when available, with a browser fallback.
- **Game-styled presentation:** a scrollable panel with orange selection indicators, native typeface where available, and game button sounds.

Both tabs are for browsing. The menu does not enable or disable plugins, edit their settings, or install Workshop items itself. Steam manages subscriptions and downloads. The settings section shows up to eight setting names and descriptions per plugin; it does not display an editable configuration form.

## Requirements

- Car Mechanic Simulator 2018 on Windows.
- A working **BepInEx 5** installation.
- Launching the game through Steam with its API available to view Workshop subscriptions. Workshop titles also depend on available local metadata or Steam responses.

No game-build compatibility matrix is included in the supplied package. The code relies on the game's menu classes, private fields, Unity UI, and Steamworks interfaces, so compatibility depends on those APIs remaining available.

## Installation

1. Close CMS18 and confirm BepInEx 5 is installed and working.
2. Download this repository using **Code → Download ZIP**, then extract it.
3. Copy the included `BepInEx` folder into your game directory beside `cms2018.exe`, merging it with the existing folder.
4. Launch the game through Steam and select **Mods** on the main menu.

For a manual install, download [CMS18ModMenu.dll](BepInEx/plugins/CMS18ModMenu/CMS18ModMenu.dll) with GitHub's **Download raw file** button and place it at:

```text
Car Mechanic Simulator 2018/
├── cms2018.exe
└── BepInEx/
    └── plugins/
        └── CMS18ModMenu/
            └── CMS18ModMenu.dll
```

Typical Steam game directory:

```text
C:\Program Files (x86)\Steam\steamapps\common\Car Mechanic Simulator 2018
```

Replace older copies of this plugin and avoid leaving duplicate copies elsewhere in `BepInEx/plugins`. The source and build script are not needed for installation.

## Using the menu

| Action | What it does |
| --- | --- |
| **Mods** on the main menu | Opens the panel with the BepInEx tab selected. |
| **BepInEx Mods** | Lists plugins with loaded instances, sorted by name. Select one for details. |
| **Steam Workshop** | Lists your subscriptions and their Steam-reported install states. |
| **Refresh** | Rereads the active tab; it does not load new plugin DLLs into the running game. |
| **Open** beside a Workshop item | Opens that item's Steam page. |
| **Browse Workshop** | Opens the CMS18 Workshop page. |
| **Close** or **Escape** | Returns to the main menu. |

Workshop item titles may update after the initial list appears. If neither local metadata nor Steam provides a title, the entry falls back to `Workshop item <ID>`. An **Installed** badge reports Steam's item state; it does not certify that an item is loaded or compatible with your game.

## Files and implementation

| File | Purpose |
| --- | --- |
| [Source/Plugin.cs](Source/Plugin.cs) | Registers the plugin, adds the main-menu entry, reads loaded plugins, and suspends/restores main-menu navigation around the panel. |
| [Source/MenuPanel.cs](Source/MenuPanel.cs) | Builds the Unity UI, tabs, plugin details, Workshop rows, scrolling, button sounds, and page-opening actions. |
| [Source/PluginDetails.cs](Source/PluginDetails.cs) | Resolves descriptions from local documentation, known plugin summaries, or assembly metadata, and reads setting descriptions. |
| [Source/WorkshopCatalog.cs](Source/WorkshopCatalog.cs) | Reads subscriptions and install states, resolves local titles, and requests Steam titles in batches of 50. |
| [Build.ps1](Build.ps1) | Compiles all C# files in `Source` against your installed game and BepInEx assemblies. |
| [BepInEx/plugins/CMS18ModMenu/CMS18ModMenu.dll](BepInEx/plugins/CMS18ModMenu/CMS18ModMenu.dll) | Supplied prebuilt plugin, ready for the installation layout above. |
| [docs/ORIGINAL-README.md](docs/ORIGINAL-README.md) | Original package documentation, preserved unchanged. |

The implementation uses Unity UI, reflection, and BepInEx's loaded-plugin registry. The supplied build script does not reference Harmony.

## Providing descriptions for other plugins

The browser looks for documentation beside a plugin DLL in this order:

1. `<DLL name without extension>.description.txt`
2. `<DLL name without extension>.md`
3. `README.md` in the plugin's own subfolder
4. `README.txt` in the plugin's own subfolder

For example, a plugin named `ExampleMod.dll` can include `ExampleMod.description.txt` beside it. Put a short, plain-language description near the beginning. The reader extracts a brief opening excerpt, skips initial headings and image lines, and limits the displayed summary to 750 characters. Generic README files directly in the shared plugins root are excluded to avoid attributing shared documentation to a single plugin.

If documentation yields no summary, recognized plugin IDs use built-in descriptions; other plugins can supply an `AssemblyDescription` attribute in their DLL metadata. Built-in summaries describe the reviewed versions and may not reflect every later change to those mods.

## Build from source

Keep `Build.ps1`, `Source`, and the package folder structure together. In Windows PowerShell, run:

```powershell
.\Build.ps1 -GamePath 'D:\SteamLibrary\steamapps\common\Car Mechanic Simulator 2018'
```

For the default Steam location, run:

```powershell
.\Build.ps1
```

The script uses the Windows .NET Framework compiler at `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`. It references game and Unity assemblies from `cms2018_Data/Managed` and `BepInEx.dll` from `BepInEx/core`, checks those references, and compiles every `.cs` file in `Source`.

A successful build writes `BepInEx/plugins/CMS18ModMenu/CMS18ModMenu.dll` inside this package, replacing the local binary. Copy the rebuilt DLL to the game's plugin folder with the game closed. Game and BepInEx dependency assemblies are not included in this repository.

## Troubleshooting

| Symptom | What to check |
| --- | --- |
| **Mods** does not appear | Confirm BepInEx 5 loads, check the DLL location, and return to the main menu. Inspect `BepInEx/LogOutput.log` for plugin errors or the “Added Mods” entry. |
| A plugin is missing from the BepInEx list | The tab lists loaded plugin instances. Check that plugin's BepInEx startup logs; copying a DLL during play and pressing Refresh does not load it. |
| A plugin has no description | Add nearby documentation as described above, or provide assembly description metadata. |
| Settings cannot be changed | This version displays setting descriptions for reference. Use the plugin's own settings UI or configuration file to make changes. |
| Workshop is unavailable | Launch CMS18 through Steam and confirm Steam is running, then refresh the tab. |
| An item shows only its ID | Its title was unavailable from local metadata and Steam. The Open button still uses its Workshop ID. |
| Building fails | Verify `-GamePath`, the installed BepInEx references, and the .NET Framework compiler path. Read the reported missing reference or compiler error. |

To uninstall, close the game and remove `BepInEx/plugins/CMS18ModMenu/CMS18ModMenu.dll`. Other plugins and Workshop subscriptions remain managed separately.

## Validation and issue reports

The supplied source, DLL, build script, and original README are preserved unchanged in this import. This guide was checked against the source; the prebuilt DLL was not rebuilt or verified against it, and no Windows build or in-game test was performed here.

When testing changes, check main-menu placement, plugin selection, description fallbacks, both tabs, Workshop title updates and page links, Refresh, and Escape/Close navigation. For issue reports, include game and BepInEx versions, reproduction steps, and relevant entries from `BepInEx/LogOutput.log`.

This is an unofficial mod for Car Mechanic Simulator 2018 and is not affiliated with or endorsed by the game's developers, publishers, or Valve.

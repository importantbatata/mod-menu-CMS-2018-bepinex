# CMS18 Mod Menu

Adds **Mods** immediately to the left of **Exit** on the Car Mechanic Simulator 2018 main menu, using the native menu font and selection underline.
The panel carries through the same typeface, orange selection underline, and button sound.

- **BepInEx Mods** lets you select a loaded plugin to see what it does, its DLL name and version, and any settings it exposes. The description comes from reviewed local mod files, nearby documentation, or the DLL’s metadata. If a DLL has no usable description, the tab says so and shows any available settings.
- **Steam Workshop** lists your CMS18 Workshop subscriptions and their Steam install status. Select **Open** to view an item or **Browse Workshop** to find more mods.

Both tabs are for browsing. Steam handles subscriptions and downloads.

## Installation

1. Close CMS18. The game needs a working BepInEx 5 installation.
2. Copy the included `BepInEx` folder into the CMS18 game folder beside `cms2018.exe`. On a typical Steam installation, the game folder is `C:\Program Files (x86)\Steam\steamapps\common\Car Mechanic Simulator 2018`.
3. Launch through Steam and select **Mods** on the main menu. Select **Close** or press **Esc** to return.

If Steam cannot supply an item title, the Workshop list shows its ID. **Refresh** rereads the active tab.

To remove the mod, close the game and delete `BepInEx\plugins\CMS18ModMenu\CMS18ModMenu.dll` from the game folder.

## Build from source

Run `Build.ps1` in PowerShell. Pass `-GamePath "C:\path\to\Car Mechanic Simulator 2018"` for a custom installation. The build uses the game's managed assemblies and installed BepInEx 5 and writes the DLL to the included plugin folder.

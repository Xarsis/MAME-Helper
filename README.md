# MAME Helper — Playnite Plugin

A comprehensive MAME library management plugin for [Playnite](https://playnite.link/). MAME Helper replaces the need for multiple separate plugins by combining ROM filtering, tagging, renaming, media management, and library export into a single integrated tool.

---

## Requirements

- [Playnite](https://playnite.link/) version 10.x (SDK 6.x)
- [MAME](https://www.mamedev.org/) installed locally, **or** a pre-exported MAME XML / DAT list file
- .NET Framework 4.7.2 (included with Windows 10/11)
- MAME ROMs imported into Playnite via the emulator auto-scan
- `catver.ini` (included) — required for catver-based Tag, Hide, and Remove operations

---

## Installation

1. Download the latest release zip and extract all files into a new folder:
   ```
   %APPDATA%\Playnite\Extensions\MAMEHelper\
       MAMEHelper.dll
       extension.yaml
       icon.png
       catver.ini
       README.md
   ```
2. Restart Playnite
3. The plugin appears under **Extensions → MAME Helper** in the menu bar
4. Go to **Extensions → MAME Helper → Settings…** to configure your MAME path and media folders before first use

---

## Settings

Access via **Extensions → MAME Helper → Settings…** in the main menu, or through the Playnite Add-ons panel.

| Setting | Description |
|---|---|
| **Use MAME executable** | Runs `mame.exe -listxml` to generate ROM data. Takes 30–90 seconds on first run. |
| **Use list file** | Loads a pre-exported `.xml` or `.dat` file instead of running MAME. Faster for repeated use. |
| **Cache age (days)** | How many days before prompting to regenerate the ROM cache. Default: 7. Set to 0 to always prompt, 9999 to never prompt. |
| **Cover images folder (primary)** | Primary folder containing PNG files named `{romname}.png` for cover art. |
| **Cover images folder (secondary)** | Fallback folder checked when no cover is found in the primary folder. |
| **Background images folder (primary)** | Primary folder containing PNG files named `{romname}.png` for background art. |
| **Background images folder (secondary)** | Fallback folder checked when no background is found in the primary folder. |

### ROM Cache

On first use, MAME Helper generates a `romcache.json` file in its plugin data folder (`%APPDATA%\Playnite\ExtensionsData\{GUID}\`). Subsequent uses load from this cache silently. After the configured number of days, you are prompted to regenerate or continue using the existing cache. You can also force a regeneration at any time via **Tools → Regenerate ROM Cache**.

The cache file can be found at:
```
%APPDATA%\Playnite\ExtensionsData\77ed2204-c7d1-48cc-9e62-034fd035e803\romcache.json
```

---

## How Matching Works

All operations match games by their ROM name against the MAME data. The lookup priority is:

1. **Notes field** — MAME Helper writes `MAME Helper - original name: {romname}` into the Notes field when renaming. This is checked first so renamed games continue to match correctly.
2. **game.Name** — used for un-renamed imports where the game name is still the ROM filename (e.g. `pacman`, `sf2`, `dkong`).
3. **ROM file path** — used as a final fallback during rename operations only.

ROM names are normalized to lowercase with spaces converted to underscores (e.g. `epo efdx` → `epo_efdx`) to ensure consistent matching against both the MAME cache and catver.ini.

**If your games were already renamed before installing MAME Helper:** run **Tools → Restore ROM Names into Notes** once after installation. This writes the original ROM name into each game's Notes field so all operations can match correctly. Any existing Notes content is preserved.

For **clone ROMs** with no direct media match, MAME Helper automatically falls back to the parent ROM's image file (checked in both primary and secondary folders).

---

## Menu Reference

### Tag

All Tag operations apply to **all games in the library**. Tags are prefixed with `MAME: ` for easy filtering in Playnite's tag browser.

#### Tag: Driver Status
Adds one of the following tags to every matched game based on MAME's emulation status:

| Tag | MAME Status | Meaning |
|---|---|---|
| `MAME: Working` | `good` | Fully emulated, plays correctly |
| `MAME: Imperfect` | `imperfect` | Mostly works — minor sound, graphic, or input issues |
| `MAME: Non-Working` | `preliminary` | Broken or unplayable |

#### Tag: Machine Type
Adds one or more of the following tags based on the machine's type in MAME's XML:

| Tag | Meaning |
|---|---|
| `MAME: BIOS` | BIOS set (not a game) |
| `MAME: Device` | Hardware device ROM (not a game) |
| `MAME: Mechanical` | Mechanical game (pinball, slot machine, etc.) |
| `MAME: Sample` | Sample-only entry (not independently playable) |

Games that are none of the above are standard arcade games and receive no machine type tag.

#### Tag: Parent / Clone
Identifies ROM relationships:

| Tag | Meaning |
|---|---|
| `MAME: Parent` | The canonical version of a game |
| `MAME: Clone` | A regional variant, revision, or bootleg of a parent ROM |

#### Tag: Category (top level)
Tags every matched game with its top-level catver.ini category, prefixed with `MAME: `. For example: `MAME: Shooter`, `MAME: Maze`, `MAME: Fighter`. Requires `catver.ini` to be present in the Extensions folder.

#### Tag: Category (full)
Tags every matched game with its full catver.ini category string, prefixed with `MAME: `. For example: `MAME: Shooter / Flying Vertical`, `MAME: Maze / Collect`. Requires `catver.ini` to be present in the Extensions folder.

#### Tag: Year and Manufacturer
Populates Playnite metadata fields (not tags) from MAME data:
- **Release Year** → set from the MAME XML `year` field
- **Developer** → set from the MAME XML `manufacturer` field (common region markers like "(Japan)" are stripped)

Only sets these fields if they are currently empty — will not overwrite existing metadata.

#### Clear All MAME Tags
Removes every tag prefixed with `MAME: ` from every game in the library. This includes driver status tags, machine type tags, parent/clone tags, and all catver category tags. Use this before re-tagging after a MAME update to avoid stale tags. Requires confirmation.

#### Set Category…
Prompts for a category name and adds it to all matched MAME games. Category appears in Playnite's left sidebar filter panel. Example: `Arcade`.

#### Set Source…
Prompts for a source name and sets it on all matched MAME games. Useful for distinguishing MAME games from other emulated games in Playnite. Example: `MAME`.

#### Set Platform…
Prompts for a platform name and sets it on all matched MAME games. Example: `Arcade`.

---

### Hide

All Hide operations set `game.Hidden = true`, making games disappear from the library view while keeping them in the database. **Fully reversible** — use **Unhide All MAME Games** or Playnite's built-in filter panel (Show Hidden Games) to restore them.

| Operation | What gets hidden |
|---|---|
| **Hide Imperfect ROMs** | Games with `imperfect` driver status |
| **Hide Non-Working ROMs** | Games with `preliminary` driver status |
| **Hide Clones** | All clone ROMs regardless of working status |
| **Hide Non-Games** | All BIOS, Device, Mechanical, and Sample entries (from MAME XML data) |
| **Hide Non-Games by catver.ini** | Games whose catver.ini top-level category is in the non-game list (see below) |
| **Hide by Year Range…** | Prompts for a from/to year; hides games outside that range |
| **Hide by Manufacturer…** | Prompts for a manufacturer name (partial match); hides matching games |
| **Unhide All MAME Games** | Removes the Hidden flag from every game in the library. Requires confirmation. |

**Recommended workflow:** Run **Tag: Driver Status** first, review the results in Playnite using tag filters, then run the relevant Hide operation once satisfied.

---

### Remove

All Remove operations **permanently delete** games from the Playnite library. This cannot be undone. Each operation shows a confirmation dialog with the count of games to be removed before proceeding.

| Operation | What gets removed |
|---|---|
| **Remove Non-Working ROMs** | All games with `preliminary` driver status |
| **Remove Clones** | All clone ROMs |
| **Remove Non-Games** | All BIOS, Device, Mechanical, and Sample entries (from MAME XML data) |
| **Remove Non-Games by catver.ini** | Games whose catver.ini top-level category is in the non-game list (see below) |

**Recommended workflow:** Use **Hide** first to verify the scope of what will be removed, then use Remove once you are certain.

### catver.ini Non-Game Categories

The following top-level catver.ini categories are treated as non-games by the **Hide/Remove Non-Games by catver.ini** operations:

`System`, `Computer`, `Computer Graphic Workstation`, `Calculator`, `Utilities`, `Telephone`, `Radio`, `Robot`, `Printer`, `Digital Camera`, `Digital Simulator`, `Medical Equipment`, `Musical Instrument`, `Musical Instrument Accessory`, `Music Player`, `Player`, `Tablet`, `Touchscreen`, `Watch`, `TV Bundle`, `Non Arcade`, `Road Indicator`, `Handheld`, `Game Console`

---

### Media

Media operations apply to **selected games only**. Select games in the Playnite library view before running these operations. Image files must be PNG format, named exactly after the ROM name (e.g. `pacman.png`).

For each game, images are searched in this order:
1. Primary folder — direct ROM name match
2. Primary folder — parent ROM name (for clones)
3. Secondary folder — direct ROM name match
4. Secondary folder — parent ROM name (for clones)

Configure the source folders in **Settings** before use.

| Operation | Description |
|---|---|
| **Set Cover Images from Folder** | Imports `{romname}.png` from the configured cover folders as the game's cover art |
| **Set Background Images from Folder** | Imports `{romname}.png` from the configured background folders as the game's background art |
| **Find Games with Missing Media** | Scans all matched games and writes a report of games missing cover and/or background images to a text file |

---

### Rename

Rename operations apply to **selected games only**. Select one or more games before running.

Before renaming, the original ROM name is written into the game's Notes field as `MAME Helper - original name: {romname}`. Any existing Notes content is preserved — the entry is appended on a new line. This allows all other MAME Helper operations to continue matching the game correctly after renaming.

| Operation | Example result |
|---|---|
| **Rename Selected Games (with region info)** | `sf2` → `Street Fighter II: The World Warrior (USA, 910206)` |
| **Rename Selected Games (without region info)** | `sf2` → `Street Fighter II: The World Warrior` |

---

### Tools

#### Generate Gamelist XML
Exports a `gamelist.xml` file compatible with **EmulationStation**, **Batocera**, and **RetroBat** front-ends. Prompts for a save location. Contains one `<game>` element per matched library entry with path, name, release date, developer, and cover image path.

#### Export Library to CSV
Exports a spreadsheet-compatible CSV file with one row per matched game. Prompts for a save location.

**Columns:** `RomName`, `DisplayName`, `Year`, `Manufacturer`, `DriverStatus`, `IsClone`, `CloneOf`, `IsBios`, `IsDevice`, `IsMechanical`, `IsSample`, `HasCover`, `HasBackground`, `Hidden`, `Tags`

Useful for auditing your collection, identifying gaps, or managing your library outside Playnite.

#### Restore ROM Names into Notes
Scans every game in the library and writes the original ROM name into the Notes field using the format `MAME Helper - original name: {romname}`. Only games with a valid ROM file path that matches a known MAME ROM are updated. Any existing Notes content is preserved — the MAME Helper entry is appended on a new line.

**Run this once if your library was renamed before MAME Helper was installed.** Without this, catver and tag operations may not be able to match renamed games.

#### Regenerate ROM Cache
Forces the ROM cache to regenerate immediately, bypassing the configured age threshold. Useful after updating MAME to a new version. Requires confirmation.

---

## Recommended First-Run Workflow

1. If your games were **already renamed** before installing MAME Helper, run **Tools → Restore ROM Names into Notes** first before doing anything else
2. Configure your MAME executable path (or list file path) in **Settings**
3. Configure cover and background image folders in **Settings** if applicable
4. Run **Tag → Tag: Driver Status** — review results in Playnite by filtering on `MAME: Non-Working`
5. Run **Tag → Tag: Machine Type** — review `MAME: BIOS`, `MAME: Device`, `MAME: Mechanical`
6. Run **Tag → Tag: Parent / Clone** — review `MAME: Clone`
7. Optionally run **Tag → Tag: Category (top level)** to tag by catver.ini category
8. Run **Tag → Set Category…**, **Set Source…**, **Set Platform…** to organise your library
9. Run **Tag → Tag: Year and Manufacturer** to populate release year and developer fields
10. Once satisfied with the tagging, run **Hide → Hide Non-Working ROMs** to clean up the library view
11. Optionally run **Hide → Hide Non-Games**, **Hide → Hide Non-Games by catver.ini**, and **Hide → Hide Clones**
12. Run **Rename → Rename Selected Games** on your visible working games to give them proper display names
13. Run **Media → Set Cover Images** and **Set Background Images** on selected games
14. Use **Tools → Export Library to CSV** to get a full inventory of your collection

---

## Driver Status Values

| MAME Status | MAME Helper Tag | Meaning |
|---|---|---|
| `good` | `MAME: Working` | Fully playable |
| `imperfect` | `MAME: Imperfect` | Playable with minor issues |
| `preliminary` | `MAME: Non-Working` | Not playable |

---

## Project Structure

```
MAMEHelper\
├── extension.yaml                  Plugin manifest
├── icon.png                        Plugin icon
├── catver.ini                      Community ROM categories (from https://www.progettosnaps.net/catver/)
├── README.md                       This file
└── src\
    ├── MAMEHelper.csproj           Visual Studio project file
    ├── MAMEHelperPlugin.cs         Plugin entry point and menu wiring
    ├── Models\
    │   ├── RomsetMachine.cs        ROM data model
    │   └── MAMEHelperSettings.cs   Persisted settings model
    ├── Services\
    │   ├── CatverParser.cs         catver.ini parser and category definitions
    │   ├── MatchingHelper.cs       ROM name resolution including Notes field lookup
    │   ├── RomDataService.cs       XML parsing, mame.exe execution, caching
    │   ├── RomIdRestorer.cs        Writes ROM names into Notes for pre-renamed libraries
    │   ├── GameTagger.cs           Tag operations
    │   ├── GameHider.cs            Hide / Unhide operations
    │   ├── GameRemover.cs          Permanent removal operations
    │   ├── GameRenamer.cs          ROM name → display name rename
    │   ├── GameMediaManager.cs     Cover and background image import
    │   ├── GameMetadataSetter.cs   Category, Source, Platform, Year, Manufacturer
    │   ├── GamelistXmlExporter.cs  EmulationStation gamelist.xml export
    │   ├── CsvExporter.cs          CSV library export
    │   └── MissingMediaFinder.cs   Missing media report
    └── UI\
        ├── MAMEHelperSettingsView.xaml      Settings page layout
        ├── MAMEHelperSettingsView.xaml.cs   Settings page code-behind
        ├── MAMEHelperSettingsViewModel.cs   Settings page view model
        ├── InputDialog.xaml                 Text input dialog layout
        └── InputDialog.xaml.cs              Text input dialog code-behind
```

---

## Building from Source

**Requirements:**
- Visual Studio 2022 with the .NET desktop workload
- `lib\Playnite.SDK.dll` — copy from your Playnite installation folder
- `lib\Newtonsoft.Json.dll` — copy from your Playnite installation folder (use the version Playnite ships with, not a newer one from NuGet)

**Steps:**
1. Place both DLLs in the `lib\` folder next to `src\`
2. Open `src\MAMEHelper.csproj` in Visual Studio
3. Press `Ctrl+Shift+B` to build
4. Output is in `src\bin\Debug\`

---

## Troubleshooting

**Plugin doesn't appear in the Extensions menu**
- Confirm all files (`MAMEHelper.dll`, `extension.yaml`, `icon.png`, `catver.ini`) are in the Extensions folder
- Check `%APPDATA%\Playnite\playnite.log` for load errors
- Ensure the `Id` in `extension.yaml` matches the GUID in `MAMEHelperPlugin.cs`

**"Most games skipped / no MAME match"**
- Your games may already be renamed to display names — run **Tools → Restore ROM Names into Notes** to fix this
- Run **Tools → Export Library to CSV** to audit what names Playnite has stored
- Re-import your MAME ROMs via the emulator auto-scan if needed, then run operations before renaming

**catver operations show high "Not in catver.ini" count**
- Some ROMs (especially newer MAME additions) may not yet be listed in catver.ini
- Update `catver.ini` from [https://www.progettosnaps.net/catver/](https://www.progettosnaps.net/catver/) to get the latest version
- This is expected for very new or obscure ROMs — the "Not in catver.ini" count is separate from the "No MAME match" count

**Cache seems wrong or outdated**
- Use **Tools → Regenerate ROM Cache** to force a rebuild
- Or delete `romcache.json` from `%APPDATA%\Playnite\ExtensionsData\77ed2204-c7d1-48cc-9e62-034fd035e803\` and the next operation will regenerate it automatically

**Progress bar appears but 0 games are processed**
- Verify your MAME executable path is set correctly in Settings
- Verify mame.exe runs correctly outside Playnite by opening a command prompt and running `mame.exe -listxml` manually

**"Could not load assembly Newtonsoft.Json"**
- The `Newtonsoft.Json.dll` in `lib\` must be the same version Playnite ships with
- Copy it directly from your Playnite installation folder rather than installing from NuGet

**Playnite hangs after running Set Cover/Background Images**
- This was fixed in v2.3.4 — update to the latest version

---

## Change History

| Version | Change |
|---|---|
| 1.0 | Initial release |
| 1.1 | Fixed `extension.yaml` Id format (must be GUID) |
| 1.2 | Renamed `.ps1` to `.psm1` (required by Playnite) |
| 1.3 | Added `GetMainMenuItems` function to register menu entries |
| 1.4 | Added `param($actionArgs)` to action functions |
| 1.5 | Moved `$MameExePath` to single top-level variable |
| 1.6 | Fixed `OutOfMemoryException` — switched to temp file + streaming XmlReader |
| 1.7 | Fixed DTD processing error — added `DtdProcessing = Parse` to XmlReaderSettings |
| 1.8 | Fixed `ContainsKey` error — suppressed `ShowMessage` return value with `[void]` |
| 1.9 | Fixed matching — switched from GameId to game.Name lookup against ROM names |
| 1.10 | Fixed `ContainsKey` on Process object — suppressed `Start-Process -PassThru` return value with `[void]` |
| 1.11 | Added Diagnose function; added Notepad++ support for diagnostic output |
| 1.12 | Added "Hide Imperfect ROMs" menu option; reordered menu |
| 1.13 | Added "Set Source" and "Set Category" menu options with user text input dialogs |
| 2.0.0 | Rewritten in C# — full Tag, Hide, Remove, Rename, Media, and Tools functionality |
| 2.1.0 | Added Settings menu item in plugin dropdown |
| 2.1.1 | Fixed Settings dialog; added Playnite theme-aware colors |
| 2.1.2 | Fixed Settings page registration in Add-ons panel |
| 2.1.3 | Added MatchingHelper; rename now saves ROM name to GameId before renaming |
| 2.1.4 | Added determinate progress bars to bulk operations |
| 2.1.5 | Added secondary cover and background image folders |
| 2.1.6 | Added Tools → Regenerate ROM Cache |
| 2.2.0 | Added catver.ini support — Hide and Remove Non-Games by category |
| 2.2.1 | Added catver.ini tagging; Clear All MAME Tags now removes catver tags too |
| 2.3.0 | Renamed matching to use Notes field with prefix, preserving existing note content |
| 2.3.1 | Updated to pass Playnite Toolbox manifest verification |
| 2.3.2 | Fixed settings not saving after GUID change |
| 2.3.3 | Added space-to-underscore normalization for catver and ROM name matching |
| 2.3.4 | Fixed Playnite hang on Set Cover/Background Images — two-pass file operation approach |

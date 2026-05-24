# MAME Helper — Playnite Plugin

A comprehensive MAME library management plugin for [Playnite](https://playnite.link/). MAME Helper replaces the need for multiple separate plugins by combining ROM filtering, tagging, renaming, media management, and library export into a single integrated tool.

---

## Requirements

- [Playnite](https://playnite.link/) version 10.x (SDK 6.x)
- [MAME](https://www.mamedev.org/) installed locally, **or** a pre-exported MAME XML / DAT list file
- .NET Framework 4.7.2 (included with Windows 10/11)
- MAME ROMs imported into Playnite via the emulator auto-scan

---

## Installation

1. Build the project in Visual Studio (`Ctrl+Shift+B`)
2. Navigate to `src\bin\Debug\` (or `Release\`)
3. Copy these three files into your Playnite extensions folder:
   ```
   %APPDATA%\Playnite\Extensions\MAMEHelper\
       MAMEHelper.dll
       extension.yaml
       icon.png
   ```
4. Restart Playnite
5. The plugin appears under **Extensions → MAME Helper** in the menu bar
6. Go to **Extensions → MAME Helper → Settings** to configure your MAME path and media folders before first use

---

## Settings

Access via **Main Menu → Extensions → MAME Helper → Settings** (or the Add-ons menu).

| Setting | Description |
|---|---|
| **Use MAME executable** | Runs `mame.exe -listxml` to generate ROM data. Takes 30–90 seconds on first run. |
| **Use list file** | Loads a pre-exported `.xml` or `.dat` file instead of running MAME. Faster for repeated use. |
| **Cache age (days)** | How many days before prompting to regenerate the ROM cache. Default: 7. Set to 0 to always prompt, 9999 to never prompt. |
| **Cover images folder** | Folder containing PNG files named `{romname}.png` for cover art. |
| **Background images folder** | Folder containing PNG files named `{romname}.png` for background art. |

### ROM Cache

On first use, MAME Helper generates a `romcache.json` file in its plugin data folder (`%APPDATA%\Playnite\ExtensionsData\MAMEHelper_...\`). Subsequent uses load from this cache silently. After the configured number of days, you are prompted to regenerate or continue using the existing cache.

---

## How Matching Works

All operations match games by `game.Name` (lowercased and trimmed) against the MAME ROM name. This works correctly when games are imported via Playnite's emulator auto-scan, which stores the ROM filename (without extension) as the game name (e.g. `pacman`, `sf2`, `dkong`).

**Important:** If you have already renamed your games to display names using a renaming tool, matching will fail. Always run MAME Helper operations **before** renaming, or use the built-in Rename feature after tagging/filtering.

For the **Rename** and **Media** operations, a fallback to the ROM filename from `game.Roms[0].Path` is attempted if the game name doesn't match.

For **clone ROMs** with no direct media match, MAME Helper automatically falls back to the parent ROM's image file.

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

#### Tag: Year and Manufacturer
Populates Playnite metadata fields (not tags) from MAME data:
- **Release Year** → set from the MAME XML `year` field
- **Developer** → set from the MAME XML `manufacturer` field (common region markers like "(Japan)" are stripped)

Only sets these fields if they are currently empty — will not overwrite existing metadata.

#### Clear All MAME Tags
Removes all `MAME: ` prefixed tags from every game in the library. Use this before re-tagging after a MAME update to avoid stale tags. Requires confirmation.

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
| **Hide Imperfect ROMs** | Games with `MAME: Imperfect` driver status |
| **Hide Non-Working ROMs** | Games with `MAME: Non-Working` driver status |
| **Hide Clones** | All clone ROMs regardless of working status |
| **Hide Non-Games** | All BIOS, Device, Mechanical, and Sample entries |
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
| **Remove Non-Games** | All BIOS, Device, Mechanical, and Sample entries |

**Recommended workflow:** Use **Hide** first to verify the scope of what will be removed, then use Remove once you are certain.

---

### Media

Media operations apply to **selected games only**. Select games in the Playnite library view before running these operations. Image files must be PNG format, named exactly after the ROM name (e.g. `pacman.png`).

For clone ROMs with no matching image file, MAME Helper automatically falls back to the parent ROM's image.

Configure the source folders in **Settings** before use.

| Operation | Description |
|---|---|
| **Set Cover Images from Folder** | Imports `{romname}.png` from the cover images folder as the game's cover art |
| **Set Background Images from Folder** | Imports `{romname}.png` from the background images folder as the game's background art |
| **Find Games with Missing Media** | Scans all matched games and writes a report of games missing cover and/or background images to a text file of your choice |

---

### Rename

Rename operations apply to **selected games only**. Select one or more games before running.

Matching uses `game.Name` first, then falls back to the ROM filename from `game.Roms[0].Path`. The new name comes from the MAME XML `description` field — no internet connection required.

| Operation | Example result |
|---|---|
| **Rename Selected Games (with region info)** | `sf2` → `Street Fighter II: The World Warrior (USA, 910206)` |
| **Rename Selected Games (without region info)** | `sf2` → `Street Fighter II: The World Warrior` |

**Note:** After renaming, ROM-name-based matching for other operations will no longer work for renamed games. Run all Tag, Hide, and Remove operations before renaming, or re-import and re-run if needed.

---

### Tools

#### Generate Gamelist XML
Exports a `gamelist.xml` file compatible with **EmulationStation**, **Batocera**, and **RetroBat** front-ends. Prompts for a save location. Contains one `<game>` element per matched library entry with path, name, release date, developer, and cover image path.

#### Export Library to CSV
Exports a spreadsheet-compatible CSV file with one row per matched game. Prompts for a save location.

**Columns:**
`RomName`, `DisplayName`, `Year`, `Manufacturer`, `DriverStatus`, `IsClone`, `CloneOf`, `IsBios`, `IsDevice`, `IsMechanical`, `IsSample`, `HasCover`, `HasBackground`, `Hidden`, `Tags`

Useful for auditing your collection, identifying gaps, or managing your library outside Playnite.

---

## Recommended First-Run Workflow

1. Configure your MAME executable path (or list file path) in **Settings**
2. Configure cover and background image folders in **Settings** if applicable
3. Run **Tag → Tag: Driver Status** — review results in Playnite by filtering on `MAME: Non-Working`
4. Run **Tag → Tag: Machine Type** — review `MAME: BIOS`, `MAME: Device`, `MAME: Mechanical`
5. Run **Tag → Tag: Parent / Clone** — review `MAME: Clone`
6. Run **Tag → Set Category…**, **Set Source…**, **Set Platform…** to organise your library
7. Run **Tag → Tag: Year and Manufacturer** to populate release year and developer fields
8. Once satisfied with the tagging, run **Hide → Hide Non-Working ROMs** to clean up the library view
9. Optionally run **Hide → Hide Non-Games** and **Hide → Hide Clones**
10. Run **Rename → Rename Selected Games** on your visible working games to give them proper display names
11. Run **Media → Set Cover Images** and **Set Background Images** on selected games
12. Use **Tools → Export Library to CSV** to get a full inventory of your collection

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
├── extension.yaml              Plugin manifest
├── icon.png                    Plugin icon
├── README.md                   This file
└── src\
    ├── MAMEHelper.csproj       Visual Studio project file
    ├── MAMEHelperPlugin.cs     Plugin entry point and menu wiring
    ├── Models\
    │   ├── RomsetMachine.cs    ROM data model
    │   └── MAMEHelperSettings.cs  Persisted settings model
    ├── Services\
    │   ├── RomDataService.cs   XML parsing, mame.exe execution, caching
    │   ├── GameTagger.cs       Tag operations
    │   ├── GameHider.cs        Hide / Unhide operations
    │   ├── GameRemover.cs      Permanent removal operations
    │   ├── GameRenamer.cs      ROM name → display name rename
    │   ├── GameMediaManager.cs Cover and background image import
    │   ├── GameMetadataSetter.cs  Category, Source, Platform, Year, Manufacturer
    │   ├── GamelistXmlExporter.cs  EmulationStation gamelist.xml export
    │   ├── CsvExporter.cs      CSV library export
    │   └── MissingMediaFinder.cs  Missing media report
    ├── Properties\
    │   └── AssemblyInfo.cs
    └── UI\
        ├── MAMEHelperSettingsView.xaml      Settings page layout
        ├── MAMEHelperSettingsView.xaml.cs   Settings page code-behind
        ├── MAMEHelperSettingsViewModel.cs   Settings page view model
        ├── InputDialog.xaml                 Text input dialog layout
        └── InputDialog.xaml.cs             Text input dialog code-behind
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
- Confirm all three files (`MAMEHelper.dll`, `extension.yaml`, `icon.png`) are in the Extensions folder
- Check `%APPDATA%\Playnite\playnite.log` for load errors
- Ensure the `Id` in `extension.yaml` matches the `Id` GUID in `MAMEHelperPlugin.cs`

**"Most games skipped / no MAME match"**
- Your games may already be renamed to display names — MAME Helper matches on ROM names (e.g. `pacman`, not `Pac-Man`)
- Run **Tools → Export Library to CSV** and check the `RomName` column to see what names Playnite has stored
- Re-import your MAME ROMs via the emulator auto-scan if needed, then run operations before renaming

**Cache seems wrong or outdated**
- Delete `romcache.json` from `%APPDATA%\Playnite\ExtensionsData\MAMEHelper_...\`
- The next operation will regenerate it automatically

**Progress bar appears but 0 games are processed**
- Verify your MAME executable path is set correctly in Settings
- Verify mame.exe runs correctly outside Playnite by opening a command prompt and running `mame.exe -listxml` manually

**"Could not load assembly Newtonsoft.Json"**
- The `Newtonsoft.Json.dll` in `lib\` must be the same version Playnite ships with
- Copy it directly from your Playnite installation folder rather than installing from NuGet

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
| 1.12 | Added "Hide Imperfect ROMs" menu option; reordered menu: Tag, Hide Imperfect, Hide Non-Working, Remove, Diagnose |
| 1.13 | Added "Set Source" and "Set Category" menu options with user text input dialogs |
| 2.0.0 | Initial release as C# — full Tag, Hide, Remove, Rename, Media, and Tools functionality |
| 2.1.0 | Addition of Settings menu in dropdown

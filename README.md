# MAME Working ROM Filter - Playnite Script Extension

A Playnite script extension that filters non-working MAME ROMs from your library using MAME's own driver status data.

## Requirements

- [Playnite](https://playnite.link/) (any recent version)
- [MAME](https://www.mamedev.org/) installed locally
- MAME ROMs imported into Playnite via the emulator auto-scan

## Installation

1. Navigate to your Playnite extensions folder:
   ```
   %APPDATA%\Playnite\Extensions\
   ```
2. Create a subfolder called `MameWorkingFilter`
3. Copy both files into that subfolder:
   ```
   MameWorkingFilter\
     MameWorkingFilter.psm1
     extension.yaml
   ```
4. Open `MameWorkingFilter.psm1` in a text editor and update the MAME path at the top of the file:
   ```powershell
   $MameExePath = "S:\MAME\MAME\mame.exe"
   ```
5. Restart Playnite
6. The extension will appear under **Extensions → MAME Working Filter**

## How It Works

When any filter action is run, the extension:
1. Calls `mame.exe -listxml` and streams the output to a temp file (to avoid loading the ~500MB XML into memory)
2. Parses the XML using a streaming `XmlReader`, building a hashtable of ROM name → driver status
3. Matches each Playnite game by `game.Name` (lowercase) against the hashtable
4. Takes the requested action based on the driver status

### Driver Status Values

| Status | Meaning |
|---|---|
| `good` | Fully working |
| `imperfect` | Mostly works — minor issues (sound glitches, graphical errors, etc.) |
| `preliminary` | Non-working / broken |

## Menu Options

### MAME: Tag All Games by Status *(recommended first step)*
Tags every matched game with one of:
- `MAME: Working`
- `MAME: Imperfect`
- `MAME: Non-Working`

Nothing is hidden or removed. Use this to review which games will be affected before running hide or remove. You can then filter your Playnite library by tag to spot-check the results.

### MAME: Hide Non-Working ROMs *(reversible)*
Hides all games with `preliminary` driver status. Games remain in your database but disappear from the library view. Reversible — unhide via Playnite's filter panel (Show Hidden Games).

### MAME: REMOVE Non-Working ROMs *(permanent)*
Permanently deletes all `preliminary` games from your Playnite library. Shows a confirmation prompt before proceeding. **This cannot be undone.**

### MAME: Diagnose GameId Format
Writes a diagnostic file to your Desktop (`MameFilter_Diagnose.txt`) showing the `Name`, `GameId`, `Source`, and `ImagePath` for the first 10 games in your library. Opens the file automatically in Notepad++. Use this to verify how your MAME games are stored in Playnite if matching results seem off.

## Recommended Workflow

1. Run **Tag by Status** first
2. In Playnite, filter by tag `MAME: Non-Working` and spot-check a few entries
3. Once satisfied, run **Hide Non-Working ROMs** to clean up your library view
4. Optionally run **Remove Non-Working ROMs** later if you want to permanently clean up

## Notes on MAME Import

Playnite does not currently have native built-in support for MAME as an arcade emulator. When importing via the emulator auto-scan:

- Games are imported with their ROM filename (without extension) as the `Name` field (e.g. `pacman`, `aof`, `1943`)
- The `GameId` field will be a GUID rather than the ROM name — this is a current Playnite limitation with arcade emulators and does not affect this extension's matching
- The `Source` field will be blank for the same reason

This extension matches on `game.Name` (lowercased) rather than `GameId` to work around this limitation.

After importing, the [MAME Utility](https://github.com/gerrykeys/playnite-mame-utility) extension can rename games from their ROM names to their proper display names (e.g. `aof` → `Art of Fighting`).

## Troubleshooting

**Extension doesn't appear in Playnite menu**
- Confirm files are in a subfolder under `Extensions\`, not loose in the `Extensions\` folder itself
- Confirm `extension.yaml` has a GUID-format `Id` field
- Confirm the file is named `.psm1`, not `.ps1`
- Check `%APPDATA%\Playnite\playnite.log` for load errors

**Only a few games matched / most skipped**
- Run **Diagnose GameId Format** and check the `Name` field matches what MAME uses as the machine name
- If games have been renamed by MAME Utility to display names (e.g. `Pac-Man`), they will no longer match ROM names (`pacman`) — run the filter before renaming, or re-import

**Out of memory error**
- Ensure you are using the current version of the script which uses streaming XML parsing and a temp file rather than loading the full XML into memory

**Wrong path error**
- Update `$MameExePath` at the top of `MameWorkingFilter.psm1` to match your MAME installation path

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

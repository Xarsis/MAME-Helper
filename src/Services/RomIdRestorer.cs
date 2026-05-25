using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MAMEHelper.Models;
using Playnite.SDK;

namespace MAMEHelper.Services
{
    /// <summary>
    /// Restores the ROM name into the Notes field for games that were renamed
    /// before MAME Helper was installed, or where the Notes entry is missing.
    ///
    /// Writes "MAME Helper - original name: {romname}" into Notes, preserving
    /// any existing note content. The ROM name is sourced from the ROM file path.
    ///
    /// After this operation, all tag, filter, and catver operations will
    /// correctly match renamed games via MatchingHelper.ResolveRomKey.
    /// </summary>
    public class RomIdRestorer
    {
        private readonly IPlayniteAPI _api;
        private readonly ILogger _logger = LogManager.GetLogger();

        public RomIdRestorer(IPlayniteAPI api) => _api = api;

        public void RestoreGameIds(Dictionary<string, RomsetMachine> romData)
        {
            int restored = 0;
            int skipped  = 0;  // Notes already has a valid MAME Helper entry
            int noRom    = 0;  // No ROM path to read from
            int noMatch  = 0;  // ROM filename not found in MAME data

            var games = _api.Database.Games.ToList();

            _api.Dialogs.ActivateGlobalProgress(args =>
            {
                args.ProgressMaxValue = games.Count;

                _api.Database.BeginBufferUpdate();
                try
                {
                    foreach (var game in games)
                    {
                        if (args.CancelToken.IsCancellationRequested) break;

                        args.CurrentProgressValue++;
                        args.Text =
                            $"Restoring ROM names ({args.CurrentProgressValue}/{games.Count})\n{game.Name}";

                        // Skip if Notes already has a valid MAME Helper entry.
                        string existing = MatchingHelper.ExtractRomNameFromNotes(game.Notes);
                        if (existing != null && romData.ContainsKey(existing))
                        {
                            skipped++;
                            continue;
                        }

                        // Get ROM name from the file path.
                        if (game.Roms == null || game.Roms.Count == 0)
                        {
                            noRom++;
                            continue;
                        }

                        string romFileName = Path.GetFileNameWithoutExtension(
                            game.Roms[0].Path)?.ToLower().Trim();

                        if (string.IsNullOrEmpty(romFileName))
                        {
                            noRom++;
                            continue;
                        }

                        // Confirm ROM filename exists in MAME data.
                        if (!romData.ContainsKey(romFileName))
                        {
                            noMatch++;
                            continue;
                        }

                        // Append to Notes, preserving any existing content.
                        _logger.Info(
                            $"MAMEHelper: Restore ROM name '{game.Name}' → Notes: '{romFileName}'");

                        game.Notes = MatchingHelper.BuildUpdatedNotes(game.Notes, romFileName);
                        _api.Database.Games.Update(game);
                        restored++;
                    }
                }
                finally { _api.Database.EndBufferUpdate(); }

            }, new GlobalProgressOptions("MAME Helper: Restoring ROM names into Notes…", true));

            _api.Dialogs.ShowMessage(
                $"Done.\n\n" +
                $"ROM names restored:  {restored}\n" +
                $"Already correct:     {skipped}\n" +
                $"No ROM path:         {noRom}\n" +
                $"Not in MAME data:    {noMatch}",
                "MAME Helper");
        }
    }
}

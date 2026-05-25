using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MAMEHelper.Models;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace MAMEHelper.Services
{
    /// <summary>
    /// Renames selected games from their ROM-name form (e.g. "pacman") to the
    /// proper display name from the MAME XML description (e.g. "Pac-Man (Midway)").
    ///
    /// Before renaming, the original ROM name is appended to the Notes field
    /// with the prefix "MAME Helper - original name: " so that tag, filter,
    /// and catver operations can still match the game after renaming.
    /// Any existing Notes content is preserved.
    ///
    /// Operates on the current Playnite selection.
    /// </summary>
    public class GameRenamer
    {
        private static readonly Regex TrailingParenRegex =
            new Regex(@"\s*\([^)]*\)\s*$", RegexOptions.Compiled);

        private readonly IPlayniteAPI _api;
        private readonly ILogger _logger = LogManager.GetLogger();

        public GameRenamer(IPlayniteAPI api) => _api = api;

        // ── Public operations ─────────────────────────────────────────────────

        public void RenameWithInfo(Dictionary<string, RomsetMachine> romData)
            => RunRenameOperation(romData, keepInfo: true);

        public void RenameWithoutInfo(Dictionary<string, RomsetMachine> romData)
            => RunRenameOperation(romData, keepInfo: false);

        // ── Runner ────────────────────────────────────────────────────────────

        private void RunRenameOperation(Dictionary<string, RomsetMachine> romData, bool keepInfo)
        {
            var selected = _api.MainView.SelectedGames?.ToList();
            if (selected == null || selected.Count == 0)
            {
                _api.Dialogs.ShowMessage(
                    "No games selected.\n\nSelect one or more games in the library before running Rename.",
                    "MAME Helper");
                return;
            }

            int renamed  = 0;
            int skipped  = 0;
            int noMatch  = 0;

            _api.Dialogs.ActivateGlobalProgress(args =>
            {
                args.ProgressMaxValue = selected.Count;

                _api.Database.BeginBufferUpdate();
                try
                {
                    foreach (var game in selected)
                    {
                        if (args.CancelToken.IsCancellationRequested) break;

                        args.CurrentProgressValue++;
                        args.Text = $"Renaming ({args.CurrentProgressValue}/{selected.Count})\n{game.Name}";

                        var machine = FindMachine(romData, game);
                        if (machine == null)
                        {
                            noMatch++;
                            continue;
                        }

                        string newName = keepInfo
                            ? machine.Description
                            : StripParens(machine.Description);

                        if (string.IsNullOrWhiteSpace(newName))
                        {
                            skipped++;
                            continue;
                        }

                        // Write ROM name into Notes with prefix, preserving existing content.
                        // Uses BuildUpdatedNotes so re-running rename won't duplicate the entry.
                        game.Notes = MatchingHelper.BuildUpdatedNotes(game.Notes, machine.RomName);

                        // Only update Name if it has actually changed.
                        if (game.Name != newName)
                        {
                            _logger.Info(
                                $"MAMEHelper: Rename '{game.Name}' → '{newName}' " +
                                $"(ROM: {machine.RomName})");
                            game.Name = newName;
                        }

                        _api.Database.Games.Update(game);
                        renamed++;
                    }
                }
                finally { _api.Database.EndBufferUpdate(); }

            }, new GlobalProgressOptions("MAME Helper: Renaming games…", true));

            _api.Dialogs.ShowMessage(
                $"Done.\n\n" +
                $"Renamed:          {renamed}\n" +
                $"Already correct:  {skipped}\n" +
                $"No MAME match:  {noMatch}",
                "MAME Helper");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Tries to find a RomsetMachine for a game.
        /// Uses MatchingHelper which checks the Notes prefix first,
        /// then falls back to game.Name, then to the ROM file path.
        /// </summary>
        private static RomsetMachine FindMachine(
            Dictionary<string, RomsetMachine> romData, Game game)
        {
            // Check Notes prefix and game.Name via MatchingHelper.
            string key = MatchingHelper.ResolveRomKey(game);
            if (key != null && romData.TryGetValue(key, out var m1))
                return m1;

            // Final fallback — ROM filename from the file path.
            if (game.Roms != null && game.Roms.Count > 0)
            {
                string romKey = Path.GetFileNameWithoutExtension(game.Roms[0].Path)
                                    ?.ToLower().Trim();
                if (romKey != null && romData.TryGetValue(romKey, out var m2))
                    return m2;
            }

            return null;
        }

        private static string StripParens(string description)
        {
            if (string.IsNullOrEmpty(description)) return description;
            return TrailingParenRegex.Replace(description, string.Empty).Trim();
        }
    }
}

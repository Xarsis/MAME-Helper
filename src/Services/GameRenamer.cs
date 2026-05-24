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
    /// Operates on the current Playnite selection.
    /// Matching uses game.Name first; if that fails it tries the ROM filename
    /// from game.Roms[0].Path (without extension) as a fallback.
    /// </summary>
    public class GameRenamer
    {
        private static readonly Regex TrailingParenRegex =
            new Regex(@"\s*\([^)]*\)\s*$", RegexOptions.Compiled);

        private readonly IPlayniteAPI _api;
        private readonly ILogger _logger = LogManager.GetLogger();

        public GameRenamer(IPlayniteAPI api) => _api = api;

        // ── Public operations ─────────────────────────────────────────────────

        /// <summary>Renames selected games, keeping region/revision info in parentheses.</summary>
        public void RenameWithInfo(Dictionary<string, RomsetMachine> romData)
            => RunRenameOperation(romData, keepInfo: true);

        /// <summary>Renames selected games, stripping trailing parenthetical region/revision info.</summary>
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

                        if (string.IsNullOrWhiteSpace(newName) || game.Name == newName)
                        {
                            skipped++;
                            continue;
                        }

                        _logger.Info($"MAMEHelper: Rename '{game.Name}' → '{newName}'");
                        game.Name = newName;
                        _api.Database.Games.Update(game);
                        renamed++;
                    }
                }
                finally { _api.Database.EndBufferUpdate(); }

            }, new GlobalProgressOptions("MAME Helper: Renaming games…", true));

            _api.Dialogs.ShowMessage(
                $"Done.\n\nRenamed:          {renamed}\n" +
                $"Already correct:  {skipped}\n" +
                $"No MAME match:  {noMatch}",
                "MAME Helper");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Tries to find a RomsetMachine for a game.
        /// First matches by game.Name; if that fails, tries the ROM filename.
        /// </summary>
        private static RomsetMachine FindMachine(
            Dictionary<string, RomsetMachine> romData, Game game)
        {
            // Try game.Name first (works after import, before any renaming).
            string key = game.Name?.ToLower().Trim();
            if (key != null && romData.TryGetValue(key, out var m1))
                return m1;

            // Fall back to ROM filename (useful if the game was already partially renamed).
            if (game.Roms != null && game.Roms.Count > 0)
            {
                string romKey = Path.GetFileNameWithoutExtension(game.Roms[0].Path)
                                    ?.ToLower().Trim();
                if (romKey != null && romData.TryGetValue(romKey, out var m2))
                    return m2;
            }

            return null;
        }

        /// <summary>Strips the last set of parentheses from a name, e.g. "Pac-Man (Midway)" → "Pac-Man".</summary>
        private static string StripParens(string description)
        {
            if (string.IsNullOrEmpty(description)) return description;
            return TrailingParenRegex.Replace(description, string.Empty).Trim();
        }
    }
}

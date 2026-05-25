using System;
using System.Collections.Generic;
using System.Linq;
using MAMEHelper.Models;
using Playnite.SDK;

namespace MAMEHelper.Services
{
    public class GameRemover
    {
        private readonly IPlayniteAPI _api;
        private readonly ILogger _logger = LogManager.GetLogger();

        public GameRemover(IPlayniteAPI api) => _api = api;

        // ── Public operations ─────────────────────────────────────────────────

        public void RemoveNonWorking(Dictionary<string, RomsetMachine> romData)
            => RunRemoveOperation("Remove Non-Working ROMs", romData,
                m => m.DriverStatus?.ToLower() == "preliminary");

        public void RemoveClones(Dictionary<string, RomsetMachine> romData)
            => RunRemoveOperation("Remove Clones", romData, m => m.IsClone);

        public void RemoveNonGames(Dictionary<string, RomsetMachine> romData)
            => RunRemoveOperation("Remove Non-Games", romData, m => m.IsNonGame);

        /// <summary>
        /// Permanently removes games whose catver.ini top-level category is
        /// in the NonGameCategories set.
        /// </summary>
        public void RemoveNonGamesByCatver(
            Dictionary<string, RomsetMachine> romData,
            Dictionary<string, string> catverData)
        {
            RunRemoveOperationWithCatver(
                "Remove Non-Games (catver.ini)",
                romData,
                catverData,
                category => CatverParser.NonGameCategories.Contains(
                    CatverParser.GetTopLevel(category)));
        }

        // ── Shared runners ────────────────────────────────────────────────────

        private void RunRemoveOperation(
            string operationName,
            Dictionary<string, RomsetMachine> romData,
            Func<RomsetMachine, bool> shouldRemove)
        {
            var toRemove = new List<Guid>();
            int skipped  = 0;

            foreach (var game in _api.Database.Games)
            {
                string key = MatchingHelper.ResolveRomKey(game);
                if (key == null || !romData.TryGetValue(key, out var machine))
                {
                    skipped++;
                    continue;
                }
                if (shouldRemove(machine))
                    toRemove.Add(game.Id);
            }

            ExecuteRemove(operationName, toRemove, skipped, 0);
        }

        private void RunRemoveOperationWithCatver(
            string operationName,
            Dictionary<string, RomsetMachine> romData,
            Dictionary<string, string> catverData,
            Func<string, bool> shouldRemove)
        {
            var toRemove = new List<Guid>();
            int skipped  = 0;
            int noCatver = 0;

            foreach (var game in _api.Database.Games)
            {
                string key = MatchingHelper.ResolveRomKey(game);
                if (key == null || !romData.ContainsKey(key))
                {
                    skipped++;
                    continue;
                }

                if (!catverData.TryGetValue(key, out var category))
                {
                    noCatver++;
                    continue;
                }

                if (shouldRemove(category))
                    toRemove.Add(game.Id);
            }

            ExecuteRemove(operationName, toRemove, skipped, noCatver);
        }

        /// <summary>
        /// Shows confirmation, then removes the collected game IDs with a progress bar.
        /// </summary>
        private void ExecuteRemove(
            string operationName,
            List<Guid> toRemove,
            int skipped,
            int noCatver)
        {
            if (toRemove.Count == 0)
            {
                _api.Dialogs.ShowMessage(
                    $"No matching games found to remove.\n\n" +
                    $"No MAME match:     {skipped}" +
                    (noCatver > 0 ? $"\nNot in catver.ini: {noCatver}" : ""),
                    "MAME Helper");
                return;
            }

            var confirm = _api.Dialogs.ShowMessage(
                $"This will permanently remove {toRemove.Count} game(s) from your library.\n\n" +
                "This cannot be undone. Continue?",
                "MAME Helper — Confirm Remove",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (confirm != System.Windows.MessageBoxResult.Yes)
                return;

            int removed = 0;

            _api.Dialogs.ActivateGlobalProgress(args =>
            {
                args.ProgressMaxValue = toRemove.Count;

                const int batchSize = 500;
                for (int i = 0; i < toRemove.Count; i += batchSize)
                {
                    if (args.CancelToken.IsCancellationRequested) break;

                    int end = Math.Min(i + batchSize, toRemove.Count);
                    for (int j = i; j < end; j++)
                    {
                        _api.Database.Games.Remove(toRemove[j]);
                        removed++;
                    }

                    args.CurrentProgressValue = removed;
                    args.Text = $"{operationName}: removed {removed} / {toRemove.Count}";
                }

            }, new GlobalProgressOptions($"MAME Helper: {operationName}…", true));

            _api.Dialogs.ShowMessage(
                $"Done.\n\n" +
                $"Removed:             {removed}\n" +
                $"No MAME match:     {skipped}" +
                (noCatver > 0 ? $"\nNot in catver.ini: {noCatver}" : ""),
                "MAME Helper");
        }
    }
}

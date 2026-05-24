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

        // ── Shared runner ─────────────────────────────────────────────────────

        private void RunRemoveOperation(
            string operationName,
            Dictionary<string, RomsetMachine> romData,
            Func<RomsetMachine, bool> shouldRemove)
        {
            // First pass: collect IDs to remove (fast, no UI).
            var toRemove = new List<Guid>();
            int skipped  = 0;

            foreach (var game in _api.Database.Games)
            {
                string key = game.Name?.ToLower().Trim();
                if (key == null || !romData.TryGetValue(key, out var machine))
                {
                    skipped++;
                    continue;
                }
                if (shouldRemove(machine))
                    toRemove.Add(game.Id);
            }

            if (toRemove.Count == 0)
            {
                _api.Dialogs.ShowMessage(
                    $"No matching games found to remove.\n\nNo MAME match: {skipped}",
                    "MAME Helper");
                return;
            }

            // Confirm before deleting.
            var confirm = _api.Dialogs.ShowMessage(
                $"This will permanently remove {toRemove.Count} game(s) from your Playnite library.\n\n" +
                "This cannot be undone. Continue?",
                "MAME Helper — Confirm Remove",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (confirm != System.Windows.MessageBoxResult.Yes)
                return;

            // Second pass: remove with progress bar.
            int removed = 0;

            _api.Dialogs.ActivateGlobalProgress(args =>
            {
                args.ProgressMaxValue = toRemove.Count;

                // Remove in batches of 500 to avoid DB churn.
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
                $"Done.\n\nRemoved:          {removed}\nNo MAME match: {skipped}",
                "MAME Helper");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MAMEHelper.Models;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace MAMEHelper.Services
{
    /// <summary>
    /// Sets non-tag metadata fields on MAME games:
    ///   - Category (Playnite Category objects)
    ///   - Source     (Playnite Source object)
    ///   - Platform   (Playnite Platform object)
    ///   - Year and Manufacturer (from ROM data → ReleaseYear and Developers fields)
    /// </summary>
    public class GameMetadataSetter
    {
        private readonly IPlayniteAPI _api;
        private readonly ILogger _logger = LogManager.GetLogger();

        public GameMetadataSetter(IPlayniteAPI api) => _api = api;

        // ── Public operations ─────────────────────────────────────────────────

        /// <summary>Sets the Category field on all matched MAME games to the supplied name.</summary>
        public void SetCategory(Dictionary<string, RomsetMachine> romData, string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName)) return;

            var category = _api.Database.Categories.Add(categoryName); // idempotent

            RunMetadataOperation("Set Category", romData, (game, machine) =>
            {
                if (game.CategoryIds == null)
                    game.CategoryIds = new List<Guid>();

                if (!game.CategoryIds.Contains(category.Id))
                    game.CategoryIds.Add(category.Id);
            });
        }

        /// <summary>Sets the Source field on all matched MAME games to the supplied name.</summary>
        public void SetSource(Dictionary<string, RomsetMachine> romData, string sourceName)
        {
            if (string.IsNullOrWhiteSpace(sourceName)) return;

            var source = _api.Database.Sources.Add(sourceName); // idempotent

            RunMetadataOperation("Set Source", romData, (game, machine) =>
            {
                game.SourceId = source.Id;
            });
        }

        /// <summary>Sets the Platform field on all matched MAME games to the supplied name.</summary>
        public void SetPlatform(Dictionary<string, RomsetMachine> romData, string platformName)
        {
            if (string.IsNullOrWhiteSpace(platformName)) return;

            var platform = _api.Database.Platforms.Add(platformName); // idempotent

            RunMetadataOperation("Set Platform", romData, (game, machine) =>
            {
                if (game.PlatformIds == null)
                    game.PlatformIds = new List<Guid>();

                if (!game.PlatformIds.Contains(platform.Id))
                    game.PlatformIds.Add(platform.Id);
            });
        }

        /// <summary>
        /// Populates ReleaseYear and Developers from ROM data for all matched games.
        /// Only sets the field if it is currently empty (won't overwrite existing metadata).
        /// </summary>
        public void SetYearAndManufacturer(Dictionary<string, RomsetMachine> romData)
        {
            RunMetadataOperation("Set Year and Manufacturer", romData, (game, machine) =>
            {
                // Year → ReleaseYear (int)
                if (game.ReleaseDate == null &&
                    int.TryParse(machine.Year, out int year) &&
                    year > 1970 && year < 2100)
                {
                    game.ReleaseDate = new Playnite.SDK.Models.ReleaseDate(year, 1, 1);
                }

                // Manufacturer → Developers
                if (!string.IsNullOrWhiteSpace(machine.Manufacturer) &&
                    (game.DeveloperIds == null || game.DeveloperIds.Count == 0))
                {
                    // Clean up the manufacturer name — MAME often includes trailing
                    // punctuation or region markers, e.g. "Namco (Japan)".
                    string mfr = CleanManufacturer(machine.Manufacturer);
                    if (!string.IsNullOrWhiteSpace(mfr))
                    {
                        var developer = _api.Database.Companies.Add(mfr);
                        if (game.DeveloperIds == null)
                            game.DeveloperIds = new List<Guid>();
                        if (!game.DeveloperIds.Contains(developer.Id))
                            game.DeveloperIds.Add(developer.Id);
                    }
                }
            });
        }

        // ── Shared runner ─────────────────────────────────────────────────────

        private void RunMetadataOperation(
            string operationName,
            Dictionary<string, RomsetMachine> romData,
            Action<Game, RomsetMachine> applyAction)
        {
            int updated = 0;
            int skipped = 0;

            _api.Dialogs.ActivateGlobalProgress(args =>
            {
                var games = _api.Database.Games.ToList();
                args.ProgressMaxValue = games.Count;

                _api.Database.BeginBufferUpdate();
                try
                {
                    foreach (var game in games)
                    {
                        if (args.CancelToken.IsCancellationRequested) break;

                        args.CurrentProgressValue++;
                        args.Text = $"{operationName} ({args.CurrentProgressValue}/{games.Count})\n{game.Name}";

                        string key = game.Name?.ToLower().Trim();
                        if (key == null || !romData.TryGetValue(key, out var machine))
                        {
                            skipped++;
                            continue;
                        }

                        applyAction(game, machine);
                        _api.Database.Games.Update(game);
                        updated++;
                    }
                }
                finally { _api.Database.EndBufferUpdate(); }

            }, new GlobalProgressOptions($"MAME Helper: {operationName}…", true));

            _api.Dialogs.ShowMessage(
                $"Done.\n\nUpdated:          {updated}\nNo MAME match: {skipped}",
                "MAME Helper");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Cleans common MAME manufacturer string noise.
        /// E.g. "Namco (Japan)" → "Namco", "bootleg" → "bootleg"
        /// </summary>
        private static string CleanManufacturer(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw;

            // Remove trailing region markers like "(Japan)", "(USA)", "(Europe)".
            var cleaned = System.Text.RegularExpressions.Regex.Replace(
                raw.Trim(),
                @"\s*\((Japan|USA|Europe|World|Korea|Asia|US|UK)\)\s*$",
                string.Empty,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return cleaned.Trim();
        }
    }
}

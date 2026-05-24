using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MAMEHelper.Models;
using Playnite.SDK;

namespace MAMEHelper.Services
{
    /// <summary>
    /// Exports a CSV summary of all MAME games in the library.
    /// Columns: RomName, DisplayName, Year, Manufacturer, DriverStatus,
    ///          IsClone, CloneOf, IsBios, IsDevice, IsMechanical, IsSample,
    ///          HasCover, HasBackground, Hidden, Tags
    /// </summary>
    public class CsvExporter
    {
        private readonly IPlayniteAPI _api;
        private readonly ILogger _logger = LogManager.GetLogger();

        public CsvExporter(IPlayniteAPI api) => _api = api;

        public void Export(Dictionary<string, RomsetMachine> romData)
        {
            string outputPath = _api.Dialogs.SaveFile(
                "CSV Files|*.csv|All Files|*.*",
                "mame_library.csv");

            if (string.IsNullOrEmpty(outputPath))
                return;

            int exported = 0;
            int skipped  = 0;

            _api.Dialogs.ActivateGlobalProgress(args =>
            {
                var games = _api.Database.Games.ToList();
                args.ProgressMaxValue = games.Count;

                try
                {
                    using (var writer = new StreamWriter(outputPath, false, new UTF8Encoding(true)))
                    {
                        // Header row.
                        writer.WriteLine(
                            "RomName,DisplayName,Year,Manufacturer,DriverStatus," +
                            "IsClone,CloneOf,IsBios,IsDevice,IsMechanical,IsSample," +
                            "HasCover,HasBackground,Hidden,Tags");

                        foreach (var game in games)
                        {
                            if (args.CancelToken.IsCancellationRequested) break;

                            args.CurrentProgressValue++;
                            args.Text = $"Exporting CSV ({args.CurrentProgressValue}/{games.Count})\n{game.Name}";

                            string key = game.Name?.ToLower().Trim();
                            if (key == null || !romData.TryGetValue(key, out var machine))
                            {
                                skipped++;
                                continue;
                            }

                            // Build tag list string.
                            string tags = string.Empty;
                            if (game.TagIds != null && game.TagIds.Count > 0)
                            {
                                var tagNames = game.TagIds
                                    .Select(id => _api.Database.Tags.Get(id)?.Name)
                                    .Where(n => n != null);
                                tags = string.Join("|", tagNames);
                            }

                            writer.WriteLine(string.Join(",",
                                CsvEscape(machine.RomName),
                                CsvEscape(machine.Description ?? game.Name),
                                CsvEscape(machine.Year),
                                CsvEscape(machine.Manufacturer),
                                CsvEscape(machine.DriverStatus),
                                machine.IsClone      ? "true" : "false",
                                CsvEscape(machine.CloneOf),
                                machine.IsBios       ? "true" : "false",
                                machine.IsDevice     ? "true" : "false",
                                machine.IsMechanical ? "true" : "false",
                                machine.IsSample     ? "true" : "false",
                                !string.IsNullOrEmpty(game.CoverImage)      ? "true" : "false",
                                !string.IsNullOrEmpty(game.BackgroundImage) ? "true" : "false",
                                game.Hidden          ? "true" : "false",
                                CsvEscape(tags)));

                            exported++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "MAMEHelper: Error exporting CSV.");
                    _api.Dialogs.ShowErrorMessage(
                        $"Error exporting CSV:\n{ex.Message}", "MAME Helper");
                }

            }, new GlobalProgressOptions("MAME Helper: Exporting library CSV…", true));

            _api.Dialogs.ShowMessage(
                $"Done.\n\nExported:         {exported}\nNo MAME match: {skipped}\n\nSaved to:\n{outputPath}",
                "MAME Helper");
        }

        /// <summary>Wraps a CSV field in quotes and escapes internal quotes.</summary>
        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}

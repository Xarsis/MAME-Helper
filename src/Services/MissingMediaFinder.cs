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
    /// Scans the library for MAME games that are missing cover or background images
    /// and writes a report to a user-chosen text file.
    /// </summary>
    public class MissingMediaFinder
    {
        private readonly IPlayniteAPI _api;
        private readonly ILogger _logger = LogManager.GetLogger();

        public MissingMediaFinder(IPlayniteAPI api) => _api = api;

        public void FindMissingMedia(Dictionary<string, RomsetMachine> romData)
        {
            string outputPath = _api.Dialogs.SaveFile(
                "Text Files|*.txt|All Files|*.*",
                "mame_missing_media.txt");

            if (string.IsNullOrEmpty(outputPath))
                return;

            int totalMatched  = 0;
            int missingCover  = 0;
            int missingBg     = 0;
            int missingBoth   = 0;
            int skipped       = 0;

            _api.Dialogs.ActivateGlobalProgress(args =>
            {
                var games = _api.Database.Games.ToList();
                args.ProgressMaxValue = games.Count;

                var missingCoverList = new List<string>();
                var missingBgList    = new List<string>();
                var missingBothList  = new List<string>();

                foreach (var game in games)
                {
                    if (args.CancelToken.IsCancellationRequested) break;

                    args.CurrentProgressValue++;
                    args.Text = $"Scanning ({args.CurrentProgressValue}/{games.Count})\n{game.Name}";

                    string key = game.Name?.ToLower().Trim();
                    if (key == null || !romData.ContainsKey(key))
                    {
                        skipped++;
                        continue;
                    }

                    totalMatched++;
                    bool noCover = string.IsNullOrEmpty(game.CoverImage);
                    bool noBg    = string.IsNullOrEmpty(game.BackgroundImage);

                    string entry = $"{game.Name}  [{key}]";

                    if (noCover && noBg)
                    {
                        missingBothList.Add(entry);
                        missingBoth++;
                    }
                    else if (noCover)
                    {
                        missingCoverList.Add(entry);
                        missingCover++;
                    }
                    else if (noBg)
                    {
                        missingBgList.Add(entry);
                        missingBg++;
                    }
                }

                // Write report.
                try
                {
                    using (var writer = new StreamWriter(outputPath, false, new UTF8Encoding(true)))
                    {
                        writer.WriteLine("MAME Helper — Missing Media Report");
                        writer.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}");
                        writer.WriteLine(new string('─', 60));
                        writer.WriteLine($"Total matched games: {totalMatched}");
                        writer.WriteLine($"Missing both cover and background: {missingBoth}");
                        writer.WriteLine($"Missing cover only: {missingCover}");
                        writer.WriteLine($"Missing background only: {missingBg}");
                        writer.WriteLine($"Not in MAME data: {skipped}");
                        writer.WriteLine();

                        if (missingBothList.Count > 0)
                        {
                            writer.WriteLine("── MISSING BOTH COVER AND BACKGROUND ──");
                            foreach (var line in missingBothList.OrderBy(s => s))
                                writer.WriteLine("  " + line);
                            writer.WriteLine();
                        }

                        if (missingCoverList.Count > 0)
                        {
                            writer.WriteLine("── MISSING COVER ONLY ──");
                            foreach (var line in missingCoverList.OrderBy(s => s))
                                writer.WriteLine("  " + line);
                            writer.WriteLine();
                        }

                        if (missingBgList.Count > 0)
                        {
                            writer.WriteLine("── MISSING BACKGROUND ONLY ──");
                            foreach (var line in missingBgList.OrderBy(s => s))
                                writer.WriteLine("  " + line);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "MAMEHelper: Error writing missing media report.");
                    _api.Dialogs.ShowErrorMessage(
                        $"Error writing report:\n{ex.Message}", "MAME Helper");
                }

            }, new GlobalProgressOptions("MAME Helper: Scanning for missing media…", true));

            _api.Dialogs.ShowMessage(
                $"Done.\n\n" +
                $"Total matched:    {totalMatched}\n" +
                $"Missing both:     {missingBoth}\n" +
                $"Missing cover:    {missingCover}\n" +
                $"Missing bg:       {missingBg}\n\n" +
                $"Report saved to:\n{outputPath}",
                "MAME Helper");
        }
    }
}

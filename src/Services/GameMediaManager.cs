using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MAMEHelper.Models;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace MAMEHelper.Services
{
    /// <summary>
    /// Sets cover or background images on selected games by matching ROM names
    /// to PNG files in user-configured folders.
    ///
    /// Lookup order for each game:
    ///   1. Primary folder — direct ROM name match
    ///   2. Primary folder — parent ROM name (for clones)
    ///   3. Secondary folder — direct ROM name match
    ///   4. Secondary folder — parent ROM name (for clones)
    ///
    /// Operates on selected games only.
    /// </summary>
    public class GameMediaManager
    {
        public enum MediaType { Cover, Background }

        private readonly IPlayniteAPI _api;
        private readonly ILogger _logger = LogManager.GetLogger();

        public GameMediaManager(IPlayniteAPI api) => _api = api;

        // ── Public operations ─────────────────────────────────────────────────

        public void SetCoverImages(
            Dictionary<string, RomsetMachine> romData,
            string primaryFolder,
            string secondaryFolder)
            => RunMediaOperation(romData, primaryFolder, secondaryFolder, MediaType.Cover);

        public void SetBackgroundImages(
            Dictionary<string, RomsetMachine> romData,
            string primaryFolder,
            string secondaryFolder)
            => RunMediaOperation(romData, primaryFolder, secondaryFolder, MediaType.Background);

        // ── Runner ────────────────────────────────────────────────────────────

        private void RunMediaOperation(
            Dictionary<string, RomsetMachine> romData,
            string primaryFolder,
            string secondaryFolder,
            MediaType mediaType)
        {
            // Validate at least the primary folder.
            if (string.IsNullOrWhiteSpace(primaryFolder) || !Directory.Exists(primaryFolder))
            {
                _api.Dialogs.ShowErrorMessage(
                    $"Primary image folder not found:\n{primaryFolder}\n\n" +
                    "Go to Extensions → MAME Helper → Settings to configure the folder.",
                    "MAME Helper");
                return;
            }

            var selected = _api.MainView.SelectedGames?.ToList();
            if (selected == null || selected.Count == 0)
            {
                _api.Dialogs.ShowMessage(
                    "No games selected.\n\nSelect one or more games before setting images.",
                    "MAME Helper");
                return;
            }

            // Build image lookups. Secondary is optional — empty dict if not configured.
            var primaryImages   = BuildImageLookup(primaryFolder);
            var secondaryImages = IsValidFolder(secondaryFolder)
                ? BuildImageLookup(secondaryFolder)
                : new Dictionary<string, string>();

            if (primaryImages.Count == 0 && secondaryImages.Count == 0)
            {
                _api.Dialogs.ShowMessage(
                    $"No PNG files found in either configured folder.",
                    "MAME Helper");
                return;
            }

            string typeName = mediaType == MediaType.Cover ? "cover" : "background";
            int applied  = 0;
            int missing  = 0;
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
                        args.Text = $"Setting {typeName} images " +
                                    $"({args.CurrentProgressValue}/{selected.Count})\n{game.Name}";

                        string key = MatchingHelper.ResolveRomKey(game);
                        if (key == null)
                        {
                            noMatch++;
                            continue;
                        }

                        // Resolve parent ROM name for clone fallback.
                        string parentKey = null;
                        if (romData.TryGetValue(key, out var machine) &&
                            machine.IsClone &&
                            !string.IsNullOrEmpty(machine.CloneOf))
                        {
                            parentKey = machine.CloneOf;
                        }

                        // Search order:
                        // 1. Primary — direct match
                        // 2. Primary — parent match
                        // 3. Secondary — direct match
                        // 4. Secondary — parent match
                        string imagePath =
                            FindInLookup(primaryImages, key) ??
                            FindInLookup(primaryImages, parentKey) ??
                            FindInLookup(secondaryImages, key) ??
                            FindInLookup(secondaryImages, parentKey);

                        if (imagePath == null)
                        {
                            missing++;
                            _logger.Info(
                                $"MAMEHelper: No {typeName} image for '{game.Name}' " +
                                $"in primary or secondary folder.");
                            continue;
                        }

                        ApplyImage(game, imagePath, mediaType);
                        _api.Database.Games.Update(game);
                        applied++;
                    }
                }
                finally { _api.Database.EndBufferUpdate(); }

            }, new GlobalProgressOptions($"MAME Helper: Setting {typeName} images…", true));

            _api.Dialogs.ShowMessage(
                $"Done.\n\n" +
                $"Images set:       {applied}\n" +
                $"No image found:   {missing}\n" +
                $"No MAME match:  {noMatch}",
                "MAME Helper");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static bool IsValidFolder(string path)
            => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);

        /// <summary>
        /// Builds a dictionary of lowercase-romname → full file path
        /// for all PNG files in the given folder.
        /// </summary>
        private static Dictionary<string, string> BuildImageLookup(string folder)
        {
            var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.GetFiles(folder, "*.png"))
            {
                string key = Path.GetFileNameWithoutExtension(file).ToLower();
                lookup[key] = file;
            }
            return lookup;
        }

        /// <summary>
        /// Returns the image path for the given key from the lookup,
        /// or null if the key is null or not found.
        /// </summary>
        private static string FindInLookup(Dictionary<string, string> lookup, string key)
        {
            if (key == null || lookup == null || lookup.Count == 0)
                return null;
            return lookup.TryGetValue(key, out var path) ? path : null;
        }

        /// <summary>
        /// Removes any existing image of the given type, then imports the new file
        /// into Playnite's media database.
        /// </summary>
        private void ApplyImage(Game game, string imagePath, MediaType mediaType)
        {
            if (mediaType == MediaType.Cover)
            {
                if (!string.IsNullOrEmpty(game.CoverImage))
                    _api.Database.RemoveFile(game.CoverImage);
                game.CoverImage = _api.Database.AddFile(imagePath, game.Id);
            }
            else
            {
                if (!string.IsNullOrEmpty(game.BackgroundImage))
                    _api.Database.RemoveFile(game.BackgroundImage);
                game.BackgroundImage = _api.Database.AddFile(imagePath, game.Id);
            }
        }
    }
}

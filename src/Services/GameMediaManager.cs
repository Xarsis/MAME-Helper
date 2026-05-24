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
    /// For clone ROMs with no direct image match, automatically falls back to
    /// the parent ROM's image file.
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
            string imageFolder)
            => RunMediaOperation(romData, imageFolder, MediaType.Cover);

        public void SetBackgroundImages(
            Dictionary<string, RomsetMachine> romData,
            string imageFolder)
            => RunMediaOperation(romData, imageFolder, MediaType.Background);

        // ── Runner ────────────────────────────────────────────────────────────

        private void RunMediaOperation(
            Dictionary<string, RomsetMachine> romData,
            string imageFolder,
            MediaType mediaType)
        {
            // Validate folder.
            if (string.IsNullOrWhiteSpace(imageFolder) || !Directory.Exists(imageFolder))
            {
                _api.Dialogs.ShowErrorMessage(
                    $"Image folder not found:\n{imageFolder}\n\n" +
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

            // Build a fast lookup of available image files (lowercase romname → full path).
            var availableImages = BuildImageLookup(imageFolder);
            if (availableImages.Count == 0)
            {
                _api.Dialogs.ShowMessage(
                    $"No PNG files found in:\n{imageFolder}",
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

                        // Find the ROM name key for this game.
                        string key = game.Name?.ToLower().Trim();
                        if (key == null)
                        {
                            noMatch++;
                            continue;
                        }

                        // Locate the image file, falling back to parent ROM for clones.
                        string imagePath = FindImagePath(availableImages, romData, key);
                        if (imagePath == null)
                        {
                            missing++;
                            _logger.Info($"MAMEHelper: No {typeName} image for '{game.Name}'.");
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
        /// Attempts to find an image for the given ROM name.
        /// Falls back to the parent ROM name for clones.
        /// </summary>
        private static string FindImagePath(
            Dictionary<string, string> availableImages,
            Dictionary<string, RomsetMachine> romData,
            string romKey)
        {
            // Direct match.
            if (availableImages.TryGetValue(romKey, out var path))
                return path;

            // If this is a clone, try the parent.
            if (romData.TryGetValue(romKey, out var machine) &&
                machine.IsClone &&
                !string.IsNullOrEmpty(machine.CloneOf))
            {
                if (availableImages.TryGetValue(machine.CloneOf, out var parentPath))
                    return parentPath;
            }

            return null;
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

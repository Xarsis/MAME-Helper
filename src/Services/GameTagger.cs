using System;
using System.Collections.Generic;
using System.Linq;
using MAMEHelper.Models;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace MAMEHelper.Services
{
    /// <summary>
    /// Applies MAME: prefixed tags to library games based on ROM data.
    /// All operations run inside ActivateGlobalProgress.
    /// </summary>
    public class GameTagger
    {
        // ── Tag name constants ────────────────────────────────────────────────

        public const string TagWorking    = "MAME: Working";
        public const string TagImperfect  = "MAME: Imperfect";
        public const string TagNonWorking = "MAME: Non-Working";
        public const string TagParent     = "MAME: Parent";
        public const string TagClone      = "MAME: Clone";
        public const string TagBios       = "MAME: BIOS";
        public const string TagDevice     = "MAME: Device";
        public const string TagMechanical = "MAME: Mechanical";
        public const string TagSample     = "MAME: Sample";

        public static readonly IReadOnlyList<string> AllMameTags = new[]
        {
            TagWorking, TagImperfect, TagNonWorking,
            TagParent, TagClone,
            TagBios, TagDevice, TagMechanical, TagSample
        };

        // ── Dependencies ──────────────────────────────────────────────────────

        private readonly IPlayniteAPI _api;
        private readonly ILogger _logger = LogManager.GetLogger();

        public GameTagger(IPlayniteAPI api) => _api = api;

        // ── Public operations ─────────────────────────────────────────────────

        /// <summary>Tags all matched games with Working / Imperfect / Non-Working.</summary>
        public void TagDriverStatus(Dictionary<string, RomsetMachine> romData)
            => RunTagOperation("Tag: Driver Status", romData, TagDriverStatusCore);

        /// <summary>Tags all matched games with BIOS / Device / Mechanical / Sample.</summary>
        public void TagMachineType(Dictionary<string, RomsetMachine> romData)
            => RunTagOperation("Tag: Machine Type", romData, TagMachineTypeCore);

        /// <summary>Tags all matched games with Parent / Clone.</summary>
        public void TagParentClone(Dictionary<string, RomsetMachine> romData)
            => RunTagOperation("Tag: Parent/Clone", romData, TagParentCloneCore);

        /// <summary>
        /// Removes all MAME: prefixed tags from every game in the library.
        /// Does not require ROM data.
        /// </summary>
        public void ClearAllMameTags()
        {
            // Build set of tag IDs to strip.
            var tagIdsToRemove = new HashSet<Guid>();
            foreach (var tagName in AllMameTags)
            {
                var existing = _api.Database.Tags.FirstOrDefault(
                    t => t.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                    tagIdsToRemove.Add(existing.Id);
            }

            if (tagIdsToRemove.Count == 0)
            {
                _api.Dialogs.ShowMessage("No MAME tags found in the library.", "MAME Helper");
                return;
            }

            int cleared = 0;

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
                        args.Text = $"Clearing tags: {game.Name}";

                        if (game.TagIds == null || game.TagIds.Count == 0) continue;

                        int before = game.TagIds.Count;
                        game.TagIds.RemoveAll(id => tagIdsToRemove.Contains(id));

                        if (game.TagIds.Count != before)
                        {
                            _api.Database.Games.Update(game);
                            cleared++;
                        }
                    }
                }
                finally { _api.Database.EndBufferUpdate(); }

            }, new GlobalProgressOptions("MAME Helper: Clearing MAME tags…", true));

            _api.Dialogs.ShowMessage(
                $"Cleared MAME tags from {cleared} game(s).",
                "MAME Helper");
        }

        // ── Core tagging logic ────────────────────────────────────────────────

        private void TagDriverStatusCore(Game game, RomsetMachine machine)
        {
            // Remove the three driver-status tags then add the correct one.
            RemoveTags(game, TagWorking, TagImperfect, TagNonWorking);

            switch (machine.DriverStatus?.ToLower())
            {
                case "good":        AddTag(game, TagWorking);    break;
                case "imperfect":   AddTag(game, TagImperfect);  break;
                case "preliminary": AddTag(game, TagNonWorking); break;
            }
        }

        private void TagMachineTypeCore(Game game, RomsetMachine machine)
        {
            RemoveTags(game, TagBios, TagDevice, TagMechanical, TagSample);

            if (machine.IsBios)       AddTag(game, TagBios);
            if (machine.IsDevice)     AddTag(game, TagDevice);
            if (machine.IsMechanical) AddTag(game, TagMechanical);
            if (machine.IsSample)     AddTag(game, TagSample);
        }

        private void TagParentCloneCore(Game game, RomsetMachine machine)
        {
            RemoveTags(game, TagParent, TagClone);
            AddTag(game, machine.IsClone ? TagClone : TagParent);
        }

        // ── Shared runner ────────────────────────────────────────────────────

        private void RunTagOperation(
            string operationName,
            Dictionary<string, RomsetMachine> romData,
            Action<Game, RomsetMachine> tagAction)
        {
            int tagged  = 0;
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

                        if (game.TagIds == null)
                            game.TagIds = new List<Guid>();


                        tagAction(game, machine);
                        _api.Database.Games.Update(game);
                        tagged++;
                    }
                }
                finally { _api.Database.EndBufferUpdate(); }

            }, new GlobalProgressOptions($"MAME Helper: {operationName}…", true));

            _api.Dialogs.ShowMessage(
                $"Done.\n\nTagged:           {tagged}\nNo MAME match: {skipped}",
                "MAME Helper");
        }

        // ── Tag helpers ───────────────────────────────────────────────────────

        /// <summary>Adds a tag to a game, creating the tag in the DB if needed.</summary>
        private void AddTag(Game game, string tagName)
        {
            var tag = _api.Database.Tags.Add(tagName); // Add is idempotent
            if (!game.TagIds.Contains(tag.Id))
                game.TagIds.Add(tag.Id);
        }

        /// <summary>Removes one or more named tags from a game if present.</summary>
        private void RemoveTags(Game game, params string[] tagNames)
        {
            if (game.TagIds == null || game.TagIds.Count == 0) return;
            foreach (var name in tagNames)
            {
                var existing = _api.Database.Tags.FirstOrDefault(
                    t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                    game.TagIds.Remove(existing.Id);
            }
        }
    }
}

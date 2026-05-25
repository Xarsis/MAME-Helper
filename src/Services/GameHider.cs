using System;
using System.Collections.Generic;
using System.Linq;
using MAMEHelper.Models;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace MAMEHelper.Services
{
    public class GameHider
    {
        private readonly IPlayniteAPI _api;
        private readonly ILogger _logger = LogManager.GetLogger();

        public GameHider(IPlayniteAPI api) => _api = api;

        // ── Public operations ─────────────────────────────────────────────────

        public void HideImperfect(Dictionary<string, RomsetMachine> romData)
            => RunHideOperation("Hide Imperfect ROMs", romData,
                m => m.DriverStatus?.ToLower() == "imperfect");

        public void HideNonWorking(Dictionary<string, RomsetMachine> romData)
            => RunHideOperation("Hide Non-Working ROMs", romData,
                m => m.DriverStatus?.ToLower() == "preliminary");

        public void HideClones(Dictionary<string, RomsetMachine> romData)
            => RunHideOperation("Hide Clones", romData, m => m.IsClone);

        public void HideNonGames(Dictionary<string, RomsetMachine> romData)
            => RunHideOperation("Hide Non-Games", romData, m => m.IsNonGame);

        public void HideByYearRange(Dictionary<string, RomsetMachine> romData,
            int fromYear, int toYear)
            => RunHideOperation($"Hide by Year Range ({fromYear}–{toYear})", romData, m =>
            {
                if (!int.TryParse(m.Year, out int y)) return false;
                return y < fromYear || y > toYear;
            });

        public void HideByManufacturer(Dictionary<string, RomsetMachine> romData,
            string manufacturer)
        {
            if (string.IsNullOrWhiteSpace(manufacturer)) return;
            string lower = manufacturer.ToLower();
            RunHideOperation($"Hide by Manufacturer: {manufacturer}", romData,
                m => m.Manufacturer?.ToLower().Contains(lower) == true);
        }

        /// <summary>
        /// Hides games whose catver.ini top-level category is in the
        /// NonGameCategories set (System, Computer, Calculator, etc.).
        /// </summary>
        public void HideNonGamesByCatver(
            Dictionary<string, RomsetMachine> romData,
            Dictionary<string, string> catverData)
        {
            RunHideOperationWithCatver(
                "Hide Non-Games (catver.ini)",
                romData,
                catverData,
                category => CatverParser.NonGameCategories.Contains(
                    CatverParser.GetTopLevel(category)));
        }

        /// <summary>Un-hides every game in the library.</summary>
        public void UnhideAll()
        {
            int unhidden = 0;
            var games = _api.Database.Games.ToList();

            _api.Dialogs.ActivateGlobalProgress(args =>
            {
                args.ProgressMaxValue = games.Count;

                _api.Database.BeginBufferUpdate();
                try
                {
                    foreach (var game in games)
                    {
                        if (args.CancelToken.IsCancellationRequested) break;
                        args.CurrentProgressValue++;
                        args.Text = $"Unhiding ({args.CurrentProgressValue}/{games.Count})\n{game.Name}";

                        if (!game.Hidden) continue;
                        game.Hidden = false;
                        _api.Database.Games.Update(game);
                        unhidden++;
                    }
                }
                finally { _api.Database.EndBufferUpdate(); }

            }, new GlobalProgressOptions("MAME Helper: Unhiding all games…", true));

            _api.Dialogs.ShowMessage(
                $"Done.\n\nGames unhidden: {unhidden}",
                "MAME Helper");
        }

        // ── Shared runners ────────────────────────────────────────────────────

        private void RunHideOperation(
            string operationName,
            Dictionary<string, RomsetMachine> romData,
            Func<RomsetMachine, bool> shouldHide)
        {
            int hidden  = 0;
            int skipped = 0;
            var games = _api.Database.Games.ToList();

            _api.Dialogs.ActivateGlobalProgress(args =>
            {
                args.ProgressMaxValue = games.Count;

                _api.Database.BeginBufferUpdate();
                try
                {
                    foreach (var game in games)
                    {
                        if (args.CancelToken.IsCancellationRequested) break;

                        args.CurrentProgressValue++;
                        args.Text = $"{operationName} ({args.CurrentProgressValue}/{games.Count})\n{game.Name}";

                        string key = MatchingHelper.ResolveRomKey(game);
                        if (key == null || !romData.TryGetValue(key, out var machine))
                        {
                            skipped++;
                            continue;
                        }

                        if (shouldHide(machine) && !game.Hidden)
                        {
                            game.Hidden = true;
                            _api.Database.Games.Update(game);
                            hidden++;
                        }
                    }
                }
                finally { _api.Database.EndBufferUpdate(); }

            }, new GlobalProgressOptions($"MAME Helper: {operationName}…", true));

            _api.Dialogs.ShowMessage(
                $"Done.\n\nHidden:           {hidden}\nNo MAME match: {skipped}",
                "MAME Helper");
        }

        private void RunHideOperationWithCatver(
            string operationName,
            Dictionary<string, RomsetMachine> romData,
            Dictionary<string, string> catverData,
            Func<string, bool> shouldHide)
        {
            int hidden   = 0;
            int skipped  = 0;
            int noCatver = 0;
            var games = _api.Database.Games.ToList();

            _api.Dialogs.ActivateGlobalProgress(args =>
            {
                args.ProgressMaxValue = games.Count;

                _api.Database.BeginBufferUpdate();
                try
                {
                    foreach (var game in games)
                    {
                        if (args.CancelToken.IsCancellationRequested) break;

                        args.CurrentProgressValue++;
                        args.Text = $"{operationName} ({args.CurrentProgressValue}/{games.Count})\n{game.Name}";

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

                        if (shouldHide(category) && !game.Hidden)
                        {
                            game.Hidden = true;
                            _api.Database.Games.Update(game);
                            hidden++;
                        }
                    }
                }
                finally { _api.Database.EndBufferUpdate(); }

            }, new GlobalProgressOptions($"MAME Helper: {operationName}…", true));

            _api.Dialogs.ShowMessage(
                $"Done.\n\nHidden:              {hidden}\n" +
                $"No MAME match:     {skipped}\n" +
                $"Not in catver.ini: {noCatver}",
                "MAME Helper");
        }
    }
}

using System;
using Playnite.SDK.Models;

namespace MAMEHelper.Services
{
    public static class MatchingHelper
    {
        /// <summary>
        /// Resolves the ROM name key for matching against the ROM data dictionary.
        /// Priority:
        ///   1. GameId field — populated by MAME Helper's rename operation
        ///   2. game.Name — works for un-renamed imports
        /// </summary>
        public static string ResolveRomKey(Game game)
        {
            if (!string.IsNullOrWhiteSpace(game.GameId) &&
                !Guid.TryParse(game.GameId, out _))
                return game.GameId.ToLower().Trim();

            return game.Name?.ToLower().Trim();
        }
    }
}

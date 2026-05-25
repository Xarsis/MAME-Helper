using System;
using Playnite.SDK.Models;

namespace MAMEHelper.Services
{
    public static class MatchingHelper
    {
        /// <summary>
        /// The prefix written into the Notes field by MAME Helper's rename
        /// and restore operations to preserve the original ROM name.
        /// </summary>
        public const string NotePrefix = "MAME Helper - original name: ";

        /// <summary>
        /// Resolves the ROM name key for matching against the ROM data dictionary.
        /// Priority:
        ///   1. Notes field — scans for the MAME Helper prefix and extracts the ROM name
        ///   2. game.Name   — works for un-renamed imports where Notes has no prefix
        /// </summary>
        public static string ResolveRomKey(Game game)
        {
            string romName = ExtractRomNameFromNotes(game.Notes);
            if (romName != null)
                return romName.ToLower().Replace(' ', '_').Trim();

            return game.Name?.ToLower().Replace(' ', '_').Trim();
        }

        /// <summary>
        /// Extracts the ROM name from the Notes field if the MAME Helper prefix
        /// is present. Returns null if the prefix is not found.
        /// e.g. "My note text\nMAME Helper - original name: rw10r" → "rw10r"
        /// </summary>
        public static string ExtractRomNameFromNotes(string notes)
        {
            if (string.IsNullOrWhiteSpace(notes))
                return null;

            // Search each line for the prefix.
            foreach (var line in notes.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith(NotePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    string romName = trimmed.Substring(NotePrefix.Length).Trim();
                    if (!string.IsNullOrEmpty(romName))
                        return romName.ToLower().Replace(' ', '_');
                }
            }

            return null;
        }

        /// <summary>
        /// Appends the MAME Helper ROM name entry to the Notes field,
        /// preserving any existing note content.
        /// If a MAME Helper entry already exists it is replaced rather than duplicated.
        /// </summary>
        public static string BuildUpdatedNotes(string existingNotes, string romName)
        {
            string newEntry = NotePrefix + romName;

            if (string.IsNullOrWhiteSpace(existingNotes))
                return newEntry;

            // If a MAME Helper line already exists, replace it.
            var lines = existingNotes.Split('\n');
            bool replaced = false;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim().StartsWith(NotePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = newEntry;
                    replaced = true;
                    break;
                }
            }

            if (replaced)
                return string.Join("\n", lines);

            // No existing MAME Helper line — append on a new line.
            return existingNotes.TrimEnd() + "\n" + newEntry;
        }

        /// <summary>
        /// Removes the MAME Helper ROM name entry from the Notes field,
        /// leaving any other note content intact.
        /// </summary>
        public static string StripRomNameFromNotes(string notes)
        {
            if (string.IsNullOrWhiteSpace(notes))
                return notes;

            var lines = notes.Split('\n');
            var kept  = new System.Collections.Generic.List<string>();

            foreach (var line in lines)
            {
                if (!line.Trim().StartsWith(NotePrefix, StringComparison.OrdinalIgnoreCase))
                    kept.Add(line);
            }

            var result = string.Join("\n", kept).Trim();
            return string.IsNullOrEmpty(result) ? null : result;
        }
    }
}

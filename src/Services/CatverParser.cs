using System;
using System.Collections.Generic;
using System.IO;
using Playnite.SDK;

namespace MAMEHelper.Services
{
    /// <summary>
    /// Parses catver.ini into a dictionary of ROM name → category string.
    ///
    /// catver.ini format:
    ///   [Category]
    ///   romname=Top Level / Sub Category
    ///
    /// Example entries:
    ///   pacman=Maze / Shooter Small
    ///   09825_67907=System / Device
    ///   1942=Shooter / Flying Vertical
    /// </summary>
    public class CatverParser
    {
        private readonly ILogger _logger = LogManager.GetLogger();

        /// <summary>
        /// Parses the catver.ini file and returns a dictionary of
        /// lowercase ROM name → full category string (e.g. "Maze / Shooter Small").
        /// Returns an empty dictionary on failure.
        /// </summary>
        public Dictionary<string, string> Parse(string catverPath)
        {
            var result = new Dictionary<string, string>(60000, StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(catverPath) || !File.Exists(catverPath))
            {
                _logger.Warn($"MAMEHelper: catver.ini not found at: {catverPath}");
                return result;
            }

            try
            {
                bool inCategorySection = false;

                foreach (var line in File.ReadLines(catverPath))
                {
                    var trimmed = line.Trim();

                    // Skip blank lines and comments.
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";"))
                        continue;

                    // Section header detection.
                    if (trimmed.StartsWith("["))
                    {
                        inCategorySection = trimmed.Equals("[Category]",
                            StringComparison.OrdinalIgnoreCase);
                        continue;
                    }

                    if (!inCategorySection)
                        continue;

                    // Parse romname=Category / SubCategory
                    int eq = trimmed.IndexOf('=');
                    if (eq <= 0) continue;

                    string romName  = trimmed.Substring(0, eq).Trim().ToLower();
                    string category = trimmed.Substring(eq + 1).Trim();

                    if (!string.IsNullOrEmpty(romName) && !string.IsNullOrEmpty(category))
                        result[romName] = category;
                }

                _logger.Info($"MAMEHelper: Parsed {result.Count} entries from catver.ini.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "MAMEHelper: Failed to parse catver.ini.");
            }

            return result;
        }

        /// <summary>
        /// Extracts the top-level category from a full category string.
        /// e.g. "Maze / Shooter Small" → "Maze"
        ///      "System / Device"      → "System"
        /// </summary>
        public static string GetTopLevel(string category)
        {
            if (string.IsNullOrWhiteSpace(category)) return string.Empty;
            int slash = category.IndexOf('/');
            return slash > 0
                ? category.Substring(0, slash).Trim()
                : category.Trim();
        }

        /// <summary>
        /// The set of top-level catver categories considered non-game / non-arcade.
        /// Used by Hide/Remove catver-based operations.
        /// </summary>
        public static readonly HashSet<string> NonGameCategories = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "System",
            "Computer",
            "Computer Graphic Workstation",
            "Calculator",
            "Utilities",
            "Telephone",
            "Radio",
            "Robot",
            "Printer",
            "Digital Camera",
            "Digital Simulator",
            "Medical Equipment",
            "Musical Instrument",
            "Musical Instrument Accessory",
            "Music Player",
            "Player",
            "Tablet",
            "Touchscreen",
            "Watch",
            "TV Bundle",
            "Non Arcade",
            "Road Indicator",
        };
    }
}

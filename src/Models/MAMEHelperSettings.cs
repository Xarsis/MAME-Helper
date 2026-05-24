namespace MAMEHelper.Models
{
    /// <summary>
    /// Persisted plugin settings. Serialised to JSON in Playnite's plugin data folder.
    /// </summary>
    public class MAMEHelperSettings
    {
        // ── ROM data source ──────────────────────────────────────────────────

        /// <summary>Full path to mame.exe, e.g. "S:\MAME\mame.exe".</summary>
        public string MameExecutablePath { get; set; } = string.Empty;

        /// <summary>
        /// When true, load ROM data from a pre-exported XML/DAT file instead of
        /// running mame.exe -listxml.
        /// </summary>
        public bool UseListFile { get; set; } = false;

        /// <summary>Path to the XML or DAT list file when UseListFile is true.</summary>
        public string ListFilePath { get; set; } = string.Empty;

        // ── Cache behaviour ──────────────────────────────────────────────────

        /// <summary>
        /// Number of days before the user is prompted to regenerate the ROM cache.
        /// Default 7. Set to 0 to always prompt. Set to int.MaxValue to never prompt.
        /// </summary>
        public int CacheAgeDaysBeforePrompt { get; set; } = 7;

        // ── Media folders ────────────────────────────────────────────────────

        /// <summary>Folder to scan for cover images (PNG files named {romname}.png).</summary>
        public string CoverImageFolder { get; set; } = string.Empty;

        /// <summary>Folder to scan for background images (PNG files named {romname}.png).</summary>
        public string BackgroundImageFolder { get; set; } = string.Empty;
    }
}

using System.ComponentModel;
using System.Collections.Generic;
using MAMEHelper.Models;
using Playnite.SDK;

namespace MAMEHelper.UI
{
    /// <summary>
    /// ViewModel for the settings page. Implements ISettings so Playnite
    /// can call BeginEdit / EndEdit / CancelEdit around the settings dialog.
    /// </summary>
    public class MAMEHelperSettingsViewModel : ISettings, INotifyPropertyChanged
    {
        private readonly MAMEHelperPlugin _plugin;
        private MAMEHelperSettings _editingClone;

        public MAMEHelperSettings Settings { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public MAMEHelperSettingsViewModel(MAMEHelperPlugin plugin)
        {
            _plugin = plugin;
            Settings = plugin.LoadSettings() ?? new MAMEHelperSettings();
        }

        // ── ISettings ────────────────────────────────────────────────────────

        public void BeginEdit()
        {
            // Deep-copy so CancelEdit can restore the original values.
            _editingClone = new MAMEHelperSettings
            {
                MameExecutablePath      = Settings.MameExecutablePath,
                UseListFile             = Settings.UseListFile,
                ListFilePath            = Settings.ListFilePath,
                CacheAgeDaysBeforePrompt = Settings.CacheAgeDaysBeforePrompt,
                CoverImageFolder        = Settings.CoverImageFolder,
                BackgroundImageFolder   = Settings.BackgroundImageFolder
            };
        }

        public void CancelEdit()
        {
            // Restore all fields from the saved clone.
            Settings.MameExecutablePath       = _editingClone.MameExecutablePath;
            Settings.UseListFile              = _editingClone.UseListFile;
            Settings.ListFilePath             = _editingClone.ListFilePath;
            Settings.CacheAgeDaysBeforePrompt = _editingClone.CacheAgeDaysBeforePrompt;
            Settings.CoverImageFolder         = _editingClone.CoverImageFolder;
            Settings.BackgroundImageFolder    = _editingClone.BackgroundImageFolder;
            OnPropertyChanged(nameof(Settings));
        }

        public void EndEdit()
        {
            _plugin.SaveSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();

            if (!Settings.UseListFile)
            {
                if (string.IsNullOrWhiteSpace(Settings.MameExecutablePath))
                    errors.Add("MAME executable path is not set.");
                else if (!System.IO.File.Exists(Settings.MameExecutablePath))
                    errors.Add($"MAME executable not found at: {Settings.MameExecutablePath}");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(Settings.ListFilePath))
                    errors.Add("List file path is not set.");
                else if (!System.IO.File.Exists(Settings.ListFilePath))
                    errors.Add($"List file not found at: {Settings.ListFilePath}");
            }

            return errors.Count == 0;
        }
        
        // ── Browse button commands (called from code-behind) ─────────────────

        public void BrowseMameExecutable()
        {
            var path = _plugin.PlayniteApi.Dialogs.SelectFile("MAME Executable|mame*.exe|All Files|*.*");
            if (!string.IsNullOrEmpty(path))
            {
                Settings.MameExecutablePath = path;
                OnPropertyChanged(nameof(Settings));
            }
        }

        public void BrowseListFile()
        {
            var path = _plugin.PlayniteApi.Dialogs.SelectFile("XML/DAT Files|*.xml;*.dat|All Files|*.*");
            if (!string.IsNullOrEmpty(path))
            {
                Settings.ListFilePath = path;
                OnPropertyChanged(nameof(Settings));
            }
        }

        public void BrowseCoverImageFolder()
        {
            var path = _plugin.PlayniteApi.Dialogs.SelectFolder();
            if (!string.IsNullOrEmpty(path))
            {
                Settings.CoverImageFolder = path;
                OnPropertyChanged(nameof(Settings));
            }
        }

        public void BrowseBackgroundImageFolder()
        {
            var path = _plugin.PlayniteApi.Dialogs.SelectFolder();
            if (!string.IsNullOrEmpty(path))
            {
                Settings.BackgroundImageFolder = path;
                OnPropertyChanged(nameof(Settings));
            }
        }

        // ── INotifyPropertyChanged ───────────────────────────────────────────

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

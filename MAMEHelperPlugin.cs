using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MAMEHelper.Models;
using MAMEHelper.Services;
using MAMEHelper.UI;
using Newtonsoft.Json;
using Playnite.SDK;
using Playnite.SDK.Plugins;

namespace MAMEHelper
{
    public class MAMEHelperPlugin : GenericPlugin
    {
        // ── Identity ──────────────────────────────────────────────────────────

        public override Guid Id { get; } =
            new Guid("c7f3a291-4b8e-4d2a-9f1c-e83d5b762a40");

        // ── State ─────────────────────────────────────────────────────────────

        private MAMEHelperSettings _settings;
        private MAMEHelperSettingsViewModel _settingsViewModel;   // ← only one of these

        private RomDataService _romData;
        private GameTagger _tagger;
        private GameHider _hider;
        private GameRemover _remover;
        private GameRenamer _renamer;
        private GameMediaManager _media;
        private GameMetadataSetter _metadata;
        private GamelistXmlExporter _gamelistExporter;
        private CsvExporter _csvExporter;
        private MissingMediaFinder _missingMedia;
        private CatverParser _catverParser;

        private readonly ILogger _logger = LogManager.GetLogger();
        private const string SettingsFileName = "settings.json";

        // ── Constructor ───────────────────────────────────────────────────────
        
        public MAMEHelperPlugin(IPlayniteAPI api) : base(api)
        {
            _settings = LoadSettings() ?? new MAMEHelperSettings();
            _settingsViewModel = new MAMEHelperSettingsViewModel(this);
            InitServices();

            // Tell Playnite this plugin has a settings page.
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };
        }
        
        private void InitServices()
        {
            _romData = new RomDataService(PlayniteApi, _settings, GetPluginUserDataPath());
            _tagger = new GameTagger(PlayniteApi);
            _hider = new GameHider(PlayniteApi);
            _remover = new GameRemover(PlayniteApi);
            _renamer = new GameRenamer(PlayniteApi);
            _media = new GameMediaManager(PlayniteApi);
            _metadata = new GameMetadataSetter(PlayniteApi);
            _gamelistExporter = new GamelistXmlExporter(PlayniteApi);
            _csvExporter = new CsvExporter(PlayniteApi);
            _missingMedia = new MissingMediaFinder(PlayniteApi);
            _catverParser = new CatverParser();
        }

        // ── Settings persistence ──────────────────────────────────────────────

        public MAMEHelperSettings LoadSettings()
        {
            try
            {
                string path = Path.Combine(GetPluginUserDataPath(), SettingsFileName);
                if (File.Exists(path))
                    return JsonConvert.DeserializeObject<MAMEHelperSettings>(
                        File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "MAMEHelper: Could not load settings.");
            }
            return null;
        }

        public void SaveSettings(MAMEHelperSettings settings)
        {
            try
            {
                _settings = settings;
                // Rebuild services so they pick up new paths.
                InitServices();
                string path = Path.Combine(GetPluginUserDataPath(), SettingsFileName);
                Directory.CreateDirectory(GetPluginUserDataPath());
                File.WriteAllText(path,
                    JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.Indented));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "MAMEHelper: Could not save settings.");
            }
        }

        // ── Playnite Settings integration ─────────────────────────────────────

        // Cached instance — Playnite calls GetSettings and GetSettingsView
        // separately and expects them to share the same object.

        public override ISettings GetSettings(bool firstRunSettings)
        {
            // Only create once; reuse on subsequent calls.
            if (_settingsViewModel == null)
                _settingsViewModel = new MAMEHelperSettingsViewModel(this);
            return _settingsViewModel;
        }

        public override System.Windows.Controls.UserControl GetSettingsView(bool firstRunSettings)
        {
            // Must use the same viewmodel instance that GetSettings returned.
            if (_settingsViewModel == null)
                _settingsViewModel = new MAMEHelperSettingsViewModel(this);
            return new MAMEHelperSettingsView(_settingsViewModel);
        }

        // ── Direct settings dialog ────────────────────────────────────────────

        /// <summary>
        /// Opens the settings page directly as a standalone dialog window.
        /// This bypasses Playnite's Add-ons routing so the user can reach
        /// settings from the plugin menu itself.
        /// </summary>
        private void OpenSettings()
        {
            var vm = new MAMEHelperSettingsViewModel(this);
            var view = new MAMEHelperSettingsView(vm);

            var window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowMinimizeButton = false,
                ShowMaximizeButton = false,
                ShowCloseButton = true
            });

            window.Title = "MAME Helper — Settings";
            window.Content = view;
            window.Width = 640;
            window.SizeToContent = SizeToContent.Height;
            window.ResizeMode = ResizeMode.NoResize;

            vm.BeginEdit();

            bool? result = window.ShowDialog();

            if (result == true)
            {
                vm.EndEdit();
                _romData.ClearSessionCache();
            }
            else
            {
                vm.CancelEdit();
            }
        }

        // ── Menu wiring ───────────────────────────────────────────────────────

        public override IEnumerable<MainMenuItem> GetMainMenuItems(
            GetMainMenuItemsArgs menuArgs)
        {
            const string root = "@MAME Helper";
            const string tagMenu = root + "|Tag";
            const string hideMenu = root + "|Hide";
            const string removeMenu = root + "|Remove";
            const string mediaMenu = root + "|Media";
            const string renameMenu = root + "|Rename";
            const string toolsMenu = root + "|Tools";

            // ── Settings shortcut ─────────────────────────────────────────────
            yield return MenuItem(root, "Settings…", _ => OpenSettings());
            yield return Separator(root, "sep_root_1");

            // ── Tag ───────────────────────────────────────────────────────────
            yield return MenuItem(tagMenu, "Tag: Driver Status",
                _ => WithRomData(d => _tagger.TagDriverStatus(d)));

            yield return MenuItem(tagMenu, "Tag: Machine Type",
                _ => WithRomData(d => _tagger.TagMachineType(d)));

            yield return MenuItem(tagMenu, "Tag: Parent / Clone",
                _ => WithRomData(d => _tagger.TagParentClone(d)));

            yield return MenuItem(tagMenu, "Tag: Category (top level)",
                _ => WithRomAndCatverData((d, c) => _tagger.TagCatverTopLevel(d, c)));

            yield return MenuItem(tagMenu, "Tag: Category (full)",
                _ => WithRomAndCatverData((d, c) => _tagger.TagCatverFull(d, c)));
            
            yield return MenuItem(tagMenu, "Tag: Year and Manufacturer",
                _ => WithRomData(d => _metadata.SetYearAndManufacturer(d)));

            yield return Separator(tagMenu, "sep_tag_1");

            yield return MenuItem(tagMenu, "Clear All MAME Tags",
                _ =>
                {
                    if (Confirm("This will remove all MAME: tags from your entire library.\nContinue?"))
                        _tagger.ClearAllMameTags();
                });

            yield return Separator(tagMenu, "sep_tag_2");

            yield return MenuItem(tagMenu, "Set Category…",
                _ => WithRomData(d =>
                {
                    string val = PromptString(
                        "Enter the Category name to apply to all matched MAME games:", "Arcade");
                    if (val != null) _metadata.SetCategory(d, val);
                }));

            yield return MenuItem(tagMenu, "Set Source…",
                _ => WithRomData(d =>
                {
                    string val = PromptString(
                        "Enter the Source name to apply to all matched MAME games:", "MAME");
                    if (val != null) _metadata.SetSource(d, val);
                }));

            yield return MenuItem(tagMenu, "Set Platform…",
                _ => WithRomData(d =>
                {
                    string val = PromptString(
                        "Enter the Platform name to apply to all matched MAME games:", "Arcade");
                    if (val != null) _metadata.SetPlatform(d, val);
                }));

            // ── Hide ──────────────────────────────────────────────────────────
            yield return MenuItem(hideMenu, "Hide Imperfect ROMs",
                _ => WithRomData(d => _hider.HideImperfect(d)));

            yield return MenuItem(hideMenu, "Hide Non-Working ROMs",
                _ => WithRomData(d => _hider.HideNonWorking(d)));

            yield return MenuItem(hideMenu, "Hide Clones",
                _ => WithRomData(d => _hider.HideClones(d)));

            yield return MenuItem(hideMenu, "Hide Non-Games (BIOS / Device / Mechanical / Sample)",
                _ => WithRomData(d => _hider.HideNonGames(d)));

            yield return MenuItem(hideMenu, "Hide Non-Games per catver.ini",
                _ => WithRomAndCatverData((d, c) => _hider.HideNonGamesByCatver(d, c)));

            yield return MenuItem(hideMenu, "Hide by Year Range…",
                _ => WithRomData(d =>
                {
                    string from = PromptString(
                        "Hide games BEFORE this year (e.g. 1985):", "1985");
                    if (from == null) return;
                    string to = PromptString(
                        "Hide games AFTER this year (e.g. 1995):", "1995");
                    if (to == null) return;

                    if (int.TryParse(from, out int fromYear) &&
                        int.TryParse(to, out int toYear))
                        _hider.HideByYearRange(d, fromYear, toYear);
                    else
                        PlayniteApi.Dialogs.ShowErrorMessage(
                            "Please enter valid 4-digit years.", "MAME Helper");
                }));

            yield return MenuItem(hideMenu, "Hide by Manufacturer…",
                _ => WithRomData(d =>
                {
                    string mfr = PromptString(
                        "Enter manufacturer name to hide (partial match):", "");
                    if (mfr != null) _hider.HideByManufacturer(d, mfr);
                }));

            yield return Separator(hideMenu, "sep_hide_1");

            yield return MenuItem(hideMenu, "Unhide All MAME Games",
                _ =>
                {
                    if (Confirm(
                        "This will un-hide all currently hidden games in your library.\nContinue?"))
                        _hider.UnhideAll();
                });

            // ── Remove ────────────────────────────────────────────────────────
            yield return MenuItem(removeMenu, "Remove Non-Working ROMs  (permanent)",
                _ => WithRomData(d => _remover.RemoveNonWorking(d)));

            yield return MenuItem(removeMenu, "Remove Clones  (permanent)",
                _ => WithRomData(d => _remover.RemoveClones(d)));

            yield return MenuItem(removeMenu, "Remove Non-Games  (permanent)",
                _ => WithRomData(d => _remover.RemoveNonGames(d)));

            yield return MenuItem(removeMenu, "Remove Non-Games per catver.ini  (permanent)",
                _ => WithRomAndCatverData((d, c) => _remover.RemoveNonGamesByCatver(d, c)));

            // ── Media ─────────────────────────────────────────────────────────
            yield return MenuItem(mediaMenu, "Set Cover Images from Folder",
                _ => WithRomData(d =>
                    _media.SetCoverImages(d, _settings.CoverImageFolder, _settings.CoverImageFolder2)));

            yield return MenuItem(mediaMenu, "Set Background Images from Folder",
                _ => WithRomData(d =>
                    _media.SetBackgroundImages(d, _settings.BackgroundImageFolder, _settings.BackgroundImageFolder2)));

            yield return Separator(mediaMenu, "sep_media_1");

            yield return MenuItem(mediaMenu, "Find Games with Missing Media",
                _ => WithRomData(d => _missingMedia.FindMissingMedia(d)));

            // ── Rename ────────────────────────────────────────────────────────
            yield return MenuItem(renameMenu, "Rename Selected Games  (with region info)",
                _ => WithRomData(d => _renamer.RenameWithInfo(d)));

            yield return MenuItem(renameMenu, "Rename Selected Games  (without region info)",
                _ => WithRomData(d => _renamer.RenameWithoutInfo(d)));

            // ── Tools ─────────────────────────────────────────────────────────
            yield return MenuItem(toolsMenu, "Generate Gamelist XML",
                _ => WithRomData(d => _gamelistExporter.Export(d)));

            yield return MenuItem(toolsMenu, "Export Library to CSV",
                _ => WithRomData(d => _csvExporter.Export(d)));

            yield return MenuItem(toolsMenu, "Regenerate ROM Cache",
                _ =>
                {
                    if (Confirm(
                        "This will regenerate the ROM cache by running mame.exe -listxml.\n\n" +
                        "This takes 30–90 seconds. Continue?"))
                    {
                        _romData.ForceRegenerate();
                    }
                });
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the path to catver.ini which lives in the plugin's install folder.
        /// </summary>
        private string GetCatverPath()
        {
            // catver.ini ships alongside the DLL in the Extensions folder.
            string pluginFolder = Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location);
            return Path.Combine(pluginFolder, "catver.ini");
        }

        /// <summary>
        /// Loads ROM data and catver data, then runs the action.
        /// Shows an error if catver.ini is missing.
        /// </summary>
        private void WithRomAndCatverData(
            Action<Dictionary<string, RomsetMachine>, Dictionary<string, string>> action)
        {
            try
            {
                var romData = _romData.GetRomData();
                if (romData == null || romData.Count == 0)
                    return;

                string catverPath = GetCatverPath();
                if (!File.Exists(catverPath))
                {
                    PlayniteApi.Dialogs.ShowErrorMessage(
                        $"catver.ini not found at:\n{catverPath}\n\n" +
                        "Make sure catver.ini is in the MAME Helper Extensions folder.",
                        "MAME Helper");
                    return;
                }

                var catverData = _catverParser.Parse(catverPath);
                if (catverData.Count == 0)
                {
                    PlayniteApi.Dialogs.ShowErrorMessage(
                        "catver.ini was found but could not be parsed. " +
                        "Make sure it is a valid catver.ini file.",
                        "MAME Helper");
                    return;
                }

                action(romData, catverData);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "MAMEHelper: Unhandled error in catver menu action.");
                PlayniteApi.Dialogs.ShowErrorMessage(
                    $"An unexpected error occurred:\n{ex.Message}", "MAME Helper");
            }
        }

        /// <summary>
        /// Loads ROM data then runs the action.
        /// Aborts cleanly if data is unavailable.
        /// </summary>
        private void WithRomData(Action<Dictionary<string, RomsetMachine>> action)
        {
            try
            {
                var data = _romData.GetRomData();
                if (data == null || data.Count == 0)
                    return;
                action(data);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "MAMEHelper: Unhandled error in menu action.");
                PlayniteApi.Dialogs.ShowErrorMessage(
                    $"An unexpected error occurred:\n{ex.Message}", "MAME Helper");
            }
        }

        /// <summary>Shows a Yes/No confirmation dialog. Returns true if Yes.</summary>
        private bool Confirm(string message)
        {
            return PlayniteApi.Dialogs.ShowMessage(
                message,
                "MAME Helper — Confirm",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }

        /// <summary>
        /// Shows the InputDialog and returns the trimmed string,
        /// or null if the user cancelled or left the field empty.
        /// </summary>
        private string PromptString(string prompt, string defaultValue = "")
        {
            var dlg = new InputDialog(prompt, defaultValue);
            bool? result = dlg.ShowDialog();
            if (result != true || string.IsNullOrWhiteSpace(dlg.InputValue))
                return null;
            return dlg.InputValue;
        }

        /// <summary>Creates a MainMenuItem with a section path and click handler.</summary>
        private static MainMenuItem MenuItem(
            string section, string description, Action<MainMenuItemActionArgs> action)
        {
            return new MainMenuItem
            {
                Description = description,
                MenuSection = section,
                Action = action
            };
        }

        /// <summary>Creates a visual separator in the menu.</summary>
        private static MainMenuItem Separator(string section, string key)
        {
            return new MainMenuItem
            {
                Description = "-",
                MenuSection = section
            };
        }
    }
}
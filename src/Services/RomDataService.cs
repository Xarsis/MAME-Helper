using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml;
using MAMEHelper.Models;
using Newtonsoft.Json;
using Playnite.SDK;

namespace MAMEHelper.Services
{
    /// <summary>
    /// Responsible for loading, parsing, and caching the full MAME ROM list.
    /// Supports two sources:
    ///   1. Running mame.exe -listxml and streaming the output to a temp file.
    ///   2. A user-supplied XML or DAT file.
    ///
    /// The parsed result is cached as romcache.json in the plugin data folder.
    /// On each use the cache age is checked; if older than the configured threshold
    /// the user is prompted to regenerate.
    /// </summary>
    public class RomDataService
    {
        // ── Constants ────────────────────────────────────────────────────────

        private const string CacheFileName = "romcache.json";

        // ── Dependencies ─────────────────────────────────────────────────────

        private readonly IPlayniteAPI _api;
        private readonly MAMEHelperSettings _settings;
        private readonly string _pluginDataPath;
        private readonly ILogger _logger = LogManager.GetLogger();

        // ── In-memory cache for the current session ───────────────────────────

        private Dictionary<string, RomsetMachine> _sessionCache;

        // ── Constructor ──────────────────────────────────────────────────────

        public RomDataService(IPlayniteAPI api, MAMEHelperSettings settings, string pluginDataPath)
        {
            _api            = api;
            _settings       = settings;
            _pluginDataPath = pluginDataPath;
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Returns the ROM dictionary for the current session.
        /// Handles all cache-age logic and source selection.
        /// Returns null if the user cancelled or an error prevented loading.
        /// Key is lowercase ROM name; value is the full machine record.
        /// </summary>
        public Dictionary<string, RomsetMachine> GetRomData()
        {
            // Return the in-memory session cache if already loaded this session.
            if (_sessionCache != null)
                return _sessionCache;

            // Validate settings before doing any work.
            if (!ValidateSettings())
                return null;

            string cachePath = Path.Combine(_pluginDataPath, CacheFileName);
            bool cacheExists = File.Exists(cachePath);
            bool cacheStale  = IsCacheStale(cachePath);

            if (cacheExists && !cacheStale)
            {
                // Happy path: load from disk cache silently.
                _sessionCache = LoadFromDiskCache(cachePath);
                if (_sessionCache != null)
                {
                    _logger.Info($"MAMEHelper: Loaded {_sessionCache.Count} machines from cache.");
                    return _sessionCache;
                }
                // Cache was corrupt — fall through to regeneration.
                _logger.Warn("MAMEHelper: Cache corrupt, regenerating.");
            }

            if (cacheExists && cacheStale)
            {
                // Cache exists but is old — ask the user.
                var choice = _api.Dialogs.ShowMessage(
                    "The MAME ROM cache is more than " + _settings.CacheAgeDaysBeforePrompt +
                    " days old.\n\nUse existing data, or regenerate now?\n\n" +
                    "(Regeneration takes 30–90 seconds.)",
                    "MAME Helper — Cache",
                    System.Windows.MessageBoxButton.YesNoCancel,
                    System.Windows.MessageBoxImage.Question);

                if (choice == System.Windows.MessageBoxResult.Cancel)
                    return null;

                if (choice == System.Windows.MessageBoxResult.No)
                {
                    // User chose to reuse stale cache.
                    _sessionCache = LoadFromDiskCache(cachePath);
                    if (_sessionCache != null)
                        return _sessionCache;
                    // Corrupt — fall through.
                }
                // Yes or corrupt: fall through to regeneration.
            }

            // Generate fresh data.
            _sessionCache = GenerateAndCache(cachePath);
            return _sessionCache;
        }

        /// <summary>
        /// Forces cache regeneration regardless of age. Used by a future
        /// "Regenerate ROM Cache" menu item if desired.
        /// </summary>
        public Dictionary<string, RomsetMachine> ForceRegenerate()
        {
            _sessionCache = null;
            string cachePath = Path.Combine(_pluginDataPath, CacheFileName);
            _sessionCache = GenerateAndCache(cachePath);
            return _sessionCache;
        }

        /// <summary>Clears the in-memory session cache (does not delete disk cache).</summary>
        public void ClearSessionCache() => _sessionCache = null;

        // ── Settings validation ───────────────────────────────────────────────

        private bool ValidateSettings()
        {
            if (!_settings.UseListFile)
            {
                if (string.IsNullOrWhiteSpace(_settings.MameExecutablePath))
                {
                    _api.Dialogs.ShowErrorMessage(
                        "MAME executable path is not set.\n\nGo to Extensions → MAME Helper → Settings.",
                        "MAME Helper");
                    return false;
                }
                if (!File.Exists(_settings.MameExecutablePath))
                {
                    _api.Dialogs.ShowErrorMessage(
                        $"MAME executable not found at:\n{_settings.MameExecutablePath}\n\n" +
                        "Go to Extensions → MAME Helper → Settings.",
                        "MAME Helper");
                    return false;
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_settings.ListFilePath))
                {
                    _api.Dialogs.ShowErrorMessage(
                        "ROM list file path is not set.\n\nGo to Extensions → MAME Helper → Settings.",
                        "MAME Helper");
                    return false;
                }
                if (!File.Exists(_settings.ListFilePath))
                {
                    _api.Dialogs.ShowErrorMessage(
                        $"ROM list file not found at:\n{_settings.ListFilePath}\n\n" +
                        "Go to Extensions → MAME Helper → Settings.",
                        "MAME Helper");
                    return false;
                }
            }
            return true;
        }

        // ── Cache age check ───────────────────────────────────────────────────

        private bool IsCacheStale(string cachePath)
        {
            if (!File.Exists(cachePath))
                return false; // Doesn't exist — not stale, just absent.

            var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(cachePath);
            return age.TotalDays > _settings.CacheAgeDaysBeforePrompt;
        }

        // ── Disk cache read/write ─────────────────────────────────────────────

        private Dictionary<string, RomsetMachine> LoadFromDiskCache(string cachePath)
        {
            try
            {
                string json = File.ReadAllText(cachePath);
                var list = JsonConvert.DeserializeObject<List<RomsetMachine>>(json);
                if (list == null || list.Count == 0)
                    return null;

                var dict = new Dictionary<string, RomsetMachine>(list.Count, StringComparer.OrdinalIgnoreCase);
                foreach (var m in list)
                    dict[m.RomName] = m;

                return dict;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "MAMEHelper: Failed to load ROM cache from disk.");
                return null;
            }
        }

        private void SaveToDiskCache(string cachePath, Dictionary<string, RomsetMachine> data)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
                string json = JsonConvert.SerializeObject(data.Values, Newtonsoft.Json.Formatting.None);
                File.WriteAllText(cachePath, json);
                _logger.Info($"MAMEHelper: Saved {data.Count} machines to cache.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "MAMEHelper: Failed to save ROM cache to disk.");
            }
        }

        // ── Generation ────────────────────────────────────────────────────────

        private Dictionary<string, RomsetMachine> GenerateAndCache(string cachePath)
        {
            Dictionary<string, RomsetMachine> result = null;

            _api.Dialogs.ActivateGlobalProgress(progressArgs =>
            {
                progressArgs.ProgressMaxValue = 0; // Indeterminate while running.
                progressArgs.Text = _settings.UseListFile
                    ? "MAME Helper: Parsing ROM list file…"
                    : "MAME Helper: Running mame.exe -listxml…\n(This takes 30–90 seconds.)";

                try
                {
                    string xmlPath = _settings.UseListFile
                        ? _settings.ListFilePath
                        : RunMameListXml(progressArgs);

                    if (xmlPath == null || progressArgs.CancelToken.IsCancellationRequested)
                        return;

                    progressArgs.Text = "MAME Helper: Parsing XML…";
                    result = ParseMameXml(xmlPath, progressArgs);

                    // If we generated a temp file, clean it up.
                    if (!_settings.UseListFile && File.Exists(xmlPath))
                        TryDeleteFile(xmlPath);

                    if (result != null && result.Count > 0)
                        SaveToDiskCache(cachePath, result);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "MAMEHelper: Error during ROM data generation.");
                    _api.Dialogs.ShowErrorMessage(
                        $"Error generating ROM data:\n{ex.Message}",
                        "MAME Helper");
                }

            }, new GlobalProgressOptions("MAME Helper: Loading ROM data…", true));

            return result;
        }

        /// <summary>
        /// Runs mame.exe -listxml, redirecting stdout to a temp file.
        /// Returns the temp file path, or null on failure.
        /// </summary>
        private string RunMameListXml(GlobalProgressActionArgs progressArgs)
        {
            string tempFile = Path.GetTempFileName() + ".xml";

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = _settings.MameExecutablePath,
                    Arguments              = "-listxml",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using (var process = new Process { StartInfo = psi })
                using (var outFile = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
                {
                    process.Start();

                    var buffer = new byte[65536];
                    var stdOut = process.StandardOutput.BaseStream;
                    int bytesRead;

                    while ((bytesRead = stdOut.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (progressArgs.CancelToken.IsCancellationRequested)
                        {
                            process.Kill();
                            TryDeleteFile(tempFile);
                            return null;
                        }
                        outFile.Write(buffer, 0, bytesRead);
                    }

                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        string err = process.StandardError.ReadToEnd();
                        throw new Exception($"mame.exe exited with code {process.ExitCode}.\n{err}");
                    }
                }

                if (!File.Exists(tempFile) || new FileInfo(tempFile).Length == 0)
                    throw new Exception("mame.exe produced no output. Check your MAME installation.");

                return tempFile;
            }
            catch
            {
                TryDeleteFile(tempFile);
                throw;
            }
        }

        /// <summary>
        /// Streaming XmlReader parse of a MAME listxml file.
        /// Handles both the "machine" element format (MAME 0.162+)
        /// and the legacy "game" element format.
        /// </summary>
        private Dictionary<string, RomsetMachine> ParseMameXml(
            string xmlPath,
            GlobalProgressActionArgs progressArgs)
        {
            var result = new Dictionary<string, RomsetMachine>(50000, StringComparer.OrdinalIgnoreCase);

            var xmlSettings = new XmlReaderSettings
            {
                DtdProcessing    = DtdProcessing.Parse,
                IgnoreWhitespace = true,
                IgnoreComments   = true
            };

            RomsetMachine current = null;

            using (var reader = XmlReader.Create(xmlPath, xmlSettings))
            {
                while (reader.Read())
                {
                    if (progressArgs.CancelToken.IsCancellationRequested)
                        return null;

                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        switch (reader.Name)
                        {
                            case "machine":
                            case "game":
                                current = ReadMachineElement(reader);
                                break;

                            case "description":
                                if (current != null && reader.Read() &&
                                    reader.NodeType == XmlNodeType.Text)
                                    current.Description = reader.Value;
                                break;

                            case "year":
                                if (current != null && reader.Read() &&
                                    reader.NodeType == XmlNodeType.Text)
                                    current.Year = reader.Value;
                                break;

                            case "manufacturer":
                                if (current != null && reader.Read() &&
                                    reader.NodeType == XmlNodeType.Text)
                                    current.Manufacturer = reader.Value;
                                break;

                            case "driver":
                                if (current != null)
                                    current.DriverStatus = reader.GetAttribute("status") ?? "preliminary";
                                break;
                        }
                    }
                    else if (reader.NodeType == XmlNodeType.EndElement &&
                             (reader.Name == "machine" || reader.Name == "game"))
                    {
                        if (current != null &&
                            !string.IsNullOrEmpty(current.RomName))
                        {
                            result[current.RomName] = current;
                        }
                        current = null;
                    }
                }
            }

            _logger.Info($"MAMEHelper: Parsed {result.Count} machines from XML.");
            return result;
        }

        /// <summary>
        /// Reads all attributes from a &lt;machine&gt; or &lt;game&gt; element
        /// into a new RomsetMachine. Does not advance the reader past the element.
        /// </summary>
        private static RomsetMachine ReadMachineElement(XmlReader reader)
        {
            string name     = reader.GetAttribute("name");
            if (string.IsNullOrEmpty(name))
                return null;

            string cloneOf  = reader.GetAttribute("cloneof");
            string sampleOf = reader.GetAttribute("sampleof");

            return new RomsetMachine
            {
                RomName      = name.ToLower(),
                IsClone      = !string.IsNullOrEmpty(cloneOf),
                CloneOf      = cloneOf?.ToLower(),
                IsBios       = reader.GetAttribute("isbios")      == "yes",
                IsDevice     = reader.GetAttribute("isdevice")    == "yes",
                IsMechanical = reader.GetAttribute("ismechanical") == "yes",
                IsSample     = !string.IsNullOrEmpty(sampleOf) && string.IsNullOrEmpty(cloneOf),
                DriverStatus = "preliminary" // default; overwritten when <driver> is read
            };
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* best-effort */ }
        }
    }
}

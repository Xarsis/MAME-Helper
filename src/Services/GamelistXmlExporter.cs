using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using MAMEHelper.Models;
using Playnite.SDK;

namespace MAMEHelper.Services
{
    /// <summary>
    /// Exports a gamelist.xml compatible with EmulationStation / Batocera / RetroBat.
    /// One &lt;game&gt; element per matched Playnite library entry.
    /// </summary>
    public class GamelistXmlExporter
    {
        private readonly IPlayniteAPI _api;
        private readonly ILogger _logger = LogManager.GetLogger();

        public GamelistXmlExporter(IPlayniteAPI api) => _api = api;

        public void Export(Dictionary<string, RomsetMachine> romData)
        {
            // Ask user where to save the file.
            string outputPath = _api.Dialogs.SaveFile(
                "XML Files|*.xml|All Files|*.*",
                "gamelist.xml");

            if (string.IsNullOrEmpty(outputPath))
                return;

            int exported = 0;
            int skipped  = 0;
            var games = _api.Database.Games.ToList();

            _api.Dialogs.ActivateGlobalProgress(args =>
            {
                args.ProgressMaxValue = games.Count;

                var xmlSettings = new XmlWriterSettings
                {
                    Indent      = true,
                    Encoding    = new UTF8Encoding(false), // UTF-8 without BOM
                    IndentChars = "  "
                };

                try
                {
                    using (var writer = XmlWriter.Create(outputPath, xmlSettings))
                    {
                        writer.WriteStartDocument();
                        writer.WriteStartElement("gameList");

                        foreach (var game in games)
                        {
                            if (args.CancelToken.IsCancellationRequested) break;

                            args.CurrentProgressValue++;
                            args.Text = $"Exporting ({args.CurrentProgressValue}/{games.Count})\n{game.Name}";

                            string key = MatchingHelper.ResolveRomKey(game);
                            if (key == null || !romData.TryGetValue(key, out var machine))
                            {
                                skipped++;
                                continue;
                            }

                            writer.WriteStartElement("game");

                            WriteElement(writer, "path",        $"./{machine.RomName}.zip");
                            WriteElement(writer, "name",        machine.Description ?? game.Name);
                            WriteElement(writer, "desc",        string.Empty);
                            WriteElement(writer, "releasedate", FormatYear(machine.Year));
                            WriteElement(writer, "developer",   machine.Manufacturer ?? string.Empty);
                            WriteElement(writer, "publisher",   machine.Manufacturer ?? string.Empty);
                            WriteElement(writer, "genre",       string.Empty);
                            WriteElement(writer, "players",     "1");

                            // Embed Playnite cover path if available.
                            // game.CoverImage holds the DB file ID; resolve to a full path.
                            if (!string.IsNullOrEmpty(game.CoverImage))
                            {
                                string coverPath = game.CoverImage; // CoverImage is already the file path in SDK 6
                                if (!string.IsNullOrEmpty(coverPath) && File.Exists(coverPath))
                                    WriteElement(writer, "image", coverPath);
                            }

                            writer.WriteEndElement(); // </game>
                            exported++;
                        }

                        writer.WriteEndElement(); // </gameList>
                        writer.WriteEndDocument();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "MAMEHelper: Error exporting gamelist XML.");
                    _api.Dialogs.ShowErrorMessage(
                        $"Error exporting gamelist:\n{ex.Message}", "MAME Helper");
                }

            }, new GlobalProgressOptions("MAME Helper: Exporting gamelist XML…", true));

            _api.Dialogs.ShowMessage(
                $"Done.\n\nExported:         {exported}\nNo MAME match: {skipped}\n\nSaved to:\n{outputPath}",
                "MAME Helper");
        }

        private static void WriteElement(XmlWriter writer, string name, string value)
        {
            writer.WriteStartElement(name);
            writer.WriteString(value ?? string.Empty);
            writer.WriteEndElement();
        }

        private static string FormatYear(string year)
        {
            if (int.TryParse(year, out int y))
                return $"{y}0101T000000"; // EmulationStation date format: YYYYMMDDTHHMMSS
            return string.Empty;
        }
    }
}

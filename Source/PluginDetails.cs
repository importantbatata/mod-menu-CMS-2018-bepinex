using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;

namespace CMS18ModMenu
{
    internal sealed class PluginDetails
    {
        // These summaries match the local source, documentation, or configuration for each plugin ID.
        private static readonly Dictionary<string, string> KnownDescriptions =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "local.cms18.turboproto", "Adds installable ProCharger P-1X superchargers and simulates their boost and dyno effects on supported engines. Find the standard kit in the Main Shop and the high-boost kit in the Tuning Shop." },
                { "local.cms18.enginebuildchecklist", "Shows the parts installed on or missing from the engine on your garage stand. Open the checklist with F8 by default or from the engine stand’s radial menu." },
                { "F7SpawnRates", "Changes how often cars appear at locations such as sheds and junkyards. It adds editable spawn weights and presets for stock cars, and generates weights for modded cars." },
                { "QoLTweaks", "Adds garage conveniences such as highlighting unfinished and original parts, remembering the shopping list, and removing purchased parts from it automatically. Features can vary with your QoLTweaks settings." },
                { "cms2018.sprint.standalone", "Lets you sprint while walking around the garage. The sprint key and speed are configurable from Sprint Mod Settings in the pause menu." },
                { Plugin.Id, "Adds this Mods menu to the main screen. Browse loaded BepInEx plugins and your Steam Workshop subscriptions from the two tabs." }
            };

        public readonly PluginInfo Info;
        public readonly string FileName;
        public readonly string Summary;
        public readonly string Source;
        public readonly string Settings;

        public PluginDetails(PluginInfo info)
        {
            Info = info;
            string path = info.Location;
            FileName = string.IsNullOrEmpty(path) ? info.Instance.GetType().Assembly.GetName().Name + ".dll" : Path.GetFileName(path);
            Settings = ReadSettings(info.Instance.Config);
            if (!string.IsNullOrEmpty(path) && TryReadDocumentation(path, out Summary, out Source))
                return;

            if (KnownDescriptions.TryGetValue(info.Metadata.GUID, out Summary))
            {
                Source = "Based on locally reviewed mod files";
                return;
            }

            var assembly = info.Instance.GetType().Assembly;
            var metadata = assembly.GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false)
                .OfType<AssemblyDescriptionAttribute>().FirstOrDefault();
            Summary = metadata == null ? "" : Excerpt(metadata.Description, 750);
            Source = "From the DLL’s description";
            if (string.IsNullOrEmpty(Summary))
            {
                Summary = "This plugin provides no readable description in its DLL or local documentation.";
                Source = "";
            }
        }

        private static string ReadSettings(ConfigFile config)
        {
            if (config == null)
                return "";

            var descriptions = new List<string>();
            foreach (ConfigDefinition key in config.Keys)
            {
                ConfigEntryBase entry = config[key];
                string description = entry.Description == null ? "" : Excerpt(entry.Description.Description, 160);
                string label = Regex.Replace(key.Key, @"(?<=[a-z0-9])(?=[A-Z])|[_-]+", " ");
                descriptions.Add(string.IsNullOrEmpty(description) ? label : label + " — " + description);
                if (descriptions.Count == 8)
                    break;
            }
            return string.Join("\n\n", descriptions.ToArray());
        }

        private static bool TryReadDocumentation(string dll, out string summary, out string source)
        {
            string folder = Path.GetDirectoryName(dll);
            string baseName = Path.GetFileNameWithoutExtension(dll);
            var candidates = new List<string>
            {
                Path.Combine(folder, baseName + ".description.txt"),
                Path.Combine(folder, baseName + ".md")
            };
            if (!string.Equals(folder, Paths.PluginPath, StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(Path.Combine(folder, "README.md"));
                candidates.Add(Path.Combine(folder, "README.txt"));
            }

            foreach (string file in candidates)
            {
                try
                {
                    if (!File.Exists(file))
                        continue;
                    using (var reader = new StreamReader(file))
                    {
                        char[] buffer = new char[10000];
                        int length = reader.ReadBlock(buffer, 0, buffer.Length);
                        string contents = new string(buffer, 0, length);
                        var lines = contents.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        var paragraphs = lines.Select(line => line.Trim())
                            .SkipWhile(line => line.StartsWith("#") || line.StartsWith("![") || line.StartsWith("---"))
                            .TakeWhile(line => !line.StartsWith("#") && !line.StartsWith("```"))
                            .Where(line => !line.StartsWith("!["))
                            .Take(4);
                        summary = Excerpt(string.Join(" ", paragraphs.ToArray()), 750);
                        if (summary.Length > 0)
                        {
                            source = "From " + Path.GetFileName(file);
                            return true;
                        }
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            summary = "";
            source = "";
            return false;
        }

        private static string Excerpt(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            string plain = Regex.Replace(value, @"\[([^\]]+)\]\([^\)]+\)", "$1");
            plain = Regex.Replace(plain.Replace("**", "").Replace("`", ""), @"\s+", " ").Trim();
            if (plain.Length <= maxLength)
                return plain;
            int cut = plain.LastIndexOf(' ', maxLength - 1);
            return plain.Substring(0, cut > maxLength / 2 ? cut : maxLength).TrimEnd() + "…";
        }
    }
}

using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonoMod.Utils;
using RWCustom;

namespace MapExporter
{
    internal static class Data
    {
        public static int Version { get; private set; } = 0;

        private static string _modDir = null;
        public static string ModDirectory => _modDir ??= ModManager.ActiveMods.First(x => x.id == Plugin.MOD_ID).path;

        private static string _dataDir = null;
        public static string DataDirectory => _dataDir ??= Path.Combine(Custom.LegacyRootFolderDirectory(), "MapExport");
        public static string PathOf(string path) => Path.Combine(DataDirectory, path);

        public static string RenderDir => PathOf("Input");
        public static string FinalDir => PathOf("Output");
        public static string DataFileDir => PathOf("data.json");

        public static string RenderOutputDir(string scug, string acronym) => Path.Combine(RenderDir, scug, acronym); // + " - " + Region.GetRegionFullName(acronym, new(scug)));
        public static string FinalOutputDir(string scug, string acronym) => Path.Combine(FinalDir, scug, acronym); // + " - " + Region.GetRegionFullName(acronym, new(scug)));

        public static void Initialize()
        {
            if (!Directory.Exists(DataDirectory))
            {
                Directory.CreateDirectory(DataDirectory);
            }
            GetData();
        }

        public readonly struct QueueData(string name, HashSet<SlugcatStats.Name> scugs) : IEquatable<QueueData>, IEquatable<string>
        {
            public readonly string acronym = name;
            public readonly HashSet<SlugcatStats.Name> scugs = scugs;

            public bool Equals(QueueData other)
            {
                return acronym.Equals(other.acronym, StringComparison.InvariantCultureIgnoreCase);
            }

            public bool Equals(string other)
            {
                return acronym == other;
            }

            public override string ToString()
            {
                return acronym + ";" + string.Join(",", [.. scugs.Select(x => x.value)]);
            }

            public Dictionary<string, object> ToJSON() => new()
            {
                {"name", acronym },
                {"scugs", scugs.Select(x => x.value).ToArray() }
            };
        }
        public static readonly Queue<QueueData> QueuedRegions = [];

        public enum SSStatus{
            Inactive,
            Unfinished,
            Errored,
            Finished
        }
        public static SSStatus ScreenshotterStatus = SSStatus.Inactive;

        public static readonly Dictionary<SlugcatStats.Name, List<string>> RenderedRegions = [];
        public static readonly Dictionary<string, List<SlugcatStats.Name>> FinishedRegions = [];

        public static void GetData()
        {
            // Misc stuff
            if (File.Exists(DataFileDir))
            {
                var json = (Dictionary<string, object>)Json.Deserialize(File.ReadAllText(DataFileDir));

                // Queue data
                QueuedRegions.Clear();
                if (json.ContainsKey("queue"))
                {
                    var regions = ((List<object>)json["queue"]).Cast<Dictionary<string, object>>();
                    foreach (var region in regions)
                    {
                        QueuedRegions.Enqueue(new QueueData((string)region["name"], [.. ((List<object>)region["scugs"]).Select(x => new SlugcatStats.Name((string)x, false))]));
                    }
                }

                // Screenshotter status
                if (json.ContainsKey("ssstatus"))
                {
                    Enum.TryParse((string)json["ssstatus"], out ScreenshotterStatus);
                }

                // Saved progress
                RenderedRegions.Clear();
                if (json.ContainsKey("rendered"))
                {
                    var regions = (Dictionary<string, object>)json["rendered"];
                    foreach (var scugRegions in regions)
                    {
                        var scug = new SlugcatStats.Name(scugRegions.Key, false);
                        var savedList = ((List<object>)scugRegions.Value).Cast<string>().ToList();

                        // Make sure the regions still exist in our file system
                        var foundList = new List<string>();
                        foreach (var region in savedList)
                        {
                            if (Directory.Exists(RenderOutputDir(scugRegions.Key, region)))
                            {
                                foundList.Add(region);
                            }
                        }

                        // Don't add the scug to the list if they have no rendered regions in the file system
                        if (foundList.Count > 0)
                        {
                            RenderedRegions.Add(scug, foundList);
                        }
                    }
                }

                FinishedRegions.Clear();
                if (json.ContainsKey("finished"))
                {
                    var regions = (Dictionary<string, object>)json["finished"];
                    foreach (var finishedRegion in regions)
                    {
                        var region = finishedRegion.Key;
                        var savedList = ((List<object>)finishedRegion.Value).Select(x => new SlugcatStats.Name((string)x)).ToList();

                        // Make sure the regions still exist in our file system
                        var scugList = new List<SlugcatStats.Name>();
                        foreach (var scug in savedList)
                        {
                            if (Directory.Exists(FinalOutputDir(scug.value, region)))
                            {
                                scugList.Add(scug);
                            }
                        }

                        // Don't add the scug to the list if they have no rendered regions in the file system
                        if (scugList.Count > 0)
                        {
                            FinishedRegions.Add(region, scugList);
                        }
                    }
                }

                // User preferences
                Preferences.Clear();
                if (json.ContainsKey("preferences"))
                {
                    Preferences.AddRange(((Dictionary<string, object>)json["preferences"]).ToDictionary(x => new KeyValuePair<string, bool>(x.Key, (bool)x.Value)));
                }
            }

            Version++;
        }

        public static void UpdateSSStatus()
        {
            ScreenshotterStatus = SSStatus.Inactive;
            if (File.Exists(DataFileDir))
            {
                var json = (Dictionary<string, object>)Json.Deserialize(File.ReadAllText(DataFileDir));
                if (json.ContainsKey("ssstatus"))
                {
                    Enum.TryParse((string)json["ssstatus"], out ScreenshotterStatus);
                }
            }

            Version++;
        }

        public static void SaveData()
        {
            Dictionary<string, string[]> rendered = [];
            foreach (var kv in RenderedRegions)
            {
                rendered.Add(kv.Key.value, [.. kv.Value]);
            }
            Dictionary<string, string[]> finished = [];
            foreach (var kv in FinishedRegions)
            {
                finished.Add(kv.Key, [.. kv.Value.Select(x => x.value)]);
            }
            Dictionary<string, object> dict = new()
            {
                {
                    "queue",
                    QueuedRegions.Select(x => x.ToJSON()).ToArray()
                },
                { "ssstatus", ScreenshotterStatus.ToString() },
                { "rendered", rendered },
                { "finished", finished },
                { "preferences", Preferences },
            };
            File.WriteAllText(DataFileDir, Json.Serialize(dict));
        }

        public static Dictionary<string, bool> Preferences = [];
        public static class PreferenceKeys
        {
            public const string SHOW_CREATURES = "show/creatures";
            public const string SHOW_INSECTS = "show/insects";
            public const string SHOW_GHOSTS = "show/ghosts";
            public const string SHOW_GUARDIANS = "show/guadians";
            public const string SHOW_ORACLES = "show/oracles";
        }
    }

}

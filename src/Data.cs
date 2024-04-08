using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RWCustom;

namespace MapExporter
{
    internal static class Data
    {
        public static string DataDirectory => Path.Combine(Custom.LegacyRootFolderDirectory(), "MapExport");
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
                return acronym.Equals(other.acronym);
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

        public static void GetData()
        {
            // Misc stuff
            if (File.Exists(DataFileDir))
            {
                var json = (Dictionary<string, object>)Json.Deserialize(File.ReadAllText(DataFileDir));

                // Queue data
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
                if (json.ContainsKey("rendered"))
                {
                    var regions = (Dictionary<string, object>)json["rendered"];
                    foreach (var region in regions)
                    {
                        RenderedRegions.Add(new SlugcatStats.Name(region.Key, false), ((List<object>)region.Value).Cast<string>().ToList());
                    }
                }
            }

            // Regions
            foreach (var scug in Directory.EnumerateDirectories(RenderDir))
            {
                List<string> regions = [];
            }
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
        }

        public static void SaveData()
        {
            Dictionary<string, string[]> rendered = [];
            foreach (var kv in RenderedRegions)
            {
                rendered.Add(kv.Key.value, [.. kv.Value]);
            }
            Dictionary<string, object> dict = new()
            {
                {
                    "queue",
                    QueuedRegions.Select(x => x.ToJSON()).ToArray()
                },
                { "ssstatus", ScreenshotterStatus.ToString() },
                { "rendered", rendered },
            };
            File.WriteAllText(DataFileDir, Json.Serialize(dict));
        }
    }
}

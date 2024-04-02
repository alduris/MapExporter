using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
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
        
        public static string RenderOutputDir(string scug, string acronym) => Path.Combine(RenderDir, scug, Region.GetRegionFullName(acronym, new(scug)));
        public static string FinalOutputDir(string scug, string acronym) => Path.Combine(FinalDir, scug, Region.GetRegionFullName(acronym, new(scug)));

        public static void TryCreateDirectories()
        {
            if (!Directory.Exists(DataDirectory))
            {
                Directory.CreateDirectory(DataDirectory);
            }
        }

        public readonly struct QueueData(string name, HashSet<SlugcatStats.Name> scugs) : IEquatable<QueueData>, IEquatable<string>
        {
            public readonly string name = name;
            public readonly HashSet<SlugcatStats.Name> scugs = scugs;

            public bool Equals(QueueData other)
            {
                return name.Equals(other.name);
            }

            public bool Equals(string other)
            {
                return name == other;
            }

            public override string ToString()
            {
                return name + ";" + string.Join(",", [.. scugs.Select(x => x.value)]);
            }
        }
        public static readonly Queue<QueueData> QueuedRegions = [];

        public enum SSStatus{
            Inactive,
            Unfinished,
            Finished
        }
        public static SSStatus ScreenshotterStatus = SSStatus.Inactive;

        public static readonly Dictionary<SlugcatStats.Name, List<string>> RenderedRegions;
        public static readonly Dictionary<SlugcatStats.Name, List<string>> FinishedRegions;

        public static void GetData()
        {
            // Misc stuff
            if (File.Exists(DataFileDir))
            {
                var json = (Dictionary<string, object>)Json.Deserialize(File.ReadAllText(DataFileDir));

                // Queue data
                if (json.ContainsKey("queue"))
                {
                    var regions = (Dictionary<string, string[]>)json["queue"];
                    foreach (var region in regions)
                    {
                        QueuedRegions.Enqueue(new QueueData(region.Key, [.. region.Value.Select(x => new SlugcatStats.Name(x, false))]));
                    }
                }

                // Screenshotter status
                if (json.ContainsKey("ssstatus"))
                {
                    Enum.TryParse((string)json["ssstatus"], out ScreenshotterStatus);
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
            Dictionary<string, object> dict = new()
            {
                {
                    "queue",
                    QueuedRegions.Select(x => new Dictionary<string, object> {
                        {"name", x.name },
                        {"scugs", x.scugs.Select(x => x.value).ToArray() }
                    }).ToArray()
                },
                { "ssstatus", ScreenshotterStatus.ToString() },
            };
            File.WriteAllText(DataFileDir, Json.Serialize(dict));
        }
    }
}

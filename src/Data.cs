using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonoMod.Utils;
using MoreSlugcats;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RWCustom;
using UnityEngine;

namespace MapExporterNew
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

        public static string RenderOutputDir(string scug, string acronym) => Path.Combine(RenderDir, scug, acronym);
        public static string FinalOutputDir(string scug, string acronym) => Path.Combine(FinalDir, acronym, scug);

        public static Dictionary<string, object> UserPreferences = [];

        public static void Initialize()
        {
            if (!Directory.Exists(DataDirectory))
            {
                Directory.CreateDirectory(DataDirectory);
            }
            GetData();
        }

        public static void CheckData()
        {
            // Get missing region names
            List<SlugcatStats.Name> scugList = [];
            for (int i = 0; i < SlugcatStats.Name.values.Count; i++)
            {
                var scug = new SlugcatStats.Name(SlugcatStats.Name.values.GetEntry(i), false);
                if (!SlugcatStats.HiddenOrUnplayableSlugcat(scug) || (ModManager.MSC && scug == MoreSlugcatsEnums.SlugcatStatsName.Sofanthiel))
                {
                    scugList.Add(scug);
                }
            }
            var regionList = Region.GetFullRegionOrder().ToHashSet();

            foreach (var item in RenderedRegions.Keys)
            {
                if (RegionNames.ContainsKey(item) || !regionList.Contains(item)) continue;
                var nameStorage = new RegionName(Region.GetRegionFullName(item, null));
                foreach (var scug in scugList)
                {
                    nameStorage.personalNames.Add(scug, Region.GetRegionFullName(item, scug));
                }
                RegionNames.Add(item, nameStorage);
            }

            foreach (var item in FinishedRegions.Keys)
            {
                // Same code as before
                if (RegionNames.ContainsKey(item) || !regionList.Contains(item)) continue;
                var nameStorage = new RegionName(Region.GetRegionFullName(item, null));
                foreach (var scug in scugList)
                {
                    nameStorage.personalNames.Add(scug, Region.GetRegionFullName(item, scug));
                }
                RegionNames.Add(item, nameStorage);
            }
        }

        public readonly struct QueueData(string name, HashSet<SlugcatStats.Name> scugs, SSUpdateMode updateMode) : IEquatable<QueueData>, IEquatable<string>
        {
            public readonly string acronym = name;
            public readonly HashSet<SlugcatStats.Name> scugs = scugs;
            public readonly SSUpdateMode updateMode = updateMode;

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
                return acronym + ";" + string.Join(",", [.. scugs.Select(x => x.value)]) + ";" + updateMode.ToString();
            }

            public Dictionary<string, object> ToJSON() => new()
            {
                {"name", acronym },
                {"scugs", scugs.Select(x => x.value).ToArray() },
                {"updatemode", updateMode },
            };
        }
        public static readonly Queue<QueueData> QueuedRegions = [];

        public enum SSStatus{
            Inactive,
            Unfinished,
            Errored,
            Relaunch,
            Finished
        }
        public static SSStatus ScreenshotterStatus = SSStatus.Inactive;

        public enum SSUpdateMode
        {
            Everything,
            ScreenshotsOnly,
            MetadataWithRoomPositions,
            MetadataNoRoomPositions,
            MergeNewRoomsOnly,
            AllButRoomPositions, // screenshots included
        }
        public static bool TakeScreenshots(SSUpdateMode updateMode) => updateMode == SSUpdateMode.Everything
            || updateMode == SSUpdateMode.ScreenshotsOnly
            || updateMode == SSUpdateMode.MergeNewRoomsOnly
            || updateMode == SSUpdateMode.AllButRoomPositions;
        public static bool CollectRoomData(SSUpdateMode updateMode) => CollectRoomPositions(updateMode)
            || updateMode == SSUpdateMode.MetadataNoRoomPositions
            || updateMode == SSUpdateMode.AllButRoomPositions;
        public static bool CollectRoomPositions(SSUpdateMode updateMode) => updateMode == SSUpdateMode.Everything
            || updateMode == SSUpdateMode.MetadataWithRoomPositions
            || updateMode == SSUpdateMode.MergeNewRoomsOnly;

        public static readonly Dictionary<string, HashSet<SlugcatStats.Name>> RenderedRegions = [];
        public static readonly Dictionary<string, HashSet<SlugcatStats.Name>> FinishedRegions = [];

        public readonly struct RegionName(string name)
        {
            public readonly string name = name;
            public readonly Dictionary<SlugcatStats.Name, string> personalNames = [];
        }
        public static readonly Dictionary<string, RegionName> RegionNames = [];
        public static string RegionNameFor(string acronym, SlugcatStats.Name name)
        {
            if (RegionNames.TryGetValue(acronym, out RegionName regionName))
            {
                if (name == null || !regionName.personalNames.TryGetValue(name, out string personalName))
                    return regionName.name;
                else
                    return personalName;
            }
            return Region.GetRegionFullName(acronym, name);
        }

        public static Dictionary<string, (string fileName, bool enabled)> PlacedObjectIcons = [];

        public static void GetData()
        {
            // Misc stuff
            if (File.Exists(DataFileDir))
            {
                var data = JsonConvert.DeserializeObject<TempSaveData>(File.ReadAllText(DataFileDir));

                // Queue data
                QueuedRegions.Clear();
                foreach (var item in data.queue)
                {
                    QueuedRegions.Enqueue(item);
                }

                // Screenshotter status
                ScreenshotterStatus = data.ssstatus;

                // Saved progress
                RenderedRegions.Clear();
                foreach ((string region, string[] rawScugs) in data.rendered)
                {
                    var scugs = rawScugs.Where(scug => Directory.Exists(RenderOutputDir(scug, region)))
                        .Select(scug => new SlugcatStats.Name(scug, false))
                        .ToHashSet();
                    if (scugs.Any())
                    {
                        RenderedRegions[region] = scugs;
                    }
                }

                FinishedRegions.Clear();
                foreach ((string region, string[] rawScugs) in data.rendered)
                {
                    var scugs = rawScugs.Where(scug => Directory.Exists(FinalOutputDir(scug, region)))
                        .Select(scug => new SlugcatStats.Name(scug, false))
                        .ToHashSet();
                    if (scugs.Any())
                    {
                        FinishedRegions[region] = scugs;
                    }
                }

                // User preferences
                UserPreferences.Clear();
                UserPreferences.AddRange(data.preferences);
            }

            Version++;
        }

        public static void UpdateSSStatus()
        {
            ScreenshotterStatus = SSStatus.Inactive;
            if (File.Exists(DataFileDir))
            {
                var json = JsonConvert.DeserializeObject<TempSaveData>(File.ReadAllText(DataFileDir));
                ScreenshotterStatus = json.ssstatus;
            }

            Version++;
        }

        public static void SaveData()
        {
            // Convert data
            Dictionary<string, string[]> rendered = [];
            foreach (var kv in RenderedRegions)
            {
                rendered.Add(kv.Key, [.. kv.Value.Select(x => x.value)]);
            }
            Dictionary<string, string[]> finished = [];
            foreach (var kv in FinishedRegions)
            {
                finished.Add(kv.Key, [.. kv.Value.Select(x => x.value)]);
            }
            Dictionary<string, Dictionary<string, string>> names = [];
            foreach (var kv in RegionNames)
            {
                var dict = new Dictionary<string, string>
                {
                    { "*", kv.Value.name }
                };
                foreach (var scug in kv.Value.personalNames)
                {
                    dict.Add(scug.Key.value, scug.Value);
                }
                names.Add(kv.Key, dict);
            }

            // Save
            File.WriteAllText(DataFileDir, JsonConvert.SerializeObject(new TempSaveData
            {
                queue = [.. QueuedRegions],
                ssstatus = ScreenshotterStatus,
                rendered = rendered,
                finished = finished,
                preferences = UserPreferences,
                names = names
            }));
        }

        private class TempSaveData
        {
            public List<QueueData> queue = [];
            public SSStatus ssstatus = SSStatus.Inactive;
            public Dictionary<string, string[]> rendered = [];
            public Dictionary<string, string[]> finished = [];
            public Dictionary<string, object> preferences = [];
            public Dictionary<string, Dictionary<string, string>> names = [];
        }
    }

}

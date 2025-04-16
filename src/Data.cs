using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonoMod.Utils;
using MoreSlugcats;
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
                var json = (Dictionary<string, object>)Json.Deserialize(File.ReadAllText(DataFileDir));

                // Queue data
                QueuedRegions.Clear();
                if (json.ContainsKey("queue"))
                {
                    var regions = ((List<object>)json["queue"]).Cast<Dictionary<string, object>>();
                    foreach (var region in regions)
                    {
                        SSUpdateMode updateMode = SSUpdateMode.Everything;
                        if (region.ContainsKey("updatemode") && !Enum.TryParse((string)region["updatemode"], out updateMode))
                            updateMode = SSUpdateMode.Everything;
                        QueuedRegions.Enqueue(new QueueData((string)region["name"], [.. ((List<object>)region["scugs"]).Select(x => new SlugcatStats.Name((string)x, false))], updateMode));
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
                        var region = scugRegions.Key;
                        var scugs = ((List<object>)scugRegions.Value).Select(x => new SlugcatStats.Name((string)x, false)).ToList();

                        // Make sure the regions still exist in our file system
                        var foundList = new HashSet<SlugcatStats.Name>();
                        foreach (var scug in scugs)
                        {
                            if (Directory.Exists(RenderOutputDir(scug.value, region)))
                            {
                                foundList.Add(scug);
                            }
                        }

                        // Don't add the scug to the list if they have no rendered regions in the file system
                        if (foundList.Count > 0)
                        {
                            RenderedRegions.Add(region, foundList);
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
                        var scugList = new HashSet<SlugcatStats.Name>();
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
                UserPreferences.Clear();
                if (json.ContainsKey("preferences"))
                {
                    UserPreferences.AddRange((Dictionary<string, object>)json["preferences"]);
                }

                // Region names
                if (json.TryGetValue("regionnames", out object nameObj) && nameObj is Dictionary<string, object> nameDict)
                {
                    RegionNames.Clear();
                    foreach (var kv in nameDict)
                    {
                        var subnames = (Dictionary<string, object>)kv.Value;
                        if (subnames.ContainsKey("*"))
                        {
                            var name = new RegionName((string)subnames["*"]);
                            foreach (var subname in subnames)
                            {
                                if (subname.Key == "*") continue;
                                name.personalNames[new SlugcatStats.Name(subname.Key, false)] = (string)subname.Value;
                            }
                        }
                    }
                }

                // Placed object icons
                /*if (json.TryGetValue("poicons", out var iconObj) && iconObj is Dictionary<string, object> iconDict)
                {
                    foreach (var kv in iconDict)
                    {
                        if (!PlacedObjectIcons.ContainsKey(kv.Key))
                        {
                            if (kv.Value is List<object> iconList && iconList.Count == 2)
                            {
                                PlacedObjectIcons.Add(kv.Key, ((string)iconList[0], (bool)iconList[1]));
                            }
                        }
                    }
                }*/
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
            Dictionary<string, List<object>> icons = [];
            foreach (var kv in PlacedObjectIcons)
            {
                icons.Add(kv.Key, [kv.Value.fileName, kv.Value.enabled]);
            }
            Dictionary<string, object> save = new()
            {
                {
                    "queue",
                    QueuedRegions.Select(x => x.ToJSON()).ToArray()
                },
                { "ssstatus", ScreenshotterStatus.ToString() },
                { "rendered", rendered },
                { "finished", finished },
                { "preferences", UserPreferences },
                { "regionnames", names },
                //{ "poicons", icons },
            };
            File.WriteAllText(DataFileDir, Json.Serialize(save));
        }

        public static Dictionary<string, object> UserPreferences = [];
    }

}

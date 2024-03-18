using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Logging;

namespace MapExporter.Screenshotter
{
    internal static class CaptureUtil
    {
        private static int ScugPriority(string slugcat)
        {
            return slugcat switch
            {
                "white" => 10,      // do White first, they have the most generic regions
                "artificer" => 9,   // do Artificer next, they have Metropolis, Waterfront Facility, and past-GW
                "saint" => 8,       // do Saint next for Undergrowth and Silent Construct
                "rivulet" => 7,     // do Rivulet for The Rot
                "inv" => 6,         // do Inv because inv
                _ => 0              // everyone else has a mix of duplicate rooms
            };
        }

        public static List<SlugcatStats.Name> SlugcatsToRenderFor(string acronym, List<SlugcatStats.Name> scugs)
        {
            if (scugs.Count <= 1)
            {
                return scugs;
            }


            HashSet<SlugcatStats.Name> results = [];

            // Determine file differences for slugcats
            foreach (var slugcat in scugs)
            {
                // Different property files for a region
                if (File.Exists(AssetManager.ResolveFilePath(Path.Combine("World", acronym, $"properties-{slugcat.value}.txt"))))
                {
                    results.Add(slugcat);
                    continue;
                }

                // Different map file (probably)
                if (File.Exists(AssetManager.ResolveFilePath(Path.Combine("World", acronym, $"map_{acronym}-{slugcat.value}.txt"))))
                {
                    results.Add(slugcat);
                    continue;
                }

                // Check world
                Region region = Region.LoadAllRegions(slugcat).Where(x => x.name.ToLower() == acronym.ToLower()).First();
                if (region == null)
                {
                    Plugin.Logger.LogError($"Region {acronym} doesn't exist! Defaulting to outskirts");
                    acronym = "su";
                    region = Region.LoadAllRegions(slugcat).Where(x => x.name.ToLower() == "su").First();
                }

                WorldLoader scugworld = new(null, slugcat, false, acronym, region, RainWorld.LoadSetupValues(true));

                scugworld.NextActivity();
                while (!scugworld.Finished)
                {
                    scugworld.Update();
                }

                foreach (var room in scugworld.abstractRooms)
                {
                    var settings = new RoomSettings(acronym, region, false, false, slugcat);
                }
            }

            // Add a default
            if (results.Count == 0)
            {
                results.Add(scugs[0]);
            }

            return [.. results];
        }
    }
}

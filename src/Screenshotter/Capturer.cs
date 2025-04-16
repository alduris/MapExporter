using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MoreSlugcats;
using UnityEngine;
using Random = UnityEngine.Random;
using SSUpdateMode = MapExporterNew.Data.SSUpdateMode;

namespace MapExporterNew.Screenshotter
{
    internal class Capturer
    {

        public static readonly Dictionary<string, int[]> blacklistedCams = new() // one indexed
        {
            { "SU_B13", new int[]{2} },
            { "GW_S08", new int[]{2} }, // in vanilla only
            { "SL_C01", new int[]{4,5} }, // crescent order or will break
        };
        public static string regionRendering = "SU"; // in case something drastic goes wrong, this is the default
        public static readonly Queue<string> slugsRendering = [];
        public static SSUpdateMode updateMode;

        public static bool NotHiddenRoom(AbstractRoom room) => !HiddenRoom(room);
        public static bool HiddenRoom(AbstractRoom room)
        {
            if (room == null)
            {
                return true;
            }
            if (room.world.DisabledMapRooms.Contains(room.name, StringComparer.InvariantCultureIgnoreCase))
            {
                Plugin.Logger.LogDebug($"Room {room.world.game.StoryCharacter}/{room.name} is disabled");
                return true;
            }
            if (!room.offScreenDen)
            {
                if (room.connections.Length == 0)
                {
                    Plugin.Logger.LogDebug($"Room {room.world.game.StoryCharacter}/{room.name} with no outward connections is ignored");
                    return true;
                }
                if (room.connections.All(r => room.world.GetAbstractRoom(r) is not AbstractRoom other || !other.connections.Contains(room.index)))
                {
                    Plugin.Logger.LogDebug($"Room {room.world.game.StoryCharacter}/{room.name} with no inward connections is ignored");
                    return true;
                }
            }
            return false;
        }

        static string PathOfRegion(string slugcat, string region)
        {
            return Directory.CreateDirectory(Data.RenderOutputDir(slugcat.ToLower(), region.ToLower())).FullName;
        }

        static string PathOfMetadata(string slugcat, string region)
        {
            return Path.Combine(PathOfRegion(slugcat, region), "metadata.json");
        }

        static string PathOfScreenshot(string slugcat, string region, string room, int num)
        {
            return $"{Path.Combine(PathOfRegion(slugcat, region), room.ToLower())}_{num}.png";
        }
        public System.Collections.IEnumerator CaptureTask(RainWorldGame game)
        {
            // Task start
            Random.InitState(0);

            var args = Environment.GetCommandLineArgs();
            int index = 0;
            for (int i = 0; i < args.Length; i++) // IndexOf wasn't working
            {
                if (args[i].ToLower() == Plugin.FLAG_TRIGGER)
                {
                    index = i + 1;
                    break;
                }
            }
            var split = args[index].Split(';');
            regionRendering = split[0];
            foreach (var str in split[1].Split(','))
            {
                slugsRendering.Enqueue(str);
            }
            updateMode = (SSUpdateMode)Enum.Parse(typeof(SSUpdateMode), split[2]);

            // load room
            while (game.cameras[0].room == null || !game.cameras[0].room.ReadyForPlayer) yield return null;
            for (int i = 0; i < 40; i++) yield return null; // give it an extra sec to load just in case
            game.cameras[0].room.abstractRoom.Abstractize();

            // Recreate scuglat list from last time if needed
            while (slugsRendering.Count > 0)
            {
                SlugcatStats.Name slugcat = new(slugsRendering.Dequeue());

                game.rainWorld.safariSlugcat = slugcat;
                game.GetStorySession.saveStateNumber = slugcat;
                game.GetStorySession.saveState.saveStateNumber = slugcat;
                game.GetStorySession.saveState.currentTimelinePosition = SlugcatStats.SlugcatToTimeline(slugcat);
                game.overWorld = new OverWorld(game); // I wonder if this is dangerous

                foreach (var step in CaptureRegion(game, regionRendering))
                    yield return step;

                if (Data.QueuedRegions.Count > 0 && Data.QueuedRegions.Peek().acronym == regionRendering)
                {
                    // Save new progress in case of bad thing
                    Data.QueuedRegions.Peek().scugs.Remove(slugcat);
                }

                if (!Data.RenderedRegions.TryGetValue(regionRendering, out var scugs))
                {
                    Data.RenderedRegions.Add(regionRendering, [slugcat]);
                }
                else
                {
                    scugs.Add(slugcat);
                }

                Data.SaveData();
            }

            // Add region name
            if (Data.RegionNames.ContainsKey(regionRendering))
            {
                Data.RegionNames.Remove(regionRendering);
            }
            List<SlugcatStats.Name> scugList = [];
            for (int i = 0; i < SlugcatStats.Name.values.Count; i++)
            {
                var scug = new SlugcatStats.Name(SlugcatStats.Name.values.GetEntry(i), false);
                if (!SlugcatStats.HiddenOrUnplayableSlugcat(scug) || (ModManager.MSC && scug == MoreSlugcatsEnums.SlugcatStatsName.Sofanthiel))
                {
                    scugList.Add(scug);
                }
            }
            var nameStorage = new Data.RegionName(Region.GetRegionFullName(regionRendering, null));
            foreach (var name in scugList)
            {
                nameStorage.personalNames.Add(name, Region.GetRegionFullName(regionRendering, name));
            }
            Data.RegionNames.Add(regionRendering, nameStorage);

            // Mark that we finished, save data, and quit
            Data.ScreenshotterStatus = Data.SSStatus.Finished;
            Data.SaveData();
            Application.Quit();
        }


        private System.Collections.IEnumerable CaptureRegion(RainWorldGame game, string region)
        {
            SlugcatStats.Name slugcat = game.StoryCharacter;

            // load region
            Random.InitState(0);
            game.overWorld.LoadWorld(region, slugcat, SlugcatStats.SlugcatToTimeline(slugcat), false);
            Plugin.Logger.LogDebug($"Loaded {slugcat}/{region} (timeline: {SlugcatStats.SlugcatToTimeline(slugcat)})");

            Directory.CreateDirectory(PathOfRegion(slugcat.value, region));

            List<AbstractRoom> rooms = [.. game.world.abstractRooms];
            RegionInfo mapContent = null;
            var inputPath = Path.Combine(Data.RenderOutputDir(slugcat.value, region), "metadata.json");
            if (updateMode != SSUpdateMode.Everything && updateMode != SSUpdateMode.MetadataWithRoomPositions && File.Exists(inputPath))
            {
                bool exception = false;
                try
                {
                    mapContent = RegionInfo.FromJson((Dictionary<string, object>)Json.Deserialize(File.ReadAllText(inputPath)));
                    if (updateMode != SSUpdateMode.MergeNewRoomsOnly) // we want to do this later in this case so we can filter out old rooms
                        mapContent.MergeNewData(game.world);
                }
                catch (Exception ex)
                {
                    exception = true;
                    Plugin.Logger.LogError("Issue loading metadata file! Reverting to 'Everything' behavior");
                    Plugin.Logger.LogError(ex);
                }
                if (exception)
                {
                    CreateErrorPopup("Uh oh!", "Issue loading metadata file. Will revert to 'Everything' behavior. Continue?", true, () =>
                    {
                        updateMode = SSUpdateMode.Everything;
                        mapContent = new(game.world);
                    });
                    while (!Input.GetKey(KeyCode.Slash))
                    yield return null;
                }
            }
            else
            {
                mapContent = new(game.world);
            }

            if (mapContent == null)
            {
                CreateErrorPopup("Uh oh!", "Map content is somehow null. Please restart process.", false);
                yield break;
            }

            // Don't image rooms not available for this slugcat
            rooms.RemoveAll(HiddenRoom);

            // Don't image offscreen dens
            rooms.RemoveAll(r => r.offScreenDen);

            // Abide by user preferences
            if (updateMode == SSUpdateMode.MergeNewRoomsOnly)
            {
                // Remove all existing rooms
                rooms.RemoveAll(x => mapContent.rooms.ContainsKey(x.name));

                // Add rooms connecting to the new rooms
                HashSet<AbstractRoom> connections = [.. rooms.SelectMany(x => x.connections).Select(game.world.GetAbstractRoom).Where(x => x != null)];
                rooms.AddRange(connections);

                Plugin.Logger.LogDebug("ROOMS TO BE MERGED:");
                foreach (var room in rooms)
                    Plugin.Logger.LogDebug(room.name);

                mapContent.MergeNewData(game.world);
            }


            // TODO: readd reuse rooms

            // Capture rooms
            foreach (var room in rooms)
            {
                foreach (var step in CaptureRoom(room, mapContent))
                    yield return step;
            }

            // Trim data
            mapContent.TrimEntries();

            // Done
            if (Data.CollectRoomData(updateMode))
            {
                File.WriteAllText(PathOfMetadata(slugcat.value, region), Json.Serialize(mapContent));
            }

            Plugin.Logger.LogDebug("Capture task done with " + region);
        }

        private System.Collections.IEnumerable CaptureRoom(AbstractRoom room, RegionInfo regionContent)
        {
            RainWorldGame game = room.world.game;

            // load room
            game.overWorld.activeWorld.loadingRooms.Clear();
            Random.InitState(0);
            game.overWorld.activeWorld.ActivateRoom(room);

            // load room until it is loaded
            if (game.overWorld.activeWorld.loadingRooms.Count > 0 && game.overWorld.activeWorld.loadingRooms[0].room == room.realizedRoom)
            {
                RoomPreparer loading = game.overWorld.activeWorld.loadingRooms[0];
                while (!loading.done)
                {
                    loading.Update();
                }
            }
            while (room.realizedRoom.loadingProgress < 3 || room.realizedRoom.waitToEnterAfterFullyLoaded >= 1)
            {
                room.realizedRoom.Update();
            }

            // Die, evil cameras!
            if (blacklistedCams.TryGetValue(room.name, out int[] cams))
            {
                var newpos = room.realizedRoom.cameraPositions.ToList();
                for (int i = cams.Length - 1; i >= 0; i--)
                {
                    newpos.RemoveAt(cams[i] - 1);
                }
                room.realizedRoom.cameraPositions = [.. newpos];
            }

            yield return null;
            Random.InitState(0);
            // go to room
            game.cameras[0].MoveCamera(room.realizedRoom, 0);
            game.cameras[0].virtualMicrophone.AllQuiet();
            // get to room
            while (game.cameras[0].loadingRoom != null) yield return null;
            Random.InitState(0);

            if (Data.CollectRoomData(updateMode))
                regionContent.UpdateRoom(room.realizedRoom);

            if (Preferences.ShowCreatures.GetValue() && Data.TakeScreenshots(updateMode))
            {
                // wait a bit so creatures can more interesting stuff
                for (int i = 0; i < 6; i++) yield return null;
            }

            for (int i = 0; i < room.realizedRoom.cameraPositions.Length; i++)
            {
                // load screen
                Random.InitState(room.name.GetHashCode()); // allow for deterministic random numbers, to make rain look less garbage
                game.cameras[0].MoveCamera(i);
                game.cameras[0].virtualMicrophone.AllQuiet();

                yield return new WaitForEndOfFrame(); // wait an extra frame or two so objects can render, why not
                yield return new WaitForEndOfFrame();

                string filename = PathOfScreenshot(game.StoryCharacter.value, room.world.name, room.name, i);
                if ((!Preferences.ScreenshotterSkipExisting.GetValue() && Data.TakeScreenshots(updateMode)) || !File.Exists(filename))
                {
                    // Only take screenshot if the user wants to ***OR*** the screenshot does not already exist, in which case it needs to exist or the program can't run
                    ScreenCapture.CaptureScreenshot(filename);
                }

                // palette and colors
                if (Data.CollectRoomData(updateMode))
                {
                    regionContent.LogPalette(game.cameras[0].currentPalette);
                }

                yield return new WaitForEndOfFrame(); // extra frame or two for safety
                yield return new WaitForEndOfFrame();
            }
            Random.InitState(0);
            room.Abstractize();
            yield return null;
        }


        public void CreateErrorPopup(string title, string message, bool canContinue, Action onContinue = null)
        {
            Plugin.errorQueue.Enqueue(new ErrorInfo
            {
                title = title,
                message = message,
                canContinue = canContinue,
                onContinue = onContinue
            });
        }
    }
}

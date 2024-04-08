using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using RWCustom;
using UnityEngine;
using MapExporter.Screenshotter;

namespace MapExporter;

sealed class RegionInfo : IJsonObject
{
    readonly Dictionary<string, RoomEntry> rooms = [];
    readonly List<ConnectionEntry> connections = [];
    readonly string acronym;
    readonly List<Color> fgcolors = [];
    readonly List<Color> bgcolors = [];
    readonly List<Color> sccolors = [];
    readonly HashSet<string> worldConditionalLinks = [];
    readonly HashSet<string> worldCreatures = [];
    readonly HashSet<string> worldRoomTags = [];

    public string copyRooms;

    public RegionInfo(World world)
    {
        acronym = world.name;

        LoadMapConfig(world);
        LoadWorldConfig(world);
    }

    private RegionInfo(string acronym)
    {
        this.acronym = acronym;
    }

    private RoomEntry GetOrCreateRoomEntry(string name)
    {
        return rooms.TryGetValue(name, out var value) ? value : rooms[name] = new(name);
    }

    private void LoadMapConfig(World world)
    {
        string path = AssetManager.ResolveFilePath(Path.Combine("World", world.name, $"map_{world.name}-{world.game.GetStorySession.saveState.saveStateNumber}.txt"));

        if (!File.Exists(path)) {
            path = AssetManager.ResolveFilePath(Path.Combine("World", world.name, $"map_{world.name}.txt"));
        }

        if (!File.Exists(path)) {
            Plugin.Logger.LogWarning($"No map data for {world.game.StoryCharacter}/{world.name} at {path}");
        }
        else {
            Plugin.Logger.LogDebug($"Found map data for {world.game.StoryCharacter}/{world.name} at {path}");

            string[] contents = File.ReadAllLines(path);

            foreach (string s in contents) {
                string[] split = Regex.Split(s, ": ");
                string sname = split[0];

                if (sname == "Connection") {
                    connections.Add(new ConnectionEntry(split[1]));
                }
                else if (!Capturer.HiddenRoom(world.GetAbstractRoom(sname))) {
                    GetOrCreateRoomEntry(sname).ParseEntry(split[1]);
                }
            }
        }
    }

    private void LoadWorldConfig(World world)
    {
        string acronym = world.region.name;
        string path = AssetManager.ResolveFilePath($"world/{acronym}/world_{acronym}.txt");
        if (File.Exists(path)) {
            AssimilateConditionalLinks(File.ReadAllLines(path));
            AssimilateRoomTags(File.ReadAllLines(path));
            AssimilateCreatures(File.ReadAllLines(path));
        }
        else {
            Plugin.Logger.LogError($"WORLD FILE DOES NOT EXIST: {path}");
        }
    }

    private void AssimilateConditionalLinks(IEnumerable<string> raw)
    {
        bool insideofconditionallinks = false;
        foreach (var item in raw)
        {
            if (item == "CONDITIONAL LINKS") insideofconditionallinks = true;
            else if (item == "END CONDITIONAL LINKS") insideofconditionallinks = false;
            else if (insideofconditionallinks)
            {
                if (string.IsNullOrEmpty(item) || item.StartsWith("//")) continue;
                worldConditionalLinks.Add(item);
            }
        }
    }
    private void AssimilateRoomTags(IEnumerable<string> raw)
    {
        bool insideofrooms = false;
        foreach (var item in raw)
        {
            if (item == "ROOMS") insideofrooms = true;
            else if (item == "END ROOMS") insideofrooms = false;
            else if (insideofrooms)
            {
                if (string.IsNullOrEmpty(item) || item.StartsWith("//")) continue;
                worldRoomTags.Add(item);
            }
        }
    }

    private void AssimilateCreatures(IEnumerable<string> raw)
    {
        bool insideofcreatures = false;
        foreach (var item in raw)
        {
            if (item == "CREATURES") insideofcreatures = true;
            else if (item == "END CREATURES") insideofcreatures = false;
            else if (insideofcreatures)
            {
                if (string.IsNullOrEmpty(item) || item.StartsWith("//")) continue;
                worldCreatures.Add(item);
            }
        }
    }

    static float[] Vec2arr(Vector2 vec) => [vec.x, vec.y];
    static float[] Vec2arr(Vector3 vec) => [vec.x, vec.y, vec.z];
    static int[] Intvec2arr(IntVector2 vec) => [vec.x, vec.y];
    static Vector2 Arr2Vec2(List<float> arr) => new(arr[0], arr[1]);
    static Vector3 Arr2Vec3(List<float> arr) => new(arr[0], arr[1], arr[2]);
    static IntVector2 Arr2IntVec2(List<int> arr) => new(arr[0], arr[1]);

    public void UpdateRoom(Room room)
    {
        GetOrCreateRoomEntry(room.abstractRoom.name).UpdateEntry(room);
    }

    public Dictionary<string, object> ToJson()
    {
        var ret = new Dictionary<string, object> {
            ["acronym"] = acronym,
            ["fgcolors"] = (from s in fgcolors select Vec2arr((Vector3)(Vector4)s)).ToList(),
            ["bgcolors"] = (from s in bgcolors select Vec2arr((Vector3)(Vector4)s)).ToList(),
            ["sccolors"] = (from s in sccolors select Vec2arr((Vector3)(Vector4)s)).ToList()
        };
        if (copyRooms == null) {
            ret["rooms"] = rooms;
            ret["connections"] = connections;
        }
        else {
            ret["copyRooms"] = copyRooms;
        }
        ret["conditionallinks"] = worldConditionalLinks.ToArray();
        ret["roomtags"] = worldRoomTags.ToArray();
        ret["creatures"] = worldCreatures.ToArray();
        return ret;
    }

    public static RegionInfo FromJSON(Dictionary<string, object> json)
    {
        var info = new RegionInfo((string)json["acronym"]);
        info.fgcolors.AddRange(
            ((List<object>)json["fgcolors"])
            .Select(x => (List<float>)x)
            .Select(x => new Color(x[0], x[1], x[2]))
        );
        info.bgcolors.AddRange(
            ((List<object>)json["bgcolors"])
            .Select(x => (List<float>)x)
            .Select(x => new Color(x[0], x[1], x[2]))
        );
        info.sccolors.AddRange(
            ((List<object>)json["sccolors"])
            .Select(x => (List<float>)x)
            .Select(x => new Color(x[0], x[1], x[2]))
        );

        if (json.ContainsKey("copyRooms"))
        {
            info.copyRooms = (string)json["copyRooms"];
        }
        else
        {
            foreach (var kv in (Dictionary<string, object>)json["rooms"])
            {
                info.rooms.Add(kv.Key, RoomEntry.FromJSON((Dictionary<string, object>)kv.Value));
            }
            info.connections.AddRange(
                ((List<object>)json["connections"])
                .Select(x => (Dictionary<string, object>)x)
                .Select(ConnectionEntry.FromJSON)
            );
        }

        foreach (string s in (string[])json["conditionallinks"])
        {
            info.worldConditionalLinks.Add(s);
        }

        foreach (string s in (string[])json["roomtags"])
        {
            info.worldRoomTags.Add(s);
        }

        foreach (string s in (string[])json["creatures"])
        {
            info.worldCreatures.Add(s);
        }
        return info;
    }

    internal void LogPalette(RoomPalette currentPalette)
    {
        // get sky color and fg color (px 00 and 07)
        Color fg = currentPalette.texture.GetPixel(0, 0);
        Color bg = currentPalette.texture.GetPixel(0, 7);
        Color sc = currentPalette.shortCutSymbol;
        fgcolors.Add(fg);
        bgcolors.Add(bg);
        sccolors.Add(sc);
    }

    sealed class RoomEntry(string roomName) : IJsonObject
    {
        public string roomName = roomName;

        // from map txt
        public Vector2 devPos;
        public Vector2 canPos;
        public int canLayer;
        public string subregion;
        public bool everParsed = false;
        public void ParseEntry(string entry)
        {
            string[] fields = Regex.Split(entry, "><");
            canPos.x = float.Parse(fields[0]);
            canPos.y = float.Parse(fields[1]);
            devPos.x = float.Parse(fields[2]);
            devPos.y = float.Parse(fields[3]);
            canLayer = int.Parse(fields[4]);
            subregion = fields[5];
            everParsed = true;
        }

        // from room
        public Vector2[] cameras; // TODO: can this cause issues if it's not the same as the cache?
        public int[] size;
        public int[,][] tiles;
        public IntVector2[] nodes;

        public void UpdateEntry(Room room)
        {
            cameras = room.cameraPositions;

            size = [room.Width, room.Height];

            tiles = new int[room.Width, room.Height][];
            for (int k = 0; k < room.Width; k++)
            {
                for (int l = 0; l < room.Height; l++)
                {
                    // Dont like either available formats ?
                    // Invent a new format
                    tiles[k, l] = [(int)room.Tiles[k, l].Terrain, (room.Tiles[k, l].verticalBeam ? 2:0) + (room.Tiles[k, l].horizontalBeam ? 1:0), room.Tiles[k, l].shortCut];
                    //terain, vb+hb, sc
                }
            }
            nodes = room.exitAndDenIndex;
        }

        // wish there was a better way to do this
        public Dictionary<string, object> ToJson()
        {
            return new Dictionary<string, object>()
            {
                { "roomName", roomName },
                { "canPos", Vec2arr(canPos) },
                { "canLayer", canLayer },
                { "devPos", Vec2arr(devPos) },
                { "subregion", subregion },
                { "cameras", cameras != null ? (from c in cameras select Vec2arr(c)).ToArray() : null},
                { "nodes", nodes != null ? (from n in nodes select Intvec2arr(n)).ToArray() : null},
                { "size", size},
                { "tiles", tiles},
            };
        }

        public static RoomEntry FromJSON(Dictionary<string, object> json)
        {
            var entry = new RoomEntry((string)json["roomName"])
            {
                canPos = Arr2Vec2([.. ((List<object>)json["canPos"]).Select(x => (float)x)]),
                canLayer = (int)json["canLayer"],
                devPos = Arr2Vec2([.. ((List<object>)json["devPos"]).Select(x => (float)x)]),
                subregion = (string)json["subregion"],
                cameras = json["cameras"] != null ? ((List<object>)json["cameras"]).Select(x => (List<object>)x).Select(x => Arr2Vec2([.. x.Select(x => (float)x)])).ToArray() : null,
                nodes = json["nodes"] != null ? ((List<object>)json["nodes"]).Select(x => (List<object>)x).Select(x => Arr2IntVec2([.. x.Select(x => (int)x)])).ToArray() : null,
                size = ((List<object>)json["size"]).Select(x => (int)x).ToArray(),
            };
            // idk how to convert object to multidimensional array (not jagged array) since you have to specify dimensions when creating it and casting won't
            // so I just copy over everything lol
            if (json["tiles"] != null)
            {
                var (w, h) = (entry.size[0], entry.size[1]);
                entry.tiles = new int[w, h][];
                var rawTiles = ((List<object>)json["tiles"]).Select(x => ((List<object>)x).Select(x => (int)x).ToArray()).ToList();
                for (int i = 0; i < w; i++)
                {
                    for (int j = 0; j < h; j++)
                    {
                        entry.tiles[i, j] = rawTiles[j + i * h]; // multidimensional arrays are row-major (rightmost dimension is contiguous)
                    }
                }
            }
            else
            {
                entry.tiles = null;
            }
            return entry;
        }
    }

    sealed class ConnectionEntry : IJsonObject
    {
        public string roomA;
        public string roomB;
        public IntVector2 posA;
        public IntVector2 posB;
        public int dirA;
        public int dirB;

        public ConnectionEntry(string entry)
        {
            string[] fields = Regex.Split(entry, ",");
            roomA = fields[0];
            roomB = fields[1];
            posA = new IntVector2(int.Parse(fields[2]), int.Parse(fields[3]));
            posB = new IntVector2(int.Parse(fields[4]), int.Parse(fields[5]));
            dirA = int.Parse(fields[6]);
            dirB = int.Parse(fields[7]);
        }

        public Dictionary<string, object> ToJson()
        {
            return new Dictionary<string, object>()
            {
                { "roomA", roomA },
                { "roomB", roomB },
                { "posA", Intvec2arr(posA) },
                { "posB", Intvec2arr(posB) },
                { "dirA", dirA },
                { "dirB", dirB },
            };
        }

        public static ConnectionEntry FromJSON(Dictionary<string, object> json)
        {
            var entry = (ConnectionEntry)FormatterServices.GetUninitializedObject(typeof(ConnectionEntry));
            entry.roomA = (string)json["roomA"];
            entry.roomB = (string)json["roomB"];
            entry.posA = Arr2IntVec2([.. ((List<object>)json["posA"]).Select(x => (int)x)]);
            entry.posB = Arr2IntVec2([.. ((List<object>)json["posB"]).Select(x => (int)x)]);
            entry.dirA = (int)json["dirA"];
            entry.dirB = (int)json["dirB"];
            return entry;
        }
    }
}

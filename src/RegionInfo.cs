using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using RWCustom;
using UnityEngine;
using DevInterface;

namespace MapExporter
{
    internal sealed class RegionInfo : IJsonObject
    {
        public Dictionary<string, RoomEntry> rooms = [];
        public List<ConnectionEntry> connections = [];
        public string acronym;
        public string name;
        public string echoRoom;
        public List<Color> fgcolors = [];
        public List<Color> bgcolors = [];
        public List<Color> sccolors = [];

        // For passing data down to children
        private readonly Dictionary<string, Vector2> devPos = [];

        public RegionInfo() { }

        public RegionInfo(World world)
        {
            // Region identity + echo room because why not grab that here
            acronym = world.name;
            name = Region.GetRegionFullName(acronym, null);
            echoRoom = world?.worldGhost?.ghostRoom?.name;

            // Figure out where the rooms are in dev tools so we can create our room representations
            var devMap = (MapPage)FormatterServices.GetUninitializedObject(typeof(MapPage));
            string slugcatFilePath = AssetManager.ResolveFilePath(
                Path.Combine(
                    "World",
                    world.name,
                    "map_" + world.name + "-" + (world.game.StoryCharacter?.value ?? "") + ".txt"
                ));
            string normalFilePath = AssetManager.ResolveFilePath(
                Path.Combine(
                    "World",
                    world.name,
                    "map_" + world.name + ".txt"
                ));
            devMap.filePath = File.Exists(slugcatFilePath) ? slugcatFilePath : normalFilePath;
            devMap.subNodes = [];
            if (devMap.filePath == normalFilePath && !File.Exists(normalFilePath))
            {
                throw new FileNotFoundException("Map file doesn't exist for region " + acronym + "!");
            }
            foreach (var room in world.abstractRooms)
            {
                var panel = (RoomPanel)FormatterServices.GetUninitializedObject(typeof(RoomPanel));
                panel.roomRep = (MapObject.RoomRepresentation)FormatterServices.GetUninitializedObject(typeof(MapObject.RoomRepresentation));
                panel.roomRep.room = room;
                devMap.subNodes.Add(panel);
            }

            devMap.LoadMapConfig();

            foreach (var node in devMap.subNodes)
            {
                var panel = node as RoomPanel;
                devPos[panel.roomRep.room.name] = panel.devPos;
            }

            // Ok continue on with initializing the rest of the object
            foreach (var room in world.abstractRooms)
            {
                rooms[room.name] = new RoomEntry(this, world, room);
                // I would initialize connections here but they require a loaded room so nope :3
            }
        }

        public void LogPalette(RoomPalette currentPalette)
        {
            // get sky color and fg color (px 00 and 07)
            Color fg = currentPalette.texture.GetPixel(0, 0);
            Color bg = currentPalette.texture.GetPixel(0, 7);
            Color sc = currentPalette.shortCutSymbol;
            fgcolors.Add(fg);
            bgcolors.Add(bg);
            sccolors.Add(sc);
        }

        public void UpdateRoom(Room room)
        {
            if (rooms.ContainsKey(room.abstractRoom.name))
            {
                rooms[room.abstractRoom.name].UpdateEntry(room);
            }
        }

        public void TrimEntries()
        {
            HashSet<string> toRemove = [];
            foreach (var kv in rooms)
            {
                if (kv.Value.offscreenDen) continue;
                if (kv.Value.size.x == 0 || kv.Value.size.y == 0 || (kv.Value.cameras?.Length ?? 0) == 0)
                {
                    toRemove.Add(kv.Key);
                }
            }

            foreach (var key in toRemove)
            {
                rooms.Remove(key);
            }

            // Theoretically any of these removed rooms shouldn't have any connections but you never know
            int i = 0;
            while (i < connections.Count)
            {
                var conn = connections[i];
                if (toRemove.Contains(conn.roomA) || toRemove.Contains(conn.roomB))
                {
                    connections.RemoveAt(i);
                }
                else
                {
                    i++;
                }
            }
        }

        public Dictionary<string, object> ToJson()
        {
            return new()
            {
                { "acronym", acronym },
                { "name", name },
                { "echoRoom", echoRoom },
                { "rooms", rooms },
                { "connections", connections },
                { "fgcolors", (from s in fgcolors select Vec2arr((Vector3)(Vector4)s)).ToList() },
                { "bgcolors", (from s in bgcolors select Vec2arr((Vector3)(Vector4)s)).ToList() },
                { "sccolors", (from s in sccolors select Vec2arr((Vector3)(Vector4)s)).ToList() },
            };
        }

        public static RegionInfo FromJson(Dictionary<string, object> json)
        {
            var entry = new RegionInfo
            {
                acronym = (string)json["acronym"],
                name = (string)json["name"],
                echoRoom = (string)json["echoRoom"],

                rooms = [],
                connections = [],
                fgcolors = ((List<object>)json["fgcolors"]).Cast<List<object>>().Select(Arr2Color).ToList(),
                bgcolors = ((List<object>)json["bgcolors"]).Cast<List<object>>().Select(Arr2Color).ToList(),
                sccolors = ((List<object>)json["sccolors"]).Cast<List<object>>().Select(Arr2Color).ToList()
            };

            // Add rooms
            foreach (var kv in (Dictionary<string, object>)json["rooms"])
            {
                entry.rooms[kv.Key] = RoomEntry.FromJson((Dictionary<string, object>)kv.Value);
            }

            // Add connections
            foreach (var data in ((List<object>)json["connections"]).Cast<Dictionary<string, object>>())
            {
                entry.connections.Add(ConnectionEntry.FromJson(data));
            }

            return entry;
        }

        static float[] Vec2arr(Vector2 vec) => [vec.x, vec.y];
        static float[] Vec2arr(Vector3 vec) => [vec.x, vec.y, vec.z];
        static int[] Intvec2arr(IntVector2 vec) => [vec.x, vec.y];
        static Vector2 Arr2Vec2(List<object> arr) => new((float)(double)arr[0], (float)(double)arr[1]);
        static Color Arr2Color(List<object> arr) => new((float)(double)arr[0], (float)(double)arr[1], (float)(double)arr[2]);
        static IntVector2 Arr2IntVec2(List<object> arr) => new((int)(long)arr[0], (int)(long)arr[1]);

        static int IntVec2Dir(IntVector2 vec) => vec.x != 0 ? (vec.x < 0 ? 0 : 2) : (vec.y < 0 ? 1 : 3);

        public class RoomEntry : IJsonObject
        {
            private readonly RegionInfo regionInfo;

            public string roomName;
            public string subregion;
            public Vector2 devPos;

            public Vector2[] cameras;
            public IntVector2 size = new(0, 0);
            public int[,][] tiles;
            public IntVector2[] nodes;

            public DenSpawnData[][] spawns;
            public string[] tags;

            internal bool offscreenDen = false;

            public RoomEntry() { }

            public RoomEntry(RegionInfo owner, World world, AbstractRoom room)
            {
                regionInfo = owner;
                offscreenDen = room.offScreenDen;

                roomName = room.name;
                subregion = room.subregionName;
                devPos = owner.devPos[room.name];

                var spawners = world.spawners;
                spawns = new DenSpawnData[spawners.Length][];
                for (int i = 0; i < spawners.Length; i++)
                {
                    var spawner = spawners[i];
                    if (spawner.den.room != room.index) continue;
                    if (spawner is World.Lineage lineage)
                    {
                        spawns[i] = new DenSpawnData[lineage.creatureTypes.Length];
                        for (int j = 0; j < spawns[i].Length; j++)
                        {
                            spawns[i][j] = new DenSpawnData()
                            {
                                chance = lineage.progressionChances[j],
                                count = 1,
                                type = lineage.creatureTypes[j] < 0 ? "" : new CreatureTemplate.Type(ExtEnum<CreatureTemplate.Type>.values.GetEntry(lineage.creatureTypes[j]), false).value,
                                night = lineage.nightCreature,
                                data = lineage.spawnData[j],
                                den = lineage.den.abstractNode
                            };
                        }
                    }
                    else if (spawner is World.SimpleSpawner simple)
                    {
                        spawns[i] = [
                            new DenSpawnData() {
                                chance = -1f,
                                count = simple.amount,
                                type = simple.creatureType?.value ?? "",
                                night = simple.nightCreature,
                                data = simple.spawnDataString,
                                den = simple.den.abstractNode
                            }
                        ];
                    }
                    else
                    {
                        Plugin.Logger.LogWarning("Invalid spawner type! Room: " + spawner.den.ResolveRoomName() + ", Type: " + spawner.GetType().FullName);
                    }
                }
                
                tags = [.. (room.roomTags ?? [])];
            }

            public void UpdateEntry(Room room)
            {
                cameras = room.cameraPositions;
                size = new IntVector2(room.Width, room.Height);
                tiles = new int[room.Width, room.Height][];
                for (int k = 0; k < room.Width; k++)
                {
                    for (int l = 0; l < room.Height; l++)
                    {
                        // Dont like either available formats ?
                        // Invent a new format
                        tiles[k, l] = [(int)room.Tiles[k, l].Terrain, (room.Tiles[k, l].verticalBeam ? 2 : 0) + (room.Tiles[k, l].horizontalBeam ? 1 : 0), room.Tiles[k, l].shortCut];
                        //terain, vb+hb, sc
                    }
                }
                nodes = room.exitAndDenIndex;

                // Initialize connections
                var aRoom = room.abstractRoom;
                for (int i = 0; i < aRoom.connections.Length; i++)
                {
                    var other = aRoom.world.GetAbstractRoom(aRoom.connections[i]);
                    if (other == null) continue;

                    ConnectionEntry conn = null;
                    foreach (var c in regionInfo.connections)
                    {
                        if (c.roomB == aRoom.name && c.roomA == other.name)
                        {
                            conn = c;
                            break;
                        }
                    }

                    if (conn == null)
                    {
                        conn = new ConnectionEntry()
                        {
                            roomA = aRoom.name,
                            roomB = other.name,
                            posA = nodes[aRoom.ExitIndex(other.index)],
                        };
                        conn.dirA = IntVec2Dir(room.ShorcutEntranceHoleDirection(conn.posA));
                        regionInfo.connections.Add(conn);
                        // posB side of things will be initialized later when we load the room
                    }
                    else
                    {
                        conn.posB = nodes[aRoom.ExitIndex(other.index)];
                        conn.dirB = IntVec2Dir(room.ShorcutEntranceHoleDirection(conn.posB));
                    }
                }
            }

            public Dictionary<string, object> ToJson()
            {
                return new()
                {
                    { "name", roomName },
                    { "subregion", subregion },
                    { "pos", Vec2arr(devPos) },

                    { "cameras", cameras?.Select(Vec2arr).ToList() },
                    { "size", size != null ? Intvec2arr(size) : null },
                    { "tiles", tiles },
                    { "nodes", nodes?.Select(Intvec2arr).ToList() },

                    { "spawns", spawns },
                    { "tags", tags },
                };
            }

            public static RoomEntry FromJson(Dictionary<string, object> json)
            {
                var entry = new RoomEntry
                {
                    roomName = (string)json["name"],
                    subregion = (string)json["subregion"],
                    devPos = Arr2Vec2((List<object>)json["pos"]),

                    spawns = ((List<object>)json["spawns"]).Cast<List<object>>().Select(x =>
                    {
                        var list = new List<DenSpawnData>();
                        foreach (Dictionary<string, object> spawn in x.Cast<Dictionary<string, object>>())
                        {
                            list.Add(DenSpawnData.FromJson(spawn));
                        }
                        return list.ToArray();
                    }).ToArray(),
                    tags = ((List<object>)json["tags"]).Cast<string>().ToArray(),
                };

                if (json["cameras"] != null)
                {
                    entry.cameras = ((List<object>)json["cameras"]).Cast<List<object>>().Select(Arr2Vec2).ToArray();
                    entry.size = Arr2IntVec2((List<object>)json["size"]);
                    entry.nodes = ((List<object>)json["nodes"]).Cast<List<object>>().Select(Arr2IntVec2).ToArray();

                    var (w, h) = (entry.size.x, entry.size.y);
                    entry.tiles = new int[w, h][];
                    var rawTiles = ((List<object>)json["tiles"]).Select(x => ((List<object>)x).Select(x => (int)(long)x).ToArray()).ToArray();
                    for (int i = 0; i < w; i++)
                    {
                        for (int j = 0; j < h; j++)
                        {
                            entry.tiles[i, j] = rawTiles[j + i * h]; // multidimensional arrays are row-major (rightmost dimension is contiguous)
                        }
                    }
                }

                return entry;
            }

            public struct DenSpawnData : IJsonObject
            {
                public string type;
                public int count;
                public float chance;
                public bool night;
                public string data;
                public int den;

                public readonly Dictionary<string, object> ToJson()
                {
                    return new Dictionary<string, object>
                    {
                        { "type", type },
                        { "count", count },
                        { "chance", chance },
                        { "night", night },
                        { "data", data },
                        { "den", den },
                    };
                }

                public static DenSpawnData FromJson(Dictionary<string, object> json)
                {
                    return new DenSpawnData()
                    {
                        type = (string)json["type"],
                        count = (int)(long)json["count"],
                        chance = (float)(double)json["chance"],
                        night = (bool)json["night"],
                        data = (string)json["data"],
                        den = (int)(long)json["den"],
                    };
                }
            }
        }

        public class ConnectionEntry : IJsonObject
        {
            public string roomA;
            public string roomB;
            public IntVector2 posA;
            public IntVector2 posB;
            public int dirA;
            public int dirB;

            public ConnectionEntry() { } // empty

            public ConnectionEntry(string entry) // old version read from world file, obsolete now
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

            public static ConnectionEntry FromJson(Dictionary<string, object> json)
            {
                var entry = new ConnectionEntry
                {
                    roomA = (string)json["roomA"],
                    roomB = (string)json["roomB"],
                    posA = Arr2IntVec2((List<object>)json["posA"]),
                    posB = Arr2IntVec2((List<object>)json["posB"]),
                    dirA = (int)(long)json["dirA"],
                    dirB = (int)(long)json["dirB"]
                };
                return entry;
            }
        }
    }
}

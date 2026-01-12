using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using DevInterface;
using MapExporterNew.Screenshotter;
using RWCustom;
using UnityEngine;

namespace MapExporterNew
{
    internal sealed class RegionInfo : IJsonObject
    {
        private const float SCALEDOWN = 2f;
        public static readonly ConditionalWeakTable<World, List<World.CreatureSpawner>> spawnerCWT = new();
        public Dictionary<string, RoomEntry> rooms = [];
        public List<ConnectionEntry> connections = [];
        public Dictionary<string, Vector2> vistaPoints = [];
        public string acronym;
        public string name;
        public string echoRoom;
        public List<Color> fgcolors = [];
        public List<Color> bgcolors = [];
        public List<Color> sccolors = [];
        public bool fromJson = false;

        // For passing data down to children
        private readonly Dictionary<string, Vector2> devPos = [];

        public RegionInfo() { }

        public RegionInfo(World world)
        {
            // Region identity + echo room because why not grab that here
            acronym = world.name;
            name = Region.GetRegionFullName(acronym, null);
            echoRoom = world.worldGhost?.ghostRoom?.name;

            MergeNewData(world);
        }

        public void MergeNewData(World world)
        {
            // Figure out where the rooms are in dev tools so we can create our room representations
            if (Data.CollectRoomPositions(Capturer.updateMode))
            {
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
            }

            // Vista locations
            if (Data.CollectRoomData(Capturer.updateMode))
            {
                var vistasPath = AssetManager.ResolveFilePath(Path.Combine("World", world.name, "vistas.txt"));
                if (File.Exists(vistasPath))
                {
                    var rawLines = File.ReadAllLines(vistasPath);
                    foreach (var lines in rawLines)
                    {
                        var split = lines.Trim().Split(',');
                        float x = float.Parse(split[1]);
                        float y = float.Parse(split[2]);
                        vistaPoints.Add(split[0], new Vector2(x, y));
                    }
                }
            }

            // Ok continue on with initializing the rest of the object
            foreach (var room in world.abstractRooms)
            {
                if (!rooms.ContainsKey(room.name))
                {
                    rooms[room.name] = new RoomEntry(this, world, room);
                    // I would initialize connections here but they require a loaded room so nope :3
                }
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

            // Filter out the theoretically impossible things we don't want like connections to rooms we removed and incomplete connection entries.
            int i = 0;
            while (i < connections.Count)
            {
                var conn = connections[i];
                if (toRemove.Contains(conn.roomA) || toRemove.Contains(conn.roomB) || !conn.complete)
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
            var newVistaPoints = new Dictionary<string, float[]>();
            foreach (var kv in vistaPoints)
            {
                newVistaPoints[kv.Key] = Vector2ToArray(kv.Value);
            }
            return new()
            {
                { "acronym", acronym },
                { "name", name },
                { "echoRoom", echoRoom },
                { "rooms", rooms },
                { "connections", connections },
                { "vistaPoints", newVistaPoints },
                { "fgcolors", (from s in fgcolors select Vector3ToArray((Vector3)(Vector4)s)).ToList() },
                { "bgcolors", (from s in bgcolors select Vector3ToArray((Vector3)(Vector4)s)).ToList() },
                { "sccolors", (from s in sccolors select Vector3ToArray((Vector3)(Vector4)s)).ToList() },
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
                vistaPoints = [],
                fgcolors = [.. ((List<object>)json["fgcolors"]).Cast<List<object>>().Select(ColorFromArray)],
                bgcolors = [.. ((List<object>)json["bgcolors"]).Cast<List<object>>().Select(ColorFromArray)],
                sccolors = [.. ((List<object>)json["sccolors"]).Cast<List<object>>().Select(ColorFromArray)],
                fromJson = true
            };

            // Add rooms
            foreach (var kv in (Dictionary<string, object>)json["rooms"])
            {
                entry.rooms[kv.Key] = RoomEntry.FromJson(entry, (Dictionary<string, object>)kv.Value);
                entry.devPos[kv.Key] = entry.rooms[kv.Key].devPos * SCALEDOWN;
            }

            // Add connections
            foreach (var data in ((List<object>)json["connections"]).Cast<Dictionary<string, object>>())
            {
                entry.connections.Add(ConnectionEntry.FromJson(data));
            }

            // Add vistas
            if (json.ContainsKey("vistaPoints"))
            {
                foreach (var data in (Dictionary<string, object>)json["vistaPoints"])
                {
                    entry.vistaPoints.Add(data.Key, Vector2FromJson(data.Value));
                }
            }

            return entry;
        }

        static int IntVec2Dir(IntVector2 vec) => vec.x == 0 && vec.y == 0 ? -1 : (vec.x != 0 ? (vec.x < 0 ? 0 : 2) : (vec.y < 0 ? 1 : 3));

        public class RoomEntry : IJsonObject
        {
            private readonly RegionInfo regionInfo;

            public string roomName;
            public string subregion;
            public Vector2 devPos;
            public bool hidden = false;

            public Vector2[] cameras;
            public IntVector2 size = new(0, 0);
            public int[,][] tiles;
            public IntVector2[] nodes;

            public DenSpawnData[][] spawns;
            public string[] tags;
            public PlacedObjectData[] placedObjects;
            public List<TerrainEntry> terrain = [];

            internal bool offscreenDen = false;

            public RoomEntry(RegionInfo owner)
            { 
                regionInfo = owner;
            }

            public RoomEntry(RegionInfo owner, World world, AbstractRoom room)
            {
                regionInfo = owner;
                offscreenDen = room.offScreenDen;

                roomName = room.name;
                subregion = room.subregionName;
                devPos = owner.devPos.TryGetValue(room.name, out var dvPos) ? dvPos / SCALEDOWN : Vector2.zero;

                // spawns
                if (spawnerCWT.TryGetValue(world, out var spawners))
                {
                    List<DenSpawnData[]> spawns = [];
                    for (int i = 0; i < spawners.Count; i++)
                    {
                        var spawner = spawners[i];
                        if (spawner.den.room != room.index) continue;
                        if (spawner is World.Lineage lineage)
                        {
                            var den = new DenSpawnData[lineage.creatureTypes.Length];
                            spawns.Add(den);
                            for (int j = 0; j < den.Length; j++)
                            {
                                den[j] = new DenSpawnData()
                                {
                                    chance = lineage.progressionChances[j],
                                    count = 1,
                                    type = lineage.creatureTypes[j] < 0 ? "" : new CreatureTemplate.Type(ExtEnum<CreatureTemplate.Type>.values.GetEntry(lineage.creatureTypes[j]), false).value,
                                    data = lineage.spawnData[j],
                                    den = lineage.den.abstractNode
                                };
                            }
                        }
                        else if (spawner is World.SimpleSpawner simple)
                        {
                            spawns.Add([
                                new DenSpawnData() {
                                    chance = -1f,
                                    count = simple.amount,
                                    type = simple.creatureType?.value ?? "",
                                    data = simple.spawnDataString,
                                    den = simple.den.abstractNode
                                }
                            ]);
                        }
                        else
                        {
                            Plugin.Logger.LogWarning("Invalid spawner type! Room: " + spawner.den.ResolveRoomName() + ", Type: " + spawner.GetType().FullName);
                        }
                    }

                    this.spawns = [.. spawns];
                }
                
                tags = [.. (room.roomTags ?? [])];
            }

            public void UpdateEntry(Room room)
            {
                var aRoom = room.abstractRoom;

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
                nodes = new IntVector2[aRoom.nodes.Length];
                for (int i = 0; i < nodes.Length; i++)
                {
                    try
                    {
                        nodes[i] = room.LocalCoordinateOfNode(i).Tile;
                    }
                    catch (Exception e) // die
                    {
                        string nodeType = aRoom.nodes[i].type?.ToString() ?? "UNKNOWN";

                        Plugin.errorQueue.Enqueue(new ErrorInfo
                        {
                            title = "Bad node!",
                            message = $"{aRoom.name} had a bad node! (index {i}, type {nodeType})",
                            canContinue = true
                        });
                        Plugin.Logger.LogError($"{aRoom.name} had a bad node! (index {i}, type {nodeType})");
                        Plugin.Logger.LogError(e);
                        nodes[i] = new IntVector2(-1, -1);
                    }
                }
                // nodes = [.. aRoom.nodes.Select((_, i) => room.LocalCoordinateOfNode(i).Tile)];

                // Initialize connections
                if (regionInfo.fromJson)
                {
                    // If we loaded from the JSON, figure out what rooms already connect to this
                    var existing = regionInfo.connections.Where(x => x.roomA == aRoom.name || x.roomB == aRoom.name)
                        .ToDictionary(x => x.roomA == aRoom.name ? x.roomB : x.roomA);
                    var visitedExisting = new HashSet<string>();
                    for (int i = 0; i < aRoom.connections.Length; i++)
                    {
                        var other = aRoom.world.GetAbstractRoom(aRoom.connections[i]);
                        if (other == null) continue;

                        // We want to know if there are any new connections.
                        int nodeIndex = room.shortcuts.Select(x => x.destNode).ToList().IndexOf(aRoom.ExitIndex(other.index));
                        if (existing.TryGetValue(other.name, out var conn))
                        {
                            visitedExisting.Add(other.name);
                            if (!conn.complete)
                            {
                                conn.posB = room.shortcuts[nodeIndex].startCoord.Tile;
                                conn.dirB = IntVec2Dir(room.ShorcutEntranceHoleDirection(conn.posB));
                                conn.complete = true;
                            }
                        }
                        else
                        {
                            // Create a new entry otherwise
                            conn = new ConnectionEntry()
                            {
                                roomA = aRoom.name,
                                roomB = other.name,
                                posA = room.shortcuts[nodeIndex].startCoord.Tile,
                            };
                            conn.dirA = IntVec2Dir(room.ShorcutEntranceHoleDirection(conn.posA));
                            regionInfo.connections.Add(conn);
                        }
                    }

                    // Remove newly non-existent connections
                    if (visitedExisting.Count != existing.Count)
                    {
                        foreach (var item in existing)
                        {
                            if (!visitedExisting.Contains(item.Key))
                            {
                                regionInfo.connections.Remove(item.Value);
                            }
                        }
                    }
                }
                else
                {
                    // Okay we did *not* load from the JSON, that's cool. We just have to recreate stuff.
                    for (int i = 0; i < aRoom.connections.Length; i++)
                    {
                        var other = aRoom.world.GetAbstractRoom(aRoom.connections[i]);
                        if (other == null) continue;

                        // Check if there are any entries already out there (as in we completed one half)
                        ConnectionEntry conn = null;
                        foreach (var c in regionInfo.connections)
                        {
                            if (c.roomB == aRoom.name && c.roomA == other.name)
                            {
                                conn = c;
                                break;
                            }
                        }

                        int nodeIndex = room.shortcuts.Select(x => x.destNode).ToList().IndexOf(aRoom.ExitIndex(other.index));
                        if (conn == null)
                        {
                            // A connection entry did not already exist; make one
                            conn = new ConnectionEntry()
                            {
                                roomA = aRoom.name,
                                roomB = other.name,
                                posA = room.shortcuts[nodeIndex].startCoord.Tile,
                            };
                            conn.dirA = IntVec2Dir(room.ShorcutEntranceHoleDirection(conn.posA));
                            regionInfo.connections.Add(conn);
                        }
                        else
                        {
                            // A connection entry did exist, finish it
                            conn.posB = room.shortcuts[nodeIndex].startCoord.Tile;
                            conn.dirB = IntVec2Dir(room.ShorcutEntranceHoleDirection(conn.posB));
                            conn.complete = true;
                        }
                    }
                }

                // Get placed objects
                placedObjects = [.. room.roomSettings.placedObjects.Where(Resources.AcceptablePlacedObject).Select(x => new PlacedObjectData(x))];

                // Terrain
                terrain = room.terrain != null ? [.. room.terrain.terrainList.Select(TerrainEntry.GetTerrainEntry).Where(x => x is not null)] : null;
            }

            public Dictionary<string, object> ToJson()
            {
                return new()
                {
                    { "name", roomName },
                    { "subregion", subregion },
                    { "pos", Utils.Vector2ToArray(devPos) },
                    { "hidden", hidden },
                    { "offscreenDen", offscreenDen },

                    { "cameras", cameras?.Select(Utils.Vector2ToArray).ToList() },
                    { "size", size != null ? IntVectorToArray(size) : null },
                    { "tiles", tiles },
                    { "nodes", nodes?.Select(IntVectorToArray).ToList() },

                    { "spawns", spawns },
                    { "tags", tags },
                    { "objects", placedObjects },
                    { "terrain", terrain },
                };
            }

            public static RoomEntry FromJson(RegionInfo owner, Dictionary<string, object> json)
            {
                var entry = new RoomEntry(owner)
                {
                    roomName = (string)json["name"],
                    subregion = (string)json["subregion"],
                    devPos = Vector2FromList((List<object>)json["pos"]),
                    hidden = json.TryGetValue("hidden", out var hidden) && (bool)hidden,
                    offscreenDen = json.TryGetValue("offscreenDen", out var offscreen) && (bool)offscreen,

                    spawns = [.. ((List<object>)json["spawns"]).Cast<List<object>>().Select(x =>
                    {
                        var list = new List<DenSpawnData>();
                        foreach (Dictionary<string, object> spawn in x.Cast<Dictionary<string, object>>())
                        {
                            list.Add(DenSpawnData.FromJson(spawn));
                        }
                        return list.ToArray();
                    })],
                    tags = ((List<object>)json["tags"]).Cast<string>().ToArray(),
                    placedObjects = [.. (json.TryGetValue("objects", out var o) && o is List<object> l ? l : [])
                        .Cast<Dictionary<string, object>>()
                        .Select(PlacedObjectData.FromJson)
                        .Where(x => x._valid)],
                };

                if (json["cameras"] != null)
                {
                    entry.cameras = ((List<object>)json["cameras"]).Cast<List<object>>().Select(Vector2FromList).ToArray();
                    entry.size = IntVectorFromList((List<object>)json["size"]);
                    entry.nodes = ((List<object>)json["nodes"]).Cast<List<object>>().Select(IntVectorFromList).ToArray();

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

                if (json.ContainsKey("terrain") && json["terrain"] != null)
                {
                    entry.terrain = [.. ((List<object>)json["terrain"]).Cast<Dictionary<string, object>>().Select(TerrainEntry.FromJson).Where(x => x is not null)];
                }

                return entry;
            }

            public struct DenSpawnData : IJsonObject
            {
                public string type;
                public int count;
                public float chance;
                public string data;
                public int den;

                public readonly Dictionary<string, object> ToJson()
                {
                    return new Dictionary<string, object>
                    {
                        { "type", type },
                        { "count", count },
                        { "chance", chance },
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
                        data = (string)json["data"],
                        den = (int)(long)json["den"],
                    };
                }
            }

            public struct PlacedObjectData : IJsonObject
            {
                public string type;
                public Vector2 pos;
                public List<string> data;
                internal bool _valid = true;

                public PlacedObjectData(PlacedObject obj)
                {
                    type = obj.type.ToString();
                    pos = obj.pos;
                    data = obj.data != null ? [.. obj.data.ToString().Split('~')] : [];
                }

                public readonly Dictionary<string, object> ToJson()
                {
                    return new()
                    {
                        { "type", type },
                        { "pos", Utils.Vector2ToArray(pos) },
                        { "data", data }
                    };
                }

                public static PlacedObjectData FromJson(Dictionary<string, object> json)
                {
                    try
                    {
                        return new PlacedObjectData()
                        {
                            _valid = true,
                            type = (string)json["type"],
                            pos = Vector2FromList((List<object>)json["pos"]),
                            data = [.. ((List<object>)json["data"]).Cast<string>()]
                        };
                    }
                    catch (KeyNotFoundException)
                    {
                        return new PlacedObjectData
                        {
                            _valid = false
                        };
                    }
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
            public bool complete = false;

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
                    { "posA", IntVectorToArray(posA) },
                    { "posB", IntVectorToArray(posB) },
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
                    posA = IntVectorFromList((List<object>)json["posA"]),
                    posB = IntVectorFromList((List<object>)json["posB"]),
                    dirA = (int)(long)json["dirA"],
                    dirB = (int)(long)json["dirB"],
                    complete = true
                };
                return entry;
            }
        }

        public abstract class TerrainEntry(TerrainEntry.TerrainType type) : IJsonObject
        {
            public readonly TerrainType type = type;

            public abstract Line? TrimLine(Line line);

            public abstract IEnumerable<Line> GetLines();

            public Dictionary<string, object> ToJson()
            {
                return new Dictionary<string, object>
                {
                    ["type"] = (int)type,
                    ["internal"] = ToInternalJson()
                };
            }

            protected abstract Dictionary<string, object> ToInternalJson();

            public static TerrainEntry FromJson(Dictionary<string, object> json)
            {
                var data = (Dictionary<string, object>)json["internal"];
                TerrainType type;
                if (json["type"] is long l)
                {
                    type = (TerrainType)(long)json["type"];
                }
                else if (json["type"] is string s)
                {
                    type = (TerrainType)Enum.Parse(typeof(TerrainType), s);
                }
                else
                {
                    throw new InvalidCastException("Could not convert to TerrainType from type '" + json["type"]?.GetType().FullName + "'!");
                }
                return type switch
                {
                    // TerrainType.LocalTerrainCurve => null,
                    TerrainType.TerrainCurve => TerrainCurveEntry.FromInternalJson(data),
                    TerrainType.SuperSlope => SuperSlopeEntry.FromInternalJson(data),
                    TerrainType.CurvedSlope => CurvedSlopeEntry.FromInternalJson(data),
                    _ => throw new NotImplementedException() // intentional
                };
            }

            public static TerrainEntry GetTerrainEntry(TerrainManager.ITerrain terrain)
            {
                return terrain switch
                {
                    // LocalTerrainCurve => null,
                    TerrainCurve => new TerrainCurveEntry(terrain as TerrainCurve),
                    SuperSlope => new SuperSlopeEntry(terrain as SuperSlope),
                    CurvedSlope => new CurvedSlopeEntry(terrain as CurvedSlope),
                    _ => null
                };
            }

            public enum TerrainType
            {
                UNKNOWN,
                TerrainCurve,
                LocalTerrainCurve,
                SuperSlope,
                CurvedSlope
            }
        }

        public class TerrainCurveEntry : TerrainEntry
        {
            private List<Line> curve;
            private float bottom;

            public TerrainCurveEntry(TerrainCurve curve) : base(TerrainType.TerrainCurve)
            {
                this.curve = [];
                bottom = curve.bottom;

                float minX = curve is LocalTerrainCurve ? 0f : curve.handles.Min(x => x.Middle.x);
                float maxX = curve is LocalTerrainCurve ? curve.room.PixelWidth : curve.handles.Max(x => x.Middle.x);
                var points = curve.collisionPoints.Where(p => p.x >= minX && p.x <= maxX).OrderBy(p => p.x).ToList();
                for (int i = 0; i < points.Count - 1; i++)
                {
                    this.curve.Add(new Line(points[i], points[i + 1]));
                }
            }

            protected TerrainCurveEntry(List<Line> curve, float bottom) : base(TerrainType.TerrainCurve)
            {
                this.curve = curve;
                this.bottom = bottom;
            }

            public override IEnumerable<Line> GetLines()
            {
                foreach (var line in curve)
                {
                    yield return line;
                }
                /*var tr = curve[curve.Count - 1].end;
                var br = new Vector2(tr.x, bottom);
                var tl = curve[0].start;
                var bl = new Vector2(tl.x, bottom);
                yield return new Line(tr, br);
                yield return new Line(br, bl);
                yield return new Line(bl, tl);*/
            }

            public override Line? TrimLine(Line line)
            {
                // Compute the other edges
                var tr = curve[^1].end;
                var br = new Vector2(tr.x, bottom);
                var tl = curve[0].start;
                var bl = new Vector2(tl.x, bottom);
                var rightLine = new Line(tr, br);
                var bottomLine = new Line(br, bl);
                var leftLine = new Line(bl, tl);

                // Check if it's completely encased within said edges (though don't check the top yet)
                var basicBounds = Rect.MinMaxRect(bl.x, bl.y, tr.x, Mathf.Max(tr.y, tl.y));
                if (basicBounds.Contains(line.start) && basicBounds.Contains(line.end))
                {
                    // Check against collisions with the top
                    var surfaceCollisions = SurfaceCollisionsWith(line, curve);
                    if (surfaceCollisions.Count > 0)
                    {
                        // actually for simplicity we won't trim them here
                        return line;
                    }

                    // No surface collisions, we must check whether or not it is above the surface
                    // Luckily, we don't have to check both ends because we know that there are no surface collisions
                    bool aboveSurface = PointAboveCurve(line.start, curve);
                    return aboveSurface ? line : null;
                }
                else
                {
                    // Check line collisions
                    var surfaceCollisions = SurfaceCollisionsWith(line, curve);
                    if (surfaceCollisions.Count > 0)
                    {
                        return line;
                        /*foreach (var surface in surfaceCollisions)
                        {
                            //
                        }*/
                    }
                    else if (line.CollisionWith(rightLine) is float rightCollision)
                    {
                        line = line.CutAt(rightCollision, line.end.x > br.x);
                    }
                    else if (line.CollisionWith(leftLine) is float leftCollision)
                    {
                        line = line.CutAt(leftCollision, line.end.x < bl.x);
                    }
                    else if (line.CollisionWith(bottomLine) is float bottomCollision)
                    {
                        line = line.CutAt(bottomCollision, line.end.y < bl.y);
                    }

                    return line;
                }

                static List<Line> SurfaceCollisionsWith(Line testLine, List<Line> curve)
                {
                    List<Line> list = [];
                    foreach (var line in curve)
                    {
                        if (line.CollisionWith(testLine) is not null)
                        {
                            list.Add(line);
                        }
                    }
                    return list;
                }

                static bool PointAboveCurve(Vector2 point, List<Line> curve)
                {
                    bool above = true;
                    foreach (var line in curve)
                    {
                        if ((line.start.x <= point.x && line.end.x >= point.x) || (line.end.x <= point.x && line.start.x >= point.x))
                        {
                            float slope = (line.end.y - line.start.y) / (line.end.x - line.start.x);
                            if (!float.IsNaN(slope) && !float.IsInfinity(slope))
                            {
                                // Good ol' point-slope form
                                above &= (slope * (point.x - line.start.x) + line.start.y) < point.y;
                            }
                        }
                    }
                    return above;
                }
            }

            protected override Dictionary<string, object> ToInternalJson()
            {
                return new Dictionary<string, object>
                {
                    ["curve"] = curve,
                    ["bottom"] = bottom < -100000000 ? -10000000.0 : bottom,
                };
            }

            public static TerrainCurveEntry FromInternalJson(Dictionary<string, object> json)
            {
                var lines = ((List<object>)json["curve"]).Select(Line.FromJson).ToList();
                var depth = (float)(double)json["bottom"];
                return new TerrainCurveEntry(lines, depth);
            }
        }

        public class SuperSlopeEntry : TerrainEntry
        {
            public Line line;
            public float depth;

            public SuperSlopeEntry(SuperSlope slope) : base(TerrainType.SuperSlope)
            {
                line = new Line(slope.pos, slope.pos2);
                depth = slope.thickness;
            }

            protected SuperSlopeEntry(Line line, float depth) : base(TerrainType.SuperSlope)
            {
                this.line = line;
                this.depth = depth;
            }

            public override IEnumerable<Line> GetLines()
            {
                var down = new Vector2(0, depth);
                yield return line;
                yield return line - down;
                yield return new Line(line.start, line.start - down);
                yield return new Line(line.end, line.end - down);
            }

            public override Line? TrimLine(Line line)
            {
                bool startInRange = PointInParallelogram(line.start);
                bool endInRange = PointInParallelogram(line.end);
                if (startInRange && endInRange)
                {
                    return null;
                }
                else if (startInRange || endInRange)
                {
                    Vector2 height = new Vector2(0, depth);
                    Line top = this.line;
                    Line right = new Line(this.line.end, this.line.end - height);
                    Line left = new Line(this.line.start, this.line.start - height);
                    Line bottom = this.line - height;

                    float? f = line.CollisionWith(top) ?? line.CollisionWith(right) ?? line.CollisionWith(left) ?? line.CollisionWith(bottom);
                    if (f.HasValue)
                    {
                        return line.CutAt(f.Value, startInRange);
                    }
                }
                return line;

                bool PointInParallelogram(Vector2 point)
                {
                    // Code adapted from https://stackoverflow.com/a/47493986
                    // Though also significantly modified to meet the usecase here
                    float d = (line.end.x - line.start.x) * -depth;
                    if (d != 0)
                    {
                        float xp = point.x - line.start.x;
                        float yp = point.y - line.start.y;
                        float bb = xp * -depth / d;
                        float cc = (yp * (line.end.x - line.start.x) - xp * (line.end.y - line.start.y)) / d;
                        return (bb >= 0 && cc >= 0 && bb <= 1 && cc <= 1);
                    }
                    return false;
                }
            }

            protected override Dictionary<string, object> ToInternalJson()
            {
                return new Dictionary<string, object>
                {
                    ["line"] = line,
                    ["depth"] = depth,
                };
            }

            public static SuperSlopeEntry FromInternalJson(Dictionary<string, object> json)
            {
                var line = Line.FromJson(json["line"]);
                var depth = (float)(double)json["depth"];
                return new SuperSlopeEntry(line, depth);
            }
        }

        public class CurvedSlopeEntry : TerrainEntry
        {
            public List<Line> curve;
            public float depth;

            public CurvedSlopeEntry(CurvedSlope slope) : base(TerrainType.CurvedSlope)
            {
                curve = [];
                depth = slope.thickness;

                var points = slope.collisionPoints.OrderBy(p => p.x).ToList();
                for (int i = 0; i < points.Count - 1; i++)
                {
                    curve.Add(new Line(points[i], points[i + 1]));
                }
            }

            protected CurvedSlopeEntry(List<Line> curve, float depth) : base(TerrainType.CurvedSlope)
            {
                this.curve = curve;
                this.depth = depth;
            }

            public override IEnumerable<Line> GetLines()
            {
                var height = new Vector2(0f, depth);
                foreach (var line in curve)
                {
                    yield return line;
                }
                foreach (var line in curve)
                {
                    yield return line - height;
                }
                yield return new Line(curve[0].start, curve[0].start - height);
                yield return new Line(curve[^1].end, curve[^1].end - height);
            }

            public override Line? TrimLine(Line line)
            {
                var height = new Vector2(0f, depth);
                if (line.start.x >= curve[0].start.x && line.end.x >= curve[0].start.x && line.start.x <= curve[^1].end.x && line.end.x <= curve[^1].end.x)
                {
                    var bottomCurve = curve.Select(x => x - height).ToList();
                    var topCollisionPoints = SurfaceCollisionsWith(line, curve);
                    var bottomCollisionPoints = SurfaceCollisionsWith(line, bottomCurve);
                    if (topCollisionPoints.Count > 0 || bottomCollisionPoints.Count > 0)
                    {
                        return line;
                    }

                    bool belowTop = !PointAboveCurve(line.start, curve) && !PointAboveCurve(line.end, curve);
                    bool aboveBottom = PointAboveCurve(line.start, bottomCurve) && PointAboveCurve(line.end, bottomCurve);
                    return belowTop && aboveBottom ? null : line;
                }
                else
                {
                    float? leftCollision = line.CollisionWith(new Line(curve[0].start, curve[0].start - height));
                    float? rightCollision = line.CollisionWith(new Line(curve[^1].start, curve[^1].start - height));
                    if (leftCollision.HasValue)
                    {
                        return line.CutAt(leftCollision.Value, line.start.x > curve[0].start.x);
                    }
                    else if (rightCollision.HasValue)
                    {
                        return line.CutAt(rightCollision.Value, line.end.x > curve[^1].end.x);
                    }

                    return line;
                }

                static List<Line> SurfaceCollisionsWith(Line testLine, List<Line> curve)
                {
                    List<Line> list = [];
                    foreach (var line in curve)
                    {
                        if (line.CollisionWith(testLine) is not null)
                        {
                            list.Add(line);
                        }
                    }
                    return list;
                }

                static bool PointAboveCurve(Vector2 point, List<Line> curve)
                {
                    bool above = true;
                    foreach (var line in curve)
                    {
                        if ((line.start.x <= point.x && line.end.x >= point.x) || (line.end.x <= point.x && line.start.x >= point.x))
                        {
                            float slope = (line.end.y - line.start.y) / (line.end.x - line.start.x);
                            if (!float.IsNaN(slope) && !float.IsInfinity(slope))
                            {
                                // Good ol' point-slope form
                                above &= (slope * (point.x - line.start.x) + line.start.y) < point.y;
                            }
                        }
                    }
                    return above;
                }
            }

            protected override Dictionary<string, object> ToInternalJson()
            {
                return new Dictionary<string, object>
                {
                    ["curve"] = curve,
                    ["depth"] = depth,
                };
            }

            public static CurvedSlopeEntry FromInternalJson(Dictionary<string, object> json)
            {
                var lines = ((List<object>)json["curve"]).Select(Line.FromJson).ToList();
                var depth = (float)(double)json["depth"];
                return new CurvedSlopeEntry(lines, depth);
            }
        }
    }
}

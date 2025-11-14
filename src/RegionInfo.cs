using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using DevInterface;
using MapExporterNew.Screenshotter;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RWCustom;
using UnityEngine;

namespace MapExporterNew
{
    internal sealed class RegionInfo
    {
        private const float SCALEDOWN = 2f;

        [JsonIgnore]
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

        [JsonIgnore]
        public bool fromJson = false;

        // For passing data down to children
        private readonly Dictionary<string, Vector2> devPos = [];

        private RegionInfo()
        {
            // Needs to exist for Newtonsoft
            fromJson = true;
        }

        public RegionInfo(World world)
        {
            // Region identity + echo room because why not grab that here
            acronym = world.name;
            name = Region.GetRegionFullName(acronym, null);
            echoRoom = world.worldGhost?.ghostRoom?.name;

            MergeNewData(world);
        }

        public void JsonInitialize()
        {
            fromJson = true;
            foreach (var room in rooms.Values)
            {
                room.regionInfo = this;
            }
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

        static int IntVec2Dir(IntVector2 vec) => vec.x == 0 && vec.y == 0 ? -1 : (vec.x != 0 ? (vec.x < 0 ? 0 : 2) : (vec.y < 0 ? 1 : 3));

        public static RegionInfo FromJson(string json)
        {
            var result = JsonConvert.DeserializeObject<RegionInfo>(json, new JsonSerializerSettings()
            {
                Converters = [new UnityStructConverter(), new TerrainEntry.TerrainEntryConverter()]
            });
            result.fromJson = true;
            result.JsonInitialize();
            return result;
        }

        public class RoomEntry
        {
            [JsonIgnore]
            internal RegionInfo regionInfo;

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

            private RoomEntry() { }  // Needs to exist for Newtonsoft

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
                        Plugin.Logger.LogError(aRoom.name + " had a bad node! (index " + i + ", type " + e.GetType().Name + ")");
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

            public struct DenSpawnData
            {
                public string type;
                public int count;
                public float chance;
                public string data;
                public int den;
            }

            public struct PlacedObjectData
            {
                public string type;
                public Vector2 pos;
                public List<string> data;

                public PlacedObjectData(PlacedObject obj)
                {
                    type = obj.type.ToString();
                    pos = obj.pos;
                    data = obj.data != null ? [.. obj.data.ToString().Split('~')] : [];
                }
            }
        }

        public class ConnectionEntry
        {
            public string roomA;
            public string roomB;
            public IntVector2 posA;
            public IntVector2 posB;
            public int dirA;
            public int dirB;

            [JsonIgnore]
            public bool complete = false;

            public ConnectionEntry()
            {
                complete = true;
            }

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
        }

        public class TerrainEntry
        {
            public readonly TerrainType type;
            public readonly List<Line> lines = [];

            protected TerrainEntry(TerrainType type)
            {
                this.type = type;
            }

            public IEnumerable<Line> GetLines()
            {
                return lines;
            }

            public static TerrainEntry GetTerrainEntry(TerrainManager.ITerrain terrain)
            {
                return terrain switch
                {
                    TerrainCurve => FromTerrainCurve(terrain as TerrainCurve),  // this also handles LocalTerrainCurve
                    SuperSlope => FromSuperSlope(terrain as SuperSlope),
                    CurvedSlope => FromCurvedSlope(terrain as CurvedSlope),
                    _ => null
                };
            }

            private static TerrainEntry FromTerrainCurve(TerrainCurve terrain)
            {
                List<Line> curve = [];
                float bottom = Mathf.Max(-10000000f, terrain.bottom);
                float minX = terrain is LocalTerrainCurve ? 0f : terrain.handles.Min(x => x.Middle.x);
                float maxX = terrain is LocalTerrainCurve ? terrain.room.PixelWidth : terrain.handles.Max(x => x.Middle.x);
                var points = terrain.collisionPoints.Where(p => p.x >= minX && p.x <= maxX).OrderBy(p => p.x).ToList();
                for (int i = 0; i < points.Count - 1; i++)
                {
                    curve.Add(new Line(points[i], points[i + 1]));
                }

                var entry = new TerrainEntry(TerrainType.TerrainCurve);
                entry.lines.AddRange(curve);
                return entry;
            }

            private static TerrainEntry FromSuperSlope(SuperSlope slope)
            {
                var line = new Line(slope.pos, slope.pos2);
                var depth = new Vector2(0, slope.thickness);

                var entry = new TerrainEntry(TerrainType.SuperSlope);
                entry.lines.Add(line);
                entry.lines.Add(line - depth);
                entry.lines.Add(new Line(line.start, line.start - depth));
                entry.lines.Add(new Line(line.end, line.end - depth));
                return entry;
            }

            private static TerrainEntry FromCurvedSlope(CurvedSlope slope)
            {
                var curve = new List<Line>();
                var height = new Vector2(0, slope.thickness);

                var points = slope.collisionPoints.OrderBy(p => p.x).ToList();
                for (int i = 0; i < points.Count - 1; i++)
                {
                    curve.Add(new Line(points[i], points[i + 1]));
                }

                var entry = new TerrainEntry(TerrainType.CurvedSlope);
                foreach (var line in curve)
                {
                    entry.lines.Add(line);
                }
                foreach (var line in curve)
                {
                    entry.lines.Add(line - height);
                }
                entry.lines.Add(new Line(curve[0].start, curve[0].start - height));
                entry.lines.Add(new Line(curve[^1].end, curve[^1].end - height));
                return entry;
            }

            public enum TerrainType
            {
                UNKNOWN,
                TerrainCurve,
                SuperSlope,
                CurvedSlope
            }

            internal class TerrainEntryConverter : JsonConverter<TerrainEntry>
            {
                public override TerrainEntry ReadJson(JsonReader reader, Type objectType, TerrainEntry existingValue, bool hasExistingValue, JsonSerializer serializer)
                {
                    TerrainType type;
                    List<Line> lines;
                    var o = JObject.Load(reader);
                    if (o.ContainsKey("internal"))
                    {
                        var legacyData = o["internal"] as JObject;
                        if (legacyData["type"].Type == JTokenType.Integer) type = (TerrainType)(int)legacyData["type"];
                        else if (legacyData["type"].Type == JTokenType.String) type = (TerrainType)Enum.Parse(typeof(TerrainType), (string)legacyData["type"]);
                        else throw new InvalidDataException("Unknown terrain type!");

                        lines = [];
                        switch (type)
                        {
                            case TerrainType.TerrainCurve:
                                lines.AddRange(((JArray)legacyData["curve"]).Cast<Line>());
                                break;
                            case TerrainType.SuperSlope:
                                lines.Add((Line)(JObject)legacyData["line"]);
                                lines.Add(legacyData.line - depth);
                                lines.Add(new Line(legacyData.line.start, legacyData.line.start - depth));
                                lines.Add(new Line(legacyData.line.end, legacyData.line.end - depth));
                                break;
                            case TerrainType.CurvedSlope:
                                lines.AddRange(legacyData.curve);
                                lines.AddRange(legacyData.curve.Select(x => x - depth));
                                lines.Add(new Line(legacyData.curve[0].start, legacyData.curve[0].start - depth));
                                lines.Add(new Line(legacyData.curve[^1].end, legacyData.curve[^1].end - depth));
                                break;
                        }
                    }
                    else
                    {
                        type = (TerrainType)Enum.Parse(typeof(TerrainType), (string)o["type"]);
                        lines = [.. ((JArray)o["lines"]).Cast<Line>()];
                    }

                    var result = new TerrainEntry(type);
                    result.lines.AddRange(lines);
                    return result;
                }

                public override void WriteJson(JsonWriter writer, TerrainEntry value, JsonSerializer serializer)
                {
                    var o = new JObject()
                    {
                        ["type"] = value.type.ToString(),
                        ["lines"] = new JArray(value.lines)
                    };
                    o.WriteTo(writer);
                }
            }
        }
    }
}

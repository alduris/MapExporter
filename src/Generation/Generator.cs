using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using RWCustom;
using UnityEngine;
using static MapExporter.Generation.GenUtil;
using Object = UnityEngine.Object;
using RoomEntry = MapExporter.RegionInfo.RoomEntry;
using DenSpawnData = MapExporter.RegionInfo.RoomEntry.DenSpawnData;
using IEnumerator = System.Collections.IEnumerator;

namespace MapExporter.Generation
{
    internal class Generator
    {
        private readonly string inputDir;
        private readonly string outputDir;
        private readonly RegionInfo regionInfo;
        private readonly bool skipExistingTiles;

        private IEnumerator[] tasks;

        public bool Done { get; private set; } = false;
        public bool Failed { get; private set; } = false;
        public float Progress { get; private set; } = 0f;

        private readonly int[,] progress = new int[8,2];
        private MetadataStep metadataStep = MetadataStep.Tiles;
        private readonly Dictionary<string, object> metadata = [];

        private enum MetadataStep
        {
            Tiles,
            Rooms,
            Connections,
            Geometry,
            Spawns,
            Misc,
            Done
        }
        private static readonly int MetadataStepCount = Enum.GetNames(typeof(MetadataStep)).Length;


        public Generator(SlugcatStats.Name scug, string region)
        {
            inputDir = Data.RenderOutputDir(scug.value, region);
            outputDir = Directory.CreateDirectory(Data.FinalOutputDir(scug.value, region)).FullName;

            if (!Directory.Exists(inputDir))
            {
                throw new ArgumentException("Input directory does not exist!");
            }

            regionInfo = RegionInfo.FromJson((Dictionary<string, object>)Json.Deserialize(File.ReadAllText(Path.Combine(inputDir, "metadata.json"))));
            foreach (var room in regionInfo.rooms.Values)
            {
                room.devPos *= 10; // convert to pixel coordinates
            }
            skipExistingTiles = false;
        }

        public void Update()
        {
            if (Done) return;
            switch (metadataStep)
            {
                case MetadataStep.Tiles:
                    {
                        if (tasks == null)
                        {
                            tasks = new IEnumerator[8];
                            for (int i = 0; i < tasks.Length; i++)
                            {
                                tasks[i] = ProcessZoomLevel(-i);
                            }
                        }

                        int tilesDone = 0;
                        int totalTiles = MetadataStepCount;
                        int tasksDone = 0;
                        for (int i = 0; i < tasks.Length; i++)
                        {
                            tilesDone += progress[i, 0];
                            totalTiles += progress[i, 1];
                        }
                        for (int i = 0; i < tasks.Length; i++)
                        {
                            if (tasks[i].MoveNext())
                            {
                                break;
                            }
                            else
                            {
                                tasksDone++;
                            }
                        }

                        Progress = (float)tilesDone / totalTiles;

                        if (tasksDone == tasks.Length)
                        {
                            metadataStep = MetadataStep.Rooms;
                            tasks = null;
                        }
                        break;
                    }
                case MetadataStep.Rooms:
                    {
                        // Find room outlines
                        List<RoomBoxInfo> boxes = [];
                        foreach (var room in regionInfo.rooms.Values)
                        {
                            Rect borderRect;
                            Vector2 namePos;
                            if (room.cameras == null || room.cameras.Length == 0)
                            {
                                borderRect = new Rect(room.devPos, offscreenSize);
                                namePos = room.devPos + offscreenSize + Vector2.left * (offscreenSize.x / 2f);
                            }
                            else
                            {
                                Vector2 blPos = room.cameras[0];
                                Vector2 trPos = room.cameras[0] + screenSize;
                                for (int i = 1; i < room.cameras.Length; i++)
                                {
                                    var cam = room.cameras[i];
                                    blPos = new Vector2(Mathf.Min(blPos.x, cam.x), Mathf.Min(blPos.y, cam.y));
                                    trPos = new Vector2(Mathf.Max(trPos.x, cam.x + screenSize.x), Mathf.Max(trPos.y, cam.y + screenSize.y));
                                }
                                borderRect = new Rect(blPos, trPos - blPos);
                                namePos = trPos + Vector2.left * ((trPos.x - blPos.x) / 2f);
                            }
                            boxes.Add(new RoomBoxInfo
                            {
                                name = room.roomName,
                                box = borderRect,
                                namePos = namePos
                            });
                        }

                        metadata["room_features"] = boxes;

                        metadataStep = MetadataStep.Connections;
                        break;
                    }
                case MetadataStep.Connections:
                    {
                        // Find room connections
                        List<ConnectionInfo> connections = [];
                        foreach (var conn in regionInfo.connections)
                        {
                            connections.Add(new ConnectionInfo
                            {
                                pointA = regionInfo.rooms[conn.roomA].devPos + conn.posA.ToVector2() * 20f + Vector2.one * 10f,
                                pointB = regionInfo.rooms[conn.roomB].devPos + conn.posB.ToVector2() * 20f + Vector2.one * 10f,
                                dirA = conn.dirA,
                                dirB = conn.dirB
                            });
                        }

                        metadata["connection_features"] = connections;

                        metadataStep = MetadataStep.Geometry;
                        break;
                    }
                case MetadataStep.Geometry:
                    {
                        List<GeometryInfo> geo = [];
                        foreach (var room in regionInfo.rooms.Values)
                        {
                            if (room.size == default || room.size.x == 0 || room.size.y == 0) continue;

                            // I think the code for processing geo changed the most from how the Python file did it, I pretty much rewrote it from scratch lol
                            geo.Add(ProcessRoomGeometry(room)); // note: this is a separate method in case I want to multithread this
                        }

                        metadata["geometry_features"] = geo;

                        metadataStep = MetadataStep.Spawns;
                        break;
                    }
                case MetadataStep.Spawns:
                    {
                        List<SpawnInfo> spawns = [];

                        foreach (var room in regionInfo.rooms.Values)
                        {
                            foreach (var data in room.spawns)
                            {
                                spawns.Add(new SpawnInfo
                                {
                                    roomName = room.roomName,
                                    spawnData = data,
                                    coords = room.devPos + room.nodes[data[0].den].ToVector2() * 20f + new Vector2(10f, 10f)
                                });
                            }
                        }

                        metadata["spawn_features"] = spawns;

                        metadataStep = MetadataStep.Misc;
                        break;
                    }
                case MetadataStep.Misc:
                    {
                        // Find colors
                        Color fgcolor = Mode(regionInfo.fgcolors);
                        Color bgcolor = Mode(regionInfo.bgcolors);
                        Color sccolor = Mode(regionInfo.sccolors);

                        metadata["bgcolor"] = Color2Arr(fgcolor);
                        metadata["highlightcolor"] = Color2Arr(bgcolor);
                        metadata["shortcutcolor"] = Color2Arr(sccolor);

                        // Calculate a geo color
                        Vector3 bvec = HSL2HSV(Custom.RGB2HSL(bgcolor));
                        Vector3 fvec = HSL2HSV(Custom.RGB2HSL(fgcolor));
                        var (bh, bs, bv) = (bvec.x, bvec.y, bvec.z);
                        var (fh, fs, fv) = (fvec.x, fvec.y, fvec.z);
                        float sh, ss, sv;
                        
                        if (Mathf.Abs(bh - fh) < 0.5f)
                        {
                            if (bh < fh)
                                bh += 1;
                            else
                                fh += 1;
                        }
                        sh = (bs == 0 && fs == 0) ? 0.5f : ((bh * fs + fh * bs) / (bs + fs));
                        sh = sh < 0 ? (1 + (sh % 1f)) : (sh % 1f);

                        ss = Mathf.Sqrt((bs*bs + fs*fs) / 2.0f); // this does some circle math stuff
                        sv = Mathf.Sqrt((bv*bv + fv*fv) / 2.0f); // ditto

                        metadata["geocolor"] = Color2Arr(HSV2HSL(sh, ss, sv).rgb);

                        metadataStep = MetadataStep.Done;
                        break;
                    }
                case MetadataStep.Done:
                    {
                        File.WriteAllText(Path.Combine(outputDir, "region.json"), Json.Serialize(metadata));
                        Done = true;
                        break;
                    }
                default: break;
            }

            if (tasks != null && metadataStep != MetadataStep.Tiles)
            {
                int count = 0;
                for (int i = 0; i < tasks.Length; i++)
                {
                    count += progress[i, 1];
                }
                Progress = (float)(count + (int)metadataStep) / (count + MetadataStepCount);
            }
        }

        private string OutputPathForStep(int step) => Path.Combine(outputDir, step.ToString());
        private static readonly IntVector2 tileSizeInt = new(256, 256);
        private static readonly Vector2    tileSize = tileSizeInt.ToVector2();
        private static readonly IntVector2 offscreenSizeInt = new(1200, 400);
        private static readonly Vector2    offscreenSize = offscreenSizeInt.ToVector2();
        private static readonly IntVector2 screenSizeInt = new(1400, 800);
        private static readonly Vector2    screenSize = screenSizeInt.ToVector2();

        private System.Collections.IEnumerator ProcessZoomLevel(int zoom)
        {
            string outputPath = Directory.CreateDirectory(OutputPathForStep(zoom)).FullName;
            float multFac = Mathf.Pow(2, zoom);

            // Find room boundaries
            Vector2 mapMin = Vector2.zero;
            Vector2 mapMax = Vector2.zero;
            foreach (var room in regionInfo.rooms.Values)
            {
                if ((room.cameras?.Length ?? 0) == 0)
                {
                    mapMin = new(Mathf.Min(room.devPos.x, mapMin.x), Mathf.Min(room.devPos.y, mapMin.y));
                    mapMax = new(Mathf.Max(room.devPos.x + offscreenSize.x, mapMax.x), Mathf.Max(room.devPos.y + offscreenSize.y, mapMax.y));
                }
            }

            // Find tile boundaries (lower left inclusive, upper right non-inclusive)
            IntVector2 llbTile = Vec2IntVecFloor(multFac * mapMin / tileSize);
            IntVector2 urbTile = Vec2IntVecCeil(multFac * mapMax / tileSize);

            // Make images
            progress[-zoom, 0] = 0;
            progress[-zoom, 1] = (urbTile.x - llbTile.x) * (urbTile.y - llbTile.y);
            Texture2D camTexture = new(1, 1, TextureFormat.RGBA32, false, false);
            for (int tileY = llbTile.y; tileY <= urbTile.y; tileY++)
            {
                for (int tileX = llbTile.x; tileX <= urbTile.x; tileX++)
                {
                    Texture2D tile = null;

                    // Get file path and see if we can skip it
                    string filePath = Path.Combine(outputPath, $"{tileX}_{-1-tileY}.png");
                    if (skipExistingTiles && File.Exists(filePath))
                    {
                        continue;
                    }

                    // Build tile
                    var camPoint = new Vector2(tileX, tileY) * multFac;
                    var camRect = new Rect(camPoint, tileSize);
                    var tileCoords = new Vector2(tileX, tileY) * tileSize;

                    foreach (var room in regionInfo.rooms.Values)
                    {
                        // Skip rooms with no cameras
                        if (room.cameras == null || room.cameras.Length == 0) continue;

                        for (int camNo = 0; camNo < room.cameras.Length; camNo++)
                        {
                            var cam = room.cameras[camNo];
                            // Determine if the camera can be seen
                            if (camRect.CheckIntersect(new Rect(cam * multFac, screenSize * multFac)))
                            {
                                string fileName = $"{room.roomName}_{camNo}.png";

                                // Create the tile if necessary
                                if (tile == null)
                                {
                                    tile = new Texture2D(tileSizeInt.x, tileSizeInt.y, TextureFormat.RGBA32, false, false);

                                    // Fill with transparent color
                                    var pixels = tile.GetPixels();
                                    for (int i = 0; i < pixels.Length; i++)
                                    {
                                        pixels[i] = new Color(0f, 0f, 0f, 0f); // original implementation used fgcolor
                                    }
                                    tile.SetPixels(pixels);
                                    tile.Apply();
                                }

                                // Open the camera so we can use it
                                camTexture.LoadImage(File.ReadAllBytes(Path.Combine(inputDir, fileName)), false);

                                if (zoom != 0) // No need to scale to the same resolution
                                    ScaleTexture(camTexture, (int)(screenSizeInt.x * multFac), (int)(screenSizeInt.y * multFac));

                                // camTexture.Apply();

                                // Copy pixels
                                Vector2 copyOffsetVec = cam + Vector2.up * screenSize.y * multFac - tileCoords - Vector2.up * tileSize.y;
                                copyOffsetVec.x *= -1; // this makes it the flipped version of pasteoffset from the original script, which we need for the copy offset
                                IntVector2 copyOffset = Vec2IntVecFloor(copyOffsetVec);

                                // int x = Math.Max(0, Math.Min(camTexture.width, copyOffset.x));
                                // int y = Math.Max(0, Math.Min(camTexture.height, copyOffset.y));
                                // int w = Math.Min(tileSizeInt.x - Math.Min(0, copyOffset.x), camTexture.width - copyOffset.x);
                                // int h = Math.Min(tileSizeInt.y - Math.Min(0, copyOffset.y), camTexture.height - copyOffset.y);
                                CopyTextureSegment(camTexture, tile, copyOffset.x, copyOffset.y, tileSizeInt.x, tileSizeInt.y, -copyOffset.x, -copyOffset.y);
                                // tile.SetPixels(copyOffset.x < 0 ? -copyOffset.x : 0, copyOffset.y < 0 ? -copyOffset.y : 0, w, h, pixelData);
                                yield return null;
                            }
                        }
                    }

                    // Write tile if we drew anything
                    if (tile != null)
                    {
                        tile.Apply();
                        File.WriteAllBytes(Path.Combine(outputPath, $"{tileX}_{-1 - tileY}.png"), tile.EncodeToPNG());
                        Object.Destroy(tile);
                    }

                    // Update progress
                    progress[-zoom, 0]++;
                    yield return null;
                }
            }
        }

        private GeometryInfo ProcessRoomGeometry(RoomEntry room)
        {
            LinkedList<(Vector2 A, Vector2 B)> lines = [];

            // Create the lines
            for (int j = 0; j < room.size.y; j++)
            {
                float y = room.devPos.y + j * 20f;
                for (int i = 0; i < room.size.x; i++)
                {
                    float x = room.devPos.x + i * 20f;
                    int[] tile = room.tiles[i, j];
                    int type = tile[0];
                    bool hpole = ((tile[1] & 1) == 1), vpole = ((tile[1] & 2) == 1);

                    // Tile type
                    switch (type)
                    {
                        case 0 or 1: // air and solid, respectively
                            if (i != room.size.x - 1)
                            {
                                int neighbor = room.tiles[i + 1, j][0];
                                if (type != neighbor && (neighbor & -2) == 0) // number & -2 will return 0 for either 0 or 1
                                {
                                    lines.AddLast((new(x, y), new(x, y + 20f)));
                                }
                            }
                            if (j != room.size.y - 1)
                            {
                                int neighbor = room.tiles[i, j + 1][0];
                                if (type != neighbor && (neighbor & -2) == 0)
                                {
                                    lines.AddLast((new(x, y), new(x + 20f, y)));
                                }
                            }
                            break;
                        case 2: // slopes
                            // Need to check all four orientations, but there are only two cases of lines to draw.
                            // In this, we are considering any check outside of the bounds of the room geometry to be solid.
                            bool up = j == room.size.y - 1 || room.tiles[i, j + 1][0] == 1;
                            bool down = j == 0 || room.tiles[i, j - 1][0] == 1;
                            bool right = i == room.size.y - 1 || room.tiles[i + 1, j][0] == 1;
                            bool left = i == 0 || room.tiles[i - 1, j][0] == 1;

                            if (up == left && down == right)
                            {
                                lines.AddLast((new(x, y), new(x + 20f, y + 20f)));
                            }
                            else if (up == right && down == left)
                            {
                                lines.AddLast((new(x, y + 20f), new(x, y + 20f)));
                            }
                            break;
                        case 3: // half floors
                            // Top and bottom lines always get drawn
                            lines.AddLast((new(x, y + 20f), new(x + 20f, y + 20f)));
                            lines.AddLast((new(x, y + 10f), new(x + 20f, y + 10f)));

                            // The edges get a little funky.
                            // We don't draw if there is another half floor there but otherwise a line needs to be drawn *somewhere*.
                            int l = i == 0 ? 3 : room.tiles[i - 1, j][0];
                            int r = i == room.size.x - 1 ? 3 : room.tiles[i + 1, j][0];
                            if (l != 3)
                            {
                                float o = l == 1 ? 0f : 10f;
                                lines.AddLast((new(x, y + o), new(x, y + o + 10f)));
                            }
                            if (r != 3)
                            {
                                float o = l == 1 ? 0f : 10f;
                                lines.AddLast((new(x + 20f, y + o), new(x + 20f, y + o + 10f)));
                            }
                            break;
                        default: // anything else
                            break;
                    }

                    // Poles
                    if (type != 1) // don't draw poles if solid
                    {
                        if (hpole)
                            lines.AddLast((new(x, y + 10f), new(x + 20f, y + 10f)));
                        if (vpole)
                            lines.AddLast((new(x + 10f, y), new(x + 10f, y + 20f)));
                    }
                }
            }

            // Optimize the lines (combining)
            List<LinkedList<Vector2>> optimized = [];
            HashSet<(Vector2, Vector2)> seen = [];

            var node = lines.First; // the type for this is very long lol
            while (node != null)
            {
                // Don't add duplicate elements
                if (!seen.Add(node.Value))
                {
                    continue;
                }

                // Try to create a continuous line segment as long as possible
                LinkedList<Vector2> line = new([node.Value.A, node.Value.B]);

                var curr = node.Next;
                while (curr != null)
                {
                    var next = curr.Next;
                    bool remove = true;

                    if (line.First.Value == curr.Value.A)
                    {
                        line.AddFirst(curr.Value.B);
                    }
                    else if(line.First.Value == curr.Value.B) {
                        line.AddFirst(curr.Value.A);
                    }
                    else if (line.Last.Value == curr.Value.A)
                    {
                        line.AddLast(curr.Value.B);
                    }
                    else if(line.Last.Value == curr.Value.B) {
                        line.AddLast(curr.Value.A);
                    }
                    else
                    {
                        remove = false;
                    }

                    if (remove)
                    {
                        lines.Remove(curr);
                    }

                    curr = next;
                }

                node = node.Next;
            }

            // Return
            return new GeometryInfo
            {
                room = room.roomName,
                lines = optimized.Select(x => x.Select(Vec2arr).ToArray()).ToArray(),
            };
        }

        ///////////////////////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////////////////////////////

        private struct RoomBoxInfo : IJsonObject
        {
            public string name;
            public Rect box;
            public Vector2 namePos;

            public readonly Dictionary<string, object> ToJson()
            {
                return new Dictionary<string, object>()
                {
                    { "type", "Feature" },
                    {
                        "geometry",
                        new Dictionary<string, object>
                        {
                            { "type", "Polygon" },
                            { "coordinates", new float[][][] { Rect2Arr(box) } }
                        }
                    },
                    {
                        "properties",
                        new Dictionary<string, object>
                        {
                            { "name", name },
                            { "popupcoords", Vec2arr(namePos) },
                        }
                    }
                };
            }
        }

        private struct ConnectionInfo : IJsonObject
        {
            public Vector2 pointA;
            public int dirA;
            public Vector2 pointB;
            public int dirB;

            private static readonly Vector2[] fourDirections = [Vector2.left, Vector2.down, Vector2.right, Vector2.up];
            public readonly Dictionary<string, object> ToJson()
            {
                float dist = (pointB - pointA).magnitude;
                Vector2 handleA = pointA + fourDirections[dirA] * dist;
                Vector2 handleB = pointB + fourDirections[dirB] * dist;
                return new Dictionary<string, object>()
                {
                    { "type", "Feature" },
                    {
                        "geometry",
                        new Dictionary<string, object>
                        {
                            { "type", "LineString" },
                            { "coordinates", new float[][] { Vec2arr(pointA), Vec2arr(handleA), Vec2arr(handleB), Vec2arr(pointB) } }
                        }
                    },
                    { "properties", new Dictionary<string, object> {} }
                };
            }
        }

        private struct GeometryInfo : IJsonObject
        {
            public string room;
            public float[][][] lines;

            public readonly Dictionary<string, object> ToJson()
            {
                return new Dictionary<string, object>()
                {
                    { "type", "Feature" },
                    {
                        "geometry",
                        new Dictionary<string, object>
                        {
                            { "type", "MultiLineString" },
                            { "coordinates", lines }
                        }
                    },
                    { "properties", new Dictionary<string, object> { { "room", room } } }
                };
            }
        }

        private struct SpawnInfo : IJsonObject
        {
            public Vector2 coords;
            public string roomName;
            public DenSpawnData[] spawnData;

            public readonly Dictionary<string, object> ToJson()
            {
                // Put together part of the dictionary
                bool isLineage = spawnData[0].chance >= 0f;
                var spawnDict = new Dictionary<string, object>()
                {
                    { "is_lineage", isLineage },
                    { "amount", spawnData[0].count },
                    { "creature", spawnData[0].type },
                    { "spawn_data", spawnData[0].data },
                    { "pre_cycle", false }, // TODO: remove these
                    { "night", spawnData[0].night }
                };

                // Lineage has extra data
                if (isLineage)
                {
                    spawnDict["lineage"] = spawnData.Select(x => x.type).ToArray();
                    spawnDict["lineage_probs"] = spawnData.Select(x => x.chance.ToString("0.0000")).ToArray();
                    spawnDict["lineage_data"] = spawnData.Select(x => x.data).ToArray();
                }

                return new Dictionary<string, object>()
                {
                    { "type", "Feature" },
                    {
                        "geometry",
                        new Dictionary<string, object>
                        {
                            { "type", "Point" },
                            { "coordinates", Vec2arr(coords) }
                        }
                    },
                    {
                        "properties",
                        new Dictionary<string, object>
                        {
                            { "room", roomName },
                            { "den", spawnData[0].den },
                            { "spawns",  spawnDict }
                        }
                    }
                };
            }
        }
    }
}

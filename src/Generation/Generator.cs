using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using RWCustom;
using UnityEngine;
using static MapExporter.Generation.GenUtil;
using Object = UnityEngine.Object;

namespace MapExporter.Generation
{
    internal class Generator
    {
        private readonly string inputDir;
        private readonly string outputDir;
        private readonly RegionInfo regionInfo;
        private readonly bool skipExistingTiles;

        private Task[] threads;

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
                        if (threads == null)
                        {
                            threads = new Task[8];
                            for (int i = 0; i < threads.Length; i++)
                            {
                                threads[i] = Task.Run(() => ProcessZoomLevel(-i));
                            }
                        }

                        int tilesDone = 0;
                        int totalTiles = MetadataStepCount;
                        int tasksDone = 0;
                        for (int i = 0; i < threads.Length; i++)
                        {
                            tilesDone += progress[i, 0];
                            totalTiles += progress[i, 1];

                            var thread = threads[i];
                            if (thread.IsCompleted)
                            {
                                tasksDone++;
                            }
                            else if (thread.IsFaulted)
                            {
                                Done = true;
                                Failed = true;
                                break;
                            }
                        }

                        Progress = (float)tilesDone / totalTiles;

                        if (tasksDone == threads.Length)
                        {
                            metadataStep = MetadataStep.Rooms;
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
                            geo.Add(ProcessRoomGeometry(room));
                        }

                        metadata["geometry_features"] = geo;

                        metadataStep = MetadataStep.Spawns;
                        break;
                    }
                case MetadataStep.Spawns:
                    {
                        //

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

            if (threads != null && metadataStep != MetadataStep.Tiles)
            {
                int count = 0;
                for (int i = 0; i < threads.Length; i++)
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

        private void ProcessZoomLevel(int zoom)
        {
            try
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
                for (int tileY = llbTile.y; tileY <= urbTile.y; tileY++)
                {
                    // Attempt to save file reads. Camera textures are saved until an iteration is found where they aren't used.
                    Dictionary<string, Texture2D> cameraCache = [];

                    for (int tileX = llbTile.x; tileX <= urbTile.x; tileX++)
                    {
                        HashSet<string> encountered = []; // This is how we keep track of camera textures to clear. If they aren't in here, they were unused.
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
                                    Texture2D camTexture;
                                    if (cameraCache.ContainsKey(fileName))
                                    {
                                        camTexture = cameraCache[fileName];
                                    }
                                    else
                                    {
                                        camTexture = new(screenSizeInt.x, screenSizeInt.y, TextureFormat.RGBA32, false, false);
                                        camTexture.LoadImage(File.ReadAllBytes(Path.Combine(inputDir, fileName)));

                                        if (zoom != 0) // No need to scale to the same resolution
                                            ScaleTexture(camTexture, (int)(screenSizeInt.x * multFac), (int)(screenSizeInt.y * multFac));

                                        camTexture.Apply();
                                        cameraCache.Add(fileName, camTexture);
                                    }
                                    encountered.Add(fileName);

                                    // Copy pixels
                                    Vector2 copyOffsetVec = cam + Vector2.up * screenSize.y * multFac - tileCoords - Vector2.up * tileSize.y;
                                    copyOffsetVec.x *= -1; // this makes it the flipped version of pasteoffset from the original script, which we need for the copy offset
                                    IntVector2 copyOffset = Vec2IntVecFloor(copyOffsetVec);

                                    int x = Math.Max(0, Math.Min(screenSizeInt.x, copyOffset.x));
                                    int y = Math.Max(0, Math.Min(screenSizeInt.y, copyOffset.y));
                                    int w = Math.Min(tileSizeInt.x - Math.Min(0, copyOffset.x), screenSizeInt.x - copyOffset.x);
                                    int h = Math.Min(tileSizeInt.y - Math.Min(0, copyOffset.y), screenSizeInt.y - copyOffset.y);
                                    var pixelData = camTexture.GetPixels(x, y, w, h);
                                    tile.SetPixels(copyOffset.x < 0 ? -copyOffset.x : 0, copyOffset.y < 0 ? -copyOffset.y : 0, w, h, pixelData);
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

                        // Explodificate unused camera textures to save memory
                        var keys = cameraCache.Keys;
                        foreach (var key in keys)
                        {
                            if (!encountered.Contains(key))
                            {
                                Object.Destroy(cameraCache[key]);
                                cameraCache.Remove(key);
                            }
                        }

                        // Update progress
                        progress[-zoom, 0]++;
                    }

                    // The cameras likely won't be used when we wrap around to the other side again so just destroy them all.
                    // Also this is the end of scope for cameraCache anyway lol
                    foreach (var cam in cameraCache)
                    {
                        Object.Destroy(cam.Value);
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError("Zoom level " + zoom + " failed with the following error: " + e);
                throw;
            }
        }

        private GeometryInfo ProcessRoomGeometry(RegionInfo.RoomEntry room)
        {
            List<(Vector2 A, Vector2 B)> lines = [];

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
                                    lines.Add((new(x, y), new(x, y + 20f)));
                                }
                            }
                            if (j != room.size.y - 1)
                            {
                                int neighbor = room.tiles[i, j + 1][0];
                                if (type != neighbor && (neighbor & -2) == 0)
                                {
                                    lines.Add((new(x, y), new(x + 20f, y)));
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
                                lines.Add((new(x, y), new(x + 20f, y + 20f)));
                            }
                            else if (up == right && down == left)
                            {
                                lines.Add((new(x, y + 20f), new(x, y + 20f)));
                            }
                            break;
                        case 3: // half floors
                            // Top and bottom lines always get drawn
                            lines.Add((new(x, y + 20f), new(x + 20f, y + 20f)));
                            lines.Add((new(x, y + 10f), new(x + 20f, y + 10f)));

                            // The edges get a little funky.
                            // We don't draw if there is another half floor there but otherwise a line needs to be drawn *somewhere*.
                            int l = i == 0 ? 3 : room.tiles[i - 1, j][0];
                            int r = i == room.size.x - 1 ? 3 : room.tiles[i + 1, j][0];
                            if (l != 3)
                            {
                                float o = l == 1 ? 0f : 10f;
                                lines.Add((new(x, y + o), new(x, y + o + 10f)));
                            }
                            if (r != 3)
                            {
                                float o = l == 1 ? 0f : 10f;
                                lines.Add((new(x + 20f, y + o), new(x + 20f, y + o + 10f)));
                            }
                            break;
                        default: // anything else
                            break;
                    }

                    // Poles
                    if (type != 1) // don't draw poles if solid
                    {
                        if (hpole)
                            lines.Add((new(x, y + 10f), new(x + 20f, y + 10f)));
                        if (vpole)
                            lines.Add((new(x + 10f, y), new(x + 10f, y + 20f)));
                    }
                }
            }

            // Optimize the lines (combining)
            List<List<Vector2>> optimized = [];

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

            public Dictionary<string, object> ToJson()
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
    }
}

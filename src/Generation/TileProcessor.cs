using System.IO;
using RWCustom;
using UnityEngine;
using static MapExporter.Generation.GenUtil;
using Object = UnityEngine.Object;
using IEnumerator = System.Collections.IEnumerator;

namespace MapExporter.Generation
{
    internal class TileProcessor(Generator owner, int zoom) : GenProcessor(owner)
    {
        private readonly int zoom = zoom;
        private readonly string outputDir = owner.outputDir;

        private string OutputPathForStep(int step) => Path.Combine(outputDir, step.ToString());
        private static readonly IntVector2 tileSizeInt = new(256, 256);
        private static readonly Vector2 tileSize = tileSizeInt.ToVector2();
        private static readonly IntVector2 offscreenSizeInt = new(1200, 400);
        private static readonly Vector2 offscreenSize = offscreenSizeInt.ToVector2();
        private static readonly IntVector2 screenSizeInt = new(1400, 800);
        private static readonly Vector2 screenSize = screenSizeInt.ToVector2();

        public override string ProcessName => "Zoom level " + zoom;

        protected override IEnumerator Process()
        {
            string outputPath = Directory.CreateDirectory(OutputPathForStep(zoom)).FullName;
            float multFac = Mathf.Pow(2, zoom);
            var regionInfo = owner.regionInfo;

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
                else
                {
                    foreach (var cam in room.cameras)
                    {
                        mapMin = new(Mathf.Min(room.devPos.x + cam.x, mapMin.x), Mathf.Min(room.devPos.y + cam.y, mapMin.y));
                        mapMax = new(Mathf.Max(room.devPos.x + cam.x + screenSize.x, mapMax.x), Mathf.Max(room.devPos.y + cam.y + screenSize.y, mapMax.y));
                    }
                }
            }

            // Find tile boundaries (lower left inclusive, upper right non-inclusive)
            IntVector2 llbTile = Vec2IntVecFloor(multFac * mapMin / tileSize);
            IntVector2 urbTile = Vec2IntVecCeil(multFac * mapMax / tileSize);

            // Make images
            int totalTiles = (urbTile.x - llbTile.x) * (urbTile.y - llbTile.y);
            int processed = 0;

            Texture2D camTexture = new(1, 1, TextureFormat.ARGB32, false, false);
            for (int tileY = llbTile.y; tileY <= urbTile.y; tileY++)
            {
                for (int tileX = llbTile.x; tileX <= urbTile.x; tileX++)
                {
                    Texture2D tile = null;

                    // Get file path and see if we can skip it
                    string filePath = Path.Combine(outputPath, $"{tileX}_{-1 - tileY}.png");
                    if (owner.skipExistingTiles && File.Exists(filePath))
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
                            if (camRect.CheckIntersect(new Rect((room.devPos + cam) * multFac - tileCoords, tileSize)))
                            {
                                string fileName = $"{room.roomName}_{camNo}.png";

                                // Create the tile if necessary
                                if (tile == null)
                                {
                                    tile = new Texture2D(tileSizeInt.x, tileSizeInt.y, TextureFormat.ARGB32, false, false);

                                    // Fill with transparent color
                                    var pixels = tile.GetPixels();
                                    for (int i = 0; i < pixels.Length; i++)
                                    {
                                        pixels[i] = new Color(0f, 0f, 0f, 0f); // original implementation used fgcolor
                                    }
                                    tile.SetPixels(pixels);
                                }

                                // Open the camera so we can use it
                                camTexture.LoadImage(File.ReadAllBytes(Path.Combine(owner.inputDir, fileName)), false);

                                if (zoom != 0) // No need to scale to the same resolution
                                    ScaleTexture(camTexture, (int)(screenSizeInt.x * multFac), (int)(screenSizeInt.y * multFac));

                                // Copy pixels
                                Vector2 copyOffsetVec = (room.devPos + cam) * multFac + Vector2.up * screenSize.y * multFac - tileCoords - Vector2.up * tileSize.y;
                                copyOffsetVec.x *= -1; // this makes it the flipped version of pasteoffset from the original script, which we need for the copy offset
                                IntVector2 copyOffset = Vec2IntVecFloor(copyOffsetVec);

                                CopyTextureSegment(camTexture, tile, copyOffset.x, copyOffset.y, tileSizeInt.x, tileSizeInt.y, copyOffset.x < 0 ? -copyOffset.x : 0, copyOffset.y < 0 ? -copyOffset.y : 0);
                                yield return null;
                            }
                        }
                    }

                    // Update progress
                    processed++;
                    Progress = (float)processed / totalTiles;

                    // Write tile if we drew anything
                    if (tile != null)
                    {
                        tile.Apply();
                        File.WriteAllBytes(Path.Combine(outputPath, $"{tileX}_{-1 - tileY}.png"), tile.EncodeToPNG());
                        Object.Destroy(tile);
                        yield return null;
                    }
                }
            }

            Finished = true;
        }
    }
}

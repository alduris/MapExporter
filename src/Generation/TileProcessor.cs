using System.Collections.Generic;
using System.IO;
using RWCustom;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using static MapExporter.Generation.GenUtil;
using Object = UnityEngine.Object;

namespace MapExporter.Generation
{
    internal class TileProcessor(Generator owner, int zoom) : Processor(owner)
    {
        private readonly int zoom = zoom;
        private readonly string outputDir = owner.outputDir;

        private string OutputPathForStep(int step) => Path.Combine(outputDir, step.ToString());

        public override string ProcessName => "Zoom level " + zoom;

        protected override IEnumerator<float> Process()
        {
            string outputPath = Directory.CreateDirectory(OutputPathForStep(zoom)).FullName;
            float multFac = Mathf.Pow(2, zoom);
            var regionInfo = owner.regionInfo;
            TextureCache<string> imageCache = new(256);

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
            int totalTiles = (urbTile.x - llbTile.x + 1) * (urbTile.y - llbTile.y + 1);
            int processed = 0;

            for (int tileY = llbTile.y; tileY <= urbTile.y; tileY++)
            {
                for (int tileX = llbTile.x; tileX <= urbTile.x; tileX++)
                {
                    Texture2D tile = null;

                    // Build tile
                    var tileCoords = new Vector2(tileX, tileY) * tileSize;
                    var tileRect = new Rect(tileCoords, tileSize);

                    foreach (var room in regionInfo.rooms.Values)
                    {
                        // Skip rooms with no cameras
                        if (room.cameras == null || room.cameras.Length == 0 || room.hidden) continue;

                        for (int camNo = 0; camNo < room.cameras.Length; camNo++)
                        {
                            var cam = room.cameras[camNo];
                            // Determine if the camera can be seen
                            if (tileRect.CheckIntersect(new Rect((room.devPos + cam) * multFac, screenSize * multFac)))
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
                                if (!imageCache.Contains(fileName))
                                {
                                    var camTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false, false);
                                    camTexture.LoadImage(File.ReadAllBytes(Path.Combine(owner.inputDir, fileName)), false);

                                    if (zoom != 0 || camTexture.width != screenSize.x || camTexture.height != screenSize.y) // Don't need to rescale to same resolution
                                        ScaleTexture(camTexture, (int)(screenSize.x * multFac), (int)(screenSize.y * multFac));

                                    imageCache[fileName] = camTexture;
                                }

                                // Copy pixels
                                Vector2 copyOffsetVec = tileCoords - (room.devPos + cam + camOffset) * multFac;
                                IntVector2 copyOffset = Vec2IntVecFloor(copyOffsetVec);

                                CopyTextureSegment(imageCache[fileName], tile, copyOffset.x, copyOffset.y, tileSizeInt.x, tileSizeInt.y, 0, 0);
                                if (owner.lessResourceIntensive)
                                {
                                    yield return (processed + 0.5f) / totalTiles;
                                }
                            }
                        }
                    }

                    // Update progress
                    processed++;

                    // Write tile if we drew anything
                    if (tile != null)
                    {
                        File.WriteAllBytes(Path.Combine(outputPath, $"{tileX}_{-1 - tileY}.png"), tile.EncodeToPNG());
                        Object.Destroy(tile);
                        yield return (float)processed / totalTiles;
                    }
                }
            }

            imageCache.Destroy();
            yield break;
        }

        private struct CPUBilinearScaleJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Color32> OldPixels;
            public NativeArray<Color32> NewPixels;
            public int OldW, OldH;
            public int NewW, NewH;

            // Use bilinear filtering because it's quick n' easy (probably could do better with something like bicubic or even Lanczos)
            public void Execute(int index)
            {
                int x = index % NewW;
                int y = index / NewW;

                float u = Custom.LerpMap(x, 0, NewW - 1, 0, OldW - 1);
                float v = Custom.LerpMap(y, 0, NewH - 1, 0, OldH - 1);
                Color32 tl = OldPixels[Mathf.FloorToInt(u) + Mathf.CeilToInt(v) * OldW];
                Color32 tr = OldPixels[Mathf.CeilToInt(u) + Mathf.CeilToInt(v) * OldW];
                Color32 bl = OldPixels[Mathf.FloorToInt(u) + Mathf.FloorToInt(v) * OldW];
                Color32 br = OldPixels[Mathf.CeilToInt(u) + Mathf.FloorToInt(v) * OldW];
                NewPixels[index] = Color32.LerpUnclamped(Color32.LerpUnclamped(tl, tr, u % 1f), Color32.LerpUnclamped(bl, br, u % 1f), v % 1f);
            }
        }

        public static void ScaleTexture(Texture2D texture, int width, int height)
        {
            var oldPixels = new NativeArray<Color32>(texture.GetRawTextureData<Color32>(), Allocator.TempJob);
            var newPixels = new NativeArray<Color32>(width * height, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            var scaler = new CPUBilinearScaleJob
            {
                OldPixels = oldPixels,
                NewPixels = newPixels,
                OldW = texture.width,
                OldH = texture.height,
                NewW = width,
                NewH = height
            };

            var scalerJob = scaler.Schedule(width * height, 64);
            scalerJob.Complete();

            // Set the new texture's content
            try
            {
                texture.Resize(width, height);
                texture.SetPixelData(newPixels, 0);
            }
            finally
            {
                // No memory leaks today!
                oldPixels.Dispose();
                newPixels.Dispose();
            }
        }

        public struct CopyTextureJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Color32> Src;
            public NativeArray<Color32> Dst;
            public int SrcTotalWidth;
            public int DstTotalWidth;
            public int SrcTotalHeight;
            public int DstTotalHeight;
            public int Sx;
            public int Sy;
            public int SW;
            public int Dx;
            public int Dy;

            public void Execute(int index)
            {
                int i = index % SW;
                int j = index / SW;

                if (Sx + i < 0 || Sx + i >= SrcTotalWidth || Dx + i < 0 || Dx + i >= DstTotalWidth) return;
                if (Sy + j < 0 || Sy + j >= SrcTotalHeight || Dy + j < 0 || Dy + j >= DstTotalHeight) return;

                Dst[(Dx + i) + (Dy + j) * DstTotalWidth] = Src[(Sx + i) + (Sy + j) * SrcTotalWidth];
            }
        }
        public static void CopyTextureSegment(Texture2D source, Texture2D destination, int sx, int sy, int sw, int sh, int dx, int dy)
        {
            var srcData = source.GetRawTextureData<Color32>();
            var dstData = destination.GetRawTextureData<Color32>();

            var job = new CopyTextureJob
            {
                Src = srcData,
                Dst = dstData,
                SrcTotalWidth = source.width,
                DstTotalWidth = destination.width,
                SrcTotalHeight = source.height,
                DstTotalHeight = destination.height,
                Sx = sx,
                Sy = sy,
                SW = sw,
                Dx = dx,
                Dy = dy
            };

            var copyJob = job.Schedule(sw * sh, 64);
            copyJob.Complete();

            dstData.CopyFrom(job.Dst);

            destination.SetPixelData(dstData, 0);

            srcData.Dispose();
            dstData.Dispose();
        }
    }
}

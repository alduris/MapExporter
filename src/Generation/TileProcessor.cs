using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using RWCustom;
using Unity.Burst;
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
            TextureCache<string> imageCache = new(Preferences.GeneratorCacheSize.GetValue());

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
                    RenderTexture rt = null; // shoutout to SlimeCubed for introducing me to this concept

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
                                if (rt == null)
                                {
                                    rt = new RenderTexture(tileSizeInt.x, tileSizeInt.y, 32, RenderTextureFormat.ARGB32);
                                    rt.Create();
                                    GL.Clear(true, true, new Color(0f, 0f, 0f, 0f));
                                }

                                // Open the camera so we can use it
                                if (!imageCache.Contains(fileName))
                                {
                                    var camTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false, false);
                                    camTexture.LoadImage(File.ReadAllBytes(Path.Combine(owner.inputDir, fileName)), false);

                                    imageCache[fileName] = camTexture;
                                }

                                // Copy pixels
                                Vector2 copyOffsetVec = tileCoords - (room.devPos + cam + camOffset) * multFac;

                                float normalizeSize = screenSize.y / imageCache[fileName].height;
                                Vector2 scale = screenSize * normalizeSize * multFac / tileSize;

                                Graphics.Blit(imageCache[fileName], rt, scale, copyOffsetVec);

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
                    if (rt != null)
                    {
                        var oldRT = RenderTexture.active;
                        RenderTexture.active = rt;

                        var tile = new Texture2D(tileSizeInt.x, tileSizeInt.y, TextureFormat.ARGB32, false, false);
                        tile.ReadPixels(new Rect(Vector2.zero, tileSize), 0, 0);

                        RenderTexture.active = oldRT;
                        rt.Release();

                        File.WriteAllBytes(Path.Combine(outputPath, $"{tileX}_{-1 - tileY}.png"), tile.EncodeToPNG());
                        Object.Destroy(tile);
                        yield return (float)processed / totalTiles;
                    }
                }
            }

            imageCache.Destroy();
            yield break;
        }

        [BurstCompile]
        #pragma warning disable CS0649 // Field is never assigned to
        private struct CPUBilinearScaleJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Color32> OldPixels;
            public NativeArray<Color32> NewPixels;
            [ReadOnly] public int OldW, OldH;
            [ReadOnly] public int NewW, NewH;

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
        #pragma warning restore CS0649 // Field is never assigned to

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast)]
        private struct CPUBicubicScaleJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Color32> OldPixels;
            public NativeArray<Color32> NewPixels;
            [ReadOnly] public int OldW, OldH;
            [ReadOnly] public int NewW, NewH;

            public void Execute(int index)
            {
                // Implementation note: we have to use Color32 because Texture2D reasons, but I use Color here. Unity converts Color32 and Color implicitly.

                float u = (index % NewW) / (NewW - 1f) * (OldW - 1f);
                float v = (index / NewW) / (NewH - 1f) * (OldH - 1f);
                int x = Mathf.FloorToInt(u);
                int y = Mathf.FloorToInt(v);

                Color[] colors = new Color[16]; // 4x4 grid
                for (int i = -1; i <= 2; i++)
                {
                    for (int j = -1; j <= 2; j++)
                    {
                        colors[(j + 1) * 4 + (i + 1)] = OldPixels[Mathf.Clamp(x + i, 0, OldW - 1) + Mathf.Clamp(y + j, 0, OldH - 1) * OldW];
                    }
                }

                for (int i = 0; i < 4; i++)
                {
                    // Reuse the array because why not
                    colors[i] = Cubic(u - x, colors[i], colors[i+4], colors[i+8], colors[i+12]);
                }

                NewPixels[index] = Cubic(v - y, colors[0], colors[1], colors[2], colors[3]);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private readonly Color Cubic(float t, Color a, Color b, Color c, Color d)
            {
                Color i = -0.5f * a + 1.5f * b - 1.5f * c + 0.5f * d;
                Color j = a - 2.5f * b + 2f * c - 0.5f * d;
                Color k = -0.5f * a + 0.5f * c;
                Color l = b;

                return i * t * t * t + j * t * t + k * t + l;
            }
        }

        public static void ScaleTexture(Texture2D texture, int width, int height)
        {
            var oldPixels = new NativeArray<Color32>(texture.GetRawTextureData<Color32>(), Allocator.TempJob);
            var newPixels = new NativeArray<Color32>(width * height, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            var scaler = new CPUBicubicScaleJob
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

        [BurstCompile]
        public struct CopyTextureJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Color32> Src;
            public NativeArray<Color32> Dst;
            [ReadOnly] public int SrcTotalWidth;
            [ReadOnly] public int DstTotalWidth;
            [ReadOnly] public int SrcTotalHeight;
            [ReadOnly] public int DstTotalHeight;
            [ReadOnly] public int Sx;
            [ReadOnly] public int Sy;
            [ReadOnly] public int SW;
            [ReadOnly] public int Dx;
            [ReadOnly] public int Dy;

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

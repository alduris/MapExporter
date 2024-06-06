using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using RWCustom;
using UnityEngine;

namespace MapExporter.Generator
{
    internal class Generator
    {
        private string inputDir;
        private string outputDir;
        private RegionInfo regionInfo;
        private bool skipExistingTiles;

        private int step = -1;

        private Task thread;

        public bool Done { get; private set; } = false;
        public bool Failed { get; private set; } = false;
        public float Progress => Math.Max(0, step) / 8f;


        public Generator(SlugcatStats.Name scug, string region)
        {
            inputDir = Data.RenderOutputDir(scug.value, region);
            outputDir = Directory.CreateDirectory(Data.FinalOutputDir(scug.value, region)).FullName;

            if (!Directory.Exists(inputDir))
            {
                throw new ArgumentException("Input directory does not exist!");
            }

            regionInfo = RegionInfo.FromJson((Dictionary<string, object>)Json.Deserialize(File.ReadAllText(Path.Combine(inputDir, "metadata.json"))));
            skipExistingTiles = false;
        }

        public void Update()
        {
            if (Done) return;
            if (thread == null || thread.IsCompleted)
            {
                step++;
                if (step >= 0 && step < 8)
                {
                    thread = new Task(() => Thread(-step));
                    thread.Start();
                }
                else if (step == 8)
                {
                    Done = true;
                }
                else
                {
                    Done = true;
                    Failed = true;
                }
            }
            else if (thread.IsFaulted)
            {
                Done = true;
                Failed = true;
            }
        }

        private string OutputPathForStep(int step) => Path.Combine(outputDir, step.ToString());
        private static readonly Vector2 tileSize = new(256f, 256f);
        private static readonly Vector2 offscreenSize = new(1200f, 400f);
        private static IntVector2 Vec2IntVecFloor(Vector2 v) => new(Mathf.FloorToInt(v.x), Mathf.FloorToInt(v.y));
        private static IntVector2 Vec2IntVecCeil(Vector2 v) => new(Mathf.CeilToInt(v.x), Mathf.CeilToInt(v.y));

        private void Thread(int zoom)
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
            for (int tileX = llbTile.x; tileX <= urbTile.x; tileX++)
            {
                for (int tileY = llbTile.y; tileY <= urbTile.y; tileY++)
                {
                    // Get file path and see if we can skip it
                    string filePath = Path.Combine(outputPath, $"{tileX}_{-1-tileY}.png");
                    if (skipExistingTiles && File.Exists(filePath))
                    {
                        continue;
                    }

                    // Build tile
                    // todo
                }
            }

            // Export metadata
            // todo
        }
    }
}

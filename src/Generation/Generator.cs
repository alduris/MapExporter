using System;
using System.Collections.Generic;
using System.IO;

namespace MapExporter.Generation
{
    internal class Generator
    {
        internal readonly string inputDir;
        internal readonly string outputDir;
        internal readonly RegionInfo regionInfo;
        internal readonly bool skipExistingTiles;

        public bool Done { get; private set; } = false;
        public bool Failed { get; private set; } = false;
        public float Progress { get; private set; } = 0f;
        public string CurrentTask { get; private set; } = "Nothing";

        private readonly Queue<Processor> processes = [];
        internal readonly Dictionary<string, object> metadata = [];


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

            // Enqueue processes
            for (int i = 0; i < 8; i++)
            {
                processes.Enqueue(new TileProcessor(this, -i));
            }
            processes.Enqueue(new RoomProcessor(this));
            processes.Enqueue(new ConnectionProcessor(this));
            processes.Enqueue(new GeometryProcessor(this));
            processes.Enqueue(new SpawnProcessor(this));
            processes.Enqueue(new MiscProcessor(this));
        }

        public void Update()
        {
            if (Done) return;

            Processor process = processes.Peek();
            bool move = !process.MoveNext();
            Progress = process.Progress;
            CurrentTask = process.ProcessName;
            if (process.Failed)
            {
                Done = true;
                Failed = true;
                return;
            }
            if (move)
            {
                process.Dispose(); // I don't think anything actually uses this but just in case for the future
                processes.Dequeue();
                if (processes.Count == 0)
                {
                    File.WriteAllText(Path.Combine(outputDir, "region.json"), Json.Serialize(metadata));
                    Done = true;
                }
            }

        }

    }
}

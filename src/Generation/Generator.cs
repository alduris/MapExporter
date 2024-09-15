using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace MapExporter.Generation
{
    internal class Generator
    {
        internal readonly string inputDir;
        internal readonly string outputDir;
        internal readonly RegionInfo regionInfo;
        public readonly bool lessResourceIntensive = false;

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

            // Load region data and adapt it to our needs (we can do this because we're not re-saving the changes)
            regionInfo = RegionInfo.FromJson((Dictionary<string, object>)Json.Deserialize(File.ReadAllText(Path.Combine(inputDir, "metadata.json"))));
            List<string> hiddenRooms = [];
            foreach (var room in regionInfo.rooms.Values)
            {
                room.devPos *= 20; // convert to room pixel coordinates
                if (room.hidden) hiddenRooms.Add(room.roomName); // don't render room if hidden
            }
            foreach (var room in hiddenRooms)
            {
                regionInfo.rooms.Remove(room);
                regionInfo.connections.RemoveAll(x => x.roomA == room || x.roomB == room);
                if (room == regionInfo.echoRoom) regionInfo.echoRoom = null;
            }

            // Enqueue processes
            processes.Enqueue(new CleanerProcessor(this));
            for (int i = 0; i < 8; i++)
            {
                processes.Enqueue(new TileProcessor(this, -i));
            }
            processes.Enqueue(new RoomProcessor(this));
            processes.Enqueue(new ConnectionProcessor(this));
            processes.Enqueue(new GeometryProcessor(this));
            processes.Enqueue(new SpawnProcessor(this));
            processes.Enqueue(new PlacedObjectProcessor(this));
            processes.Enqueue(new MiscProcessor(this));

            // Preferences
            // lessResourceIntensive = Preferences.GeneratorLessInsense.GetValue();
        }

        public void Update()
        {
            if (Done) return;

            var timer = new Stopwatch();
            timer.Start();
            while (timer.ElapsedMilliseconds < 100)
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
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MapExporter.Generation
{
    internal class Cleaner : Processor
    {
        private readonly List<string> files = [];

        public Cleaner(Generator owner) : base(owner)
        {
            // Figure out what files need to be cleared
            for (int i = 0; i > -8; i--)
            {
                try
                {
                    string dir = OutputPathForStep(i);
                    if (Directory.Exists(dir))
                    {
                        // Only the png files
                        files.AddRange(Directory.GetFiles(dir).Where(x => x.EndsWith(".png")));
                    }
                }
                catch (UnauthorizedAccessException) { }
            }
        }

        private string OutputPathForStep(int step) => Path.Combine(owner.outputDir, step.ToString());

        public override string ProcessName => "Cleaning old files";

        protected override IEnumerator Process()
        {
            int i = 0;
            foreach (var file in files)
            {
                i++;
                File.Delete(file);
                Progress = (float)i / files.Count;
                if (i % (owner.lessResourceIntensive ? 100 : 250) == 0) yield return null;
            }

            Progress = 1f;
            Done = true;
            yield return null;
        }
    }
}

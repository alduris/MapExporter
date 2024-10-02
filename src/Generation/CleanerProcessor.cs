using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MapExporter.Generation
{
    internal class CleanerProcessor : Processor
    {
        private readonly List<string> files = [];
        public bool SkipGenerator { get; private set; } = false;

        public CleanerProcessor(Generator owner) : base(owner)
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
                        if (files.Count > 0 && Preferences.GeneratorSkipExisting.GetValue())
                        {
                            SkipGenerator = true;
                        }
                    }
                }
                catch (UnauthorizedAccessException) { } // this shouldn't happen
            }
        }

        private string OutputPathForStep(int step) => Path.Combine(owner.outputDir, step.ToString());

        public override string ProcessName => "Cleaning old files";

        protected override IEnumerator<float> Process()
        {
            if (SkipGenerator) yield break;

            int i = 0;
            foreach (var file in files)
            {
                i++;
                try
                {
                    File.Delete(file);
                }
                catch (UnauthorizedAccessException) { } // this shouldn't happen

                if (i % (owner.lessResourceIntensive ? 100 : 250) == 0)
                    yield return (float)i / files.Count;
            }

            yield break;
        }
    }
}

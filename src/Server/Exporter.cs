using System;
using System.IO;
using System.IO.Compression;

namespace MapExporter.Server
{
    internal static class Exporter
    {
        public static void ExportServer(ExportType exportType, string fileLocation)
        {
            if (exportType == ExportType.None) return;

            // Get temporary directory
            var tempDir = Path.Combine(Path.GetTempPath(), "mapexporttemp");
            if (Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }

            try
            {
                // Copy frontend files
                RecursivelyCopyDirectory(Resources.FEPathTo(), tempDir);
                Resources.TryGetJsonResource("/regions.json", out var regionsJson);
                File.WriteAllBytes(Path.Combine(tempDir, "regions.json"), regionsJson);
                Resources.TryGetJsonResource("/slugcats.json", out var slugcatsJson);
                File.WriteAllBytes(Path.Combine(tempDir, "slugcats.json"), slugcatsJson);

                // Copy output files
                var outPath = Path.Combine(tempDir, "slugcats");
                RecursivelyCopyDirectory(Data.FinalDir, outPath);

                // Copy self-host executable if wanted
                if (exportType == ExportType.Server)
                {
                    RecursivelyCopyDirectory(Path.Combine(Data.ModDirectory, "map-server"), outPath);
                }

                ZipFile.CreateFromDirectory(tempDir, Path.Combine(fileLocation, "mapexport.zip"));
            }
            catch
            {
                throw;
            }
            finally
            {
                // Delete our temporary dir
                Directory.Delete(tempDir, true);
            }
        }

        private static void RecursivelyCopyDirectory(string path, string destinationPath)
        {
            try
            {
                if (!Directory.Exists(destinationPath))
                {
                    Directory.CreateDirectory(destinationPath);
                }
                foreach (var dir in Directory.GetDirectories(path))
                {
                    var newDest = Path.Combine(destinationPath, new DirectoryInfo(dir).Name);
                    RecursivelyCopyDirectory(dir, newDest);
                }
                foreach (var file in Directory.GetFiles(path))
                {
                    Plugin.Logger.LogDebug(Path.Combine(destinationPath, new FileInfo(file).Name));
                    //File.Copy(file, Path.Combine(destinationPath, new FileInfo(file).Name), true);
                }
            }
            catch (UnauthorizedAccessException) { }
        }

        public enum ExportType
        {
            /// <summary>
            /// Default value, does nothing
            /// </summary>
            None,
            /// <summary>
            /// Full batch with no self-host file
            /// </summary>
            NoServer,
            /// <summary>
            /// Full batch with self-host file
            /// </summary>
            Server
        }
    }
}

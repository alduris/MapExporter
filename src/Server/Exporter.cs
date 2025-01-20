using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;

#pragma warning disable CS0162 // Unreachable code detected
namespace MapExporter.Server
{
    internal static class Exporter
    {
        private const bool DEBUG = true;

        private static int fileCount = 1;
        private static readonly Stack<string> toCheck = [];
        public static void ResetFileCounter()
        {
            fileCount = 2; // regions.json and slugcats.json
            toCheck.Clear();
            toCheck.Push(Resources.FEPathTo());
            toCheck.Push(Data.FinalDir);
            toCheck.Push(Path.Combine(Data.ModDirectory, "map-server"));
        }
        public static void UpdateFileCounter()
        {
            if (toCheck.Count > 0)
            {
                var path = toCheck.Pop();
                try
                {
                    foreach (var file in Directory.GetFiles(path))
                    {
                        fileCount++;
                    }
                    foreach (var dir in Directory.GetDirectories(path))
                    {
                        toCheck.Push(dir);
                    }
                }
                catch { }

                if (toCheck.Count == 0)
                {
                    Plugin.Logger.LogDebug($"{fileCount} files to copy");
                }
            }
        }

        public static void ExportServer(ExportType exportType, string fileLocation = "mapexport")
        {
            if (exportType == ExportType.None) return;

            fileLocation = SafeFileName(fileLocation);

            // Get temporary directory
            var tempDir = Path.Combine(Path.GetTempPath(), "mapexporttemp");
            //Directory.CreateDirectory(tempDir);

            // Copy frontend files
            RecursivelyCopyDirectory(Resources.FEPathTo(), tempDir);

            Resources.TryGetJsonResource("/regions.json", out var regionsJson);
            if (!DEBUG) File.WriteAllBytes(Path.Combine(tempDir, "regions.json"), regionsJson);
            Resources.TryGetJsonResource("/slugcats.json", out var slugcatsJson);
            if (!DEBUG) File.WriteAllBytes(Path.Combine(tempDir, "slugcats.json"), slugcatsJson);

            // Copy output files
            var tilePath = Path.Combine(tempDir, "slugcats");
            RecursivelyCopyDirectory(Data.FinalDir, tilePath);

            // Copy self-host executable if wanted
            if (exportType == ExportType.Server)
            {
                RecursivelyCopyDirectory(Path.Combine(Data.ModDirectory, "map-server"), tempDir);
            }

            try
            {
                if (!DEBUG) ZipFile.CreateFromDirectory(tempDir, Path.Combine(fileLocation, fileLocation + ".zip"));
            }
            catch
            {
                throw;
            }
            finally
            {
                // Delete our temporary dir
                if (!DEBUG) Directory.Delete(tempDir, true);
            }
        }

        private static void RecursivelyCopyDirectory(string path, string destinationPath)
        {
            try
            {
                if (!DEBUG) Directory.CreateDirectory(destinationPath);
                foreach (var dir in Directory.GetDirectories(path))
                {
                    var newDest = Path.Combine(destinationPath, new DirectoryInfo(dir).Name);
                    RecursivelyCopyDirectory(dir, newDest);
                }

                foreach (var file in Directory.GetFiles(path))
                {
                    if (!DEBUG) File.Copy(file, Path.Combine(destinationPath, new FileInfo(file).Name), true);
                    else Plugin.Logger.LogDebug(Path.Combine(destinationPath, new FileInfo(file).Name));
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
            Server,
            /// <summary>
            /// Full batch with self-host file but it's Python instead of my homemade solution (which is probably really insecure so this is probably the better option)
            /// </summary>
            PythonServer
        }

        /// <summary>
        /// Removes most non-alphanumeric characters to guarantee a safe file name
        /// </summary>
        /// <param name="filename">Raw file name</param>
        /// <returns>Safe string</returns>
        public static string SafeFileName(string filename) => Regex.Replace(filename, @"[^\w\d\-]", "_");
    }
}
#pragma warning restore CS0162 // Unreachable code detected

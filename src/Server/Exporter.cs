using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using static MapExporter.Data;

#pragma warning disable CS0162 // Unreachable code detected
namespace MapExporter.Server
{
    internal static class Exporter
    {
        private const bool DEBUG = false;

        public static int currentProgress = 0;
        public static bool inProgress = false;
        public static bool zipping = false;

        public static int FileCount { get; private set; } = 1;
        private static readonly Stack<string> toCheck = [];
        public static void ResetFileCounter()
        {
            FileCount = 2; // regions.json and slugcats.json
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
                        FileCount++;
                    }
                    foreach (var dir in Directory.GetDirectories(path))
                    {
                        toCheck.Push(dir);
                    }
                }
                catch { }

                if (toCheck.Count == 0)
                {
                    Plugin.Logger.LogDebug($"{FileCount} files to copy");
                }
            }
        }

        public static void ExportServer(ExportType exportType, bool zip, string outputLoc)
        {
            currentProgress = 0;
            zipping = false;
            inProgress = true;

            // Get temporary directory
            var outDir = zip ? Path.Combine(Path.GetTempPath(), "mapexporttemp") : outputLoc;
            if (!DEBUG) Directory.CreateDirectory(outDir);

            // Copy frontend files
            RecursivelyCopyDirectory(Resources.FEPathTo(), outDir);

            Resources.TryGetJsonResource("/regions.json", out var regionsJson);
            if (!DEBUG) File.WriteAllBytes(Path.Combine(outDir, "regions.json"), regionsJson);
            currentProgress++;
            Resources.TryGetJsonResource("/slugcats.json", out var slugcatsJson);
            if (!DEBUG) File.WriteAllBytes(Path.Combine(outDir, "slugcats.json"), slugcatsJson);
            currentProgress++;

            // Copy output files
            var tilePath = Path.Combine(outDir, "slugcats");
            RecursivelyCopyDirectory(Data.FinalDir, tilePath);

            // Copy self-host executable if wanted
            if (exportType == ExportType.Server)
            {
                RecursivelyCopyDirectory(Path.Combine(Data.ModDirectory, "map-server"), outDir);
            }
            else if (exportType == ExportType.PythonServer)
            {
                if (!DEBUG) File.WriteAllText(Path.Combine(outDir, "run.bat"), "python -m http.server");
            }

            if (zip)
            {
                try
                {
                    zipping = true;
                    if (!DEBUG) ZipFile.CreateFromDirectory(outDir, Path.Combine(outputLoc, outputLoc + ".zip"));
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError(ex);
                    throw;
                }
                finally
                {
                    // Delete our temporary dir
                    try
                    {
                        if (!DEBUG) Directory.Delete(outDir, true);
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogError(ex);
                    }
                    inProgress = false;
                }
            }
            else
            {
                inProgress = false;
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
                    currentProgress++;
                }
            }
            catch (UnauthorizedAccessException) { }
        }

        public enum ExportType
        {
            /// <summary>
            /// Full batch with self-host file
            /// </summary>
            Server,
            /// <summary>
            /// Full batch with no self-host file
            /// </summary>
            NoServer,
            /// <summary>
            /// Full batch with self-host file but it's Python instead of my homemade solution (which is probably really insecure so this is probably the better option)
            /// </summary>
            PythonServer
        }

        public static string ExportTypeName(string exportType) {
            var enumVal = Enum.Parse(typeof(ExportType), exportType);
            return enumVal switch
            {
                ExportType.Server => "Include server (executable)",
                ExportType.NoServer => "Do not include server",
                ExportType.PythonServer => "Include server (Python batch file)",
                _ => throw new NotImplementedException()
            };
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

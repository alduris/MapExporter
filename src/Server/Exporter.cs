using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace MapExporter.Server
{
    internal static class Exporter
    {
        public static void ExportServer(ExportType exportType, string fileLocation)
        {
            if (exportType == ExportType.None) return;

            // Get temporary directory
            var tempDir = Path.GetTempPath();

            // Copy frontend files
            //

            // Copy output files
            //

            // Copy self-host executable if wanted
            if (exportType == ExportType.Server)
            {
                //
            }

            ZipFile.CreateFromDirectory(tempDir, Path.Combine(fileLocation, "mapexport.zip"));

            // Delete our temporary dir
            new DirectoryInfo(tempDir).Delete();
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

using System.IO;
using System.IO.Compression;

namespace PodRelay.App.Services;

public static class DiagnosticsExportService
{
    public static void Export(string applicationDataDirectory, string destinationZip)
    {
        if (!Directory.Exists(applicationDataDirectory))
        {
            throw new DirectoryNotFoundException("PodRelay has not created any local diagnostics yet.");
        }

        if (File.Exists(destinationZip))
        {
            File.Delete(destinationZip);
        }

        ZipFile.CreateFromDirectory(applicationDataDirectory, destinationZip, CompressionLevel.Optimal, includeBaseDirectory: false);
    }
}

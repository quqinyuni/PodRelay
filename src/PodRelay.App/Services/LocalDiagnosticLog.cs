using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PodRelay.App.Services;

public sealed class LocalDiagnosticLog
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private readonly string logDirectory;

    public LocalDiagnosticLog(string applicationDataDirectory)
    {
        logDirectory = Path.Combine(applicationDataDirectory, "logs");
        PruneOldLogs();
    }

    public async Task WriteAsync(string eventName, object? details = null)
    {
        try
        {
            var entry = JsonSerializer.Serialize(new
            {
                timestamp = DateTimeOffset.Now,
                eventName,
                details
            }, SerializerOptions);
            await writeLock.WaitAsync();
            Directory.CreateDirectory(logDirectory);
            try
            {
                var path = Path.Combine(logDirectory, $"podrelay-{DateTime.Now:yyyyMMdd}.jsonl");
                await File.AppendAllTextAsync(path, entry + Environment.NewLine);
            }
            finally
            {
                writeLock.Release();
            }
        }
        catch
        {
            // Diagnostics are best-effort and must never take down the relay process.
        }
    }

    private void PruneOldLogs()
    {
        try
        {
            if (!Directory.Exists(logDirectory))
            {
                return;
            }

            var cutoff = DateTime.UtcNow.AddDays(-14);
            foreach (var path in Directory.EnumerateFiles(logDirectory, "podrelay-*.jsonl"))
            {
                if (File.GetLastWriteTimeUtc(path) < cutoff)
                {
                    File.Delete(path);
                }
            }
        }
        catch
        {
            // Retention is best-effort for the same reason as log writes.
        }
    }
}

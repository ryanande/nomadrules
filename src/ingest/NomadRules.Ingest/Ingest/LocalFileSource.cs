namespace NomadRules.Ingest.Ingest;

// Reads the *.json files the crawler's LocalFilePublisher writes. Handle = the file path.
// Complete deletes the file (only after commit); DeadLetter moves it to a sibling `dead/` folder.
public class LocalFileSource(IngestOptions options, ILogger<LocalFileSource> log) : IMessageSource
{
    private readonly string _dir = options.LocalQueueDir;

    public async Task<IReadOnlyList<QueuedMessage>> ReceiveAsync(int max, CancellationToken ct)
    {
        if (!Directory.Exists(_dir))
        {
            log.LogDebug("Local queue dir {Dir} does not exist yet — nothing to ingest", _dir);
            return [];
        }

        // Oldest-first for stable ordering; skip the dead/ subfolder.
        var files = Directory.EnumerateFiles(_dir, "*.json")
            .OrderBy(f => File.GetCreationTimeUtc(f))
            .Take(max);

        var messages = new List<QueuedMessage>();
        foreach (var path in files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                messages.Add(new QueuedMessage(await File.ReadAllTextAsync(path, ct), path));
            }
            catch (IOException ex)
            {
                // File may be mid-write by the crawler; skip this pass, pick it up next tick.
                log.LogWarning(ex, "Could not read {Path} this pass", path);
            }
        }
        return messages;
    }

    public Task CompleteAsync(QueuedMessage message, CancellationToken ct)
    {
        var path = (string)message.Handle;
        try { File.Delete(path); }
        catch (IOException ex) { log.LogWarning(ex, "Failed to delete completed message {Path}", path); }
        return Task.CompletedTask;
    }

    public Task DeadLetterAsync(QueuedMessage message, string reason, CancellationToken ct)
    {
        var path = (string)message.Handle;
        var deadDir = Path.Combine(_dir, "dead");
        Directory.CreateDirectory(deadDir);
        var dest = Path.Combine(deadDir, Path.GetFileName(path));
        try
        {
            File.Move(path, dest, overwrite: true);
            log.LogError("Dead-lettered {Path} -> {Dest}: {Reason}", path, dest, reason);
        }
        catch (IOException ex)
        {
            log.LogError(ex, "Failed to dead-letter {Path} ({Reason})", path, reason);
        }
        return Task.CompletedTask;
    }
}

namespace NomadRules.Ingest.Ingest;

// One raw message plus a transport-specific handle used to ack/dead-letter it after processing.
public sealed class QueuedMessage(string raw, object handle)
{
    public string Raw { get; } = raw;
    public object Handle { get; } = handle;
}

// Symmetric with the crawler's IMessagePublisher: one abstraction, two transports selected by config.
// Ack-after-commit is the caller's contract — Complete only after the DB row is durable (design Decision 4).
public interface IMessageSource
{
    Task<IReadOnlyList<QueuedMessage>> ReceiveAsync(int max, CancellationToken ct);
    Task CompleteAsync(QueuedMessage message, CancellationToken ct);        // ack: durable, drop from queue
    Task DeadLetterAsync(QueuedMessage message, string reason, CancellationToken ct); // set aside a poison message
}

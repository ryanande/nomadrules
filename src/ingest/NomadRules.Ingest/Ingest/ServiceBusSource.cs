using Azure.Messaging.ServiceBus;

namespace NomadRules.Ingest.Ingest;

// Azure Service Bus receiver — the prod transport, symmetric with the crawler's ServiceBusPublisher.
// Handle = the ServiceBusReceivedMessage. Complete/DeadLetter map to the SB settlement APIs.
public class ServiceBusSource : IMessageSource, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusReceiver _receiver;
    private readonly ILogger<ServiceBusSource> _log;

    public ServiceBusSource(IngestOptions options, ILogger<ServiceBusSource> log)
    {
        _log = log;
        _client = new ServiceBusClient(options.ServiceBusConnectionString);
        _receiver = _client.CreateReceiver(options.ServiceBusQueue, new ServiceBusReceiverOptions
        {
            ReceiveMode = ServiceBusReceiveMode.PeekLock, // settle explicitly after commit
        });
    }

    public async Task<IReadOnlyList<QueuedMessage>> ReceiveAsync(int max, CancellationToken ct)
    {
        var batch = await _receiver.ReceiveMessagesAsync(max, TimeSpan.FromSeconds(2), ct);
        return batch.Select(m => new QueuedMessage(m.Body.ToString(), m)).ToList();
    }

    public Task CompleteAsync(QueuedMessage message, CancellationToken ct) =>
        _receiver.CompleteMessageAsync((ServiceBusReceivedMessage)message.Handle, ct);

    public Task DeadLetterAsync(QueuedMessage message, string reason, CancellationToken ct)
    {
        _log.LogError("Dead-lettering Service Bus message: {Reason}", reason);
        return _receiver.DeadLetterMessageAsync((ServiceBusReceivedMessage)message.Handle,
            deadLetterReason: reason, cancellationToken: ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _receiver.DisposeAsync();
        await _client.DisposeAsync();
    }
}

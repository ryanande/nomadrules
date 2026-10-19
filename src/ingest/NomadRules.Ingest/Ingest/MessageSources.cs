namespace NomadRules.Ingest.Ingest;

// Transport registry: the only place that knows which brokers exist, mirroring the crawler's `transports` map.
// To add one (e.g. RabbitMQ), implement IMessageSource and add an entry here.
public static class MessageSources
{
    private static readonly Dictionary<string, Type> Transports = new(StringComparer.OrdinalIgnoreCase)
    {
        ["local"] = typeof(LocalFileSource),
        ["servicebus"] = typeof(ServiceBusSource),
    };

    public static Type TypeFor(string transport) =>
        Transports.TryGetValue(transport, out var type)
            ? type
            : throw new InvalidOperationException(
                $"Unknown Ingest:Transport '{transport}' (expected one of: {string.Join(", ", Transports.Keys)})");
}

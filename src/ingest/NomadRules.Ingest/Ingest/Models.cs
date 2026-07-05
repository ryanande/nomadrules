using System.Text.Json;

namespace NomadRules.Ingest.Ingest;

public class IngestOptions
{
    public string Transport { get; set; } = "local";               // local | servicebus
    public string LocalQueueDir { get; set; } = "./local-queue";
    public string? ServiceBusConnectionString { get; set; }
    public string ServiceBusQueue { get; set; } = "law-changes";
    public string MvpDefaultState { get; set; } = "TX";
    public int BatchSize { get; set; } = 20;
}

// Mirrors the crawler's message contract (src/crawler/src/types/messages.ts). Crawler emits camelCase JSON;
// deserialization is case-insensitive so these PascalCase members bind.
public record LawChangeDetected(
    string MessageType,
    string MessageId,
    string SourceId,
    string RawContent,
    string ContentHash,
    string? PreviousHash,
    string Url,
    string? State,
    string Category,
    string DetectedAt);

// A row ready to insert into law_changes. Summarizer columns (headline/summary/severity/processed_at) stay null.
public record LawChangeInsert(
    string Id,
    string SourceMessageId,
    string SourceId,
    string Url,
    string RawContent,
    string State,
    string DetectedAt);

public static class Mapping
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // Returns null if the payload can't be parsed into a usable message (malformed → dead-lettered by caller).
    public static LawChangeDetected? TryParse(string json)
    {
        try
        {
            var msg = JsonSerializer.Deserialize<LawChangeDetected>(json, JsonOpts);
            if (msg is null || string.IsNullOrWhiteSpace(msg.MessageId) || string.IsNullOrWhiteSpace(msg.SourceId))
                return null;
            return msg;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Map a message to a row. Our row id is a fresh system-owned UUID — NOT the producer's messageId, which
    // is carried separately in source_message_id as the dedup key. State falls back to the MVP default.
    public static LawChangeInsert ToRow(LawChangeDetected msg, string defaultState) => new(
        Id: Guid.NewGuid().ToString(),
        SourceMessageId: msg.MessageId,
        SourceId: msg.SourceId,
        Url: msg.Url,
        RawContent: msg.RawContent,
        State: string.IsNullOrWhiteSpace(msg.State) ? defaultState : msg.State,
        DetectedAt: msg.DetectedAt);
}

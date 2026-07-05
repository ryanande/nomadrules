using Dapper;
using NomadRules.Ingest.Infrastructure;

namespace NomadRules.Ingest.Ingest;

public class IngestRepository(Db db)
{
    // Idempotent insert scoped to the source_message_id conflict ONLY. Returns true if a new row was created,
    // false if a row for this messageId already existed (duplicate/redelivery). A bare INSERT OR IGNORE would
    // also swallow NOT NULL / PRIMARY KEY violations and silently ack them as "duplicate"; ON CONFLICT(...)
    // DO NOTHING suppresses only the intended dedup conflict, so any other constraint violation throws and
    // hits the caller's un-ack/redelivery path.
    public async Task<bool> TryInsertAsync(LawChangeInsert row)
    {
        using var conn = db.Open();
        var affected = await conn.ExecuteAsync("""
            INSERT INTO law_changes
              (id, source_message_id, content_hash, previous_hash, source_id, url, raw_content, state, detected_at, processed_at)
            VALUES
              (@Id, @SourceMessageId, @ContentHash, @PreviousHash, @SourceId, @Url, @RawContent, @State, @DetectedAt, NULL)
            ON CONFLICT(source_message_id) DO NOTHING
            """, row);
        return affected > 0;
    }
}

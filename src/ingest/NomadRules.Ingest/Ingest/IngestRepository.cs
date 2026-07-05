using Dapper;
using NomadRules.Ingest.Infrastructure;

namespace NomadRules.Ingest.Ingest;

public class IngestRepository(Db db)
{
    // Idempotent insert keyed on source_message_id's unique index. Returns true if a new row was created,
    // false if a row for this messageId already existed (duplicate/redelivery). Either way the caller acks;
    // only a thrown exception (DB failure) should leave the message un-acked for redelivery.
    public async Task<bool> TryInsertAsync(LawChangeInsert row)
    {
        using var conn = db.Open();
        var affected = await conn.ExecuteAsync("""
            INSERT OR IGNORE INTO law_changes
              (id, source_message_id, source_id, url, raw_content, state, detected_at, processed_at)
            VALUES
              (@Id, @SourceMessageId, @SourceId, @Url, @RawContent, @State, @DetectedAt, NULL)
            """, row);
        return affected > 0;
    }
}

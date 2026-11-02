using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace CloudSaveServer;

internal sealed partial class SaveStore
{
    // These descriptors are code-owned; SQL identifiers must never come from a request.
    sealed record ProgressionSettlement<TOperation, TReply>(string Table, string Prefix,
        Func<string, int, long?, Exception> Failure, Func<TOperation, SaveSnapshot, bool, TReply> Reply)
        where TOperation : class where TReply : class;

    TReply? FindProgression<TOperation, TReply>(string player, Guid requestId, string? fingerprint,
        ProgressionSettlement<TOperation, TReply> settlement) where TOperation : class where TReply : class
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction(deferred: true);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.Parameters.AddWithValue("$player", player);
        command.Parameters.AddWithValue("$request", requestId.ToString("D"));
        var reply = ReadProgressionReceipt(command, fingerprint, settlement);
        transaction.Commit();
        return reply;
    }

    static TReply? ReadProgressionReceipt<TOperation, TReply>(SqliteCommand command, string? fingerprint,
        ProgressionSettlement<TOperation, TReply> settlement) where TOperation : class where TReply : class
    {
        command.CommandText = $"""
            SELECT r.request_hash,r.operation_json,s.save_json,s.revision,s.updated_at_utc
            FROM {settlement.Table} r JOIN saves s ON r.player_id=s.player_id
            WHERE r.player_id=$player AND r.request_id=$request;
            """;
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;
        if (fingerprint != null && fingerprint != reader.GetString(0))
            throw settlement.Failure("request_id_reused", 409, null);
        var operation = JsonSerializer.Deserialize<TOperation>(reader.GetString(1), GachaJson.Options)
            ?? throw new InvalidDataException("Invalid progression receipt.");
        // An old receipt describes its original operation, but recovery always returns the latest save.
        return settlement.Reply(operation, new(reader.GetString(2), reader.GetInt64(3), reader.GetString(4)), true);
    }

    TReply SettleProgression<TOperation, TReply>(string player, Guid requestId, string fingerprint, long expectedRevision,
        ProgressionSettlement<TOperation, TReply> settlement, Func<JsonObject, long, TOperation> mutate)
        where TOperation : class where TReply : class
    {
        using var connection = Open();
        // Reserve the write transaction before lookup so concurrent retries cannot both settle.
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.Parameters.AddWithValue("$player", player);
        command.Parameters.AddWithValue("$request", requestId.ToString("D"));
        var replay = ReadProgressionReceipt(command, fingerprint, settlement);
        if (replay != null) return replay;
        command.CommandText = "SELECT save_json,revision FROM saves WHERE player_id=$player;";
        JsonObject save;
        long revision;
        using (var reader = command.ExecuteReader())
        {
            if (!reader.Read()) throw settlement.Failure(settlement.Prefix + "_save_required", 409, null);
            save = JsonNode.Parse(reader.GetString(0))!.AsObject();
            revision = reader.GetInt64(1);
        }
        if (revision != expectedRevision) throw settlement.Failure("save_conflict", 409, revision);
        if (save["version"]?.GetValue<int>() != 8)
            throw settlement.Failure(settlement.Prefix + "_save_upgrade_required", 409, null);
        try
        {
            long next = checked(revision + 1);
            var operation = mutate(save, next);
            string timestamp = DateTimeOffset.UtcNow.ToString("O");
            save["savedAtUtc"] = timestamp;
            string json = save.ToJsonString(GachaJson.Options);
            if (Encoding.UTF8.GetByteCount(json) > settings.MaxSaveBytes)
                throw settlement.Failure(settlement.Prefix + "_save_too_large", 400, null);
            command.Parameters.AddWithValue("$json", json);
            command.Parameters.AddWithValue("$next", next);
            command.Parameters.AddWithValue("$time", timestamp);
            command.Parameters.AddWithValue("$hash", fingerprint);
            command.Parameters.AddWithValue("$operation", JsonSerializer.Serialize(operation, GachaJson.Options));
            // Mutation and receipt share one commit; a failed write rolls back both.
            command.CommandText = $"""
                UPDATE saves SET save_json=$json,revision=$next,updated_at_utc=$time WHERE player_id=$player;
                INSERT INTO {settlement.Table} VALUES($player,$request,$hash,$operation);
                """;
            command.ExecuteNonQuery();
            transaction.Commit();
            return settlement.Reply(operation, new(json, next, timestamp), false);
        }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or NullReferenceException or KeyNotFoundException)
        { throw settlement.Failure("invalid_" + settlement.Prefix + "_save", 400, null); }
    }
}

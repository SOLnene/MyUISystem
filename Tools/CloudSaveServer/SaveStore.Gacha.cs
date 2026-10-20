using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace CloudSaveServer;

internal sealed partial class SaveStore
{
    static void InitializeGachaSchema(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS gacha_player_state (
                player_id TEXT PRIMARY KEY REFERENCES saves(player_id),
                pity_json TEXT NOT NULL, next_instance_id INTEGER NOT NULL CHECK(next_instance_id>0));
            CREATE TABLE IF NOT EXISTS gacha_receipts (
                player_id TEXT NOT NULL REFERENCES saves(player_id), request_id TEXT NOT NULL,
                request_hash TEXT NOT NULL, operation_json TEXT NOT NULL,
                PRIMARY KEY(player_id,request_id));
            """;
        command.ExecuteNonQuery();
    }

    internal GachaDrawReply? FindGacha(string player, Guid requestId, string? fingerprint = null)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction(deferred: true);
        var result = ReadGachaReceipt(connection, transaction, player, requestId, fingerprint);
        transaction.Commit();
        return result;
    }

    static GachaDrawReply? ReadGachaReceipt(SqliteConnection connection, SqliteTransaction transaction,
        string player, Guid id, string? fingerprint)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.Parameters.AddWithValue("$player", player);
        command.Parameters.AddWithValue("$request", id.ToString("D"));
        command.CommandText = """
            SELECT r.request_hash,r.operation_json,s.save_json,s.revision,s.updated_at_utc
            FROM gacha_receipts r JOIN saves s ON s.player_id=r.player_id
            WHERE r.player_id=$player AND r.request_id=$request;
            """;
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;
        if (fingerprint != null && reader.GetString(0) != fingerprint) throw new GachaFailure("request_id_reused");
        var operation = JsonSerializer.Deserialize<GachaOperation>(reader.GetString(1), GachaJson.Options)
            ?? throw new InvalidDataException("Invalid stored gacha receipt.");
        return new GachaDrawReply(operation,
            new SaveSnapshot(reader.GetString(2), reader.GetInt64(3), reader.GetString(4)), true);
    }

    internal GachaDrawReply DrawGacha(string player, GachaDrawRequest request, GachaPoolConfig pool, GachaCatalog catalog)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        var replay = ReadGachaReceipt(connection, transaction, player, request.RequestId, request.Fingerprint());
        if (replay != null) return replay;
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.Parameters.AddWithValue("$player", player);
        command.CommandText = "SELECT save_json,revision FROM saves WHERE player_id=$player;";
        string json;
        long revision;
        using (var reader = command.ExecuteReader())
        {
            if (!reader.Read()) throw new GachaFailure("gacha_save_required");
            json = reader.GetString(0); revision = reader.GetInt64(1);
        }
        if (revision != request.ExpectedRevision) throw new GachaFailure("save_conflict", revision: revision);
        var save = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { MaxDepth = 64 }) as JsonObject
            ?? throw new GachaFailure("invalid_gacha_save", 400);
        // General cloud storage is opaque; settlement only edits the explicitly supported save layout.
        int version = save["version"]?.GetValue<int>() ?? 0;
        if (version != 8) throw new GachaFailure("gacha_save_upgrade_required");
        var pity = GachaPity.Parse((save["gacha"] ?? new JsonObject()).ToJsonString());
        long nextId = 1;
        command.CommandText = "SELECT pity_json,next_instance_id FROM gacha_player_state WHERE player_id=$player;";
        using (var reader = command.ExecuteReader())
            if (reader.Read()) { pity = GachaPity.Parse(reader.GetString(0)); nextId = reader.GetInt64(1); }
        var entries = GachaRoller.Draw(pool, request.Count, pity);
        var results = catalog.Grant(save, entries, ref nextId);
        string timestamp = DateTimeOffset.UtcNow.ToString("O");
        save["gacha"] = JsonSerializer.SerializeToNode(pity, GachaJson.Options);
        save["savedAtUtc"] = timestamp;
        var progress = GachaCatalog.Array(GachaCatalog.Section(save, "achievements"), "progress");
        var counter = progress.OfType<JsonObject>().SingleOrDefault(p => p["progressKey"]?.GetValue<string>() == "gacha_draw");
        if (counter == null) progress.Add(new JsonObject { ["progressKey"] = "gacha_draw", ["value"] = request.Count });
        else counter["value"] = checked((counter["value"]?.GetValue<int>() ?? 0) + request.Count);
        string updated = save.ToJsonString(GachaJson.Options);
        if (Encoding.UTF8.GetByteCount(updated) > settings.MaxSaveBytes) throw new GachaFailure("gacha_save_too_large", 400);
        long nextRevision = checked(revision + 1);
        var operation = new GachaOperation(request.RequestId, request.GachaKey, request.Count,
            request.ConfigVersion, nextRevision, timestamp, results);
        command.Parameters.AddWithValue("$json", updated);
        command.Parameters.AddWithValue("$revision", nextRevision);
        command.Parameters.AddWithValue("$time", timestamp);
        command.Parameters.AddWithValue("$pity", pity.Canonical());
        command.Parameters.AddWithValue("$nextId", nextId);
        command.Parameters.AddWithValue("$request", request.RequestId.ToString("D"));
        command.Parameters.AddWithValue("$hash", request.Fingerprint());
        command.Parameters.AddWithValue("$operation", JsonSerializer.Serialize(operation, GachaJson.Options));
        command.CommandText = """
            UPDATE saves SET save_json=$json,revision=$revision,updated_at_utc=$time WHERE player_id=$player;
            INSERT INTO gacha_player_state VALUES($player,$pity,$nextId)
                ON CONFLICT(player_id) DO UPDATE SET pity_json=$pity,next_instance_id=$nextId;
            INSERT INTO gacha_receipts VALUES($player,$request,$hash,$operation);
            """;
        command.ExecuteNonQuery();
        // Rewards, pity and the durable result commit together; a dropped HTTP reply cannot reroll them.
        transaction.Commit();
        return new GachaDrawReply(operation, new SaveSnapshot(updated, nextRevision, timestamp), false);
    }

    static bool AcceptGachaPity(SqliteCommand command, string json)
    {
        command.CommandText = "SELECT pity_json FROM gacha_player_state WHERE player_id=$player;";
        object? stored = command.ExecuteScalar();
        if (stored == null) return true;
        try
        {
            var root = JsonNode.Parse(json) as JsonObject;
            var incoming = GachaPity.Parse(root?["gacha"]?.ToJsonString() ?? "{}");
            return incoming.Canonical() == (string)stored;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or GachaFailure)
        {
            return false;
        }
    }
}

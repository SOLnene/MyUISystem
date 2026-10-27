using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace CloudSaveServer;

internal sealed partial class SaveStore
{
    static void InitializeWeaponEnhanceSchema(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS weapon_enhance_receipts (
                player_id TEXT NOT NULL REFERENCES saves(player_id),request_id TEXT NOT NULL,
                request_hash TEXT NOT NULL,operation_json TEXT NOT NULL,PRIMARY KEY(player_id,request_id));
            """;
        command.ExecuteNonQuery();
    }

    internal WeaponEnhanceReply? FindWeaponEnhancement(string player, Guid requestId, string? fingerprint = null)
    {
        using var connection = Open(); using var transaction = connection.BeginTransaction(deferred: true);
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.Parameters.AddWithValue("$player", player); command.Parameters.AddWithValue("$request", requestId.ToString("D"));
        var reply = ReadWeaponEnhanceReceipt(command, fingerprint); transaction.Commit(); return reply;
    }

    static WeaponEnhanceReply? ReadWeaponEnhanceReceipt(SqliteCommand command, string? fingerprint)
    {
        command.CommandText = """
            SELECT r.request_hash,r.operation_json,s.save_json,s.revision,s.updated_at_utc
            FROM weapon_enhance_receipts r JOIN saves s ON r.player_id=s.player_id
            WHERE r.player_id=$player AND r.request_id=$request;
            """;
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;
        if (fingerprint != null && fingerprint != reader.GetString(0)) throw new WeaponEnhanceFailure("request_id_reused");
        var operation = JsonSerializer.Deserialize<WeaponEnhanceOperation>(reader.GetString(1), GachaJson.Options)
            ?? throw new InvalidDataException("Invalid weapon enhancement receipt.");
        return new(operation, new(reader.GetString(2), reader.GetInt64(3), reader.GetString(4)), true);
    }

    internal WeaponEnhanceReply EnhanceWeapon(string player, WeaponEnhanceRequest request, WeaponEnhanceConfig config)
    {
        using var connection = Open(); using var transaction = connection.BeginTransaction(deferred: false);
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.Parameters.AddWithValue("$player", player); command.Parameters.AddWithValue("$request", request.RequestId.ToString("D"));
        var replay = ReadWeaponEnhanceReceipt(command, request.Fingerprint());
        if (replay != null) return replay;
        command.CommandText = "SELECT save_json,revision FROM saves WHERE player_id=$player;";
        JsonObject save; long revision;
        using (var reader = command.ExecuteReader())
        {
            if (!reader.Read()) throw new WeaponEnhanceFailure("enhance_save_required");
            save = JsonNode.Parse(reader.GetString(0))!.AsObject(); revision = reader.GetInt64(1);
        }
        if (revision != request.ExpectedRevision) throw new WeaponEnhanceFailure("save_conflict", revision: revision);
        if (save["version"]?.GetValue<int>() != 8) throw new WeaponEnhanceFailure("enhance_save_upgrade_required");
        try
        {
            var rules = config.Rules;
            if (request.MaterialInstanceIds.Length > rules.MaxMaterials) throw new WeaponEnhanceFailure("enhance_material_limit");
            var inventory = save["inventory"]!.AsObject();
            var equips = inventory["equips"]!.AsArray();
            var byId = equips.Select(e => e!.AsObject()).ToDictionary(e => e["instanceId"]!.GetValue<long>());
            if (!byId.TryGetValue(request.WeaponInstanceId, out var target)) throw new WeaponEnhanceFailure("enhance_weapon_missing");
            int id = target["itemId"]!.GetValue<int>(), oldLevel = target["level"]!.GetValue<int>(), oldExp = target["exp"]!.GetValue<int>();
            int rank = target["rank"]!.GetValue<int>();
            var equipped = save["characters"]!["characters"]!.AsArray()
                .Select(c => c!["equippedWeaponInstanceId"]!.GetValue<long>()).ToHashSet();
            int materialExp = 0;
            foreach (long materialId in request.MaterialInstanceIds)
            {
                if (!byId.TryGetValue(materialId, out var material)) throw new WeaponEnhanceFailure("enhance_material_missing");
                if (equipped.Contains(materialId)) throw new WeaponEnhanceFailure("enhance_material_equipped");
                materialExp = checked(materialExp + rules.MaterialExp(material["itemId"]!.GetValue<int>(),
                    material["level"]!.GetValue<int>(), material["exp"]!.GetValue<int>(), material["rank"]!.GetValue<int>()));
            }
            var prediction = rules.Preview(id, oldLevel, oldExp, rank, materialExp);
            if (prediction.GainedExp <= 0) throw new WeaponEnhanceFailure("enhance_level_limit");
            int cost = checked(prediction.GainedExp * rules.GoldPerExp);
            var gold = save["currencies"]!["items"]!.AsArray().SingleOrDefault(c => c!["itemId"]!.GetValue<int>() == rules.GoldItemId);
            int balance = gold?["amount"]?.GetValue<int>() ?? 0;
            if (balance < cost) throw new WeaponEnhanceFailure("enhance_insufficient_gold");
            if (gold != null) gold["amount"] = balance - cost;
            target["level"] = prediction.Level; target["exp"] = prediction.Exp;
            foreach (long materialId in request.MaterialInstanceIds) equips.Remove(byId[materialId]);
            if (inventory["unseenEquipInstanceIds"] is JsonArray unseen)
                foreach (var node in unseen.Where(n => request.MaterialInstanceIds.Contains(n!.GetValue<long>())).ToArray()) unseen.Remove(node);
            var progress = save["achievements"]!["progress"]!.AsArray();
            var counter = progress.SingleOrDefault(p => p!["progressKey"]!.GetValue<string>() == "weapon_enhance");
            if (counter == null) progress.Add(new JsonObject { ["progressKey"] = "weapon_enhance", ["value"] = 1 });
            else counter["value"] = checked(counter["value"]!.GetValue<int>() + 1);
            string timestamp = DateTimeOffset.UtcNow.ToString("O"); save["savedAtUtc"] = timestamp;
            string json = save.ToJsonString(GachaJson.Options);
            if (Encoding.UTF8.GetByteCount(json) > settings.MaxSaveBytes) throw new WeaponEnhanceFailure("enhance_save_too_large", 400);
            long next = checked(revision + 1);
            var operation = new WeaponEnhanceOperation(request.RequestId, request.WeaponInstanceId, request.MaterialInstanceIds,
                config.ConfigVersion, next, oldLevel, oldExp, prediction.Level, prediction.Exp, prediction.GainedExp, cost);
            command.Parameters.AddWithValue("$json", json); command.Parameters.AddWithValue("$next", next);
            command.Parameters.AddWithValue("$time", timestamp); command.Parameters.AddWithValue("$hash", request.Fingerprint());
            command.Parameters.AddWithValue("$operation", JsonSerializer.Serialize(operation, GachaJson.Options));
            command.CommandText = """
                UPDATE saves SET save_json=$json,revision=$next,updated_at_utc=$time WHERE player_id=$player;
                INSERT INTO weapon_enhance_receipts VALUES($player,$request,$hash,$operation);
                """;
            command.ExecuteNonQuery(); transaction.Commit();
            return new(operation, new(json, next, timestamp), false);
        }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or NullReferenceException or KeyNotFoundException)
        { throw new WeaponEnhanceFailure("invalid_enhance_save", 400); }
    }
}

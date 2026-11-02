using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace CloudSaveServer;

internal sealed partial class SaveStore
{
    static readonly ProgressionSettlement<WeaponEnhanceOperation, WeaponEnhanceReply> WeaponEnhanceSettlement = new(
        "weapon_enhance_receipts", "enhance",
        (code, status, revision) => new WeaponEnhanceFailure(code, status, revision),
        (operation, state, replayed) => new(operation, state, replayed));

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
        => FindProgression(player, requestId, fingerprint, WeaponEnhanceSettlement);

    internal WeaponEnhanceReply EnhanceWeapon(string player, WeaponEnhanceRequest request, WeaponEnhanceConfig config)
    {
        return SettleProgression(player, request.RequestId, request.Fingerprint(), request.ExpectedRevision, WeaponEnhanceSettlement, (save, next) =>
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
            return new WeaponEnhanceOperation(request.RequestId, request.WeaponInstanceId, request.MaterialInstanceIds,
                config.ConfigVersion, next, oldLevel, oldExp, prediction.Level, prediction.Exp, prediction.GainedExp, cost);
        });
    }
}

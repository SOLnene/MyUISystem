using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace CloudSaveServer;

internal sealed partial class SaveStore
{
    static readonly ProgressionSettlement<WeaponRefineOperation, WeaponRefineReply> WeaponRefineSettlement = new(
        "weapon_refine_receipts", "refine",
        (code, status, revision) => new WeaponRefineFailure(code, status, revision),
        (operation, state, replayed) => new(operation, state, replayed));

    static void InitializeWeaponRefineSchema(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS weapon_refine_receipts (
                player_id TEXT NOT NULL REFERENCES saves(player_id),request_id TEXT NOT NULL,
                request_hash TEXT NOT NULL,operation_json TEXT NOT NULL,PRIMARY KEY(player_id,request_id));
            """;
        command.ExecuteNonQuery();
    }

    internal WeaponRefineReply? FindWeaponRefinement(string player, Guid requestId, string? fingerprint = null)
        => FindProgression(player, requestId, fingerprint, WeaponRefineSettlement);

    internal WeaponRefineReply RefineWeapon(string player, WeaponRefineRequest request, WeaponRefineConfig config)
    {
        return SettleProgression(player, request.RequestId, request.Fingerprint(), request.ExpectedRevision, WeaponRefineSettlement, (save, next) =>
        {
            var rules = config.Rules;
            if (request.MaterialInstanceIds.Length > rules.MaxMaterials) throw new WeaponRefineFailure("refine_material_limit");
            var inventory = save["inventory"]!.AsObject();
            var equips = inventory["equips"]!.AsArray();
            var byId = equips.Select(e => e!.AsObject()).ToDictionary(e => e["instanceId"]!.GetValue<long>());
            if (!byId.TryGetValue(request.WeaponInstanceId, out var target)) throw new WeaponRefineFailure("refine_weapon_missing");
            int id = target["itemId"]!.GetValue<int>(), oldRefinement = target["refinementLevel"]!.GetValue<int>();
            rules.Weapon(id);
            if (oldRefinement < 1 || oldRefinement > rules.RefinementCap) throw new WeaponRefineFailure("invalid_refine_save", 400);
            if (oldRefinement == rules.RefinementCap) throw new WeaponRefineFailure("refine_level_limit");
            if (request.MaterialInstanceIds.Length > rules.RefinementCap - oldRefinement)
                throw new WeaponRefineFailure("refine_material_limit");
            var equipped = save["characters"]!["characters"]!.AsArray()
                .Select(c => c!["equippedWeaponInstanceId"]!.GetValue<long>()).ToHashSet();
            foreach (long materialId in request.MaterialInstanceIds)
            {
                if (!byId.TryGetValue(materialId, out var material)) throw new WeaponRefineFailure("refine_material_missing");
                if (material["itemId"]!.GetValue<int>() != id) throw new WeaponRefineFailure("refine_material_mismatch");
                if (equipped.Contains(materialId)) throw new WeaponRefineFailure("refine_material_equipped");
                int materialRefinement = material["refinementLevel"]!.GetValue<int>();
                if (materialRefinement < 1 || materialRefinement > rules.RefinementCap)
                    throw new WeaponRefineFailure("invalid_refine_save", 400);
            }
            int cost = rules.PreviewCost(id, oldRefinement, request.MaterialInstanceIds.Length);
            var gold = save["currencies"]!["items"]!.AsArray().SingleOrDefault(c => c!["itemId"]!.GetValue<int>() == rules.GoldItemId);
            int balance = gold?["amount"]?.GetValue<int>() ?? 0;
            if (balance < cost) throw new WeaponRefineFailure("refine_insufficient_gold");
            if (gold != null) gold["amount"] = balance - cost;
            int refinement = oldRefinement + request.MaterialInstanceIds.Length;
            target["refinementLevel"] = refinement;
            foreach (long materialId in request.MaterialInstanceIds) equips.Remove(byId[materialId]);
            if (inventory["unseenEquipInstanceIds"] is JsonArray unseen)
                foreach (var node in unseen.Where(n => request.MaterialInstanceIds.Contains(n!.GetValue<long>())).ToArray()) unseen.Remove(node);
            return new WeaponRefineOperation(request.RequestId, request.WeaponInstanceId, request.MaterialInstanceIds,
                config.ConfigVersion, next, oldRefinement, refinement, cost);
        });
    }
}

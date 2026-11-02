using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace CloudSaveServer;

internal sealed partial class SaveStore
{
    static readonly ProgressionSettlement<CharacterEnhanceOperation, CharacterEnhanceReply> CharacterEnhanceSettlement = new(
        "character_enhance_receipts", "character_enhance",
        (code, status, revision) => new CharacterEnhanceFailure(code, status, revision),
        (operation, state, replayed) => new(operation, state, replayed));

    static void InitializeCharacterEnhanceSchema(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS character_enhance_receipts (
                player_id TEXT NOT NULL REFERENCES saves(player_id),request_id TEXT NOT NULL,
                request_hash TEXT NOT NULL,operation_json TEXT NOT NULL,PRIMARY KEY(player_id,request_id));
            """;
        command.ExecuteNonQuery();
    }

    internal CharacterEnhanceReply? FindCharacterEnhancement(string player, Guid requestId, string? fingerprint = null)
        => FindProgression(player, requestId, fingerprint, CharacterEnhanceSettlement);

    internal CharacterEnhanceReply EnhanceCharacter(string player, CharacterEnhanceRequest request, CharacterEnhanceConfig config)
    {
        return SettleProgression(player, request.RequestId, request.Fingerprint(), request.ExpectedRevision, CharacterEnhanceSettlement, (save, next) =>
        {
            var rules = config.Rules;
            if (request.Books.Sum(b => (long)b.Count) > rules.MaxBookCount) throw new CharacterEnhanceFailure("character_enhance_material_limit");
            var target = save["characters"]!["characters"]!.AsArray().SingleOrDefault(c => c!["characterKey"]!.GetValue<string>() == request.CharacterKey);
            if (target == null) throw new CharacterEnhanceFailure("character_enhance_character_missing");
            int oldLevel = target["level"]!.GetValue<int>(), oldExp = target["exp"]!.GetValue<int>(), rank = target["rank"]!.GetValue<int>();
            var totals = rules.BookTotals(request.Books);
            var predicted = rules.Preview(request.CharacterKey, oldLevel, oldExp, rank, totals.Experience);
            if (predicted.GainedExp <= 0) throw new CharacterEnhanceFailure("character_enhance_level_limit");
            var inventory = save["inventory"]!.AsObject();
            var stacks = inventory["stacks"]!.AsArray();
            var byId = stacks.ToDictionary(s => s!["itemId"]!.GetValue<int>());
            foreach (var book in request.Books)
                if (!byId.TryGetValue(book.ItemId, out var stack) || stack!["count"]!.GetValue<int>() < book.Count)
                    throw new CharacterEnhanceFailure("character_enhance_insufficient_books");
            var gold = save["currencies"]!["items"]!.AsArray().SingleOrDefault(c => c!["itemId"]!.GetValue<int>() == rules.GoldItemId);
            int balance = gold?["amount"]?.GetValue<int>() ?? 0;
            if (balance < 0) throw new InvalidDataException("Invalid gold balance.");
            if (balance < totals.GoldCost) throw new CharacterEnhanceFailure("character_enhance_insufficient_gold");
            if (gold != null) gold["amount"] = balance - totals.GoldCost;
            foreach (var book in request.Books)
            {
                var stack = byId[book.ItemId]!;
                int count = stack["count"]!.GetValue<int>() - book.Count;
                if (count > 0) stack["count"] = count;
                else
                {
                    stacks.Remove(stack);
                    if (inventory["unseenMaterialIds"] is JsonArray unseen)
                        foreach (var node in unseen.Where(n => n!.GetValue<int>() == book.ItemId).ToArray()) unseen.Remove(node);
                }
            }
            target["level"] = predicted.Level; target["exp"] = predicted.Exp;
            var progress = save["achievements"]!["progress"]!.AsArray();
            var counter = progress.SingleOrDefault(p => p!["progressKey"]!.GetValue<string>() == "character_enhance");
            if (counter == null) progress.Add(new JsonObject { ["progressKey"] = "character_enhance", ["value"] = 1 });
            else counter["value"] = checked(counter["value"]!.GetValue<int>() + 1);
            return new CharacterEnhanceOperation(request.RequestId, request.CharacterKey, request.Books, config.ConfigVersion,
                next, oldLevel, oldExp, predicted.Level, predicted.Exp, predicted.GainedExp, totals.GoldCost);
        });
    }
}

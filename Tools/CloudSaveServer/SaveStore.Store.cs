using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace CloudSaveServer;

internal sealed partial class SaveStore
{
    static void InitializeStoreSchema(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS store_players (player_id TEXT PRIMARY KEY REFERENCES saves(player_id));
            CREATE TABLE IF NOT EXISTS store_purchases (
                player_id TEXT NOT NULL REFERENCES saves(player_id), store_item_id INTEGER NOT NULL,
                period_key TEXT NOT NULL, purchased_count INTEGER NOT NULL CHECK(purchased_count>=0),
                PRIMARY KEY(player_id,store_item_id));
            CREATE TABLE IF NOT EXISTS store_receipts (
                player_id TEXT NOT NULL REFERENCES saves(player_id),request_id TEXT NOT NULL,
                request_hash TEXT NOT NULL,operation_json TEXT NOT NULL,PRIMARY KEY(player_id,request_id));
            """;
        command.ExecuteNonQuery();
    }

    static (JsonObject Save, long Revision) ReadStoreSave(SqliteCommand command)
    {
        command.CommandText = "SELECT save_json,revision FROM saves WHERE player_id=$player;";
        using var reader = command.ExecuteReader();
        if (!reader.Read()) throw new StoreFailure("store_save_required");
        var save = JsonNode.Parse(reader.GetString(0)) as JsonObject;
        if (save?["version"]?.GetValue<int>() != 8) throw new StoreFailure("store_save_upgrade_required");
        return (save!, reader.GetInt64(1));
    }

    static Dictionary<int, int> StoreCounts(SqliteCommand command, JsonObject save, StoreDefinition config, StorePeriod period)
    {
        command.CommandText = "INSERT OR IGNORE INTO store_players VALUES($player);";
        bool migrate = command.ExecuteNonQuery() == 1;
        if (migrate)
        {
            // Import legacy counters once. Later ordinary saves never become the authority for purchase limits.
            var records = StoreSave.Array(StoreSave.Section(save, "store"), "records");
            foreach (var record in records.OfType<JsonObject>())
            {
                if (record["periodKey"]?.GetValue<string>() != period.Key) continue;
                int id = record["storeItemId"]?.GetValue<int>() ?? 0;
                int count = record["purchasedCount"]?.GetValue<int>() ?? -1;
                if (id <= 0 || count < 0) throw new StoreFailure("invalid_store_save", 400);
                command.CommandText = "INSERT OR IGNORE INTO store_purchases VALUES($player,$id,$period,$count);";
                command.Parameters.AddWithValue("$id", id); command.Parameters.AddWithValue("$period", period.Key);
                command.Parameters.AddWithValue("$count", count); command.ExecuteNonQuery();
                command.Parameters.RemoveAt("$id"); command.Parameters.RemoveAt("$period"); command.Parameters.RemoveAt("$count");
            }
        }
        var result = new Dictionary<int, int>();
        command.CommandText = "SELECT store_item_id,purchased_count FROM store_purchases WHERE player_id=$player AND period_key=$key;";
        command.Parameters.AddWithValue("$key", period.Key);
        using (var reader = command.ExecuteReader())
            while (reader.Read()) result.Add(reader.GetInt32(0), reader.GetInt32(1));
        command.Parameters.RemoveAt("$key");
        return result;
    }

    internal StoreSnapshot ReadStore(string player, StoreDefinition config, DateTimeOffset now)
    {
        using var connection = Open(); using var transaction = connection.BeginTransaction(deferred: false);
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.Parameters.AddWithValue("$player", player);
        var (save, _) = ReadStoreSave(command);
        var period = config.Period(now);
        var counts = StoreCounts(command, save, config, period);
        var listings = config.Configuration.Items.Where(i => i.Enabled).OrderBy(i => i.Category).ThenBy(i => i.Order)
            .Select(i => new StoreListing(i, Math.Max(0, i.DailyPurchaseLimit - counts.GetValueOrDefault(i.StoreItemId)))).ToList();
        transaction.Commit();
        return new(config.Token, now, period.NextRefreshAt, period.Key, listings);
    }

    internal StorePurchaseReply? FindStorePurchase(string player, Guid id, string? fingerprint = null)
    {
        using var connection = Open(); using var transaction = connection.BeginTransaction(deferred: true);
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.Parameters.AddWithValue("$player", player); command.Parameters.AddWithValue("$request", id.ToString("D"));
        var reply = ReadStoreReceipt(command, fingerprint); transaction.Commit(); return reply;
    }

    static StorePurchaseReply? ReadStoreReceipt(SqliteCommand command, string? fingerprint)
    {
        command.CommandText = """
            SELECT r.request_hash,r.operation_json,s.save_json,s.revision,s.updated_at_utc
            FROM store_receipts r JOIN saves s ON r.player_id=s.player_id
            WHERE r.player_id=$player AND r.request_id=$request;
            """;
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;
        if (fingerprint != null && reader.GetString(0) != fingerprint) throw new StoreFailure("request_id_reused");
        var operation = JsonSerializer.Deserialize<StoreOperation>(reader.GetString(1), GachaJson.Options)
            ?? throw new InvalidDataException("Invalid store receipt.");
        return new(operation, new(reader.GetString(2), reader.GetInt64(3), reader.GetString(4)), true);
    }

    internal StorePurchaseReply PurchaseStore(string player, StorePurchaseRequest request, StoreDefinition config,
        Dictionary<int, StoreItemDefinition> catalog, DateTimeOffset now)
    {
        using var connection = Open(); using var transaction = connection.BeginTransaction(deferred: false);
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.Parameters.AddWithValue("$player", player); command.Parameters.AddWithValue("$request", request.RequestId.ToString("D"));
        var replay = ReadStoreReceipt(command, request.Fingerprint());
        if (replay != null) return replay;
        var (save, revision) = ReadStoreSave(command);
        if (revision != request.ExpectedRevision) throw new StoreFailure("save_conflict", revision: revision);
        var offer = config.Configuration.Items.SingleOrDefault(i => i.StoreItemId == request.StoreItemId && i.Enabled)
            ?? throw new StoreFailure("store_item_unavailable");
        var period = config.Period(now);
        var counts = StoreCounts(command, save, config, period);
        int prior = counts.GetValueOrDefault(offer.StoreItemId);
        if (request.Count > offer.DailyPurchaseLimit - prior) throw new StoreFailure("store_limit_exceeded");
        int price = checked(offer.Price * request.Count), amount = checked(offer.Count * request.Count);
        var balances = StoreSave.Balances(save);
        if (!balances.TryGetValue(offer.CostItemId, out var currency) || currency["amount"]!.GetValue<int>() < price)
            throw new StoreFailure("store_insufficient_currency");
        currency["amount"] = currency["amount"]!.GetValue<int>() - price;
        StoreSave.Grant(save, catalog[offer.ItemId], amount);
        counts[offer.StoreItemId] = checked(prior + request.Count);
        save["store"] = new JsonObject { ["records"] = new JsonArray(counts.Select(p => (JsonNode)new JsonObject
            { ["storeItemId"] = p.Key, ["periodKey"] = period.Key, ["purchasedCount"] = p.Value }).ToArray()) };
        var progress = StoreSave.Array(StoreSave.Section(save, "achievements"), "progress");
        var counter = progress.OfType<JsonObject>().SingleOrDefault(p => p["progressKey"]?.GetValue<string>() == "store_purchase");
        if (counter == null) progress.Add(new JsonObject { ["progressKey"] = "store_purchase", ["value"] = request.Count });
        else counter["value"] = checked((counter["value"]?.GetValue<int>() ?? 0) + request.Count);
        string timestamp = now.ToString("O"); save["savedAtUtc"] = timestamp;
        string json = save.ToJsonString(GachaJson.Options);
        if (Encoding.UTF8.GetByteCount(json) > settings.MaxSaveBytes) throw new StoreFailure("store_save_too_large", 400);
        long next = checked(revision + 1);
        var operation = new StoreOperation(request.RequestId, offer.StoreItemId, request.Count, config.Token,
            next, offer.ItemId, amount, offer.CostItemId, price);
        command.Parameters.AddWithValue("$id", offer.StoreItemId); command.Parameters.AddWithValue("$key", period.Key);
        command.Parameters.AddWithValue("$count", counts[offer.StoreItemId]); command.Parameters.AddWithValue("$json", json);
        command.Parameters.AddWithValue("$next", next); command.Parameters.AddWithValue("$time", timestamp);
        command.Parameters.AddWithValue("$hash", request.Fingerprint());
        command.Parameters.AddWithValue("$operation", JsonSerializer.Serialize(operation, GachaJson.Options));
        command.CommandText = """
            INSERT INTO store_purchases VALUES($player,$id,$key,$count)
                ON CONFLICT(player_id,store_item_id) DO UPDATE SET period_key=$key,purchased_count=$count;
            UPDATE saves SET save_json=$json,revision=$next,updated_at_utc=$time WHERE player_id=$player;
            INSERT INTO store_receipts VALUES($player,$request,$hash,$operation);
            """;
        command.ExecuteNonQuery(); transaction.Commit();
        return new(operation, new(json, next, timestamp), false);
    }
}

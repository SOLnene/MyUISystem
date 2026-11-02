using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace CloudSaveServer;

internal sealed partial class SaveStore
{
    static readonly ProgressionSettlement<PromoteOperation, PromoteReply> PromoteSettlement = new(
        "promote_receipts", "promote",
        (code, status, revision) => new PromoteFailure(code, status, revision),
        (operation, state, replayed) => new(operation, state, replayed));

    static void InitializePromoteSchema(SqliteConnection connection,SqliteTransaction transaction)
    {
        using var command=connection.CreateCommand();command.Transaction=transaction;
        command.CommandText="""
            CREATE TABLE IF NOT EXISTS promote_receipts (
                player_id TEXT NOT NULL REFERENCES saves(player_id),request_id TEXT NOT NULL,
                request_hash TEXT NOT NULL,operation_json TEXT NOT NULL,PRIMARY KEY(player_id,request_id));
            """;
        command.ExecuteNonQuery();
    }
    internal PromoteReply? FindPromotion(string player, Guid requestId, string? fingerprint = null)
        => FindProgression(player, requestId, fingerprint, PromoteSettlement);

    internal PromoteReply Promote(string player,PromoteRequest request,PromoteConfig config)
    {
        return SettleProgression(player, request.RequestId, request.Fingerprint(), request.ExpectedRevision, PromoteSettlement, (save, next) =>
        {
            // Only target lookup and EXP policy differ. Both targets share validation, consumption and one commit.
            bool character=request.Target.Kind=="character";
            var target=character?save["characters"]!["characters"]!.AsArray().SingleOrDefault(c=>c!["characterKey"]!.GetValue<string>()==request.Target.CharacterKey)
                :save["inventory"]!["equips"]!.AsArray().SingleOrDefault(w=>w!["instanceId"]!.GetValue<long>()==request.Target.WeaponInstanceId);
            if(target==null)throw new PromoteFailure("promote_target_missing");
            int level=target["level"]!.GetValue<int>(),rank=target["rank"]!.GetValue<int>(),exp=target["exp"]!.GetValue<int>();
            if(rank<0||rank>6||level<1||level>config.Rules.RankMaxLevels[rank]||exp<0)throw new InvalidDataException("Invalid promotion state.");
            if(rank==6)throw new PromoteFailure("promote_rank_limit");
            if(level!=config.Rules.RankMaxLevels[rank])throw new PromoteFailure("promote_level_required");
            var resolved=config.Rules.Resolve(request.Target,character?0:target["itemId"]!.GetValue<int>(),rank);
            var inventory=save["inventory"]!.AsObject();var stacks=inventory["stacks"]!.AsArray();
            var byId=stacks.ToDictionary(s=>s!["itemId"]!.GetValue<int>());
            foreach(var material in resolved.Rule.Materials)
                if(!byId.TryGetValue(material.ItemId,out var stack)||stack!["count"]!.GetValue<int>()<material.Count)
                    throw new PromoteFailure("promote_insufficient_materials");
            var gold=save["currencies"]!["items"]!.AsArray().SingleOrDefault(c=>c!["itemId"]!.GetValue<int>()==config.Rules.GoldItemId);
            int balance=gold?["amount"]?.GetValue<int>()??0;
            if(balance<resolved.GoldCost)throw new PromoteFailure("promote_insufficient_gold");
            foreach(var material in resolved.Rule.Materials)
            {
                var stack=byId[material.ItemId]!;int count=stack["count"]!.GetValue<int>()-material.Count;
                if(count>0)stack["count"]=count;
                else
                {
                    stacks.Remove(stack);
                    if(inventory["unseenMaterialIds"] is JsonArray unseen)
                        foreach(var node in unseen.Where(n=>n!.GetValue<int>()==material.ItemId).ToArray())unseen.Remove(node);
                }
            }
            if(gold!=null)gold["amount"]=balance-resolved.GoldCost;
            target["rank"]=rank+1;target["exp"]=character?exp:0;
            return new PromoteOperation(request.RequestId,request.Target,config.ConfigVersion,next,level,rank,rank+1,exp,character?exp:0,resolved.GoldCost,resolved.Rule.Materials);
        });
    }
}

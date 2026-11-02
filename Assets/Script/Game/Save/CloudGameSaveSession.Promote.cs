using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Domain.Promote;
using Newtonsoft.Json;

internal sealed partial class CloudGameSaveSession
{
    internal CloudPromoteConfig PromoteConfig { get; private set; }
    internal bool HasPendingPromote => pendingProgression?.Kind == ProgressionKind.Promote;
    internal CloudPromoteRequest PendingPromote => HasPendingPromote ? (CloudPromoteRequest)pendingProgression.Request : null;
    internal async UniTask LoadPromoteConfigAsync(CancellationToken token)
    {
        RequireOwner(); var config = await client.LoadPromoteConfigAsync(PlayerId, token);
        await UniTask.SwitchToMainThread(token); RequireOwner(); PromoteConfig = config;
    }
    internal void BeginPromotion(PromotionTarget target)
    {
        RequireOwner(); RequireNoPendingStore(); RequireNoPendingProgression();
        if (HasConflict || !IsReady || IsSaving || HasPendingSave || HasPendingGacha || PromoteConfig == null)
            throw new InvalidOperationException("Promotion session is not ready.");
        var request = new CloudPromoteRequest { requestId = Guid.NewGuid().ToString("D"), configVersion = PromoteConfig.configVersion,
            expectedRevision = revision, target = new PromotionTarget { Kind = target.Kind, CharacterKey = target.CharacterKey, WeaponInstanceId = target.WeaponInstanceId } };
        if (!request.IsValid) throw new InvalidOperationException("Invalid promotion target.");
        BeginProgression(ProgressionKind.Promote, request);
    }
    internal UniTask<CloudPromoteReply> ResolvePromotionAsync(CancellationToken token)
    {
        RequireOwner(); var request = PendingPromote ?? throw new InvalidOperationException("No pending promotion.");
        return ResolveProgressionAsync(ProgressionKind.Promote, "promote_request_not_found",
            ct => client.SendPromotionAsync(PlayerId, request, false, ct), ct => client.SendPromotionAsync(PlayerId, request, true, ct), token);
    }
    internal void AcceptPromotion(CloudPromoteReply reply) => AcceptProgression(ProgressionKind.Promote, reply.operation.requestId, reply.state);
    static CloudPromoteRequest ReadPromotionRequest(string json)
    {
        var request = JsonConvert.DeserializeObject<CloudPromoteRequest>(json);
        if (request?.IsValid != true) throw new InvalidDataException("Invalid promotion journal; preserved for recovery.");
        return request;
    }
}

using System;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Domain.Enhance;
using Newtonsoft.Json;

internal sealed partial class CloudGameSaveSession
{
    internal CloudCharacterEnhanceConfig CharacterEnhanceConfig { get; private set; }
    internal bool HasPendingCharacterEnhance => pendingProgression?.Kind == ProgressionKind.CharacterEnhance;
    internal CloudCharacterEnhanceRequest PendingCharacterEnhance => HasPendingCharacterEnhance ? (CloudCharacterEnhanceRequest)pendingProgression.Request : null;

    internal async UniTask LoadCharacterEnhanceConfigAsync(CancellationToken token)
    {
        RequireOwner();
        var config = await client.LoadCharacterEnhanceConfigAsync(PlayerId, token);
        await UniTask.SwitchToMainThread(token); RequireOwner(); CharacterEnhanceConfig = config;
    }

    internal void BeginCharacterEnhancement(string characterKey, CharacterEnhanceBookSelection[] books)
    {
        RequireOwner(); RequireNoPendingStore(); RequireNoPendingProgression();
        if (HasConflict || !IsReady || IsSaving || HasPendingSave || HasPendingGacha || CharacterEnhanceConfig == null)
            throw new InvalidOperationException("Character enhancement session is not ready.");
        var request = new CloudCharacterEnhanceRequest { requestId = Guid.NewGuid().ToString("D"), characterKey = characterKey,
            books = books.Select(b => new CharacterEnhanceBookSelection { ItemId = b.ItemId, Count = b.Count }).ToArray(),
            expectedRevision = revision, configVersion = CharacterEnhanceConfig.configVersion };
        if (!request.IsValid) throw new InvalidOperationException("Invalid character enhancement selection.");
        // Persist before prediction or POST, using the same journal as the other progression operations.
        BeginProgression(ProgressionKind.CharacterEnhance, request);
    }

    internal UniTask<CloudCharacterEnhanceReply> ResolveCharacterEnhancementAsync(CancellationToken token)
    {
        RequireOwner();
        var request = PendingCharacterEnhance ?? throw new InvalidOperationException("No character enhancement is awaiting confirmation.");
        return ResolveProgressionAsync(ProgressionKind.CharacterEnhance, "character_enhance_request_not_found",
            ct => client.EnhanceCharacterAsync(PlayerId, request, ct),
            ct => client.FindCharacterEnhancementAsync(PlayerId, request, ct), token);
    }

    internal void AcceptCharacterEnhancement(CloudCharacterEnhanceReply reply)
    { AcceptProgression(ProgressionKind.CharacterEnhance, reply.operation.requestId, reply.state); }

    static CloudCharacterEnhanceRequest ReadCharacterEnhancementRequest(string json)
    {
        var request = JsonConvert.DeserializeObject<CloudCharacterEnhanceRequest>(json);
        if (request == null || !request.IsValid) throw new InvalidDataException("Invalid character enhancement journal; preserved for recovery.");
        return request;
    }
}

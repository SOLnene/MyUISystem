using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Domain.Enhance;
using Newtonsoft.Json;

internal sealed partial class CloudSaveHttpClient
{
    internal async UniTask<CloudCharacterEnhanceConfig> LoadCharacterEnhanceConfigAsync(string player, CancellationToken token)
    {
        using var response = await SendAsync(HttpMethod.Get, player, null, token, new Uri(saveUri, "/v1/characters/enhance/config"));
        string body = await response.Content.ReadAsStringAsync().AsUniTask(); EnsureSuccess(response, body);
        var config = JsonConvert.DeserializeObject<CloudCharacterEnhanceConfig>(body);
        if (config?.rules == null || config.configVersion?.Length != 64) throw new InvalidDataException("Invalid character enhancement config.");
        config.rules.Validate(); return config;
    }

    internal async UniTask<CloudCharacterEnhanceReply> EnhanceCharacterAsync(string player, CloudCharacterEnhanceRequest request, CancellationToken token)
    {
        using var response = await SendAsync(HttpMethod.Post, player, JsonConvert.SerializeObject(request), token,
            new Uri(saveUri, "/v1/characters/enhance"));
        string body = await response.Content.ReadAsStringAsync().AsUniTask(); EnsureSuccess(response, body);
        return ReadCharacterEnhanceReply(body, request);
    }

    internal async UniTask<CloudCharacterEnhanceReply> FindCharacterEnhancementAsync(string player, CloudCharacterEnhanceRequest request, CancellationToken token)
    {
        using var response = await SendAsync(HttpMethod.Get, player, null, token,
            new Uri(saveUri, "/v1/characters/enhance/requests/" + request.requestId));
        string body = await response.Content.ReadAsStringAsync().AsUniTask(); EnsureSuccess(response, body);
        return ReadCharacterEnhanceReply(body, request);
    }

    static CloudCharacterEnhanceReply ReadCharacterEnhanceReply(string body, CloudCharacterEnhanceRequest request)
    {
        var reply = JsonConvert.DeserializeObject<CloudCharacterEnhanceReply>(body);
        var op = reply?.operation;
        if (op == null || reply.state == null || op.requestId != request.requestId || op.characterKey != request.characterKey ||
            op.books == null || op.books.Length != request.books.Length ||
            !op.books.Zip(request.books, (a, b) => a != null && a.ItemId == b.ItemId && a.Count == b.Count).All(equal => equal) ||
            op.configVersion != request.configVersion || op.committedRevision != checked(request.expectedRevision + 1) ||
            reply.state.revision < op.committedRevision || op.level < op.oldLevel || op.exp < 0 || op.gainedExp <= 0 || op.goldCost < 0 ||
            string.IsNullOrWhiteSpace(reply.state.saveJson)) throw new InvalidDataException("Invalid character enhancement receipt.");
        return reply;
    }
}

[Serializable]
internal sealed class CloudCharacterEnhanceConfig
{
    public string configVersion;
    public CharacterEnhanceRules rules;
}
[Serializable]
internal sealed class CloudCharacterEnhanceRequest
{
    public string requestId, characterKey, configVersion;
    public CharacterEnhanceBookSelection[] books;
    public long expectedRevision;
    internal bool IsValid => Guid.TryParseExact(requestId, "D", out var id) && id != Guid.Empty &&
        !string.IsNullOrWhiteSpace(characterKey) && characterKey.Length <= 128 && configVersion?.Length == 64 &&
        expectedRevision > 0 && expectedRevision < long.MaxValue && books != null && books.Length > 0 && books.Length <= 3 &&
        books.All(b => b != null && b.ItemId >= 501 && b.ItemId <= 503 && b.Count > 0 && b.Count <= 100000) &&
        books.Select(b => b.ItemId).Distinct().Count() == books.Length;
}
[Serializable]
internal sealed class CloudCharacterEnhanceReply
{
    public CloudCharacterEnhanceOperation operation;
    public CloudSaveSnapshot state;
    public bool replayed;
}
[Serializable]
internal sealed class CloudCharacterEnhanceOperation
{
    public string requestId, characterKey, configVersion;
    public CharacterEnhanceBookSelection[] books;
    public long committedRevision;
    public int oldLevel, oldExp, level, exp, gainedExp, goldCost;
}

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CloudSaveServer;

internal sealed class ServerSettings
{
    public string ProjectId { get; set; } = "";
    public string EnvironmentId { get; set; } = "";
    public string DatabasePath { get; set; } = "data/saves.db";
    public int MaxSaveBytes { get; set; } = 1048576;

    internal void Validate(string contentRoot)
    {
        if (!Guid.TryParse(ProjectId, out _) || !Guid.TryParse(EnvironmentId, out _))
            throw new InvalidOperationException("Configure the Unity project and environment IDs.");
        if (string.IsNullOrWhiteSpace(DatabasePath) || MaxSaveBytes is < 1024 or > 1048576)
            throw new InvalidOperationException("Invalid database path or save size limit.");
        DatabasePath = Path.GetFullPath(DatabasePath, contentRoot);
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SaveWriteRequest(long? ExpectedRevision, Guid? RequestId, string? SaveJson)
{
    internal string? Validate(ServerSettings settings)
    {
        if (ExpectedRevision is null or < 0 or long.MaxValue)
            return "expectedRevision must be zero for creation, or the loaded revision for updates.";
        if (RequestId is null || RequestId == Guid.Empty)
            return "requestId must be a non-empty UUID retained across retries.";
        if (string.IsNullOrWhiteSpace(SaveJson))
            return "saveJson is required.";
        if (Encoding.UTF8.GetByteCount(SaveJson) > settings.MaxSaveBytes)
            return "saveJson exceeds the configured byte limit.";
        try
        {
            using var json = JsonDocument.Parse(SaveJson, new JsonDocumentOptions { MaxDepth = 64 });
            if (json.RootElement.ValueKind != JsonValueKind.Object ||
                !json.RootElement.TryGetProperty("version", out var version) ||
                version.ValueKind != JsonValueKind.Number ||
                !version.TryGetInt32(out var number) || number < 1)
                return "saveJson must be an object with a positive integer version.";
        }
        catch (JsonException)
        {
            return "saveJson is not valid JSON.";
        }
        return null;
    }
}

internal sealed record SaveSnapshot(string SaveJson, long Revision, string UpdatedAtUtc);
internal sealed record WriteReceipt(long Revision, string UpdatedAtUtc, bool Replayed);
internal sealed record ApiError(string Code, string Message, string TraceId, long? CurrentRevision = null);
internal sealed record StoreWriteResult(WriteReceipt? Receipt, string? ConflictCode, long? CurrentRevision);

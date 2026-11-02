using System;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

internal sealed partial class CloudGameSaveSession
{
    // Cloud accounts now upload client-authored saves. Legacy endpoints remain for recovering old journals only.
    internal static bool UsesServerSettlement => false;
    internal string ClientSaveBackupPath => Path.Combine(Application.persistentDataPath,
        "cloud-save-" + client.GachaJournalKey(PlayerId) + ".json");

    sealed class ClientSaveBackup
    {
        public int version = 1;
        public long revision;
        public string json, pendingJson;
        public SavedRequest inFlightRequest;
    }

    sealed class SavedRequest
    {
        public long expectedRevision;
        public string requestId, saveJson;
    }

    void PersistClientSave()
    {
        var backup = new ClientSaveBackup { revision = revision, json = Json,
            pendingJson = pendingJson, inFlightRequest = inFlightRequest == null ? null : new SavedRequest
                { expectedRevision = inFlightRequest.expectedRevision, requestId = inFlightRequest.requestId, saveJson = inFlightRequest.saveJson } };
        string path = ClientSaveBackupPath, temporary = path + ".tmp";
        byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(backup));
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
        if (File.Exists(path)) File.Replace(temporary, path, null);
        else File.Move(temporary, path);
    }

    async UniTask RestoreClientSaveAsync(CancellationToken token)
    {
        if (!File.Exists(ClientSaveBackupPath)) return;
        var backup = JsonConvert.DeserializeObject<ClientSaveBackup>(File.ReadAllText(ClientSaveBackupPath, Encoding.UTF8));
        if (backup == null || backup.version != 1 || backup.revision < 0)
            throw new InvalidDataException("Invalid local cloud-save backup; original file preserved.");
        if (backup.pendingJson == null && backup.inFlightRequest == null) return;
        long expected = backup.revision;
        if (backup.inFlightRequest != null)
        {
            // A lost reply may already have committed. Reuse its ID before considering any newer local snapshot.
            var request = new CloudSaveWriteRequest(backup.inFlightRequest.expectedRevision, backup.inFlightRequest.saveJson)
                { requestId = backup.inFlightRequest.requestId };
            var receipt = await client.SaveAsync(PlayerId, request, token);
            expected = receipt.revision;
            if (backup.pendingJson == backup.inFlightRequest.saveJson) backup.pendingJson = null;
            var latest = await client.LoadAsync(PlayerId, token)
                ?? throw new InvalidDataException("Cloud save disappeared during recovery.");
            revision = latest.revision;
            Json = latest.saveJson;
        }
        if (backup.pendingJson != null)
        {
            // Another device's newer save must not be silently replaced by an offline backup.
            if (revision != expected) throw new CloudSaveServerException(409, "save_conflict");
            pendingJson = backup.pendingJson;
            Json = pendingJson;
        }
        PersistClientSave();
    }

    internal UniTask<GachaConfigSnapshot> LoadClientGachaConfigAsync(CancellationToken token) =>
        client.LoadGachaConfigAsync(PlayerId, token);

    async UniTask LoadClientRulesAsync(CancellationToken token)
    {
        await UniTask.WhenAll(LoadEnhanceConfigAsync(token), LoadRefineConfigAsync(token),
            LoadCharacterEnhanceConfigAsync(token), LoadPromoteConfigAsync(token));
    }
}

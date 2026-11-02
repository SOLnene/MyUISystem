using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

internal sealed class ProgressionRequestRecord
{
    internal readonly ProgressionKind Kind;
    internal readonly object Request;
    internal readonly string RequestId, RequestJson;
    internal bool MayHaveBeenSent;

    internal ProgressionRequestRecord(ProgressionKind kind, object request, string requestId)
    { Kind = kind; Request = request; RequestId = requestId; RequestJson = JsonConvert.SerializeObject(request); }
}

internal sealed class ProgressionRequestJournal
{
    readonly string path, enhanceLegacyPath, refineLegacyPath;

    sealed class Envelope
    {
        public int version;
        public ProgressionKind kind;
        public string requestJson;
    }

    internal ProgressionRequestJournal(string path, string enhanceLegacyPath, string refineLegacyPath)
    { this.path = path; this.enhanceLegacyPath = enhanceLegacyPath; this.refineLegacyPath = refineLegacyPath; }

    internal ProgressionRequestRecord Read(Func<ProgressionKind, string, ProgressionRequestRecord> decode)
    {
        ProgressionRequestRecord pending = null;
        bool exists = File.Exists(path);
        if (exists)
        {
            var envelope = JsonConvert.DeserializeObject<Envelope>(File.ReadAllText(path, Encoding.UTF8));
            if (envelope?.version != 1 || string.IsNullOrWhiteSpace(envelope.requestJson))
                throw new InvalidDataException("Invalid progression journal envelope.");
            pending = decode(envelope.kind, envelope.requestJson);
        }
        MergeLegacy(enhanceLegacyPath, ProgressionKind.WeaponEnhance);
        MergeLegacy(refineLegacyPath, ProgressionKind.WeaponRefine);
        if (pending == null) return null;
        // A migrated request may already have committed. Never manufacture a new ID or predict again on login.
        if (!exists) Write(pending);
        File.Delete(enhanceLegacyPath); File.Delete(refineLegacyPath);
        pending.MayHaveBeenSent = true;
        return pending;

        void MergeLegacy(string legacyPath, ProgressionKind kind)
        {
            if (!File.Exists(legacyPath)) return;
            var legacy = decode(kind, File.ReadAllText(legacyPath, Encoding.UTF8));
            if (pending != null && (pending.Kind != legacy.Kind || pending.RequestId != legacy.RequestId || pending.RequestJson != legacy.RequestJson))
                throw new InvalidDataException("Multiple different progression requests require recovery; journals were preserved.");
            pending ??= legacy;
        }
    }

    internal void Write(ProgressionRequestRecord record)
    {
        if (File.Exists(path)) throw new InvalidOperationException("An unconfirmed progression journal already exists.");
        string temporary = path + ".tmp";
        byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new Envelope
            { version = 1, kind = record.Kind, requestJson = record.RequestJson }));
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
        // Move cannot overwrite a request from another session/process. The final file exists before any prediction or POST.
        File.Move(temporary, path);
    }

    internal void Clear() => File.Delete(path);
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

[Serializable]
internal sealed class GachaScheduleDocument
{
    internal const string BeijingTimeFormat = "yyyy-MM-dd HH:mm:ss";
    static readonly TimeSpan BeijingOffset = TimeSpan.FromHours(8);
    static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
    static readonly Regex KeyPattern = new Regex(@"\A[A-Za-z0-9_-]{1,64}\z", RegexOptions.CultureInvariant);

    [SerializeField] internal string SourcePath;
    [SerializeField] internal long Version;
    [SerializeField] internal List<Slot> Pools = new List<Slot>();
    [SerializeField] string loadedJson;

    [Serializable]
    internal sealed class Slot
    {
        [SerializeField] internal string Key;
        [SerializeField] internal bool Enabled;
        [SerializeField] internal string StartsAtBeijing = "";
        [SerializeField] internal string EndsAtBeijing = "";

        internal string Status(DateTimeOffset now)
        {
            try
            {
                var start = ParseBeijing(StartsAtBeijing);
                var end = ParseBeijing(EndsAtBeijing);
                if (start.HasValue && end.HasValue && start >= end) return "时间范围无效";
                if (!Enabled) return "未启用";
                if (start.HasValue && now < start) return "未开始";
                if (end.HasValue && now >= end) return "已结束";
                return "开放中";
            }
            catch (InvalidDataException) { return "时间格式无效"; }
        }
    }

    internal static GachaScheduleDocument Load(string path)
    {
        string json = ReadUtf8(path);
        var root = ReadObject(json);
        RequireProperties(root, "version", "pools");
        if (root["version"]?.Type != JTokenType.Integer || root["pools"] is not JArray slots)
            throw new InvalidDataException("排期必须包含整数 version 和 pools 列表。");
        var document = new GachaScheduleDocument
        {
            SourcePath = Path.GetFullPath(path), Version = root.Value<long>("version"), loadedJson = json
        };
        foreach (var token in slots)
        {
            if (token is not JObject slot) throw new InvalidDataException("排期列表中存在无效条目。");
            RequireProperties(slot, "gachaKey", "enabled", "startsAtUtc", "endsAtUtc");
            if (slot["gachaKey"]?.Type != JTokenType.String || slot["enabled"]?.Type != JTokenType.Boolean)
                throw new InvalidDataException("每个排期条目必须包含卡池键和启用开关。");
            document.Pools.Add(new Slot
            {
                Key = slot.Value<string>("gachaKey"), Enabled = slot.Value<bool>("enabled"),
                StartsAtBeijing = UtcToBeijing(slot["startsAtUtc"]),
                EndsAtBeijing = UtcToBeijing(slot["endsAtUtc"])
            });
        }
        document.Validate(false);
        return document;
    }

    internal void Validate(bool checkFiles = true)
    {
        if (Version <= 0 || Version == long.MaxValue || Pools == null || Pools.Count > 64)
            throw new InvalidDataException("排期版本或卡池数量无效；最多支持 64 个池。");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var slot in Pools)
        {
            if (slot == null || slot.Key == null || !KeyPattern.IsMatch(slot.Key) || !keys.Add(slot.Key))
                throw new InvalidDataException("排期存在无效或重复的卡池键。");
            try
            {
                var start = ParseBeijing(slot.StartsAtBeijing);
                var end = ParseBeijing(slot.EndsAtBeijing);
                if (start.HasValue && end.HasValue && start >= end)
                    throw new InvalidDataException("结束时间必须晚于开始时间。");
                if (checkFiles) ReadPoolKind(Path.GetDirectoryName(SourcePath), slot.Key);
            }
            catch (Exception error) when (error is IOException || error is JsonException)
            {
                throw new InvalidDataException(slot.Key + "：" + error.Message, error);
            }
        }
    }

    internal string Save()
    {
        Validate();
        // Preserve an external edit instead of silently replacing a newer schedule with this window's draft.
        if (!string.Equals(ReadUtf8(SourcePath), loadedJson, StringComparison.Ordinal))
            throw new InvalidOperationException("排期文件已被其他操作修改，请重新读取后再编辑。");
        long nextVersion = checked(Version + 1);
        string json = ToJson(nextVersion);
        string root = Path.GetDirectoryName(SourcePath);
        string backupDirectory = Path.Combine(root, "data", "backups");
        Directory.CreateDirectory(backupDirectory);
        string suffix = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N");
        string backup = Path.Combine(backupDirectory, "gacha-schedule-v" + Version + "-" + suffix + ".json");
        string temporary = Path.Combine(root, ".gacha-schedule-" + suffix + ".tmp");
        try
        {
            File.WriteAllText(temporary, json, Utf8);
            if (!string.Equals(ReadUtf8(SourcePath), loadedJson, StringComparison.Ordinal))
                throw new InvalidOperationException("保存期间排期文件发生变化，请重新读取。");
            // Replace on the same volume so a running server sees a complete old or new file; the old file becomes the backup.
            File.Replace(temporary, SourcePath, backup);
            Version = nextVersion;
            loadedJson = json;
            return backup;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    internal string ToJson(long version)
    {
        var slots = new JArray();
        foreach (var slot in Pools)
            slots.Add(new JObject
            {
                ["gachaKey"] = slot.Key, ["enabled"] = slot.Enabled,
                ["startsAtUtc"] = ToUtcToken(slot.StartsAtBeijing), ["endsAtUtc"] = ToUtcToken(slot.EndsAtBeijing)
            });
        return new JObject { ["version"] = version, ["pools"] = slots }.ToString(Formatting.Indented) + Environment.NewLine;
    }

    internal static string ReadPoolKind(string root, string key)
    {
        if (key == null || !KeyPattern.IsMatch(key)) throw new InvalidDataException("卡池键无效。");
        var pool = ReadObject(ReadUtf8(Path.Combine(root, "gacha-" + key + ".json")));
        if (pool["gachaKey"]?.Type != JTokenType.String || pool.Value<string>("gachaKey") != key)
            throw new InvalidDataException("卡池文件中的 gachaKey 与排期不一致。");
        var common = pool["commonPool"];
        if (common == null || common.Type == JTokenType.Null) return "旧权重规则";
        if (common.Type != JTokenType.String || !KeyPattern.IsMatch(common.Value<string>()))
            throw new InvalidDataException("共享配置引用无效。");
        ReadObject(ReadUtf8(Path.Combine(root, "gacha-common-" + common.Value<string>() + ".json")));
        return "角色活动规则";
    }

    internal static DateTimeOffset? ParseBeijing(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!DateTime.TryParseExact(text.Trim(), new[] { BeijingTimeFormat, "yyyy-MM-dd HH:mm:ss.FFFFFFF" }, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var time))
            throw new InvalidDataException("请输入北京时间 yyyy-MM-dd HH:mm:ss，或留空表示不限。");
        try { return new DateTimeOffset(DateTime.SpecifyKind(time, DateTimeKind.Unspecified), BeijingOffset).ToUniversalTime(); }
        catch (ArgumentException error) { throw new InvalidDataException("时间超出支持范围。", error); }
    }

    static JToken ToUtcToken(string text)
    {
        var time = ParseBeijing(text);
        return time.HasValue ? new JValue(time.Value.ToString("O", CultureInfo.InvariantCulture)) : JValue.CreateNull();
    }

    static string UtcToBeijing(JToken token)
    {
        if (token == null || token.Type == JTokenType.Null) return "";
        if (token.Type != JTokenType.String || !DateTimeOffset.TryParseExact(token.Value<string>(),
                new[] { "yyyy-MM-dd'T'HH:mm:ss'Z'", "O" }, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var time) || time.Offset != TimeSpan.Zero)
            throw new InvalidDataException("排期文件中的时间必须是 UTC ISO 8601 格式。");
        return time.ToOffset(BeijingOffset).ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture);
    }

    static string ReadUtf8(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length > 1024 * 1024) throw new InvalidDataException("配置文件超过 1 MB。");
        int offset = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf ? 3 : 0;
        return Utf8.GetString(bytes, offset, bytes.Length - offset);
    }

    static JObject ReadObject(string json)
    {
        // Disable automatic date conversion so Unity's local timezone never changes a server timestamp.
        using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None, MaxDepth = 16 };
        var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        if (reader.Read()) throw new InvalidDataException("JSON 对象后存在多余内容。");
        return root;
    }

    static void RequireProperties(JObject value, params string[] names)
    {
        if (value.Properties().Any(property => !names.Contains(property.Name)))
            throw new InvalidDataException("排期存在工具不支持的字段，已停止读取以避免丢失数据。");
    }
}

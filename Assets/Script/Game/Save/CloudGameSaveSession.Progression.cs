using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

internal sealed partial class CloudGameSaveSession
{
    ProgressionRequestRecord pendingProgression;
    internal bool HasPendingProgression => pendingProgression != null;
    internal ProgressionRequestRecord PendingProgression => pendingProgression;
    string ProgressionJournalPath => Path.Combine(Application.persistentDataPath,
        "progression-request-" + client.GachaJournalKey(PlayerId) + ".json");
    ProgressionRequestJournal ProgressionJournal => new(ProgressionJournalPath, EnhanceJournalPath, RefineJournalPath);

    internal void RequireNoPendingProgression()
    {
        if (HasPendingProgression) throw new InvalidOperationException("养成结果尚未确认，请先恢复原操作或重新登录同步。");
    }

    internal void MarkProgressionNeedsSync() => HasConflict = true;

    void BeginProgression(ProgressionKind kind, object request)
    {
        RequireOwner(); RequireNoPendingProgression();
        var record = DecodeProgression(kind, JsonConvert.SerializeObject(request));
        ProgressionJournal.Write(record); pendingProgression = record;
    }

    static ProgressionRequestRecord DecodeProgression(ProgressionKind kind, string json)
    {
        switch (kind)
        {
            case ProgressionKind.WeaponEnhance:
                var enhance = ReadEnhancementRequest(json);
                return new(kind, enhance, enhance.requestId);
            case ProgressionKind.WeaponRefine:
                var refine = ReadRefinementRequest(json);
                return new(kind, refine, refine.requestId);
            case ProgressionKind.CharacterEnhance:
                var character = ReadCharacterEnhancementRequest(json);
                return new(kind, character, character.requestId);
            case ProgressionKind.Promote:
                var promote = ReadPromotionRequest(json);
                return new(kind, promote, promote.requestId);
            default: throw new InvalidDataException("Unknown progression operation; the journal was preserved.");
        }
    }

    async UniTask<TReply> ResolveProgressionAsync<TReply>(ProgressionKind kind, string notFoundCode,
        Func<CancellationToken, UniTask<TReply>> send, Func<CancellationToken, UniTask<TReply>> query, CancellationToken token)
    {
        RequireOwner();
        var pending = pendingProgression;
        if (pending == null || pending.Kind != kind) throw new InvalidOperationException("The pending progression operation changed.");
        try
        {
            if (pending.MayHaveBeenSent)
            {
                try
                {
                    var recovered = await query(token);
                    await UniTask.SwitchToMainThread(token); RequireOwner(); return recovered;
                }
                catch (CloudSaveServerException error) when (error.StatusCode == 404 && error.Code == notFoundCode)
                {
                    // A first POST may still be running. Its exact request remains idempotent even after a missing receipt.
                }
            }
            pending.MayHaveBeenSent = true;
            var reply = await send(token);
            await UniTask.SwitchToMainThread(token); RequireOwner(); return reply;
        }
        catch (CloudSaveServerException error) when (error.StatusCode == 400 || error.StatusCode == 409)
        {
            if (IsProgressionRejection(kind, error.Code)) ClearProgressionJournal();
            else HasConflict = true;
            throw;
        }
        catch (Exception error) when (error is InvalidDataException || error is JsonException)
        {
            // An unverifiable reply may describe a committed operation; keep its recovery record.
            HasConflict = true; throw;
        }
    }

    static bool IsProgressionRejection(ProgressionKind kind, string code)
    {
        string prefix = kind == ProgressionKind.Promote ? "promote" : kind == ProgressionKind.CharacterEnhance ? "character_enhance" :
            kind == ProgressionKind.WeaponEnhance ? "enhance" : "refine";
        if (code == "invalid_" + prefix + "_request") return true;
        switch (code)
        {
            case "enhance_config_changed": case "enhance_material_limit": case "enhance_weapon_missing":
            case "enhance_material_missing": case "enhance_material_equipped": case "enhance_level_limit":
            case "enhance_insufficient_gold": case "enhance_value_overflow": case "enhance_save_too_large":
                return kind == ProgressionKind.WeaponEnhance;
            case "refine_config_changed": case "refine_material_limit": case "refine_weapon_missing":
            case "refine_material_missing": case "refine_material_equipped": case "refine_material_mismatch":
            case "refine_level_limit": case "refine_insufficient_gold": case "refine_value_overflow": case "refine_save_too_large":
                return kind == ProgressionKind.WeaponRefine;
            case "character_enhance_config_changed": case "character_enhance_material_limit":
            case "character_enhance_character_missing": case "character_enhance_level_limit":
            case "character_enhance_insufficient_books": case "character_enhance_insufficient_gold":
            case "character_enhance_value_overflow": case "character_enhance_save_too_large":
                return kind == ProgressionKind.CharacterEnhance;
            case "promote_config_changed": case "promote_target_missing": case "promote_rank_limit":
            case "promote_level_required": case "promote_insufficient_materials": case "promote_insufficient_gold":
            case "promote_value_overflow": case "promote_save_too_large":
                return kind == ProgressionKind.Promote;
            default: return false;
        }
    }

    void AcceptProgression(ProgressionKind kind, string requestId, CloudSaveSnapshot state)
    {
        RequireOwner();
        if (pendingProgression?.Kind != kind || pendingProgression.RequestId != requestId)
            throw new InvalidOperationException("The receipt does not match the pending progression operation.");
        Json = state.saveJson; revision = state.revision;
        pendingJson = null; inFlightRequest = null; ClearProgressionJournal();
    }

    void ClearProgressionJournal()
    { ProgressionJournal.Clear(); pendingProgression = null; }

    async UniTask RecoverProgressionAsync(CancellationToken token)
    {
        pendingProgression = ProgressionJournal.Read(DecodeProgression);
        if (pendingProgression == null) return;
        try
        {
            switch (pendingProgression.Kind)
            {
                case ProgressionKind.WeaponEnhance: AcceptEnhancement(await ResolveEnhancementAsync(token)); break;
                case ProgressionKind.WeaponRefine: AcceptRefinement(await ResolveRefinementAsync(token)); break;
                case ProgressionKind.CharacterEnhance: AcceptCharacterEnhancement(await ResolveCharacterEnhancementAsync(token)); break;
                case ProgressionKind.Promote: AcceptPromotion(await ResolvePromotionAsync(token)); break;
                default: throw new InvalidDataException("Unknown progression recovery operation.");
            }
        }
        catch (CloudSaveServerException error) when (error.Code == "save_conflict")
        {
            // Receipt lookup found nothing and the stale POST was rejected: import the latest complete snapshot.
            var snapshot = await client.LoadAsync(PlayerId, token); RequireOwner();
            Json = snapshot?.saveJson; revision = snapshot?.revision ?? 0;
            ClearProgressionJournal(); HasConflict = false;
        }
        catch (CloudSaveServerException) when (!HasConflict && !HasPendingProgression)
        {
            // A known rejection did not commit; login imports the authoritative snapshot already loaded above.
        }
    }
}

using System;
using System.Collections.Generic;

public sealed class TeamRepository
{
    public const int PresetCount = 4;
    public const int MemberCapacity = 4;

    readonly string[][] presetMemberKeys = CreatePresetMemberKeys();
    readonly bool[] presetInitialized = new bool[PresetCount];
    internal event Action Changed;

    public IReadOnlyList<string> MemberKeys => presetMemberKeys[ActivePresetIndex];
    public int ActivePresetIndex { get; private set; }
    public bool IsInitialized => presetInitialized[ActivePresetIndex];
    public bool HasAnyInitializedPreset
    {
        get
        {
            for (int presetIndex = 0; presetIndex < presetInitialized.Length; presetIndex++)
            {
                if (presetInitialized[presetIndex])
                {
                    return true;
                }
            }

            return false;
        }
    }

    public bool TryGetMemberKey(int index, out string characterKey)
    {
        return TryGetMemberKey(ActivePresetIndex, index, out characterKey);
    }

    public bool TryGetMemberKey(int presetIndex, int memberIndex, out string characterKey)
    {
        if (presetIndex < 0
            || presetIndex >= PresetCount
            || memberIndex < 0
            || memberIndex >= MemberCapacity)
        {
            characterKey = null;
            return false;
        }

        characterKey = presetMemberKeys[presetIndex][memberIndex];
        return !string.IsNullOrEmpty(characterKey);
    }

    public string[] GetPresetSnapshot(int presetIndex)
    {
        ValidatePresetIndex(presetIndex);
        return (string[])presetMemberKeys[presetIndex].Clone();
    }

    // HUD and shortcuts use occupied slots in order while the formation keeps its saved layout.
    internal bool TryGetGameMemberKey(int orderIndex, out string characterKey)
    {
        if (orderIndex >= 0 && orderIndex < MemberCapacity)
        {
            foreach (var key in presetMemberKeys[ActivePresetIndex])
            {
                if (string.IsNullOrEmpty(key)) continue;
                if (orderIndex == 0)
                {
                    characterKey = key;
                    return true;
                }
                orderIndex--;
            }
        }

        characterKey = null;
        return false;
    }

    public bool IsPresetInitialized(int presetIndex)
    {
        ValidatePresetIndex(presetIndex);
        return presetInitialized[presetIndex];
    }

    internal bool EnsureNonEmptyPresets(string defaultCharacterKey)
    {
        if (string.IsNullOrEmpty(defaultCharacterKey))
        {
            throw new ArgumentException("A default character is required.", nameof(defaultCharacterKey));
        }

        bool changed = false;
        for (int presetIndex = 0; presetIndex < PresetCount; presetIndex++)
        {
            if (Array.Exists(presetMemberKeys[presetIndex], key => !string.IsNullOrEmpty(key)))
            {
                continue;
            }

            presetMemberKeys[presetIndex][0] = defaultCharacterKey;
            presetInitialized[presetIndex] = true;
            changed = true;
        }

        if (changed) Changed?.Invoke();
        return changed;
    }

    public void ReplaceMembers(IReadOnlyList<string> newMemberKeys)
    {
        ReplacePreset(ActivePresetIndex, newMemberKeys);
    }

    public void ReplacePreset(int presetIndex, IReadOnlyList<string> newMemberKeys)
    {
        ValidatePresetIndex(presetIndex);
        string[] nextMemberKeys = CreateValidatedMembers(newMemberKeys);
        Array.Copy(nextMemberKeys, presetMemberKeys[presetIndex], MemberCapacity);
        presetInitialized[presetIndex] = true;
        Changed?.Invoke();
    }

    public void ReplaceAllPresets(IReadOnlyList<string[]> newPresets, int activePresetIndex)
    {
        if (newPresets == null)
        {
            throw new ArgumentNullException(nameof(newPresets));
        }

        if (newPresets.Count != PresetCount)
        {
            throw new ArgumentException(
                $"Team preset count must be {PresetCount}.",
                nameof(newPresets));
        }

        ValidatePresetIndex(activePresetIndex);
        string[][] validatedPresets = new string[PresetCount][];
        for (int presetIndex = 0; presetIndex < PresetCount; presetIndex++)
        {
            validatedPresets[presetIndex] = CreateValidatedMembers(newPresets[presetIndex]);
        }

        for (int presetIndex = 0; presetIndex < PresetCount; presetIndex++)
        {
            Array.Copy(
                validatedPresets[presetIndex],
                presetMemberKeys[presetIndex],
                MemberCapacity);
            presetInitialized[presetIndex] = true;
        }

        ActivePresetIndex = activePresetIndex;
        Changed?.Invoke();
    }

    public void SetActivePreset(int presetIndex)
    {
        ValidatePresetIndex(presetIndex);
        ActivePresetIndex = presetIndex;
        Changed?.Invoke();
    }

    static string[] CreateValidatedMembers(IReadOnlyList<string> newMemberKeys)
    {
        if (newMemberKeys == null)
        {
            throw new ArgumentNullException(nameof(newMemberKeys));
        }

        if (newMemberKeys.Count > MemberCapacity)
        {
            throw new ArgumentException(
                $"Team member count cannot exceed {MemberCapacity}.",
                nameof(newMemberKeys));
        }

        string[] nextMemberKeys = new string[MemberCapacity];
        HashSet<string> uniqueKeys = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < newMemberKeys.Count; index++)
        {
            string characterKey = newMemberKeys[index];
            if (string.IsNullOrEmpty(characterKey))
            {
                continue;
            }

            if (!uniqueKeys.Add(characterKey))
            {
                throw new ArgumentException(
                    $"Duplicate team member: {characterKey}",
                    nameof(newMemberKeys));
            }

            nextMemberKeys[index] = characterKey;
        }

        // Imported empty presets are initialized separately; editing must never commit an empty team.
        if (uniqueKeys.Count == 0)
        {
            throw new ArgumentException("A team must contain at least one character.", nameof(newMemberKeys));
        }

        return nextMemberKeys;
    }

    public TeamSaveData ExportSaveData()
    {
        TeamSaveData saveData = new TeamSaveData
        {
            activePresetIndex = ActivePresetIndex
        };
        for (int presetIndex = 0; presetIndex < PresetCount; presetIndex++)
        {
            TeamPresetSaveData presetData = new TeamPresetSaveData
            {
                presetIndex = presetIndex,
                isInitialized = presetInitialized[presetIndex]
            };
            for (int memberIndex = 0; memberIndex < MemberCapacity; memberIndex++)
            {
                string characterKey = presetMemberKeys[presetIndex][memberIndex];
                if (!string.IsNullOrEmpty(characterKey))
                {
                    presetData.members.Add(new TeamMemberSaveData(memberIndex, characterKey));
                }
            }

            saveData.presets.Add(presetData);
        }

        return saveData;
    }

    public void ImportSaveData(TeamSaveData saveData)
    {
        ClearAllPresets();
        if (saveData == null)
        {
            Changed?.Invoke();
            return;
        }

        if (saveData.presets != null && saveData.presets.Count > 0)
        {
            bool[] importedPresets = new bool[PresetCount];
            foreach (TeamPresetSaveData presetData in saveData.presets)
            {
                if (presetData == null
                    || presetData.presetIndex < 0
                    || presetData.presetIndex >= PresetCount
                    || importedPresets[presetData.presetIndex])
                {
                    continue;
                }

                int presetIndex = presetData.presetIndex;
                importedPresets[presetIndex] = true;
                presetInitialized[presetIndex] = presetData.isInitialized;
                ImportMembers(presetIndex, presetData.members);
            }

            ActivePresetIndex = saveData.activePresetIndex >= 0
                && saveData.activePresetIndex < PresetCount
                ? saveData.activePresetIndex
                : 0;
            Changed?.Invoke();
            return;
        }

        presetInitialized[0] = saveData.isInitialized;
        ImportMembers(0, saveData.members);
        Changed?.Invoke();
    }

    void ImportMembers(int presetIndex, IReadOnlyList<TeamMemberSaveData> members)
    {
        if (members == null)
        {
            return;
        }

        HashSet<int> occupiedSlots = new HashSet<int>();
        HashSet<string> uniqueKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (TeamMemberSaveData memberData in members)
        {
            if (memberData == null
                || memberData.slotIndex < 0
                || memberData.slotIndex >= MemberCapacity
                || string.IsNullOrEmpty(memberData.characterKey)
                || !occupiedSlots.Add(memberData.slotIndex)
                || !uniqueKeys.Add(memberData.characterKey))
            {
                continue;
            }

            presetMemberKeys[presetIndex][memberData.slotIndex] = memberData.characterKey;
        }
    }

    void ClearAllPresets()
    {
        for (int presetIndex = 0; presetIndex < PresetCount; presetIndex++)
        {
            Array.Clear(presetMemberKeys[presetIndex], 0, MemberCapacity);
            presetInitialized[presetIndex] = false;
        }

        ActivePresetIndex = 0;
    }

    static string[][] CreatePresetMemberKeys()
    {
        string[][] presets = new string[PresetCount][];
        for (int presetIndex = 0; presetIndex < PresetCount; presetIndex++)
        {
            presets[presetIndex] = new string[MemberCapacity];
        }

        return presets;
    }

    static void ValidatePresetIndex(int presetIndex)
    {
        if (presetIndex < 0 || presetIndex >= PresetCount)
        {
            throw new ArgumentOutOfRangeException(nameof(presetIndex));
        }
    }
}

using System;
using System.Collections.Generic;
using Game.Domain.Character;
using UniRx;

internal sealed class HubTeamViewModel : IDisposable
{
    readonly TeamRepository teams;
    readonly CharacterRepository characters;
    readonly CharacterModel[] members = new CharacterModel[TeamRepository.MemberCapacity];
    readonly ReactiveProperty<int> selectedIndex = new(-1);
    readonly ReactiveProperty<CharacterModel> selectedCharacter = new();
    readonly ReactiveProperty<CharacterModel> activeCharacter = new();
    readonly WorldPartyController party;
    readonly ReactiveProperty<int> revision = new();

    internal IReadOnlyList<CharacterModel> Members => members;
    internal IReadOnlyReactiveProperty<int> SelectedIndex => selectedIndex;
    internal IReadOnlyReactiveProperty<CharacterModel> SelectedCharacter => selectedCharacter;
    internal IReadOnlyReactiveProperty<CharacterModel> ActiveCharacter => activeCharacter;
    internal IReadOnlyReactiveProperty<int> Revision => revision;

    internal HubTeamViewModel(TeamRepository teams, CharacterRepository characters, WorldPartyController party = null)
    {
        this.teams = teams;
        this.characters = characters;
        this.party = party;
        teams.Changed += Refresh;
        if (party != null) party.ActiveCharacterChanged += RefreshActiveCharacter;
        Refresh();
    }

    internal void Refresh()
    {
        string previousKey = selectedCharacter.Value?.Definition.key;
        int next = -1;
        for (int i = 0; i < members.Length; i++)
        {
            members[i] = teams.TryGetGameMemberKey(i, out string key) ? characters.GetByKey(key) : null;
            if (members[i] != null && members[i].Definition.key == previousKey) next = i;
        }
        if (next < 0) next = Array.FindIndex(members, member => member != null);
        selectedIndex.Value = next;
        selectedCharacter.Value = next >= 0 ? members[next] : null;
        RefreshActiveCharacter();
        revision.Value++;
    }

    internal void Select(int index)
    {
        if (index < 0 || index >= members.Length || members[index] == null) return;
        if (party != null)
        {
            party.RequestSwitch(index);
            return;
        }
        // The authoring entry still supports inspection without creating a world player.
        selectedIndex.Value = index;
        selectedCharacter.Value = members[index];
        activeCharacter.Value = members[index];
    }

    void RefreshActiveCharacter()
    {
        activeCharacter.Value = party != null ? party.ActiveCharacter : selectedCharacter.Value;
    }

    public void Dispose()
    {
        teams.Changed -= Refresh;
        if (party != null) party.ActiveCharacterChanged -= RefreshActiveCharacter;
        selectedIndex.Dispose();
        selectedCharacter.Dispose();
        activeCharacter.Dispose();
        revision.Dispose();
    }
}

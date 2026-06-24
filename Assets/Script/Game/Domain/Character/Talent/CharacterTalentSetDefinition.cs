using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Domain.Character
{
    public enum TalentStatType
    {
        MaxHp,
        Attack,
        Defense,
        CritRate,
        CritDamage,
        ElementDamage,
    }

    public enum TalentModifierType
    {
        Flat,
        Percent,
    }

    [Serializable]
    public class TalentEffectDefinition
    {
        public TalentStatType StatType;
        public TalentModifierType ModifierType;
        public float Value;
    }

    [Serializable]
    public class TalentNodeDefinition
    {
        public string Name;
        [TextArea]
        public string Description;
        public List<TalentEffectDefinition> Effects = new();
    }

    [CreateAssetMenu(menuName = "Game/Character/Talent Set")]
    public class CharacterTalentSetDefinition : ScriptableObject
    {
        public const int MaxNodeCount = 6;

        public List<TalentNodeDefinition> Nodes = new(MaxNodeCount);

        public int NodeCount => Nodes?.Count ?? 0;

        public TalentNodeDefinition GetNode(int index)
        {
            if (Nodes == null || index < 0 || index >= Nodes.Count)
            {
                return null;
            }

            return Nodes[index];
        }

        public void CollectActiveEffects(int talentLevel, List<TalentEffectDefinition> results)
        {
            results.Clear();

            if (Nodes == null)
            {
                return;
            }

            int count = Mathf.Clamp(talentLevel, 0, Mathf.Min(MaxNodeCount, Nodes.Count));
            for (int i = 0; i < count; i++)
            {
                var node = Nodes[i];
                if (node?.Effects == null)
                {
                    continue;
                }

                results.AddRange(node.Effects);
            }
        }
    }
}

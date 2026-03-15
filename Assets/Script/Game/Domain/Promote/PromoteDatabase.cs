using System.Collections.Generic;
using UnityEngine;

namespace Game.Domain.Promote
{
    /// <summary>
    /// 角色突破数据库：
    /// 管理所有角色的突破总表 (CharacterPromoteDefinition)。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Database/Promote Database")]
    public class PromoteDatabase : ScriptableObject
    {
        [SerializeField]
        private List<CharacterPromoteDefinition> promoteDefinitions = new();

        private Dictionary<string, CharacterPromoteDefinition> promoteMap;

        /// <summary>
        /// 根据角色 Key 获取其对应的突破规则总表
        /// </summary>
        public CharacterPromoteDefinition Get(string characterKey)
        {
            if (promoteMap == null)
            {
                promoteMap = new Dictionary<string, CharacterPromoteDefinition>();
                foreach (var def in promoteDefinitions)
                {
                    if (def != null && !string.IsNullOrEmpty(def.characterKey))
                    {
                        promoteMap[def.characterKey] = def;
                    }
                }
            }

            if (!promoteMap.TryGetValue(characterKey, out var result))
            {
                Debug.LogWarning($"CharacterPromoteDatabase: 找不到角色为 {characterKey} 的突破配置");
                return null;
            }

            return result;
        }

        public void Add(CharacterPromoteDefinition def)
        {
            if (!promoteDefinitions.Contains(def))
            {
                promoteDefinitions.Add(def);
                promoteMap?.Add(def.characterKey, def);
            }
        }

        public void Remove(CharacterPromoteDefinition def)
        {
            promoteDefinitions.Remove(def);
            promoteMap?.Remove(def.characterKey);
        }

        public IReadOnlyList<CharacterPromoteDefinition> AllPromoteDefinitions => promoteDefinitions;
    }
}

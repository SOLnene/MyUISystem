using UnityEngine;
namespace Game.Domain.Character
{
    [CreateAssetMenu(menuName = "Game/Character Definition")]
    public class CharacterDefinition : ScriptableObject
    {
        public int id;
        public string key;  //来自image名称
        public string displayName; //显示的名字

        public int baseHp;
        public int baseAttack;
        public int baseDefense;

        public int rarity;

        public string element;
        public int baseElementalMastery;

        public string description;
        public string weaponType;
    }
}

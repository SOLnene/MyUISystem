using System.Collections.Generic;
using UniRx;
namespace Game.Domain.Character
{
    public class CharacterModel: IEnhanceable
    {
        //静态数据
        public CharacterDefinition Definition { get; }

        public IReadOnlyReactiveProperty<string> Name { get; }
        public IReadOnlyReactiveProperty<int> Star { get; }
        public IReadOnlyReactiveProperty<string> Description { get; }

        //经验系统
        public IReadOnlyReactiveProperty<int> LevelRP => levelRP;
        public IReadOnlyReactiveProperty<int> ExpRP => expRP;
        readonly ReactiveProperty<int> levelRP;
        readonly ReactiveProperty<int> expRP;
        public LevelSystem LevelSystem { get; private set; }
        //属性系统
        public CharacterStats Stats { get; }


        // ==== 构造函数 ====

        public CharacterModel(CharacterDefinition definition, int level, int exp = 0)
        {
            this.Definition = definition;
            this.Name = new ReactiveProperty<string>(definition.displayName);
            this.Star = new ReactiveProperty<int>(definition.rarity);
            this.Description = new ReactiveProperty<string>(definition.description);
            // 初始化系统
            this.LevelSystem = new LevelSystem(level, exp, definition.rarity);
            this.levelRP = new ReactiveProperty<int>(level);
            this.expRP = new ReactiveProperty<int>(exp);
            this.Stats = new CharacterStats();

            // 初始同步一次属性
            RefreshBaseStats();
        }

        public ExpGainResult AddExp(int exp)
        {
            var result = LevelSystem.AddExp(exp);
            if (result == ExpGainResult.LeveledUp)
            {
                RefreshBaseStats();
            }
            levelRP.Value = LevelSystem.Level;
            expRP.Value = LevelSystem.CurrentExp;
            return result;
        }

        /// <summary>
        /// 获取增加经验后属性的预览数据（主要用于UI展示）
        /// </summary>
        /// <param name="addedExp"></param>
        /// <returns></returns>
        public List<StatPreviewData> GetStatPreview(int addedExp)
        {
            var preview = LevelSystem.GetPreviewWithExp(addedExp);
            int previewLevel = preview.finalLevel;
            var data = new List<StatPreviewData>
            {
                new StatPreviewData
                {
                    label = "基础生命值",
                    currentValue = Stats.BaseHP.Value,
                    nextValue = Definition.baseHp + (previewLevel - 1) * 100
                },
                new StatPreviewData
                {
                    label = "基础攻击力",
                    currentValue = Stats.BaseAtk.Value,
                    nextValue = Definition.baseAttack + (previewLevel - 1) * 10
                },
                new StatPreviewData
                {
                    label = "基础防御力",
                    currentValue = Stats.BaseDef.Value,
                    nextValue = Definition.baseDefense + (previewLevel - 1) * 5
                }
            };
            return data;
        }
        
        // 根据当前等级，从 Definition 中重新计算基础属性
        private void RefreshBaseStats()
        {
            int curLevel = LevelSystem.Level;
            // Stats 只需要负责“怎么算最终值”，Model 负责“从配置里拿基础值”
            Stats.BaseHP.Value = Definition.baseHp + (curLevel - 1) * 100;
            Stats.BaseAtk.Value = Definition.baseAttack + (curLevel - 1) * 10;
            Stats.BaseDef.Value = Definition.baseDefense + (curLevel - 1) * 5;
            Stats.ElementalMastery.Value = Definition.baseElementalMastery;
        }
        
    }
}

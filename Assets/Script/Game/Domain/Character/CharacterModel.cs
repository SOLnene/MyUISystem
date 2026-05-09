using System;
using System.Collections.Generic;
using UniRx;
namespace Game.Domain.Character
{
    public class CharacterModel: IEnhanceable, IPromotable
    {
        //静态数据
        public CharacterDefinition Definition { get; }

        //todo:名字不该是rp
        public IReadOnlyReactiveProperty<string> Name { get; }
        public IReadOnlyReactiveProperty<int> Star { get; }
        public IReadOnlyReactiveProperty<string> Description { get; }

        //经验系统
        public IReadOnlyReactiveProperty<int> LevelRP => levelRP;
        public IReadOnlyReactiveProperty<int> ExpRP => expRP;
        readonly ReactiveProperty<int> levelRP;
        readonly ReactiveProperty<int> expRP;

        public IReadOnlyReactiveProperty<int> RankRP => rankRP;
        readonly ReactiveProperty<int> rankRP;
        
        
        //等级系统
        //外部查询可以调用levelsystem，数据修改不能走
        public LevelSystem LevelSystem { get; private set; }
        //阶级系统
        public RankSystem RankSystem { get; private set; }
        //属性系统
        public CharacterStats Stats { get; }

        public  IObservable<Unit> ChangeRP { get; }
        
        public ReactiveProperty<EquipItem> CurrentEquipRP { get; private set; }
        // ==== 构造函数 ====

        public CharacterModel(CharacterDefinition definition, int level, int exp = 0,int rank = 0)
        {
            this.Definition = definition;
            this.Name = new ReactiveProperty<string>(definition.displayName);
            this.Star = new ReactiveProperty<int>(definition.rarity);
            this.Description = new ReactiveProperty<string>(definition.description);
            // 初始化系统
            this.LevelSystem = new LevelSystem(level, exp, definition.rarity);
            this.RankSystem = new RankSystem(rank);
            this.levelRP = new ReactiveProperty<int>(level);
            this.expRP = new ReactiveProperty<int>(exp);
            // RP 与 RankSystem 当前阶保持一致
            this.rankRP = new ReactiveProperty<int>(this.RankSystem.CurrentRank);
            this.Stats = new CharacterStats();
            
            ChangeRP = Observable.CombineLatest(LevelRP, RankRP)
                .Select(_ => Unit.Default);
            CurrentEquipRP = new ReactiveProperty<EquipItem>();
            // 初始同步一次属性
            RefreshBaseStats();
        }

        public ExpGainResult AddExp(int exp)
        {
            var result = LevelSystem.AddExp(exp,GetCurrentMaxLevel());
            if (result == ExpGainResult.LeveledUp)
            {
                RefreshBaseStats();
            }
            levelRP.Value = LevelSystem.Level;
            expRP.Value = LevelSystem.CurrentExp;
            return result;
        }

        public int GetCurrentMaxLevel()
        {
            return RankSystem.CurrentRankMaxLevel;
        }
        
        /// <summary>
        /// 获取增加经验后属性的预览数据（主要用于UI展示）
        /// </summary>
        /// <param name="addedExp"></param>
        /// <returns></returns>
        public List<StatPreviewData> GetStatPreview(int addedExp,bool promoting = false)
        {
            var preview = LevelSystem.GetPreviewWithExp(addedExp, GetCurrentMaxLevel());
            int previewLevel = preview.finalLevel;
            int rank = promoting?RankSystem.CurrentRank+1: RankSystem.CurrentRank;
            var data = new List<StatPreviewData>
            {
                new StatPreviewData
                {
                    label = "基础生命值",
                    currentValue = Stats.BaseHP.Value,
                    nextValue = Definition.baseHp + (previewLevel - 1) * 100 + rank * 200
                },
                new StatPreviewData
                {
                    label = "基础攻击力",
                    currentValue = Stats.BaseAtk.Value,
                    nextValue = Definition.baseAttack + (previewLevel - 1) * 10 + rank * 20
                },
                new StatPreviewData
                {
                    label = "基础防御力",
                    currentValue = Stats.BaseDef.Value,
                    nextValue = Definition.baseDefense + (previewLevel - 1) * 5 + rank * 10
                }
            };
            return data;
        }
        
        // 根据当前等级，从 Definition 中重新计算基础属性
        private void RefreshBaseStats()
        {
            int curLevel = LevelSystem.Level;
            int curRank = RankSystem.CurrentRank;
            // Stats 只需要负责“怎么算最终值”，Model 负责“从配置里拿基础值”
            Stats.BaseHP.Value = Definition.baseHp + (curLevel - 1) * 100 + curRank * 200;
            Stats.BaseAtk.Value = Definition.baseAttack + (curLevel - 1) * 10 + curRank * 20;
            Stats.BaseDef.Value = Definition.baseDefense + (curLevel - 1) * 5 + curRank *10;
            Stats.ElementalMastery.Value = Definition.baseElementalMastery;
        }
        
        public bool CanPromote()
        {
            //todo:判断
            return RankSystem.CanPromote(LevelSystem.Level);
        }

        public bool Promote()
        {
            if (!RankSystem.CanPromote(LevelSystem.Level))
                return false;

            RankSystem.Promote();
            rankRP.Value = RankSystem.CurrentRank;
            RefreshBaseStats();
            // 未来可能增加逻辑
            // ResetExp();
            // TriggerEvent();

            return true;
        }
        /// <summary>
        /// 突破金币公式
        /// </summary>
        /// <returns></returns>
        public int GetPromoteGoldCost()
        {
            return PromoteCostFormula.GetGoldCost(RankSystem.CurrentRank, Definition.rarity);
        }

        public void ChangeEquip(EquipItem equipItem)
        {
            CurrentEquipRP.Value = equipItem;
        }
    }
    
}

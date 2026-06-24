using UniRx;
namespace Game.Domain.Character
{
    public class CharacterStats
    {
        public ReactiveProperty<float> BaseHP = new();
        public ReactiveProperty<float> BonusHP = new();
        public ReactiveProperty<float> TalentBonusHP = new();
        public IReadOnlyReactiveProperty<float> FinalHP;

        public ReactiveProperty<float> BaseAtk = new();
        public ReactiveProperty<float> BonusAtk = new();
        public ReactiveProperty<float> TalentBonusAtk = new();
        public IReadOnlyReactiveProperty<float> FinalAtk;

        public ReactiveProperty<float> BaseDef = new();
        public ReactiveProperty<float> BonusDef = new();
        public ReactiveProperty<float> TalentBonusDef = new();
        public IReadOnlyReactiveProperty<float> FinalDef;

        public ReactiveProperty<float> ElementalMastery = new();
        public ReactiveProperty<float> Stamina = new();
        public ReactiveProperty<float> Favor = new(); 
        public CharacterStats()
        {
            FinalHP = Combine(BaseHP, BonusHP, TalentBonusHP);
            FinalAtk = Combine(BaseAtk, BonusAtk, TalentBonusAtk);
            FinalDef = Combine(BaseDef, BonusDef, TalentBonusDef);
        }

        private IReadOnlyReactiveProperty<float> Combine(
            ReactiveProperty<float> a,
            ReactiveProperty<float> b,
            ReactiveProperty<float> c)
        {
            return Observable
                .CombineLatest(a, b, c, (x, y, z) => x + y + z)
                .ToReadOnlyReactiveProperty();
        }
    }
}


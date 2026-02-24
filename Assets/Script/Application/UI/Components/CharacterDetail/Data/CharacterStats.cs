
using UniRx;
public class CharacterStats
{
    public ReactiveProperty<float> BaseHP = new();
    public ReactiveProperty<float> BonusHP = new();
    public IReadOnlyReactiveProperty<float> FinalHP;

    public ReactiveProperty<float> BaseAtk = new();
    public ReactiveProperty<float> BonusAtk = new();
    public IReadOnlyReactiveProperty<float> FinalAtk;

    public ReactiveProperty<float> BaseDef = new();
    public ReactiveProperty<float> BonusDef = new();
    public IReadOnlyReactiveProperty<float> FinalDef;

    public ReactiveProperty<float> ElementalMastery = new();
    public ReactiveProperty<float> Stamina = new();
    public ReactiveProperty<float> Favor = new(); 
    public CharacterStats()
    {
        FinalHP = Combine(BaseHP, BonusHP);
        FinalAtk = Combine(BaseAtk, BonusAtk);
        FinalDef = Combine(BaseDef, BonusDef);
    }

    private IReadOnlyReactiveProperty<float> Combine(
        ReactiveProperty<float> a,
        ReactiveProperty<float> b)
    {
        return Observable
            .CombineLatest(a, b, (x, y) => x + y)
            .ToReadOnlyReactiveProperty();
    }
}


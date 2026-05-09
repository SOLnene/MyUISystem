using UniRx;

public class EquipStats
{
    public ReactiveProperty<float> BaseAttack = new();
    public ReactiveProperty<float> CriticalDamage = new();
}

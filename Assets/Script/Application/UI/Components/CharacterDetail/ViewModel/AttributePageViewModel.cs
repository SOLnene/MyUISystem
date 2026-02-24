using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttributePageViewModel
{
    public List<StatItemViewModel> Stats;

    public AttributePageViewModel(CharacterModel model)
    {
        Stats = new List<StatItemViewModel>
        {
            new StatItemViewModel(null,"生命值上限", model.Stats.FinalHP),
            new StatItemViewModel(null,"攻击力", model.Stats.FinalAtk),
            new StatItemViewModel(null,"防御力", model.Stats.FinalDef),
            new StatItemViewModel(null,"元素精通", model.Stats.ElementalMastery),
            new StatItemViewModel(null,"体力上限", model.Stats.Stamina)
        };
    }
}

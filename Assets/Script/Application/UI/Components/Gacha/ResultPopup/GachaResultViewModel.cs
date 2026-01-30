using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GachaResultViewModel
{
    public IReadOnlyList<GachaEntryViewModel> items;
    
    public GachaResultViewModel(IReadOnlyList<GachaEntryViewModel> items)
    {
        this.items = items;
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;
//弃用，目前reveal不作为独立uiview
public class GachaResultRevealViewModel
{
    public IReadOnlyReactiveProperty<GachaEntryViewModel> Entry { get; }
    
    public GachaResultRevealViewModel(IReadOnlyReactiveProperty<GachaEntryViewModel> entry)
    {
        Entry = entry;
    }
}

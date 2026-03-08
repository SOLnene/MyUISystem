using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public interface IGachaVisualProvider
{
    public GachaVisual GetVisual(GachaEntry entry);
}

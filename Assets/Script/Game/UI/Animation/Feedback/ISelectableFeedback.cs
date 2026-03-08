using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ISelectableFeedback
{
    public void OnHoverEnter();
    public void OnHoverExit();
    public void OnSelect();
    public void OnDeselect();
    public void OnClick();
    public void Reset();
}

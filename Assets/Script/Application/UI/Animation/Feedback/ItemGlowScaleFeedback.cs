using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class ItemGlowScaleFeedback : MonoBehaviour,ISelectableFeedback
{
    [SerializeField]
    Image glow;
    [SerializeField]
    Transform root;
    
    Tween hoverTween;
    Tween loopTween;

    public void OnHoverEnter()
    {
        hoverTween?.Kill();
        hoverTween = DOTween.Sequence()
            .Append(root.DOScale(1.1f, 0.15f)
                .SetEase(Ease.OutBack))
            .Join(glow.DOFade(1.0f, 0.15f));
    }

    public void OnHoverExit()
    {
        hoverTween?.Kill();
        hoverTween = DOTween.Sequence()
            .Append(root.DOScale(1.0f, 0.15f))
            .Join(glow.DOFade(0.0f, 0.15f));
    }

    public void OnHover()
    {
        loopTween?.Kill();
        loopTween = DOTween.Sequence()
            .Append(glow.DOFade(1.0f, 1.0f))
            .SetLoops(-1, LoopType.Yoyo);
    }

    public void OnSelect()
    {
        loopTween?.Kill();

        loopTween = DOTween.Sequence()
            .Append(glow.DOFade(1.0f, 0.1f))
            .Join(glow.transform.DOScale(1.1f, 0.1f))
            .Append(glow.DOFade(0f, 0.2f))
            .Join(glow.transform.DOScale(1.0f, 0.2f));
    }
    
    public void OnDeselect()
    {
        loopTween?.Kill();
        glow.DOFade(0.0f, 0.1f);
    }
    
    public void OnClick(){}

    public void Reset()
    {
        hoverTween?.Kill();
        loopTween?.Kill();
        root.localScale = Vector3.one;
        glow.color = new Color(glow.color.r, glow.color.g, glow.color.b, 0);
    }
}

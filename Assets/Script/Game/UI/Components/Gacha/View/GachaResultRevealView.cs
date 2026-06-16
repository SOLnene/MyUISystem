using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using SkierFramework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;


public partial class GachaResultRevealView : BindableUI
{
    //UIControlData
  #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
    [ControlBinding]
    private Image cardFrame;
    [ControlBinding]
    private Image icon;
    [ControlBinding]
    private RectTransform content;
    [ControlBinding]
    private Button darkMask;
    [ControlBinding]
    private TextMeshProUGUI nameText;
    [ControlBinding]
    private RectTransform starContent;
    [ControlBinding]
    private RectTransform infoContent;
    [ControlBinding]
    private RectTransform iconTarget;
    [ControlBinding]
    private RectTransform infoTarget;
    [ControlBinding]
    private Image[] stars;

		#pragma warning restore 0649
#endregion





    GachaEntryViewModel vm;
    
    Sequence revealSeq;
    
    Sequence detailRevealSeq;
    
    bool isRevealing;

    Vector2 iconBasePos;
    Vector2 infoBasePos;
    Color starBaseColor;
    
    Subject<Unit> onRevealFinish = new Subject<Unit>();
    
    RevealPhase CurrentPhase { get; set; } = RevealPhase.Idle;
    
    CompositeDisposable disposable = new CompositeDisposable();

    public override void OnEnable()
    {
        base.OnEnable();
    }
    
    public void Bind(GachaEntryViewModel entry)
    {
        /*vm = entry;
        entry.
            .Subscribe(entry =>
            {
                BindEntry(entry);
            }).AddTo(this);
        PlayRevealAsync().Forget();*/
    }
    
    public void BindEntry(GachaEntryViewModel entry)
    {
        vm = entry;
        disposable.Clear();
        
        CacheBaseState();
        nameText.text = entry.Name;
        content.gameObject.SetActive(true);
        PrepareInitialState();
        entry.DetailImage
            .Where(sprite => sprite != null)     // 过滤掉 null
            .Take(1)                             // 只反应第一次有效值（防止未来误设）
            .Subscribe(sprite =>
            {
                icon.sprite = sprite;
                cardFrame.color = RarityConfig.GetColor(vm.Rarity);
                PlayRevealAsync().Forget();
            })
            .AddTo(disposable);
    }
    
    async UniTask PlayRevealAsync()
    {
        if (CurrentPhase != RevealPhase.Idle)
        {
            return;
        }
        CurrentPhase = RevealPhase.Revealing;
        revealSeq?.Kill();
        isRevealing = true;
        PrepareRevealState();
        icon.transform.localScale = new Vector3(2,2,2);

        revealSeq = DOTween.Sequence()
            .Append(icon.transform.DOScale(1f, 0.3f)
                .From(2f)
                .SetEase(Ease.OutBack))
            .AppendInterval(0.2f);
        
        await revealSeq.AsyncWaitForCompletion();
        await PlayDetailRevealAsync();
        CurrentPhase = RevealPhase.Finished;
        isRevealing = false;
     
    }

    async UniTask PlayDetailRevealAsync()
    {
        if (CurrentPhase != RevealPhase.Revealing)
        {
            return;
        }
        CurrentPhase = RevealPhase.DetailRevealing;
        detailRevealSeq?.Kill();
        PrepareDetailState();
        detailRevealSeq = DOTween.Sequence();

        detailRevealSeq.Append(
            icon.DOColor(Color.white, 0.35f));
        //icon右移
        detailRevealSeq.Append(icon.rectTransform
            .DOAnchorPosX(iconBasePos.x + 120.0f, 0.35f)
            .SetEase(Ease.OutCubic));
            
        //文本向左
        detailRevealSeq.Join(infoContent
            .DOAnchorPosX(infoBasePos.x, 0.35f)
            .From(infoBasePos + 120 *Vector2.right)
            .SetEase(Ease.OutCubic));

        detailRevealSeq.Join(nameText
            .DOFade(1, 0.35f)
            .SetEase(Ease.OutCubic));
        
        //星星动画
        for (int i = 0; i < vm.Rarity; i++)
        {
            int index = i;

            detailRevealSeq.AppendCallback(() =>
            {
                stars[index].gameObject.SetActive(true);
            });

            detailRevealSeq.Append(
                stars[index].transform
                    .DOScale(1f, 0.15f)
                    .From(2f)
                    .SetEase(Ease.OutBack)
                );

            detailRevealSeq.Join(
                stars[index]
                    .DOColor(Color.white, 0.075f)
                    .From(starBaseColor)
                );

            detailRevealSeq.Append(
                stars[index]
                    .DOColor(starBaseColor, 0.075f)
                );

            detailRevealSeq.AppendInterval(0.05f);
        }

        await detailRevealSeq.AsyncWaitForCompletion();
    }

    void PrepareInitialState()
    {
        icon.color = Color.black;
        icon.transform.localScale = Vector3.one * 2f;
        icon.rectTransform.anchoredPosition = iconBasePos;

        infoContent.anchoredPosition = infoBasePos;
        nameText.alpha = 0;
        for (int i = 0; i < stars.Length; i++)
        {
            stars[i].gameObject.SetActive(false);
            stars[i].transform.localScale = Vector3.one * 2f;
            stars[i].color = Color.white;
        }
        CurrentPhase = RevealPhase.Idle;
    }
    
    void PrepareRevealState()
    {
        // Reveal 阶段只关心 scale / color
        icon.transform.localScale = Vector3.one * 2f;
        icon.color = Color.black;
        
    }
    
    void PrepareDetailState()
    {
        icon.rectTransform.anchoredPosition = iconBasePos;
        infoContent.anchoredPosition = infoBasePos;
    }
    
    public void SkipReveal()
    {
        if (CurrentPhase == RevealPhase.Finished)
        {
            return;
        }

        revealSeq?.Kill(); // true = complete
        detailRevealSeq.Kill();
        ApplyRevealFinalState();
        isRevealing = false; 
    }
    /// <summary>
    /// 展示最终状态
    /// </summary>
    void ApplyRevealFinalState()
    {
        // icon
        icon.color = Color.white;
        icon.transform.localScale = Vector3.one;
        icon.rectTransform.anchoredPosition = iconBasePos + Vector2.right * 120f;

        // info
        infoContent.anchoredPosition = infoBasePos;
        nameText.alpha = 1f;

        // stars
        for (int i = 0; i < stars.Length; i++)
        {
            if (i < vm.Rarity)
            {
                stars[i].gameObject.SetActive(true);
                stars[i].transform.localScale = Vector3.one;
                stars[i].color = starBaseColor;
            }
            else
            {
                stars[i].gameObject.SetActive(false);
            }
        }

        CurrentPhase = RevealPhase.Finished;
    }

    void CacheBaseState()
    {
        iconBasePos = iconTarget.anchoredPosition;
        infoBasePos = infoTarget.anchoredPosition;
        starBaseColor = stars[0].color;
    }

    public bool IsRevealFinished()
    {
        return CurrentPhase == RevealPhase.Finished;
    }

    void OnDisable()
    {
        disposable.Clear();
        revealSeq?.Kill();
        detailRevealSeq?.Kill();
    }

}
//动画阶段
enum RevealPhase
{
    Idle,
    Revealing,
    DetailRevealing,
    Finished
}

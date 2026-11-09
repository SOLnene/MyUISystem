using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Domain.Character;
using UniRx;
using SkierFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 只做显示
/// </summary>
public class TeamInfoSlot : BindableUI,IPointerClickHandler
{
    #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
    [ControlBinding]
    private TextMeshProUGUI IndexText;
    [ControlBinding]
    private Image CharaIcon;
    [ControlBinding]
    private BarBase bar;
    [ControlBinding]
    private TextMeshProUGUI CharaNameText;
    [ControlBinding]
    private Image ChargeIcon;
    [ControlBinding]
    private Image SelectedImage;

		#pragma warning restore 0649
#endregion

    PlayerStateEvent playerdata;
    readonly CompositeDisposable characterSubscriptions = new();
    CharacterModel character;
    int slotIndex;
    CancellationTokenSource portraitCancellation;
    string portraitAddress;
    Sprite boundPortrait;
    Vector2? fallbackPortraitSize;
    [SerializeField, Min(0.01f)] float activePortraitScale = .73f;
    [SerializeField, Min(0.01f)] float standbyPortraitScale = .83f;
    [SerializeField] Image keyHintBackground;
    [SerializeField] GameObject activeBackdrop;
    [SerializeField] GameObject standbyBackdrop;
    [SerializeField] Vector2 activeNamePosition = new(-154f, 0f);
    [SerializeField] Vector2 standbyNamePosition = new(-126f, 9f);

    public override void OnEnable()
    {
        base.OnEnable();
        if (character != null) BindCharacter(character, slotIndex, boundPortrait);
    }

    internal void BindCharacter(CharacterModel model, int index, Sprite portrait = null)
    {
        base.Bind(model);
        characterSubscriptions.Clear();
        character = model;
        boundPortrait = portrait;
        slotIndex = index;
        if (model == null)
        {
            onClick = null;
            gameObject.SetActive(false);
            return;
        }
        if (!gameObject.activeSelf)
        {
            // OnEnable binds the retained model after this slot becomes visible again.
            gameObject.SetActive(true);
            return;
        }
        if (!isActiveAndEnabled) return;
        UpdatePortraitSize();
        IndexText.text = (index + 1).ToString();
        ChargeIcon.gameObject.SetActive(false);
        model.Name
            .Subscribe(text => CharaNameText.text = text).AddTo(characterSubscriptions);
        Observable.CombineLatest(model.CurrentHpRP, model.Stats.FinalHP, (hp, max) => new { hp, max })
            .Subscribe(value => bar.SetValue(Mathf.CeilToInt(value.hp), Mathf.CeilToInt(value.max)))
            .AddTo(characterSubscriptions);
        if (boundPortrait != null)
        {
            portraitCancellation?.Cancel();
            portraitCancellation?.Dispose();
            portraitCancellation = null;
            portraitAddress = null;
            CharaIcon.sprite = boundPortrait;
            CharaIcon.enabled = true;
            return;
        }
        string address = CharacterVisualAddressResolver.ResolveAvatarIcon(model.Definition.key);
        if (portraitAddress == address && CharaIcon.sprite != null)
        {
            CharaIcon.enabled = true;
            return;
        }
        portraitCancellation?.Cancel();
        portraitCancellation?.Dispose();
        portraitAddress = address;
        portraitCancellation = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        LoadPortraitAsync(address, portraitCancellation.Token).Forget();
    }

    void UpdatePortraitSize()
    {
        var portraitRect = CharaIcon.rectTransform;
        // Only HUD side portraits use native dimensions; the regular avatar keeps the prefab box.
        fallbackPortraitSize ??= portraitRect.sizeDelta;
        float scale = isSelected ? activePortraitScale : standbyPortraitScale;
        portraitRect.sizeDelta = boundPortrait != null ? boundPortrait.rect.size * scale : fallbackPortraitSize.Value;
    }

    async UniTask LoadPortraitAsync(string address, CancellationToken token)
    {
        CharaIcon.enabled = false;
        try
        {
            await IconLoader.SetSpriteAsync(CharaIcon, address, token);
            if (!token.IsCancellationRequested && this != null)
                CharaIcon.enabled = CharaIcon.sprite != null;
        }
        catch (OperationCanceledException) { }
    }

    void OnDisable()
    {
        characterSubscriptions.Clear();
        portraitCancellation?.Cancel();
        portraitCancellation?.Dispose();
        portraitCancellation = null;
    }

    void OnDestroy()
    {
        characterSubscriptions.Dispose();
    }
    /// <summary>
    /// 是否被选中
    /// </summary>
    bool isSelected;

    /// <summary>
    /// 由外部面板管理点击回调
    /// </summary>
    public Action onClick;
    
    
    /// <summary>
    /// 预留，之后会有更复杂的初始化（）
    /// </summary>
    /// <param name="slotIndex"></param>
    /// <param name="data"></param>
    public void Init(PlayerStateEvent data)
    {
        playerdata = data;
        UpdateVisualize();
    }

    public void SetData(PlayerStateEvent data)
    {
        playerdata = data;
        UpdateVisualize();
    }
    
    public void UpdateVisualize()
    {
        bar.SetValue(playerdata.hp, playerdata.maxHp);
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        onClick?.Invoke();
    }

    /// <summary>
    /// 更新被选中显示
    /// </summary>
    /// <param name="selected"></param>
    public void SetSelect(bool selected)
    {
        isSelected = selected;
        UpdatePortraitSize();
        // The active marker replaces this character's small HP bar; the row itself keeps its fixed height.
        CharaNameText.rectTransform.anchoredPosition = selected ? activeNamePosition : standbyNamePosition;
        IndexText.color = new Color(.23f, .23f, .23f, selected ? .6f : 1f);
        // Standalone test slots may omit the HUD keycap background.
        if (keyHintBackground != null)
        {
            keyHintBackground.color = new Color(1f, 1f, 1f, selected ? .5f : 1f);
            activeBackdrop.SetActive(selected);
            standbyBackdrop.SetActive(!selected);
        }
        bar.gameObject.SetActive(!selected);
        SelectedImage.gameObject.SetActive(selected);
    }
    

}

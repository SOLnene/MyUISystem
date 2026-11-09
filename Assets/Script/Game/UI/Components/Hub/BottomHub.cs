using System;
using System.Collections;
using System.Collections.Generic;
using SkierFramework;
using TMPro;
using UniRx;
using UnityEngine;

/// <summary>
/// 等级和血条
/// 如果没有复用或更复杂的逻辑的话可以直接作为uicontroldata让hubroot来赋值
/// 或许可以复用到别的血条上
/// </summary>
public class BottomHub : MonoBehaviour,IBindableUI
{
    #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
    [ControlBinding]
    private BarWithText barBg;
    [ControlBinding]
    private TextMeshProUGUI LevelText;

		#pragma warning restore 0649
#endregion
    
    //TODO:换用专有事件？
    EventBinding<PlayerStateEvent> binding;
    readonly CompositeDisposable selectionSubscriptions = new();
    readonly CompositeDisposable characterSubscriptions = new();
    HubTeamViewModel teamViewModel;

    internal void BindModel(HubTeamViewModel viewModel)
    {
        GetComponent<UIControlData>().BindDataTo(this);
        selectionSubscriptions.Clear();
        characterSubscriptions.Clear();
        teamViewModel = viewModel;
        if (viewModel == null)
        {
            LevelText.gameObject.SetActive(false);
            barBg.gameObject.SetActive(false);
            return;
        }
        viewModel.ActiveCharacter.Subscribe(character =>
        {
            characterSubscriptions.Clear();
            LevelText.gameObject.SetActive(character != null);
            barBg.gameObject.SetActive(character != null);
            if (character == null) return;
            Observable.CombineLatest(character.LevelRP, character.CurrentHpRP, character.Stats.FinalHP,
                    (level, hp, maximum) => new PlayerStateEvent
                    {
                        level = level,
                        hp = Mathf.CeilToInt(hp),
                        maxHp = Mathf.CeilToInt(maximum)
                    })
                .Subscribe(UpdateState).AddTo(characterSubscriptions);
        }).AddTo(selectionSubscriptions);
    }
    
    void OnEnable()
    {
        UIControlData ctrlData = gameObject.GetComponent<UIControlData>();
        if(ctrlData != null)
        {
            ctrlData.BindDataTo(this);
        }
        binding = new EventBinding<PlayerStateEvent>(state =>
        {
            // Legacy preview events cannot overwrite the character currently bound by the real HUD.
            if (teamViewModel == null) UpdateState(state);
        });
        EventBus<PlayerStateEvent>.Register(binding);
        if (teamViewModel != null) BindModel(teamViewModel);
    }

    public void UpdateState(PlayerStateEvent playerStateEvent)
    {
        LevelText.text = "Lv." + playerStateEvent.level;
        barBg.SetValue(playerStateEvent.hp, playerStateEvent.maxHp);
    }

    void OnDisable()
    {
        selectionSubscriptions.Clear();
        characterSubscriptions.Clear();
        EventBus<PlayerStateEvent>.Deregister(binding);
    }

    void OnDestroy()
    {
        selectionSubscriptions.Dispose();
        characterSubscriptions.Dispose();
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using SkierFramework;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class GachaResultListView : BindableUI
{
    #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
    [ControlBinding]
    private HorizontalLayoutGroup itemContainer;
    [ControlBinding]
    private GachaResultItemView[] resultItem;

		#pragma warning restore 0649
#endregion
	

	ResultPopupState currentState = ResultPopupState.Idle;

	CancellationTokenSource tcs;
	
	public async UniTask PlayEnter(List<GachaResultItemView> results)
	{
		tcs = new CancellationTokenSource();
		var token = tcs.Token;
		if (currentState != ResultPopupState.Idle)
		{
			return;
		}
		currentState = ResultPopupState.Playing;
		//itemContainer.enabled = false;
		try
		{
			for (int i = 0; i < results.Count; i++)
			{
				token.ThrowIfCancellationRequested();
				results[i].PlayEnter(token).Forget();
				await UniTask.Delay(150, cancellationToken: token);
			}
			currentState = ResultPopupState.Finished;
			SetItemClickEnabled(results, true);
		}
		catch (OperationCanceledException)
		{
			currentState = ResultPopupState.Finished;
			SetItemClickEnabled(results, true);
		}
		
		//itemContainer.enabled = true;
	}

	public void ResetToIdle(List<GachaResultItemView> results)
	{
		currentState = ResultPopupState.Idle;
		for (int i = 0; i < results.Count; i++)
		{
			results[i].ResetForEnter();
		}
		SetItemClickEnabled(results, false);
	}

	public void SetClick(ReactiveCommand<GachaEntryViewModel> command)
	{
		foreach (var item in resultItem)
		{
			item.clickCommand = command;
		}
	}
	
	public void SkipToEnd(List<GachaResultItemView> results)
	{
		if (currentState != ResultPopupState.Playing)
		{
			return;
		}
		
		tcs?.Cancel();
		
		foreach (var result in results)
		{
			result.SkipToEnd();
		}
		currentState = ResultPopupState.Finished;
		SetItemClickEnabled(results, true);
	}

	void SetItemClickEnabled(List<GachaResultItemView> results, bool enabled)
	{
		for (int i = 0; i < results.Count; i++)
		{
			results[i].SetClickEnabled(enabled);
		}
	}
    
	public bool IsFinished()
	{
		return currentState == ResultPopupState.Finished;
	}

	public void Cancel()
	{
		
	}
}

enum ResultPopupState
{
	Idle,           // 未初始化
	Playing,        // 正在播放总动画
	Finished,  // 所有结果已展示（静态）
}

using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using SkierFramework;
using UnityEngine;
using UnityEngine.UI;

public class GachaTabFeedback : BindableUI
{
    #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
	[ControlBinding]
	private Image selectBg;
	[ControlBinding]
	private Image normalBg;
	[ControlBinding]
	private Image icon;
	[ControlBinding]
	private RectTransform bgContent;

		#pragma warning restore 0649
#endregion



	Vector2 iconBasePos;

	Sequence seq;

	protected override void AfterBind()
	{
		iconBasePos = icon.rectTransform.anchoredPosition;
	}

	public UniTask SetSelected(bool selected)
	{
		seq?.Kill();
		if (selected)
		{
			seq = DOTween.Sequence()
				.Append(icon.rectTransform
					.DOAnchorPos(iconBasePos + Vector2.up * 10, 0.35f)
					.SetEase(Ease.OutCubic));
			seq.Join(selectBg.DOFade(1, 0.2f)
				.SetEase(Ease.OutCubic));
			seq.Join(normalBg.DOFade(0, 0.2f)
				.SetEase(Ease.OutCubic));
			seq.Join(bgContent.DOScale(1.1f, 0.2f)
				.SetEase(Ease.OutCubic));
		}
		else
		{
			seq = DOTween.Sequence()
				.Append(icon.rectTransform
					.DOAnchorPos(iconBasePos, 0.35f)
					.SetEase(Ease.OutCubic));
			seq.Join(selectBg.DOFade(0, 0.2f)
				.SetEase(Ease.OutCubic));
			seq.Join(normalBg.DOFade(1, 0.2f)
				.SetEase(Ease.OutCubic));
			seq.Join(bgContent.DOScale(1f, 0.2f)
				.SetEase(Ease.OutCubic));
		}

		return seq.AsyncWaitForCompletion().AsUniTask();
	}
	
	public void ResetToIdle()
	{
		
	}
}

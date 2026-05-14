using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class ProgressBarView : MonoBehaviour
{
	[SerializeField]
	Image fillImage;
	[SerializeField]
	Image previewFillImage;
	
	Tween valueTween;
	int playVersion;

	public void SetValue(int current, int max, int preview = -1)
	{
	    if (max <= 0)
	    {
		    SetFill(fillImage, 0f);
		    SetFill(previewFillImage, 0f);
		    return;
	    }

	    SetValue((float)current / max, preview < 0 ? -1f : (float)preview / max);
	}
	
	public void SetValue(float percent, float preview = -1.0f)
	{
		SetFill(fillImage, percent);

		if (preview < 0 || float.IsNaN(preview) || float.IsInfinity(preview))
		{
			SetFill(previewFillImage, 0f);
			return;
		}

		SetFill(previewFillImage, preview);
	}

	public async UniTask PlayValue(float from, float to, float duration)
	{
		int version = ++playVersion;
		valueTween?.Kill();

		from = Mathf.Clamp01(from);
		to = Mathf.Clamp01(to);
		SetFill(fillImage, from);

		if (duration <= 0f || fillImage == null)
		{
			SetFill(fillImage, to);
			valueTween = null;
			return;
		}

		valueTween = DOTween.To(
			() => fillImage.fillAmount,
			value => fillImage.fillAmount = value,
			to,
			duration
		).SetEase(Ease.OutCubic);

		await valueTween.AsyncWaitForCompletion();
		if (version != playVersion)
			return;

		SetFill(fillImage, to);
		valueTween = null;
	}

	public async UniTask PlaySegmentedValue(float from, float to, int fullSegmentCount, float duration, int maxVisibleFullSegments = 5)
	{
		if (fullSegmentCount <= 0)
		{
			await PlayValue(from, to, duration);
			return;
		}

		int visibleFullSegments = Mathf.Min(fullSegmentCount, Mathf.Max(1, maxVisibleFullSegments));
		int segmentCount = visibleFullSegments + 1;
		float segmentDuration = duration <= 0f ? 0f : duration / segmentCount;

		await PlayValue(from, 1f, segmentDuration);

		for (int i = 1; i < visibleFullSegments; i++)
		{
			await PlayValue(0f, 1f, segmentDuration);
		}

		await PlayValue(0f, to, segmentDuration);
	}

	static void SetFill(Image image, float value)
	{
		if (image != null)
		{
			image.fillAmount = Mathf.Clamp01(value);
		}
	}

	void OnDestroy()
	{
		++playVersion;
		valueTween?.Kill();
		valueTween = null;
	}

}

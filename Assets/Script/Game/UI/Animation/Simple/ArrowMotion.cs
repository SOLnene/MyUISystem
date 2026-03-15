using System.Collections;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// 让箭头在指定方向上重复往返移动的动画（使用DOTween）
/// </summary>
public class ArrowMotion : MonoBehaviour
{
    [Header("运动偏移（本地坐标）")]
    public Vector3 moveOffset = new Vector3(0, 30f, 0);
    [Header("往返一次所需时间(s)")]
    public float duration = 0.6f;
    [Header("是否在Awake时自动播放")]
    public bool autoPlay = true;

    private Vector3 originLocalPos;
    private Tweener tweener;

    private void Awake()
    {
        originLocalPos = transform.localPosition;
    }

    private void OnEnable()
    {
        if (autoPlay)
        {
            PlayMotion();
        }
    }

    private void OnDisable()
    {
        StopMotion();
    }

    public void PlayMotion()
    {
        StopMotion();
        tweener = transform.DOLocalMove(originLocalPos + moveOffset, duration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    public void StopMotion()
    {
        if (tweener != null && tweener.IsActive())
        {
            tweener.Kill();
        }
        // 回到初始位置
        transform.localPosition = originLocalPos;
    }
}

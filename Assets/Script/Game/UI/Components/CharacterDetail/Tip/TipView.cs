using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

public class TipView : UIView
{
    [SerializeField]
    TextMeshProUGUI tipText;
    [SerializeField]
    AnimatedPanel animatedPanel;
    [SerializeField]
    float stayDuration = 2f;

    int showVersion;

    void Awake()
    {
        if (animatedPanel == null)
        {
            animatedPanel = GetComponent<AnimatedPanel>();
        }

        animatedPanel?.HideImmediate();
    }

    public void SetText(string text)
    {
        if (tipText != null)
        {
            tipText.text = text ?? string.Empty;
        }
    }

    public void Show(string text)
    {
        ShowAsync(text).Forget();
    }

    async UniTask ShowAsync(string text)
    {
        int version = ++showVersion;

        SetText(text);

        if (animatedPanel != null)
        {
            await animatedPanel.Show();
        }
        else
        {
            gameObject.SetActive(true);
        }

        await UniTask.Delay(
            TimeSpan.FromSeconds(stayDuration),
            cancellationToken: this.GetCancellationTokenOnDestroy());

        if (version != showVersion)
        {
            return;
        }

        if (animatedPanel != null)
        {
            await animatedPanel.Hide();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}

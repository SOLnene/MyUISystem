using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CostItemView : MonoBehaviour
{
    [SerializeField]
    Image icon;
    [SerializeField]
    TextMeshProUGUI currentValueText;
    [SerializeField]
    TextMeshProUGUI requireValueText;
    [SerializeField]
    Color enoughColor = Color.white;
    [SerializeField]
    Color insufficientColor = new Color(1f, 0.23f, 0.18f, 1f);

    int iconRequestVersion;
    CancellationTokenSource loadCts;

    public void Set(Sprite iconSprite, int currentValue, int requireValue)
    {
        SetIcon(iconSprite);
        SetCount(currentValue, requireValue);
    }

    public void Set(string iconPath, int currentValue, int requireValue)
    {
        SetIconPath(iconPath);
        SetCount(currentValue, requireValue);
    }

    public void SetCount(int currentValue, int requireValue)
    {
        currentValueText.text = currentValue.ToString();
        requireValueText.text = requireValue.ToString();
        currentValueText.color = currentValue >= requireValue ? enoughColor : insufficientColor;
    }

    public void SetIcon(Sprite iconSprite)
    {
        ++iconRequestVersion;
        icon.sprite = iconSprite;
    }

    public void SetIconPath(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
        {
            return;
        }

        loadCts?.Cancel();
        loadCts?.Dispose();
        loadCts = new CancellationTokenSource();
        LoadIconAsync(iconPath, ++iconRequestVersion, loadCts.Token).Forget();
    }

    async UniTask LoadIconAsync(string iconPath, int requestVersion, CancellationToken cancellationToken)
    {
        Sprite sprite;
        try
        {
            sprite = await ResourceManager.Instance.LoadAssetAsync<Sprite>(iconPath)
                .AttachExternalCancellation(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (this == null || requestVersion != iconRequestVersion)
        {
            return;
        }

        icon.sprite = sprite;
    }

    void OnDestroy()
    {
        loadCts?.Cancel();
        loadCts?.Dispose();
    }
}

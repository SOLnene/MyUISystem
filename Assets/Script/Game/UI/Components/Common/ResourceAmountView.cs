using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResourceAmountView : MonoBehaviour
{
    [SerializeField]
    Image icon;
    [SerializeField]
    TextMeshProUGUI amountText;

    string currentIconPath;
    int iconRequestVersion;
    CancellationTokenSource iconLoadCts;

    public void Bind(string iconPath, int amount)
    {
        currentIconPath = iconPath;
        SetAmount(amount);
        LoadIcon(iconPath);
    }

    public void SetAmount(int amount)
    {
        amountText.text = amount.ToString();
    }

    void LoadIcon(string iconPath)
    {
        CancelIconLoad();
        icon.sprite = null;
        if (string.IsNullOrEmpty(iconPath))
        {
            return;
        }

        int requestVersion = iconRequestVersion;
        var requestCts = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());
        iconLoadCts = requestCts;
        LoadIconAsync(iconPath, requestVersion, requestCts).Forget();
    }

    async UniTask LoadIconAsync(
        string iconPath,
        int requestVersion,
        CancellationTokenSource requestCts)
    {
        try
        {
            Sprite sprite = await ResourceManager.Instance.LoadAssetAsync<Sprite>(
                iconPath,
                requestCts.Token);
            if (requestVersion != iconRequestVersion ||
                !ReferenceEquals(iconLoadCts, requestCts) ||
                currentIconPath != iconPath)
            {
                return;
            }

            icon.sprite = sprite;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(iconLoadCts, requestCts))
            {
                iconLoadCts = null;
                requestCts.Dispose();
            }
        }
    }

    void CancelIconLoad()
    {
        iconRequestVersion++;
        CancellationTokenSource requestCts = iconLoadCts;
        iconLoadCts = null;
        requestCts?.Cancel();
        requestCts?.Dispose();
    }

    void OnDisable()
    {
        CancelIconLoad();
    }

    void OnDestroy()
    {
        CancelIconLoad();
    }
}

using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CurrencyValueView : MonoBehaviour
{
    [SerializeField]
    Image icon;
    [SerializeField]
    TextMeshProUGUI amountText;

    CancellationTokenSource iconLoadCts;

    public void Bind(string iconPath, int amount)
    {
        SetAmount(amount);
        LoadIcon(iconPath);
    }

    public void SetAmount(int amount)
    {
        amountText.text = amount.ToString();
    }

    void CancelIconLoad()
    {
        iconLoadCts?.Cancel();
        iconLoadCts?.Dispose();
        iconLoadCts = null;
    }

    void LoadIcon(string iconPath)
    {
        CancelIconLoad();
        iconLoadCts = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());
        IconLoader.SetSpriteAsync(icon, iconPath, iconLoadCts.Token).Forget();
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

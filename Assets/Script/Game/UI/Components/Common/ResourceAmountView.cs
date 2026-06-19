using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResourceAmountView : MonoBehaviour
{
    [SerializeField]
    Image icon;
    [SerializeField]
    TextMeshProUGUI amountText;

    CancellationTokenSource iconLoadCts;

    public void Bind(string iconPath, int amount)
    {
        SetAmount(amount);
        iconLoadCts = IconLoader.LoadSpriteAsync(icon, iconPath, this, iconLoadCts);
    }

    public void SetAmount(int amount)
    {
        amountText.text = amount.ToString();
    }

    void CancelIconLoad()
    {
        IconLoader.Cancel(iconLoadCts);
        iconLoadCts = null;
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

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class GachaView
{
    [SerializeField] CurrencyValueView primogemBalance;
    [SerializeField] CurrencyValueView fateBalance;
    [SerializeField] CurrencyValueView singleDrawCost;
    [SerializeField] CurrencyValueView tenDrawCost;
    [SerializeField] GameObject paymentConfirmationRoot;
    [SerializeField] TMP_Text paymentConfirmationText;
    [SerializeField] Button paymentConfirmButton;
    [SerializeField] Button paymentCancelButton;

    int activeFateItemId;
    int pendingDrawCount;
    int pendingPrimogemLimit;
    long pendingPaymentVersion;
    string pendingPaymentPool;

    void InitializePaymentControls()
    {
        paymentConfirmationRoot.SetActive(false);
        paymentConfirmButton.onClick.AddListener(ConfirmPayment);
        paymentCancelButton.onClick.AddListener(CancelPaymentConfirmation);
    }

    void RefreshPaymentDisplay()
    {
        var economy = GameEconomy.Instance;
        if (activeFateItemId == 0)
            primogemBalance.Bind(GameDatabase.ItemDatabase.GetItemByID(201).iconPath, economy.GetCurrency(201));
        else primogemBalance.SetAmount(economy.GetCurrency(201));
        var selected = vm.CurrentPoolConfig.Value;
        bool hasPool = selected != null;
        fateBalance.gameObject.SetActive(hasPool);
        singleDrawCost.gameObject.SetActive(hasPool);
        tenDrawCost.gameObject.SetActive(hasPool);
        if (!hasPool)
        {
            // 隐藏会取消图标加载，恢复相同消耗类型时也必须重新绑定。
            activeFateItemId = 0;
            return;
        }
        var cost = boundGachaService.GetCost(selected.gachaKey);
        if (activeFateItemId != cost.fateItemId)
        {
            string icon = GameDatabase.ItemDatabase.GetItemByID(cost.fateItemId).iconPath;
            fateBalance.Bind(icon, economy.GetCurrency(cost.fateItemId));
            singleDrawCost.Bind(icon, 1);
            tenDrawCost.Bind(icon, 10);
            activeFateItemId = cost.fateItemId;
        }
        else fateBalance.SetAmount(economy.GetCurrency(cost.fateItemId));
    }

    GachaPaymentQuote PreviewPayment(int count, GachaCost cost)
    {
        var economy = GameEconomy.Instance;
        return cost.Quote(count, economy.GetCurrency(cost.fateItemId), economy.GetCurrency(201));
    }

    void RequestDraw(int count)
    {
        if (paymentConfirmationRoot.activeSelf) return;
        // A pending request already owns its payment consent; retry it before evaluating a new quote.
        if (CloudGameSaveSession.Current?.HasPendingGacha ?? false)
        {
            vm.DrawWithPayment(count, 0);
            return;
        }
        var selected = vm.CurrentPoolConfig.Value;
        var cost = boundGachaService.GetCost(selected.gachaKey);
        var quote = PreviewPayment(count, cost);
        if (!quote.CanAfford)
        {
            ShowConfigRefreshStatus("原石和祈愿道具不足，无法进行本次祈愿。", false);
            return;
        }
        if (quote.PrimogemCost == 0)
        {
            vm.DrawWithPayment(count, 0);
            return;
        }
        pendingDrawCount = count;
        pendingPrimogemLimit = quote.PrimogemCost;
        pendingPaymentPool = selected.gachaKey;
        pendingPaymentVersion = boundGachaService.ConfigurationVersion;
        string fateName = GameDatabase.ItemDatabase.GetItemByID(cost.fateItemId).itemName;
        paymentConfirmationText.text = $"还缺少 {count - quote.FatesSpent} 个{fateName}，是否消耗 {quote.PrimogemCost} 原石补足并进行 {count} 次祈愿？";
        paymentConfirmationRoot.SetActive(true);
        ApplyInputState();
    }

    void ConfirmPayment()
    {
        if (!paymentConfirmationRoot.activeSelf || isClosing || vm.isDrawing.Value) return;
        int count = pendingDrawCount;
        int limit = pendingPrimogemLimit;
        bool samePool = vm.CurrentPoolConfig.Value?.gachaKey == pendingPaymentPool &&
            boundGachaService.ConfigurationVersion == pendingPaymentVersion;
        CancelPaymentConfirmation();
        if (!samePool || !configurationVerified)
        {
            ShowConfigRefreshStatus("卡池已更新，请重新确认祈愿消耗。", false);
            return;
        }
        var quote = PreviewPayment(count, boundGachaService.GetCost(vm.CurrentPoolConfig.Value.gachaKey));
        if (!quote.CanAfford)
        {
            ShowConfigRefreshStatus("原石和祈愿道具不足，无法进行本次祈愿。", false);
            return;
        }
        // Inventory may change while the confirmation is open; an increased cost needs fresh consent.
        if (quote.PrimogemCost > limit)
        {
            RequestDraw(count);
            return;
        }
        vm.DrawWithPayment(count, limit);
    }

    void CancelPaymentConfirmation()
    {
        paymentConfirmationRoot.SetActive(false);
        pendingDrawCount = 0;
        pendingPrimogemLimit = 0;
        pendingPaymentPool = null;
        ApplyInputState();
    }
}

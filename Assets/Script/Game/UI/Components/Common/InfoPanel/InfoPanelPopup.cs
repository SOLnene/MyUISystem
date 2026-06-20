using Cysharp.Threading.Tasks;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class InfoPanelPopup : UIView
{
    [SerializeField]
    InfoPanelView infoPanelView;
    [SerializeField]
    TextMeshProUGUI currentAmountText;
    [SerializeField]
    Button closeHandle;
    [SerializeField]
    AnimatedPanel contentAnimatedPanel;

    bool isClosing;

    public override bool RefreshWhenAlreadyOpen => true;

    public override void OnOpen(object data)
    {
        isClosing = false;
        base.OnOpen(data);
        Show(data);
        contentAnimatedPanel.Show().Forget();
    }

    public override void OnClose()
    {
        contentAnimatedPanel.HideImmediate();
        infoPanelView.Hide();
        isClosing = false;
        base.OnClose();
    }

    public override void OnCancel()
    {
        CloseAsync().Forget();
    }

    public override void OnAddListener()
    {
        base.OnAddListener();
        closeHandle.onClick.RemoveListener(OnCloseHandleClicked);
        closeHandle.onClick.AddListener(OnCloseHandleClicked);
    }

    public override void OnRemoveListener()
    {
        closeHandle.onClick.RemoveListener(OnCloseHandleClicked);
        base.OnRemoveListener();
    }

    void Show(object data)
    {
        if (data is ItemViewModel itemViewModel)
        {
            Show(itemViewModel);
            return;
        }

        if (data is ItemDefinition itemDefinition)
        {
            Show(itemDefinition);
            return;
        }

        Debug.LogError($"InfoPanelPopupView open data is invalid: {data?.GetType().Name ?? "null"}");
    }

    void Show(ItemViewModel itemViewModel)
    {
        if (itemViewModel == null)
        {
            Debug.LogError("InfoPanelPopupView cannot show null item.");
            return;
        }

        infoPanelView.Show(itemViewModel);
    }

    void Show(ItemDefinition itemDefinition)
    {
        if (itemDefinition == null)
        {
            Debug.LogError("InfoPanelPopupView cannot show null item definition.");
            return;
        }

        infoPanelView.Show(new ItemViewModel(new InventoryItem(itemDefinition)));
    }

    public void SetCurrentAmount(string text)
    {
        currentAmountText.text = text;
    }

    void OnCloseHandleClicked()
    {
        OnCancel();
    }

    async UniTask CloseAsync()
    {
        if (isClosing)
        {
            return;
        }

        isClosing = true;
        await contentAnimatedPanel.Hide();
        base.OnCancel();
    }
}

using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public abstract class SelectionPanelView : MonoBehaviour
{
    [SerializeField]
    protected Transform content;
    // 全屏点击遮罩
    [SerializeField]
    protected Button clickHandler;
    [SerializeField]
    protected AnimatedPanel animatedPanel;

    protected readonly CompositeDisposable disposable = new();
    protected int slotCreateVersion;

    public void Show(object data)
    {
        gameObject.SetActive(true);
        disposable.Clear();
        slotCreateVersion++;
        OnShow(data);

        if (clickHandler != null)
        {
            clickHandler.onClick.RemoveAllListeners();
            clickHandler.onClick.AddListener(OnClickHandlerClicked);
        }

        if (animatedPanel != null)
        {
            animatedPanel.Show().Forget();
        }
    }

    public void Hide()
    {
        HideAsync().Forget();
    }

    async UniTask HideAsync()
    {
        slotCreateVersion++;

        if (clickHandler != null)
        {
            clickHandler.onClick.RemoveAllListeners();
        }

        OnBeforeHide();

        if (animatedPanel != null)
        {
            await animatedPanel.Hide();
        }
        else
        {
            gameObject.SetActive(false);
        }

        OnHidden();
    }

    protected abstract void OnShow(object data);

    protected virtual void OnBeforeHide()
    {
    }

    protected virtual void OnHidden()
    {
    }

    protected virtual void OnCancelRequested()
    {
    }

    void OnClickHandlerClicked()
    {
        OnCancelRequested();
        Hide();
    }
}

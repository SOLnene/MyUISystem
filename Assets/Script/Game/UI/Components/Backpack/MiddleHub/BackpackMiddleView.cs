using Cysharp.Threading.Tasks;
using UnityEngine;

public class BackpackMiddleView : MonoBehaviour
{
    [SerializeField]
    BackpackItemGridView itemGridView;
    [SerializeField]
    InfoPanelView infoPanelView;
    [SerializeField]
    BackpackVirtualGridView virtualItemGridView;
    [SerializeField]
    AnimatedPanel anim;

    public UniTask Show()
    {
        return anim.Show();
    }

    public async UniTask Hide()
    {
        await anim.Hide();
    }

    public void Bind(BackpackMiddleViewModel middleVM, InfoPanelViewModel infoVM)
    {
        //itemGridView.Bind(middleVM);
        virtualItemGridView.Bind(middleVM);
        infoPanelView.Bind(infoVM);
    }
}

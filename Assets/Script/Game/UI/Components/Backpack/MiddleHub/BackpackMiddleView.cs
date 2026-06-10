using UnityEngine;

public class BackpackMiddleView : MonoBehaviour
{
    [SerializeField]
    BackpackItemGridView itemGridView;
    [SerializeField]
    InfoPanelView infoPanelView;

    public void Bind(BackpackMiddleViewModel middleVM, InfoPanelViewModel infoVM)
    {
        itemGridView.Bind(middleVM);
        infoPanelView.Bind(infoVM);
    }
}

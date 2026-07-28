using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RewardPopupView : UIView
{
    [SerializeField]
    Button closeHandle;
    [SerializeField]
    TextMeshProUGUI titleText;
    [SerializeField]
    TextMeshProUGUI sectionTitleText;
    [SerializeField]
    RewardListView rewardListView;

    public override void OnAddListener()
    {
        closeHandle.onClick.RemoveListener(HandleClose);
        closeHandle.onClick.AddListener(HandleClose);
    }

    public override void OnRemoveListener()
    {
        closeHandle.onClick.RemoveListener(HandleClose);
    }

    public override void OnOpen(object data)
    {
        base.OnOpen(data);

        if (data is not RewardPopupDisplayData displayData)
        {
            rewardListView.Clear();
            return;
        }

        titleText.text = displayData.Title;
        sectionTitleText.text = displayData.SectionTitle;
        rewardListView.Bind(displayData.Items);
    }

    public override void OnRelease()
    {
        rewardListView.Clear();
    }

    void HandleClose()
    {
        if (Handle == null)
        {
            gameObject.SetActive(false);
            return;
        }

        OnCancel();
    }
}

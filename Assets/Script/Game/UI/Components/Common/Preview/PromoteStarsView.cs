using UnityEngine;
using UnityEngine.UI;

public class PromoteStarsView : MonoBehaviour
{
    [SerializeField]
    Graphic[] starIcons;
    [SerializeField]
    Color activeColor = Color.white;
    [SerializeField]
    Color inactiveColor = Color.grey;

    public void SetRank(int rank)
    {
        int activeCount = Mathf.Clamp(rank, 0, starIcons.Length);
        for (int i = 0; i < starIcons.Length; i++)
        {
            starIcons[i].color = i < activeCount ? activeColor : inactiveColor;
        }
    }
}

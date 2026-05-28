using TMPro;
using UnityEngine;

public class LevelCapLineView : MonoBehaviour
{
    public enum VisualState
    {
        AllDim,
        ValuesNormal,
        MaxHighlighted
    }

    [SerializeField]
    TextMeshProUGUI labelText;
    [SerializeField]
    TextMeshProUGUI currentLevelText;
    [SerializeField]
    TextMeshProUGUI slashText;
    [SerializeField]
    TextMeshProUGUI maxLevelText;

    [SerializeField]
    Color dimColor = new Color(1f, 1f, 1f, 0.45f);
    [SerializeField]
    Color labelDimColor = new Color(1f, 1f, 1f, 0.65f);
    [SerializeField]
    Color valueColor = Color.white;
    [SerializeField]
    Color highlightColor = new Color(1f, 0.78f, 0.25f, 1f);

    public void SetValue(int currentLevel, int maxLevel)
    {
        currentLevelText.text = currentLevel.ToString();
        maxLevelText.text = maxLevel.ToString();
    }

    public void SetState(VisualState state)
    {
        switch (state)
        {
            case VisualState.AllDim:
                ApplyColors(dimColor, dimColor, dimColor, dimColor);
                break;
            case VisualState.ValuesNormal:
                ApplyColors(labelDimColor, valueColor, valueColor, valueColor);
                break;
            case VisualState.MaxHighlighted:
                ApplyColors(labelDimColor, valueColor, valueColor, highlightColor);
                break;
        }
    }

    public void SetValueAndState(int currentLevel, int maxLevel, VisualState state)
    {
        SetValue(currentLevel, maxLevel);
        SetState(state);
    }

    void ApplyColors(Color label, Color currentLevel, Color slash, Color maxLevel)
    {
        labelText.color = label;
        currentLevelText.color = currentLevel;
        slashText.color = slash;
        maxLevelText.color = maxLevel;
    }
}

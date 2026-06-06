using Cysharp.Threading.Tasks;
using UnityEngine;

public static class UIFxLoader
{
    const string LevelUpFxAddress = "ui/particle/levelupfx";

    public static async UniTask<ResultAccentFxView> CreateLevelUpFxAsync(Transform parent)
    {
        if (parent == null)
            return null;

        GameObject fxObject = await ResourceManager.Instance.InstantiateItemAsync(LevelUpFxAddress, parent);
        if (fxObject == null)
            return null;

        ResetLocalTransform(fxObject.transform);
        return fxObject.GetComponent<ResultAccentFxView>();
    }

    static void ResetLocalTransform(Transform target)
    {
        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;

        if (target is RectTransform rectTransform)
            rectTransform.anchoredPosition = Vector2.zero;
        else
            target.localPosition = Vector3.zero;
    }
}

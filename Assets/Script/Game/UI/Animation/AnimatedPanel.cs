using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class AnimatedPanel : MonoBehaviour
{
    [SerializeField]
    GameObject panelRoot;
    [SerializeField]
    UIMotionBase motion;
    [SerializeField]
    CanvasGroup inputGroup;

    int requestVersion;

    void Awake()
    {
        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        if (inputGroup == null)
        {
            inputGroup = GetComponent<CanvasGroup>();
        }
    }

    public async UniTask Show(bool instant = false)
    {
        int version = ++requestVersion;

        panelRoot.SetActive(true);
        SetInteractable(false);

        if (motion != null)
        {
            await motion.PlayEnter(instant);
        }

        if (version != requestVersion)
        {
            return;
        }

        SetInteractable(true);
    }

    public async UniTask<bool> Hide(bool instant = false)
    {
        int version = ++requestVersion;

        SetInteractable(false);

        if (motion != null)
        {
            await motion.PlayExit(instant);
        }

        if (version != requestVersion)
        {
            return false;
        }

        panelRoot.SetActive(false);
        return true;
    }

    public void HideImmediate()
    {
        ++requestVersion;
        SetInteractable(false);
        motion?.Cancel();
        panelRoot.SetActive(false);
    }

    void SetInteractable(bool interactable)
    {
        if (inputGroup == null)
        {
            return;
        }

        inputGroup.interactable = interactable;
        inputGroup.blocksRaycasts = interactable;
    }
}

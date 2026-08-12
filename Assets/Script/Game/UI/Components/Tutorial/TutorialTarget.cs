using System;
using System.Collections.Generic;
using UnityEngine;

internal enum TutorialTargetId
{
    // 数值已被 prefab 序列化；新增成员只能使用新编号，不能重排或复用旧编号。
    None = 0,
    MainMenuCharacter = 1,
    CharacterEnhanceEntry = 2,
    CharacterQuickAdd = 3,
    CharacterUpgrade = 4,
    CharacterEnhancePanel = 5,
}

internal static class TutorialTargetRegistry
{
    readonly struct TargetEntry
    {
        public readonly RectTransform RectTransform;
        public readonly UnityEngine.Object Owner;

        public TargetEntry(RectTransform rectTransform, UnityEngine.Object owner)
        {
            RectTransform = rectTransform;
            Owner = owner;
        }
    }

    static readonly Dictionary<TutorialTargetId, TargetEntry> Targets = new();

    public static event Action<TutorialTargetId, RectTransform> Registered;
    public static event Action<TutorialTargetId, RectTransform> Unregistered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        Targets.Clear();
        Registered = null;
        Unregistered = null;
    }

    public static void Register(
        TutorialTargetId targetId,
        RectTransform rectTransform,
        UnityEngine.Object owner)
    {
        if (targetId == TutorialTargetId.None)
        {
            return;
        }

        if (Targets.TryGetValue(targetId, out TargetEntry existing)
            && existing.Owner != owner)
        {
            Debug.LogError($"Tutorial target {targetId} has more than one active owner.", owner);
            return;
        }

        Targets[targetId] = new TargetEntry(rectTransform, owner);
        Registered?.Invoke(targetId, rectTransform);
    }

    public static void Unregister(TutorialTargetId targetId, UnityEngine.Object owner)
    {
        if (!Targets.TryGetValue(targetId, out TargetEntry existing)
            || existing.Owner != owner)
        {
            return;
        }

        Targets.Remove(targetId);
        Unregistered?.Invoke(targetId, existing.RectTransform);
    }

    public static bool TryGet(TutorialTargetId targetId, out RectTransform rectTransform)
    {
        if (Targets.TryGetValue(targetId, out TargetEntry entry)
            && entry.RectTransform != null
            && entry.RectTransform.gameObject.activeInHierarchy)
        {
            rectTransform = entry.RectTransform;
            return true;
        }

        rectTransform = null;
        return false;
    }
}

[DisallowMultipleComponent]
public sealed class TutorialTarget : MonoBehaviour
{
    [SerializeField] TutorialTargetId targetId;

    void OnEnable()
    {
        TutorialTargetRegistry.Register(targetId, transform as RectTransform, this);
    }

    void OnDisable()
    {
        TutorialTargetRegistry.Unregister(targetId, this);
    }
}

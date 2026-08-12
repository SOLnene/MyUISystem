#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SkierFramework
{
    internal sealed class UIBindingTypeCandidate
    {
        public string Name { get; }
        public Type Type { get; }

        public UIBindingTypeCandidate(string name, Type type)
        {
            Name = name;
            Type = type;
        }
    }

    internal static class UIBindingTypeResolver
    {
        public static List<UIBindingTypeCandidate> GetCandidates(
            CtrlItemData itemData,
            string[] fallbackNames,
            Type[] fallbackTypes)
        {
            var candidates = new List<UIBindingTypeCandidate>();
            AddCandidate(candidates, fallbackNames[0], fallbackTypes[0]);

            // 绑定项尚未填写完整时保留旧列表，避免编辑过程中类型选项突然消失。
            if (!TryGetTargetObjects(itemData, out List<GameObject> targetObjects))
            {
                AddFallbackCandidates(candidates, fallbackNames, fallbackTypes);
                AddSavedTypeCandidate(candidates, itemData);
                return candidates;
            }

            // 多目标绑定只暴露每个目标都具备的类型，保证一次切换可以整体成功。
            for (int i = 1; i < fallbackTypes.Length; i++)
            {
                if (IsAvailableOnAllTargets(targetObjects, fallbackTypes[i]))
                    AddCandidate(candidates, fallbackNames[i], fallbackTypes[i]);
            }

            if (TryGetSavedType(itemData, out Type savedType) &&
                IsAvailableOnAllTargets(targetObjects, savedType))
            {
                AddCandidate(candidates, itemData.type, savedType);
            }

            Component[] components = targetObjects[0].GetComponents<Component>();
            foreach (Component component in components)
            {
                if (component == null)
                    continue;

                Type componentType = component.GetType();
                if (IsAvailableOnAllTargets(targetObjects, componentType))
                    AddCandidate(candidates, componentType.Name, componentType);
            }

            return candidates;
        }

        private static void AddSavedTypeCandidate(
            List<UIBindingTypeCandidate> candidates,
            CtrlItemData itemData)
        {
            if (TryGetSavedType(itemData, out Type savedType))
                AddCandidate(candidates, itemData.type, savedType);
        }

        private static bool TryGetSavedType(CtrlItemData itemData, out Type savedType)
        {
            savedType = null;
            return !string.IsNullOrEmpty(itemData.type) &&
                   UIBindingTypeRegistry.TryResolve(itemData.type, out savedType);
        }

        private static bool TryGetTargetObjects(CtrlItemData itemData, out List<GameObject> targetObjects)
        {
            targetObjects = new List<GameObject>();
            if (itemData.targets == null || itemData.targets.Length == 0)
                return false;

            foreach (Object target in itemData.targets)
            {
                if (target is GameObject gameObject)
                {
                    targetObjects.Add(gameObject);
                }
                else if (target is Component component)
                {
                    targetObjects.Add(component.gameObject);
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsAvailableOnAllTargets(List<GameObject> targetObjects, Type type)
        {
            foreach (GameObject targetObject in targetObjects)
            {
                if (type != typeof(GameObject) && targetObject.GetComponent(type) == null)
                    return false;
            }

            return true;
        }

        private static void AddFallbackCandidates(
            List<UIBindingTypeCandidate> candidates,
            string[] fallbackNames,
            Type[] fallbackTypes)
        {
            for (int i = 1; i < fallbackTypes.Length; i++)
                AddCandidate(candidates, fallbackNames[i], fallbackTypes[i]);
        }

        private static void AddCandidate(List<UIBindingTypeCandidate> candidates, string name, Type type)
        {
            if (candidates.Exists(candidate => candidate.Name == name && candidate.Type == type))
                return;

            if (candidates.Exists(candidate => candidate.Name == name && candidate.Type != type))
                name = type.FullName ?? name;

            candidates.Add(new UIBindingTypeCandidate(name, type));
        }
    }
}
#endif

using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Editor
{
    public static class GameUIHierarchyCreateMenu
    {
        [MenuItem("GameObject/My UI/Page Root", false, 10)]
        public static void CreatePageRoot(MenuCommand command)
        {
            var parent = ResolveParent(command);
            var pageRoot = new GameObject("PageRoot", typeof(RectTransform), typeof(CanvasGroup));

            Undo.RegisterCreatedObjectUndo(pageRoot, "Create PageRoot");

            var rectTransform = pageRoot.GetComponent<RectTransform>();
            if (parent != null)
            {
                Undo.SetTransformParent(rectTransform, parent, "Parent PageRoot");
            }

            SetupFullScreenRect(rectTransform);
            SetupCanvasGroup(pageRoot.GetComponent<CanvasGroup>());

            Selection.activeGameObject = pageRoot;
        }

        [MenuItem("GameObject/My UI/Input Block", false, 11)]
        public static void CreateInputBlock(MenuCommand command)
        {
            var parent = ResolveParent(command);
            var inputBlock = new GameObject("InputBlock", typeof(RectTransform), typeof(Image));

            Undo.RegisterCreatedObjectUndo(inputBlock, "Create InputBlock");

            var rectTransform = inputBlock.GetComponent<RectTransform>();
            if (parent != null)
            {
                Undo.SetTransformParent(rectTransform, parent, "Parent InputBlock");
            }

            SetupFullScreenRect(rectTransform);
            SetupInputBlockImage(inputBlock.GetComponent<Image>());

            Selection.activeGameObject = inputBlock;
        }

        static Transform ResolveParent(MenuCommand command)
        {
            if (command.context is GameObject contextGo && contextGo.transform is RectTransform)
            {
                return contextGo.transform;
            }

            if (Selection.activeTransform is RectTransform)
            {
                return Selection.activeTransform;
            }

            var canvas = Object.FindObjectOfType<Canvas>();
            return canvas != null ? canvas.transform : null;
        }

        static void SetupFullScreenRect(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.anchoredPosition3D = Vector3.zero;
        }

        static void SetupCanvasGroup(CanvasGroup canvasGroup)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.ignoreParentGroups = false;
        }

        static void SetupInputBlockImage(Image image)
        {
            image.color = new Color(0f, 0f, 0f, 0.4f);
            image.raycastTarget = true;
        }
    }
}

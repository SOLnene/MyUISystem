#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace SkierFramework
{
    public static class UIBindingTypeRegistry
    {
        /// <summary>
        /// 已知类型列表，自定义类型可以添加到下面指定区域
        /// </summary>
        private static readonly Dictionary<string, Type> TypeMap = new()
        {
            { "TextMeshProUGUI", typeof(TMPro.TextMeshProUGUI) },
            { "TextMeshPro", typeof(TMPro.TextMeshPro) },
            { "TMP_InputField", typeof(TMPro.TMP_InputField) },
            { "TMP_Dropdown", typeof(TMPro.TMP_Dropdown) },
            { "Text", typeof(Text)},
            { "RawImage", typeof(RawImage)},
            { "Button", typeof(Button)},
            { "Toggle", typeof(Toggle)},
            { "Slider", typeof(Slider)},
            { "Scrollbar", typeof(Scrollbar)},
            { "Dropdown", typeof(Dropdown)},
            { "InputField", typeof(InputField)},
            { "Canvas", typeof(Canvas)},
            //{ "UIScrollView", typeof(UIScrollView) },
            { "ScrollRect", typeof(ScrollRect)},
            { "SpriteRenderer", typeof(SpriteRenderer)},
            {"HorizontalLayoutGroup",typeof(HorizontalLayoutGroup)},
            { "GridLayoutGroup", typeof(GridLayoutGroup) },
            { "Animation", typeof(Animation) },
            { "VideoPlayer", typeof(UnityEngine.Video.VideoPlayer) },
            { "CanvasGroup", typeof(CanvasGroup) },
            { "PlayableDirector", typeof(PlayableDirector) },

            //{ "UITweener", typeof(UITweener) },

            ////////自定义控件类型请放这里////////
            /// todo:这里应该只放纯显示控件，具体的类应该使用subview，然后getcompoent获取
            ///////////////后续添加的通用组件///////////////
            ///////////////UI通用动画///////////////
            //{"StatItemView",typeof(StatItemView)},
            {"BindableUI",typeof(BindableUI)},
            //////////////////////////////////////

            { "Image", typeof(Image)},
            { "RectTransform", typeof(RectTransform)},
            { "Transform", typeof(Transform)},
            { "GameObject", typeof(GameObject)},
        };

        // 自动识别只依赖 Unity 类型和 Framework 契约，业务组件由目标对象动态发现。
        private static readonly Type[] AutomaticTypes =
        {
            typeof(TMPro.TextMeshProUGUI),
            typeof(TMPro.TextMeshPro),
            typeof(TMPro.TMP_InputField),
            typeof(TMPro.TMP_Dropdown),
            typeof(Text),
            typeof(RawImage),
            typeof(Button),
            typeof(Toggle),
            typeof(Slider),
            typeof(Scrollbar),
            typeof(Dropdown),
            typeof(InputField),
            typeof(Canvas),
            typeof(ScrollRect),
            typeof(SpriteRenderer),
            typeof(HorizontalLayoutGroup),
            typeof(GridLayoutGroup),
            typeof(Animation),
            typeof(UnityEngine.Video.VideoPlayer),
            typeof(CanvasGroup),
            typeof(PlayableDirector),
            typeof(IBindableUI),
            typeof(Image),
            typeof(RectTransform),
            typeof(Transform),
            typeof(GameObject),
        };

        private static readonly Dictionary<string, string> LegacyTypeAliases = new()
        {
            { "HpBarWithText", "BarWithText" },
            { "HpBarBase", "BarBase" },
        };

        private static readonly Dictionary<string, Type> ResolvedTypeCache = new();

        public static string[] GetDefaultTypeNames()
        {
            string[] names = new string[TypeMap.Count + 1];
            names[0] = "自动";
            TypeMap.Keys.CopyTo(names, 1);
            return names;
        }

        public static Type[] GetDefaultTypes()
        {
            Type[] types = new Type[TypeMap.Count + 1];
            types[0] = typeof(UnityEngine.Object);
            TypeMap.Values.CopyTo(types, 1);
            return types;
        }

        public static IEnumerable<Type> GetAutomaticTypes()
        {
            return AutomaticTypes;
        }

        public static bool TryResolve(string typeName, out Type type)
        {
            if (TypeMap.TryGetValue(typeName, out type))
                return true;

            if (ResolvedTypeCache.TryGetValue(typeName, out type))
                return true;

            string resolvedTypeName = LegacyTypeAliases.TryGetValue(typeName, out string alias)
                ? alias
                : typeName;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] assemblyTypes;
                try
                {
                    assemblyTypes = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    assemblyTypes = exception.Types;
                }

                foreach (Type assemblyType in assemblyTypes)
                {
                    if (assemblyType != null &&
                        (assemblyType.Name == resolvedTypeName || assemblyType.FullName == resolvedTypeName))
                    {
                        type = assemblyType;
                        ResolvedTypeCache[typeName] = type;
                        return true;
                    }
                }
            }

            type = null;
            return false;
        }
    }
}
#endif

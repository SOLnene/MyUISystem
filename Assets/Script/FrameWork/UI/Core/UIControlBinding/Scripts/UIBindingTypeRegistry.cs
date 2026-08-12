#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using Game.UI.Components.CharacterDetail;
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
            {"BarBase",typeof(BarBase)},
            {"ItemSlotView",typeof(ItemSlotView)},
            {"HpBarWithText",typeof(BarWithText)},
            {"BottomHub",typeof(BottomHub)},
            {"TeamInfoSlot",typeof(TeamInfoSlot)},
            {"SkillSlot",typeof(SkillSlot)},
            {"UIMotionBase",typeof(UIMotionBase)},
            {"GachaResultItemView",typeof(GachaResultItemView)},
            {"BackpackTopView",typeof(BackpackTopView)},
            {"BackpackMiddleView",typeof(BackpackMiddleView)},
            {"WeaponDetailMiddleView",typeof(WeaponDetailMiddleView)},
            {"GachaPoolTabView",typeof(GachaPoolTabView)},
            {"GachaTopHubView",typeof(GachaTopHubView)},
            {"GachaMiddleView",typeof(GachaMiddleView)},
            {"GachaResultRevealView",typeof(GachaResultRevealView)},
            {"GachaResultListView",typeof(GachaResultListView)},
            {"GachaTabFeedback",typeof(GachaTabFeedback)},
            {"CharacterDetailContentView",typeof(CharacterDetailContentView)},
            {"CharacterDetailTabView",typeof(CharacterDetailTabView)},
            {"CharacterDetailPreviewView",typeof(CharacterDetailPreviewView)},
            {"CharacterDetailInfoPanelView",typeof(CharacterDetailInfoPanelView)},
            ///////////////后续添加的通用组件///////////////
            {"EnhanceLevelPreviewView",typeof(EnhanceLevelPreviewView)},
            ///////////////UI通用动画///////////////
            {"ISelectableFeedback",typeof(ISelectableFeedback)},
            {"ItemGlowScaleFeedback",typeof(ItemGlowScaleFeedback)},
            //{"StatItemView",typeof(StatItemView)},
            {"BindableUI",typeof(BindableUI)},
            //////////////////////////////////////

            { "Image", typeof(Image)},
            { "RectTransform", typeof(RectTransform)},
            { "Transform", typeof(Transform)},
            { "GameObject", typeof(GameObject)},
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
            return TypeMap.Values;
        }

        public static bool TryResolve(string typeName, out Type type)
        {
            if (TypeMap.TryGetValue(typeName, out type))
                return true;

            if (ResolvedTypeCache.TryGetValue(typeName, out type))
                return true;

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
                    if (assemblyType != null && (assemblyType.Name == typeName || assemblyType.FullName == typeName))
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

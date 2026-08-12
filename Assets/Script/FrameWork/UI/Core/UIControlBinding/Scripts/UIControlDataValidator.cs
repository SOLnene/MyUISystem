#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SkierFramework
{
    public static class UIControlDataValidator
    {
        public static bool CorrectComponents(UIControlData controlData)
        {
            if (controlData.ctrlItemDatas == null) return true;

            bool isOK = true;
            for(int i = 0, imax = controlData.ctrlItemDatas.Count; i < imax; i++)
            {
                if (string.IsNullOrEmpty(controlData.ctrlItemDatas[i].name)) // TODO Check if is a valid varible name
                {
                    Debug.LogErrorFormat("[{1}]第 {0} 个控件没有名字，请修正", i + 1, controlData.gameObject.name);
                    return false;
                }

                for (int j = controlData.ctrlItemDatas.Count - 1; j >= 0; j--)
                {
                    if(controlData.ctrlItemDatas[i].name == controlData.ctrlItemDatas[j].name && i != j)
                    {
                        Debug.LogErrorFormat("[{3}]控件名字 [{0}] 第 {1} 项与第 {2} 项重复，请修正", controlData.ctrlItemDatas[i].name, i + 1, j + 1, controlData.gameObject.name);
                        return false;
                    }
                }
            }

            isOK = ReplaceTargetsToUIComponent(controlData);
            if(isOK)
                Debug.LogFormat("[{0}]控件绑定修正完毕", controlData.gameObject.name);

            return isOK;
        }

        public static bool CheckSubUIs(UIControlData controlData)
        {
            for (int i = 0, imax = controlData.subUIItemDatas.Count; i < imax; i++)
            {
                var subUI = controlData.subUIItemDatas[i];
                if(subUI != null)
                {
                    if (string.IsNullOrEmpty(subUI.name))
                    {
                        Debug.LogErrorFormat("[{0}]第 {1} 个子UI没有设置名字, 请修正", controlData.gameObject.name, i + 1);
                        return false;
                    }

                    if(subUI.subUIData == null)
                    {
                        Debug.LogErrorFormat("[{0}]第 {1} 个子UI没有赋值, 请修正", controlData.gameObject.name, i + 1);
                        return false;
                    }

                    // 必须拖当前 Prefab 下的子UI
                    if (!IsInCurrentPrefab(controlData, subUI.subUIData.transform))
                    {
                        Debug.LogErrorFormat("[{0}]第 {1} 个子UI [{2}]不是当前 Prefab 下的对象，请修正", controlData.gameObject.name, i + 1, subUI.name);
                        return false;
                    }
                }
                else
                {
                    Debug.LogError("internal error at ControlBinding, pls contact author");
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 由于自动拖上去的对象永远都是 GameObject，所以我们需要把它修正为正确的对象类型
        /// </summary>
        private static bool ReplaceTargetsToUIComponent(UIControlData controlData)
        {
            for (int i = 0, imax = controlData.ctrlItemDatas.Count; i < imax; i++)
            {
                var objs = controlData.ctrlItemDatas[i].targets;
                Type type = null;
                for(int j = 0, jmax = objs.Length; j < jmax; j++)
                {
                    if(objs[j] == null)
                    {
                        Debug.LogErrorFormat("[{2}]控件名字 [{0}] 第 {1} 项为空，请修正", controlData.ctrlItemDatas[i].name, j + 1, controlData.gameObject.name);
                        return false;
                    }

                    GameObject go = objs[j] as GameObject;
                    if (go == null)
                        go = (objs[j] as Component).gameObject;

                    // 必须拖当前 Prefab 下的控件
                    if (!IsInCurrentPrefab(controlData, go.transform))
                    {
                        Debug.LogErrorFormat("[{2}]控件名字 [{0}] 第 {1} 项不是当前 Prefab 下的控件，请修正", controlData.ctrlItemDatas[i].name, j + 1, controlData.gameObject.name);
                        return false;
                    }

                    UnityEngine.Object correctComponent = FindCorrectComponent(go, controlData.ctrlItemDatas[i].type);
                    if(correctComponent == null)
                    {
                        Debug.LogErrorFormat("[{3}]控件 [{0}] 第 {1} 项不是 {2} 类型，请修正", controlData.ctrlItemDatas[i].name, j + 1, controlData.ctrlItemDatas[i].type, controlData.gameObject.name);
                        return false;
                    }

                    if (type == null) // 当前变量的第一个控件时执行
                    {
                        if (string.IsNullOrEmpty(controlData.ctrlItemDatas[i].type))
                        {
                            type = correctComponent.GetType();
                        }else
                        {
                            if(!UIBindingTypeRegistry.TryResolve(controlData.ctrlItemDatas[i].type, out type))
                            {
                                Debug.LogError("Internal Error, pls contact author");
                                return false;
                            }
                        }
                    }
                    else if(!type.IsAssignableFrom(correctComponent.GetType()))
                    {
                        Debug.LogErrorFormat("[{2}]控件名字 [{0}] 第 {1} 项与第 1 项的类型不同，请修正", controlData.ctrlItemDatas[i].name, j + 1, controlData.gameObject.name);
                        return false;
                    }

                    if (objs[j] != correctComponent)
                        controlData.dataHasChanged = true;

                    objs[j] = correctComponent;
                }

                if(string.IsNullOrEmpty(controlData.ctrlItemDatas[i].type))
                {
                    controlData.ctrlItemDatas[i].type = type.Name;
//#if UNITY_2019_1_OR_NEWER
//                    EditorUtility.ClearDirty(this);
//#endif
                    EditorUtility.SetDirty(controlData);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(controlData);
                }
            }
            return true;
        }

        private static bool IsInCurrentPrefab(UIControlData controlData, Transform t)
        {
            do
            {
                if (t == controlData.transform)
                    return true;
                t = t.parent;
            } while (t != null);
            return false;
        }

        private static UnityEngine.Object FindCorrectComponent(GameObject go, string typename)
        {
            if (typename == "GameObject")
                return go;

            List<Component> components = new List<Component>();
            go.GetComponents(components);

            Func<Type, Component> getSpecialTypeComp = (Type t) =>
            {
                foreach (var comp in components)
                {
                    Type compType = comp.GetType();
                    if (t.IsAssignableFrom(compType))
                    {
                        return comp;
                    }
                }
                return null;
            };

            Component newComp = null;

            if (string.IsNullOrEmpty(typename))
            {
                // 类型名为空则为自动类型，在 _typeMap 里从上往下找
                foreach (Type automaticType in UIBindingTypeRegistry.GetAutomaticTypes())
                {
                    newComp = getSpecialTypeComp(automaticType);
                    if (newComp != null)
                        break;
                }
            }
            else
            {// 指定了类型名则只找指定类型的控件
                Type type = null;
                if (UIBindingTypeRegistry.TryResolve(typename, out type))
                {
                    newComp = getSpecialTypeComp(type);
                }
            }

            return newComp;
        }
    }
}
#endif

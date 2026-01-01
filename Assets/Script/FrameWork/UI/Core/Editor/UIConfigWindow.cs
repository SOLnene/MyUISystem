using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using SkierFramework;
using UnityEditor;
using UnityEngine;


public class UIConfigWindow : EditorWindow
{
    [MenuItem("Assets/CreateUI")]
    static void CopyUI()
    {
        if(Selection.activeObject==null||!(Selection.activeObject is GameObject))
        {
            Debug.LogError("请选择一个UI预制体");
            return;
        }
        OpenWindow().uiPrefab = Selection.activeObject as GameObject;
    }

    [MenuItem("Tools/管理UI")]
    public static UIConfigWindow OpenWindow()
    {
        //不需要判空？
        var window = GetWindow<UIConfigWindow>("UI管理面板");
        window.Focus();
        return window;
    }

    Vector2 scroll;
    Vector2 scroll2;
    Vector2 scroll3;
    
    //基础文件路径
    string uiViewTemplatePath;
    string uiViewBindingTemplatePath;
    
    string uiConfigPath;
    string uiTypePath;
    string saveUIPath;

    string uiName;
    
    string input;

    public GameObject uiPrefab;

    bool isWindow = true;
    UILayer layer = UILayer.NormalLayer;    
    /// <summary>
    /// uiName相应的script路径
    /// </summary>
    Dictionary<string, string> uiNames = new Dictionary<string, string>();
    /// <summary>
    /// uiName相应的configdata
    /// </summary>
    Dictionary<string, UIConfigData> uiJsonDatas = new Dictionary<string, UIConfigData>();
    
    
    void OnEnable()
    {
        TryGetPath(ref uiViewTemplatePath,nameof(uiViewTemplatePath),".txt");
        TryGetPath(ref uiViewBindingTemplatePath,nameof(uiViewBindingTemplatePath),".txt");
        TryGetPath(ref uiTypePath,nameof(uiTypePath),".cs");
        TryGetPath(ref uiConfigPath,nameof(uiConfigPath),".json");
        saveUIPath = PlayerPrefs.GetString(nameof(saveUIPath), "Assets/Script");
        
        uiJsonDatas.Clear();
        uiNames.Clear();
        string[] strs = Enum.GetNames(typeof(UIType));
        foreach (var str in strs)
        {
            if (str.Equals("Max"))
            {
                continue;
            }
            var jsonData = GetUIJson(str);
            if (jsonData == null || string.IsNullOrEmpty(jsonData.uiPath))
            {
                continue;
            }
            CheckMove(jsonData);
            var scriptPath = GetUIScriptPath(str,true);
            uiNames[str] = scriptPath;

        }
    }

    void OnGUI()
    {
        //创建一个带滚动条的区域，并且传入当前的 scroll 值作为初始滚动位置
        scroll = EditorGUILayout.BeginScrollView(scroll);
        //{}不是函数体，只是普通的代码块
        {
            EditorGUILayout.HelpBox("UI基础文件",MessageType.Info);
            {
                PathField("UIView模板", ref uiViewTemplatePath, nameof(uiViewTemplatePath), ".txt");
                PathField("UIView控件绑定模板", ref uiViewBindingTemplatePath, nameof(uiViewBindingTemplatePath), ".txt");
                PathField("UI配置文件.json",ref uiConfigPath,nameof(uiConfigPath),".json");
                PathField("UIType.cs", ref uiTypePath, nameof(uiTypePath), ".cs");
                
            }
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            {
                //左半边的方形滚动区域
                scroll2 = EditorGUILayout.BeginScrollView(scroll2, "box", GUILayout.Width(position.width * 0.4f - 6));
                {
                    EditorGUILayout.HelpBox("已创建的UI列表",MessageType.Info);
                    input = EditorGUILayout.TextField(input, EditorStyles.toolbarSearchField, GUILayout.Height(20));
                    string[] types = Enum.GetNames(typeof(UIType));
                    //
                    foreach (var type in types)
                    {
                        if (type.Equals("Max"))
                        {
                            continue;
                        }
                        if (!string.IsNullOrEmpty(input) && !type.Contains(input))
                        {
                            continue;
                        }
                        var jsonData = GetUIJson(type);
                        var scriptPath = GetUIScriptPath(type);
                        if (jsonData == null ||
                            string.IsNullOrEmpty(jsonData.uiPath) ||
                            string.IsNullOrEmpty(scriptPath))
                        {
                            continue;
                        }
                        var defaultColor = GUI.color;
                        if (type.Equals(uiName))
                        {
                            GUI.color = Color.yellow;
                        }
                        EditorGUILayout.BeginHorizontal("box");
                        if(GUILayout.Button("选中"))
                        {
                            uiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(jsonData.uiPath);
                        }
                        EditorGUILayout.ObjectField(AssetDatabase.LoadAssetAtPath<GameObject>(jsonData.uiPath), typeof(GameObject),true);
                        EditorGUILayout.ObjectField(AssetDatabase.LoadAssetAtPath<TextAsset>(scriptPath), typeof(TextAsset), true);

                        EditorGUILayout.EndHorizontal();
                        GUI.color = defaultColor;
                    }
                }
                EditorGUILayout.EndScrollView();
                //右半边的方形滚动区域
                scroll3 = EditorGUILayout.BeginScrollView(scroll3, "box",GUILayout.Width(position.width * 0.6f - 6) );
                {
                    EditorGUILayout.HelpBox("UI操作",MessageType.Info);
                    uiPrefab = EditorGUILayout.ObjectField("UI预制体",uiPrefab, typeof(GameObject),true) as GameObject;
                    if (uiPrefab != null)
                    {
                        uiName = uiPrefab.name;
                        var uiScriptPath = GetUIScriptPath(uiName);
                        if (string.IsNullOrEmpty(uiScriptPath))
                        {
                            if (GUILayout.Button($"选择创建路径:{saveUIPath}"))
                            {
                                //得到脚本的绝对路径
                                var newPath = EditorUtility.OpenFolderPanel("UI生成路径", saveUIPath, "");
                                //替换为相对路径
                                saveUIPath = newPath.Replace(Application.dataPath,"Assets");
                                PlayerPrefs.SetString(nameof(saveUIPath),saveUIPath);
                            }
                            if (uiPrefab != null)
                            {
                                EditorGUILayout.TextField("UI代码生成路径", $"{saveUIPath}/{uiName}.cs");
                            }
                            isWindow = EditorGUILayout.Toggle("是否为窗口", isWindow);
                            layer = (UILayer)EditorGUILayout.EnumPopup("UILayer设置", layer);
                            
                            var defaultColor = GUI.color;
                            GUI.color = Color.green;
                            if (GUILayout.Button("创建UI"))
                            {
                                //Regex.Replace(input, pattern, replacement)：使用正则表达式把文本中符合 pattern 的内容替换为 replacement
                                //生成代码
                                string uiScriptContent = Regex.Replace(File.ReadAllText(uiViewTemplatePath), "UIXXXView", uiName);
                                string newPath = $"{saveUIPath}/{uiName}.cs";
                                
                                File.WriteAllText(newPath,uiScriptContent);
                                UIControlData uiControlData = uiPrefab.GetComponent<UIControlData>();
                                if (uiControlData != null)
                                {
                                    uiControlData.CopyCodeToClipBoardPrivate();
                                }
                                string uiBindingScriptContent = Regex.Replace(File.ReadAllText(uiViewBindingTemplatePath), "UIXXXView", uiName);
                                uiBindingScriptContent = Regex.Replace(uiBindingScriptContent, "//UIControlData", uiControlData != null ? GUIUtility.systemCopyBuffer : "");
                                string newBindingPath = $"{saveUIPath}/{uiName}.binding.cs";
                                File.WriteAllText(newBindingPath,uiBindingScriptContent);

                                //生成json文件
                                var jsonData = new UIConfigData()
                                {
                                    uiType = uiName,
                                    uiPath = AssetDatabase.GetAssetPath(uiPrefab),
                                    uiLayer = layer.ToString(),
                                    isWindow = isWindow
                                        
                                };
                                //TODO:或许应该改为赋值避免无法更新uiname，这种情况发生于uiconfigjson存在uiname缺不存在uipath
                                uiJsonDatas.Add(uiName,jsonData);
                                uiNames.Add(uiName,newPath);
                                SaveJson();
                                
                                //添加uiType
                                //\n：换行符，把 Max, 移到下一行
                                //\t\t：两个制表符（tab），增加缩进
                                var newUITypeContent = Regex.Replace(File.ReadAllText(uiTypePath), "Max,", $"{uiName},\n\t\tMax,");
                                File.Delete(uiTypePath);
                                File.WriteAllText(uiTypePath,newUITypeContent);
                                Debug.Log("UI创建成功"+newPath);
                                AssetDatabase.SaveAssets();
                                AssetDatabase.Refresh();
                            }
                            GUI.color = defaultColor;
                        }
                        else
                        {
                            //读取现有的json数据
                            var jsonData = GetUIJson(uiName);
                            EditorGUILayout.ObjectField("已存在UI代码", AssetDatabase.LoadAssetAtPath(uiScriptPath, typeof(TextAsset)),typeof(TextAsset),true);
                            jsonData.isWindow = EditorGUILayout.Toggle("是否为窗口",jsonData.isWindow);
                            Enum.TryParse(jsonData.uiLayer, out UILayer layer);
                            jsonData.uiLayer = EditorGUILayout.EnumPopup("UI层级设置", layer).ToString();

                            var defaultColor = GUI.color;
                            GUI.color = Color.green;
                            if (GUILayout.Button("保存设置"))
                            {
                                SaveJson();
                                AssetDatabase.SaveAssets();
                                AssetDatabase.Refresh();
                            }
                            if (GUILayout.Button("更新绑定代码"))
                            {
                                UpdateBindingCode();
                                AssetDatabase.SaveAssets();
                                AssetDatabase.Refresh();
                            }
                            GUI.color = defaultColor;
                            GUI.color = Color.red;
                            if (GUILayout.Button("删除UI"))
                            {
                                if (EditorUtility.DisplayDialog("是否确认删除", $"请确认是否删除:\n{uiScriptPath}\n同时会清楚Json和UIType中相关数据", "确定", "取消"))
                                {
                                    //删除uiType中数据
                                    var uiTypeContent = File.ReadAllText(uiTypePath);
                                    int index = uiTypeContent.IndexOf(uiName);
                                    //取 uiName 之前的部分。找到它前面最后一个逗号的位置。
                                    int left = uiTypeContent.Substring(0, index).LastIndexOf(',') + 1;
                                    int right = uiTypeContent.Substring(index, uiTypeContent.Length - index).IndexOf(',') + index + 1;
                                    string newUITypeContent = uiTypeContent.Substring(0, left) + uiTypeContent.Substring(right, uiTypeContent.Length - right);
                                    File.Delete(uiTypePath);
                                    File.WriteAllText(uiTypePath, newUITypeContent);
                                    //清除json数据和name索引
                                    uiJsonDatas.Remove(uiName)
;                                   uiNames.Remove(uiName);
                                    SaveJson();
                                    //删除脚本文件
                                    File.Delete(uiScriptPath);
                                    AssetDatabase.SaveAssets();
                                    AssetDatabase.Refresh();
                                    //uiNames.Remove(uiName);
                                }
                            }
                            GUI.color = defaultColor;
                        }
                    }
                    else
                    {
                        uiName = "";
                    }
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    /// <summary>
    /// 遍历uiNames对应的uiJsonDatas，保存到json文件
    /// </summary>
    void SaveJson()
    {
        List<UIConfigData> list = new List<UIConfigData>();
        foreach (var name in uiNames.Keys)
        {
            if (uiJsonDatas.TryGetValue(name, out var data))
            {
                list.Add(data);
            }
        }
        File.Delete(uiConfigPath);
        File.WriteAllText(uiConfigPath,JsonConvert.SerializeObject(list,Formatting.Indented));
    }
    
    /// <summary>
    /// 尝试查询某个名字文件的路径
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="pathName">不喊后缀的文件名字</param>
    /// <param name="endsWith">文件后缀</param>
    void TryGetPath(ref string path, string pathName, string endsWith)
    {
        //一个pathname可能对应多个文件
        path = PlayerPrefs.GetString(pathName);
        if (!File.Exists(path))
        {
            string[] ids = AssetDatabase.FindAssets(pathName);
            if (ids != null)
            {
                foreach (var id in ids)
                {
                    var str = AssetDatabase.GUIDToAssetPath(id);
                    if (str.EndsWith(endsWith))
                    {
                        path = str;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 查询某个uiType对应的脚本路径
    /// </summary>
    /// <param name="name"></param>
    /// <param name="tryFind">是否在项目中查询</param>
    /// <returns></returns>
    string GetUIScriptPath(string name, bool tryFind = false)
    {
        if (uiNames.TryGetValue(name, out string path))
        {
            return path;
        }
        if (!tryFind)
        {
            return string.Empty;
        }
        if (GetUIJson(name) == null)
        {
            return string.Empty;
        }
        string[] ids = AssetDatabase.FindAssets(name);
        if (ids != null)
        {
            foreach (var id in ids)
            {
                var str = AssetDatabase.GUIDToAssetPath(id);
                if (str.EndsWith(".cs"))
                {
                    return str;
                }
            }
        }
        return string.Empty;
    }
    
    /// <summary>
    /// 读取uiconfig，将反序列化的数据缓存到uiJsondatas
    /// </summary>
    /// <param name="name">uiType</param> 
    /// <returns>name对应的uiConfigData</returns> 
    UIConfigData GetUIJson(string name)
    {
        if (uiJsonDatas.Count == 0)
        {
            try
            {
                var json = File.ReadAllText(uiConfigPath);
                var list = JsonConvert.DeserializeObject<List<UIConfigData>>(json);
                foreach (var data in list)
                {
                    uiJsonDatas[data.uiType] = data;
                }
            }
            catch(Exception e)
            {
                Debug.LogError(e);
            }
        }
        if (uiJsonDatas.TryGetValue(name, out var jsondata))
        {
            return jsondata;
        }
        return null;
    }

    void PathField(string name, ref string path, string pathName, string endsWith)
    {
        // 在 Editor 界面绘制一个 ObjectField（对象选择框），显示名字为 name
        // 初始值是根据当前 path 路径加载出来的 TextAsset
        var obj = EditorGUILayout.ObjectField(name, AssetDatabase.LoadAssetAtPath(path, typeof(TextAsset)), typeof(TextAsset), true);
        if (obj != null)
        {
            var newPath = AssetDatabase.GetAssetPath(obj);
            if (newPath.EndsWith(endsWith) && !newPath.Equals(path))
            {
                path = newPath;
                PlayerPrefs.SetString(pathName,path);
            }
        }
    }

    void UpdateBindingCode()
    {
        UIControlData uiControlData = uiPrefab.GetComponent<UIControlData>();
        if (uiControlData != null)
        {
            uiControlData.CopyCodeToClipBoardPrivate();
        }
        string uiBindingScriptContent = Regex.Replace(File.ReadAllText(uiViewBindingTemplatePath), "UIXXXView", uiName);
        uiBindingScriptContent = Regex.Replace(uiBindingScriptContent, "//UIControlData", uiControlData != null ? GUIUtility.systemCopyBuffer : "");
        string newBindingPath = $"{saveUIPath}/{uiName}.binding.cs";
        File.WriteAllText(newBindingPath,uiBindingScriptContent);
    }

    void CheckMove(UIConfigData jsonData)
    {
        if (jsonData != null && !string.IsNullOrEmpty(jsonData.uiPath))
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(jsonData.uiPath);
            if (asset == null)
            {
                // 说明文件被移动了，尝试重新搜索
                string[] ids = AssetDatabase.FindAssets($"{jsonData.uiType} t:prefab");
                if (ids.Length > 0)
                {
                    string newPath = AssetDatabase.GUIDToAssetPath(ids[0]);
                    jsonData.uiPath = newPath;
                    Debug.LogWarning($"UIConfigWindow: {jsonData.uiType} 的路径已更新为 {newPath}");
                }
            }
        }
    }
}

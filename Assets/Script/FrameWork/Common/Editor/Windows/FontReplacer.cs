using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

public class FontReplacer : EditorWindow
{
    // 用来存放你想选的那种字体
    private TMP_FontAsset targetFont;

    [MenuItem("Tools/批量字体替换工具")]
    public static void ShowWindow()
    {
        GetWindow<FontReplacer>("字体替换");
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.HelpBox("1. 先在下方选择你想替换成的字体\n2. 在层级面板(Hierarchy)选中要处理的根节点\n3. 点击执行", MessageType.Info);
        
        // 这里的 ObjectField 让你在面板上自由选择字体
        targetFont = (TMP_FontAsset)EditorGUILayout.ObjectField("目标字体资产", targetFont, typeof(TMP_FontAsset), false);

        EditorGUILayout.Space(10);

        if (GUILayout.Button("替换选中物体及其子节点的字体", GUILayout.Height(30)))
        {
            if (targetFont == null)
            {
                EditorUtility.DisplayDialog("提示", "请先选择目标字体！", "确定");
                return;
            }

            if (Selection.activeGameObject == null)
            {
                EditorUtility.DisplayDialog("提示", "请先在层级面板选中一个根物体！", "确定");
                return;
            }

            ExecuteReplace();
        }
    }

    private void ExecuteReplace()
    {
        // 获取选中的物体及其所有子物体的 TextMeshProUGUI 组件
        var textComponents = Selection.activeGameObject.GetComponentsInChildren<TextMeshProUGUI>(true);
        int count = 0;

        foreach (var text in textComponents)
        {
            // 记录 Undo 以便撤销，并且支持 Prefab Overwrite 字体加粗（如果你是在场景里操作）
            Undo.RecordObject(text, "Replace Font");
            text.font = targetFont;
            
            // 标记脏数据，确保修改被保存
            EditorUtility.SetDirty(text);
            count++;
        }

        Debug.Log($"<color=green>字体替换完成！共处理 {count} 个组件。</color>");
        EditorUtility.DisplayDialog("替换成功", $"成功替换了 {count} 个文字组件。", "太棒了");
    }
}

using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ModelViewer))]
public class ModelViewerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        // 获取当前选中的 ModelViewer 实例
        ModelViewer viewer = (ModelViewer)target;

        GUILayout.Space(15); 
        
        GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f); 
        if (GUILayout.Button("📸 将当前视角导出为 Preset", GUILayout.Height(35)))
        {
            ExportCurrentViewToPreset();
        }
        GUI.backgroundColor = Color.white; 
    }

    private void ExportCurrentViewToPreset()
    {
        // 创建一个弹窗，让你选择保存路径和文件名
        string path = EditorUtility.SaveFilePanelInProject(
            "保存镜头预设",
            "CameraPreset_New",
            "asset",
            "请选择保存路径"
        );

        // 如果点击了取消，路径为空，直接返回
        if (string.IsNullOrEmpty(path)) return;

        // 实例化一个新的 ScriptableObject
        CameraPreset newPreset = ScriptableObject.CreateInstance<CameraPreset>();

        // 直接获取你已经在 ModelViewer 中序列化的 Transform 和 Camera 引用
        SerializedProperty displayCamProp = serializedObject.FindProperty("displayCamera");
        SerializedProperty cameraPivotProp = serializedObject.FindProperty("cameraPivot");

        Camera cam = displayCamProp?.objectReferenceValue as Camera;
        Transform pivot = cameraPivotProp?.objectReferenceValue as Transform;

        if (cam != null && pivot != null)
        {
            newPreset.cameraLocalPosition = cam.transform.localPosition;
            newPreset.fov = cam.fieldOfView;

            // 读取真实物体的欧拉角
            // 注意：Unity Inspector 显示的负数角度（如 -10度），在代码里其实是 350度
            // 这里我们做一下转换，保证导出的数据和你的 minPitch/maxPitch 逻辑对齐
            float p = pivot.localEulerAngles.x;
            newPreset.pitch = p > 180f ? p - 360f : p;

            float y = pivot.localEulerAngles.y;
            newPreset.yaw = y > 180f ? y - 360f : y;
            
            AssetDatabase.CreateAsset(newPreset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = newPreset;

            Debug.Log($"<b><color=green>镜头预设导出成功！</color></b> 保存路径: {path}");
        }
        else
        {
            Debug.LogError("导出失败：请检查 ModelViewer 面板，确保 displayCamera, cameraPivot 和 modelRoot 都已赋值！");
        }
    }
}
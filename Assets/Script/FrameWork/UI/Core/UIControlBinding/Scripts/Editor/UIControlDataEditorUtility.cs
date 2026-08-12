#if UNITY_EDITOR
using UnityEditor;

namespace SkierFramework
{
    internal static class UIControlDataEditorUtility
    {
        public static void SetDirty(UIControlData controlData)
        {
            // 1. 标记组件本身已改变
            EditorUtility.SetDirty(controlData);
#if UNITY_2021_1_OR_NEWER
            var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(controlData.gameObject);
#else
            var prefabStage = UnityEditor.Experimental.SceneManagement.PrefabStageUtility.GetPrefabStage(controlData.gameObject);
#endif
            if (prefabStage != null)
            {
                //2. 如果是在 Prefab 模式下（Prefab Stage），标记场景已改变
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(prefabStage.scene);
            }
        }
    }
}
#endif

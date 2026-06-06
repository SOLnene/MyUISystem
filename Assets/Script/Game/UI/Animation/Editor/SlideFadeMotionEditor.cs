using UnityEditor;

[CustomEditor(typeof(SlideFadeMotion))]
public class SlideFadeMotionEditor : Editor
{
    SerializedProperty motionGroup;
    SerializedProperty motionRoot;
    SerializedProperty mode;
    SerializedProperty preset;
    SerializedProperty targetMove;
    SerializedProperty originMove;
    SerializedProperty moveDuration;
    SerializedProperty fadeDuration;
    SerializedProperty moveEase;
    SerializedProperty fadeEase;

    void OnEnable()
    {
        motionGroup = serializedObject.FindProperty("motionGroup");
        motionRoot = serializedObject.FindProperty("motionRoot");
        mode = serializedObject.FindProperty("mode");
        preset = serializedObject.FindProperty("preset");
        targetMove = serializedObject.FindProperty("targetMove");
        originMove = serializedObject.FindProperty("originMove");
        moveDuration = serializedObject.FindProperty("moveDuration");
        fadeDuration = serializedObject.FindProperty("fadeDuration");
        moveEase = serializedObject.FindProperty("moveEase");
        fadeEase = serializedObject.FindProperty("fadeEase");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(motionGroup);
        EditorGUILayout.PropertyField(motionRoot);
        EditorGUILayout.PropertyField(mode);

        EditorGUILayout.Space();

        if (mode.enumValueIndex == (int)SlideFadeMotionMode.Preset)
        {
            EditorGUILayout.PropertyField(preset);

            if (preset.objectReferenceValue == null)
                EditorGUILayout.HelpBox("Preset mode needs a SlideFadeMotionPreset.", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.PropertyField(targetMove);
            EditorGUILayout.PropertyField(originMove);
            EditorGUILayout.PropertyField(moveDuration);
            EditorGUILayout.PropertyField(fadeDuration);
            EditorGUILayout.PropertyField(moveEase);
            EditorGUILayout.PropertyField(fadeEase);
        }

        serializedObject.ApplyModifiedProperties();
    }
}

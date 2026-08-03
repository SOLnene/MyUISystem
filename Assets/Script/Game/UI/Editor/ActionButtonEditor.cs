using UnityEditor;
using UnityEditor.UI;
using UnityEngine;

[CustomEditor(typeof(ActionButton))]
[CanEditMultipleObjects]
public class ActionButtonEditor : ButtonEditor
{
    SerializedProperty canvasGroup;
    SerializedProperty frame;
    SerializedProperty bg;
    SerializedProperty text;
    SerializedProperty highLightImage;
    SerializedProperty normalBgColor;
    SerializedProperty normalTextColor;
    SerializedProperty highlightedBgColor;
    SerializedProperty highlightedTextColor;
    SerializedProperty hoverFrameColor;
    SerializedProperty disabledAlpha;
    SerializedProperty transitionDuration;
    SerializedProperty pressedDuration;

    protected override void OnEnable()
    {
        base.OnEnable();

        canvasGroup = serializedObject.FindProperty("canvasGroup");
        frame = serializedObject.FindProperty("frame");
        bg = serializedObject.FindProperty("bg");
        text = serializedObject.FindProperty("text");
        highLightImage = serializedObject.FindProperty("highLightImage");
        normalBgColor = serializedObject.FindProperty("normalBgColor");
        normalTextColor = serializedObject.FindProperty("normalTextColor");
        highlightedBgColor = serializedObject.FindProperty("highlightedBgColor");
        highlightedTextColor = serializedObject.FindProperty("highlightedTextColor");
        hoverFrameColor = serializedObject.FindProperty("hoverFrameColor");
        disabledAlpha = serializedObject.FindProperty("disabledAlpha");
        transitionDuration = serializedObject.FindProperty("transitionDuration");
        pressedDuration = serializedObject.FindProperty("pressedDuration");
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        serializedObject.Update();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Visual References", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(canvasGroup);
        EditorGUILayout.PropertyField(frame);
        EditorGUILayout.PropertyField(bg);
        EditorGUILayout.PropertyField(text);
        EditorGUILayout.PropertyField(highLightImage, new GUIContent("Highlight Image"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Colors", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(normalBgColor, new GUIContent("Normal Background"));
        EditorGUILayout.PropertyField(normalTextColor, new GUIContent("Normal Text"));
        EditorGUILayout.PropertyField(hoverFrameColor, new GUIContent("Hover Frame"));
        EditorGUILayout.PropertyField(highlightedBgColor, new GUIContent("Pressed Background"));
        EditorGUILayout.PropertyField(highlightedTextColor, new GUIContent("Pressed Text"));
        EditorGUILayout.PropertyField(disabledAlpha);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(transitionDuration);
        EditorGUILayout.PropertyField(pressedDuration);

        serializedObject.ApplyModifiedProperties();
    }
}

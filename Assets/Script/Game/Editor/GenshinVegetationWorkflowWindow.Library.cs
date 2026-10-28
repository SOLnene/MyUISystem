using System;
using System.Linq;
using Game.Vegetation;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    internal sealed partial class GenshinVegetationWorkflowWindow
    {
        private const string VegetationLibraryPath = "Assets/Art/Map/Genshin/Mondstadt/Terrain/GenshinVegetation_Library.asset";
        [SerializeField] private GenshinVegetationData vegetationLibrary;
        private bool libraryResolved;
        private bool showVegetationLibrary = true;
        private string vegetationSearch = "";
        private int vegetationPage;
        private const int VegetationPageSize = 12;

        private void DrawVegetationLibrary()
        {
            if (!libraryResolved)
            {
                libraryResolved = true;
                if (vegetationLibrary == null)
                    vegetationLibrary = AssetDatabase.LoadAssetAtPath<GenshinVegetationData>(VegetationLibraryPath);
            }
            showVegetationLibrary = EditorGUILayout.Foldout(showVegetationLibrary, "原神小植被库", true);
            if (!showVegetationLibrary) return;
            vegetationLibrary = (GenshinVegetationData)EditorGUILayout.ObjectField("植被库资产", vegetationLibrary,
                typeof(GenshinVegetationData), false);
            if (vegetationLibrary == null) return;
            EditorGUILayout.HelpBox("搜索原始资源名或地区代号（Md、Ly、Dq、Xm、Fd、Nt 等），点击后加入当前图层并切换为只刷此种。草种库仅提供资源，不会生成原生落点。", MessageType.Info);
            if (!IsAuthored)
                EditorGUILayout.HelpBox("请先选择独立绘制图层，再将库中的植被加入画笔。", MessageType.Info);
            string search = EditorGUILayout.TextField("搜索植被", vegetationSearch);
            if (search != vegetationSearch) { vegetationSearch = search; vegetationPage = 0; }
            var entries = vegetationLibrary.Prototypes.Where(p => string.IsNullOrWhiteSpace(vegetationSearch) ||
                p.key.IndexOf(vegetationSearch, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            int pages = Mathf.Max(1, (entries.Length + VegetationPageSize - 1) / VegetationPageSize);
            vegetationPage = Mathf.Clamp(vegetationPage, 0, pages - 1);
            EditorGUILayout.LabelField("匹配 " + entries.Length + " / 库中 " + vegetationLibrary.Prototypes.Length);
            foreach (var entry in entries.Skip(vegetationPage * VegetationPageSize).Take(VegetationPageSize))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(entry.key, EditorStyles.wordWrappedMiniLabel);
                    using (new EditorGUI.DisabledScope(!IsAuthored || EditorApplication.isPlayingOrWillChangePlaymode))
                        if (GUILayout.Button("只刷此种", GUILayout.Width(72))) Run(() => SelectLibraryPlant(entry));
                    if (GUILayout.Button("定位资源", GUILayout.Width(72))) EditorGUIUtility.PingObject(entry.mesh);
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(vegetationPage == 0))
                    if (GUILayout.Button("上一页")) vegetationPage--;
                GUILayout.Label((vegetationPage + 1) + " / " + pages, GUILayout.Width(65));
                using (new EditorGUI.DisabledScope(vegetationPage + 1 == pages))
                    if (GUILayout.Button("下一页")) vegetationPage++;
            }
        }

        private void SelectLibraryPlant(GenshinVegetationData.Prototype entry)
        {
            if (!IsAuthored || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Choose an authored layer in Edit mode.");
            GenshinVegetationData.Validate(new[] { entry }, new[] { new GenshinVegetationData.Instance {
                id = "library-binding-check", prototype = entry.key, rotation = Quaternion.identity, scale = Vector3.one } });
            EndBrushStroke();
            var binding = data.Prototypes.FirstOrDefault(p => p.mesh == entry.mesh && p.material == entry.material);
            if (binding == null)
            {
                if (data.Prototypes.Any(p => p.key == entry.key))
                    throw new InvalidOperationException("A different plant already uses this identity: " + entry.key);
                Undo.RecordObject(data, "Add vegetation library binding");
                var serialized = new SerializedObject(data);
                var palette = serialized.FindProperty("prototypes");
                int index = palette.arraySize++;
                var target = palette.GetArrayElementAtIndex(index);
                target.FindPropertyRelative("key").stringValue = entry.key;
                target.FindPropertyRelative("mesh").objectReferenceValue = entry.mesh;
                target.FindPropertyRelative("material").objectReferenceValue = entry.material;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                data.NotifyChanged();
                binding = data.Prototypes[index];
                Changed();
            }
            // Choosing a library plant changes the next stroke, preserving all existing instances.
            foreach (var prototype in data.Prototypes)
            {
                var weight = rangeBrush.weights.Find(w => w.key == prototype.key);
                if (weight == null)
                {
                    weight = new GenshinVegetationBrush.Weight { key = prototype.key };
                    rangeBrush.weights.Add(weight);
                }
                weight.value = prototype.key == binding.key ? 1 : 0;
            }
            prototypeIndex = Array.FindIndex(data.Prototypes, p => p.key == binding.key);
            brushMode = 3;
            preview = true;
            SceneView.RepaintAll();
            message = "当前画笔：" + binding.key + "。已有植被保持原样；修改草种比例可恢复混种。";
        }
    }
}

using System;
using System.IO;
using System.Linq;
using Game.Vegetation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using EditorEventType = UnityEngine.EventType;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    internal sealed partial class GenshinVegetationWorkflowWindow : EditorWindow
    {
        private const string Analysis = ".codex_tmp_frame_analysis/genshin_grass_20261022/";
        private const string DecodedSample = Analysis + "vegetation_source_20261024/import_plan.json";
        private const string NativeSample = Analysis + "placement_investigation_20261024/mondstadt_cell_verified_structure.json";
        [SerializeField] private string sourcePath = NativeSample;
        [SerializeField] private string assetPath = "Assets/Art/Map/Genshin/Mondstadt/Terrain/GenshinVegetation_SourceSample.asset";
        [SerializeField] private GenshinVegetationData data;
        [SerializeField] private bool preview;
        [SerializeField] private int tab;
        private GenshinVegetationSourceReader.Document document;
        private GenshinVegetationData.Instance[] points = Array.Empty<GenshinVegetationData.Instance>();
        private GenshinVegetationRenderer sceneRenderer;
        private string selectedId;
        private string message;
        private string error;
        private Vector2 scroll;
        private Collider paintSurface;
        private int brushMode;
        private int prototypeIndex;
        private float eraseRadius = 1;
        private float newScale = 1;
        private bool showEdits;

        [MenuItem("Tools/Genshin Region/Vegetation Workflow")]
        internal static void Open()
        {
            var window = GetWindow<GenshinVegetationWorkflowWindow>("植被恢复工作流");
            window.minSize = new Vector2(510, 480);
            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += DrawScene;
            Undo.undoRedoPerformed += UndoRedo;
            var restored = data;
            if (!string.IsNullOrEmpty(sourcePath) && File.Exists(GenshinVegetationSourceReader.ProjectFile(sourcePath)))
                Run(ReadSource);
            if (restored != null && restored.SourceKey != null && restored.SourceKey.StartsWith("painted/", StringComparison.Ordinal))
                data = restored;
            RefreshPoints();
        }

        private void OnDisable()
        {
            EndBrushStroke();
            SceneView.duringSceneGui -= DrawScene;
            Undo.undoRedoPerformed -= UndoRedo;
            SceneView.RepaintAll();
        }

        private void OnGUI()
        {
            if (brushStroke != null && (tab != 2 || data != strokeData || paintSurface != strokeSurface || brushMode != strokeMode))
                EndBrushStroke();
            tab = GUILayout.Toolbar(tab, new[] { "1 源数据", "2 预览与导入", "3 局部编辑" });
            scroll = EditorGUILayout.BeginScrollView(scroll);
            using (new EditorGUI.DisabledGroupScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
            {
                if (tab == 0) DrawSource();
                else if (tab == 1) DrawImport();
                else DrawEditing();
            }
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        private void DrawSource()
        {
            EditorGUILayout.LabelField("读取源布局", EditorStyles.boldLabel);
            sourcePath = EditorGUILayout.TextField("布局 JSON", sourcePath);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("选择文件"))
                {
                    string selected = EditorUtility.OpenFilePanel("选择布局或原生检查报告", Application.dataPath, "json");
                    if (!string.IsNullOrEmpty(selected)) { sourcePath = selected; Run(ReadSource); }
                }
                if (GUILayout.Button("检查数据")) Run(ReadSource);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("原生草分块 · 1392 条")) { sourcePath = NativeSample; Run(ReadSource); }
                if (GUILayout.Button("已解码花草 · 17 个")) { sourcePath = DecodedSample; Run(ReadSource); }
            }
            if (document == null) return;
            EditorGUILayout.HelpBox(document.summary, document.decoded ? MessageType.Info : MessageType.Warning);
            EditorGUILayout.LabelField("源文件 SHA256", document.hash, EditorStyles.wordWrappedMiniLabel);
            if (!document.decoded) return;
            EditorGUILayout.Vector3Field("已应用的地图原点", document.origin);
            EditorGUILayout.LabelField("绑定项目资源", EditorStyles.boldLabel);
            foreach (var prototype in document.prototypes)
            {
                EditorGUILayout.LabelField(prototype.key, EditorStyles.wordWrappedLabel);
                prototype.mesh = (Mesh)EditorGUILayout.ObjectField("网格", prototype.mesh, typeof(Mesh), false);
                prototype.material = (Material)EditorGUILayout.ObjectField("材质", prototype.material, typeof(Material), false);
            }
        }

        private void DrawImport()
        {
            if (!IsAuthored) EditorGUILayout.HelpBox("原始位置只转换一次地图原点。导入不会重新随机、贴地或自动改写源高度。", MessageType.Info);
            if (IsAuthored) EditorGUILayout.HelpBox("当前为手绘植被图层，所有笔刷落点独立保存，可以从零实例开始绘制。", MessageType.Info);
            if (document != null && !IsAuthored) EditorGUILayout.HelpBox(document.summary, document.decoded ? MessageType.Info : MessageType.Warning);
            var selected = (GenshinVegetationData)EditorGUILayout.ObjectField("分块数据资产", data, typeof(GenshinVegetationData), false);
            if (selected != data) { data = selected; selectedId = null; RefreshPoints(); }
            assetPath = EditorGUILayout.TextField("数据资产路径", assetPath);
            DrawLayerCreation();
            using (new EditorGUI.DisabledGroupScope(document == null || !document.decoded || IsAuthored))
                if (GUILayout.Button("创建 / 重新导入数据资产")) Run(() => {
                    string destination = data == null ? assetPath : AssetDatabase.GetAssetPath(data);
                    data = GenshinVegetationSourceReader.Import(document, destination);
                    RefreshPoints(); message = "导入 " + data.SourceInstances.Length + " 个源实例；保留 " + data.EditCount + " 条局部修改。";
                });
            using (new EditorGUI.DisabledGroupScope(points.Length == 0))
            {
                bool show = EditorGUILayout.Toggle("Scene 落点预览", preview);
                if (show != preview) { preview = show; SceneView.RepaintAll(); }
                if (GUILayout.Button("定位预览范围")) Run(FramePoints);
            }
            if (data == null) return;
            EditorGUILayout.LabelField("目标场景", data.SourceScene, EditorStyles.wordWrappedLabel);
            if (data.SourceKey.EndsWith("/RegionVegetation_SourceSample", StringComparison.Ordinal))
                EditorGUILayout.HelpBox("此样例的 17 个花草已存在于地图。改用批量节点前，请先停用旧的 RegionVegetation_SourceSample 组，避免重复显示。", MessageType.Warning);
            if (GUILayout.Button("加法打开目标地图")) Run(() => {
                if (!TargetScene().isLoaded)
                {
                    Scene original = SceneManager.GetActiveScene();
                    EditorSceneManager.OpenScene(data.SourceScene, OpenSceneMode.Additive);
                    SceneManager.SetActiveScene(original);
                }
                message = "目标地图已加载；原活动场景保持打开。";
            });
            using (new EditorGUI.DisabledGroupScope(!TargetScene().isLoaded))
                if (GUILayout.Button("创建 / 更新场景批量绘制节点")) Run(() => {
                    sceneRenderer = ApplyToScene(data, TargetScene());
                    Selection.activeGameObject = sceneRenderer.gameObject;
                    RefreshPoints(); message = "场景实例 " + sceneRenderer.InstanceCount + "，绘制批次 " + sceneRenderer.BatchCount + "。场景尚未自动保存。";
                });
            if (GUILayout.Button("保存数据资产")) Run(SaveData);
            using (new EditorGUI.DisabledGroupScope(!TargetScene().isLoaded))
                if (GUILayout.Button("将目标地图设为活动场景")) Run(() => SceneManager.SetActiveScene(TargetScene()));
            using (new EditorGUI.DisabledGroupScope(!TargetScene().isLoaded || SceneManager.GetActiveScene() != TargetScene()))
                if (GUILayout.Button("保存活动的目标地图场景")) Run(() => {
                    SaveData();
                    if (!EditorSceneManager.SaveScene(TargetScene())) throw new IOException("Scene save failed.");
                    message = "已保存目标地图和植被数据。";
                });
            EditorGUILayout.LabelField("场景保存只对活动的目标地图开放，不会保存其他已打开场景。", EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawEditing()
        {
            if (data == null) { EditorGUILayout.HelpBox("先在预览与导入页选择或创建数据资产。", MessageType.Info); return; }
            EditorGUILayout.LabelField("源实例 " + data.SourceInstances.Length + " / 当前 " + points.Length + " / 修改 " + data.EditCount);
            if (data.OrphanedEditCount() > 0)
                EditorGUILayout.HelpBox("存在已不在当前源数据中的修改，保留待核对：" + data.OrphanedEditCount(), MessageType.Warning);
            DrawVegetationLibrary();
            bool show = EditorGUILayout.Toggle("Scene 落点与操纵柄", preview);
            if (show != preview) { preview = show; SceneView.RepaintAll(); }
            brushMode = GUILayout.Toolbar(brushMode, new[] { "选择 / 移动", "单点补种", "范围擦除", "范围种植" });
            if (brushMode != 0)
            {
                paintSurface = (Collider)EditorGUILayout.ObjectField("可种植表面", paintSurface, typeof(Collider), true);
                if (brushMode != 3)
                {
                prototypeIndex = EditorGUILayout.Popup("补种草种", Mathf.Clamp(prototypeIndex, 0, data.Prototypes.Length - 1), data.Prototypes.Select(p => p.key).ToArray());
                newScale = EditorGUILayout.FloatField("补种缩放", newScale);
                eraseRadius = EditorGUILayout.Slider("擦除半径", eraseRadius, 0.1f, 10);
                }
                else DrawRangeBrush();
                if (brushMode == 1) DrawPlantAppearance();
                EditorGUILayout.HelpBox("在 Scene 视图左键操作。补种仅使用指定表面；原始实例高度不会被吸附。Alt 保留视角操作。", MessageType.Info);
            }
            if (points.Length > 0)
            {
                int index = Array.FindIndex(points, p => p.id == selectedId);
                int next = EditorGUILayout.IntSlider("实例序号", Mathf.Max(index, 0), 0, points.Length - 1);
                if (index < 0 || next != index) selectedId = points[next].id;
                var value = points[next];
                EditorGUILayout.LabelField(value.id, EditorStyles.wordWrappedMiniLabel);
                EditorGUI.BeginChangeCheck();
                int kind = EditorGUILayout.Popup("草种", Array.FindIndex(data.Prototypes, p => p.key == value.prototype), data.Prototypes.Select(p => p.key).ToArray());
                value.position = EditorGUILayout.Vector3Field("项目坐标", value.position);
                Vector3 euler = EditorGUILayout.Vector3Field("朝向", value.rotation.eulerAngles);
                value.scale = EditorGUILayout.Vector3Field("缩放", value.scale);
                if (EditorGUI.EndChangeCheck()) Run(() => { value.prototype = data.Prototypes[kind].key;
                    value.rotation = Quaternion.Euler(euler); ChangeInstance(value, false); });
                if (GUILayout.Button("删除选中实例（可撤销）")) Run(() => ChangeInstance(value, true));
                if (GUILayout.Button("恢复选中实例的源值")) Run(() => {
                    Undo.RecordObject(data, "Restore vegetation instance"); data.Restore(value.id); Changed();
                });
            }
            showEdits = EditorGUILayout.Foldout(showEdits, "查看 / 恢复修改记录", true);
            if (showEdits) DrawEditPage();
            if (showEdits)
                foreach (var edit in data.GetEdits().Skip(editPage * 50).Take(50))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField((edit.deleted ? "删除 " : edit.added ? "补种 " : "调整 ") + edit.value.id,
                            EditorStyles.wordWrappedMiniLabel);
                        if (GUILayout.Button("恢复", GUILayout.Width(48))) Run(() => {
                            Undo.RecordObject(data, "Restore vegetation edit"); data.Restore(edit.value.id); Changed();
                        });
                    }
                }
            if (GUILayout.Button("撤销")) Undo.PerformUndo();
            if (GUILayout.Button("重做")) Undo.PerformRedo();
            if (GUILayout.Button("保存局部修改")) Run(SaveData);
        }

        private void ReadSource()
        {
            EndBrushStroke();
            var previous = data;
            // Clear the prior decoded document before parsing: an invalid new file must never import stale points.
            document = null; points = Array.Empty<GenshinVegetationData.Instance>(); preview = false; data = null;
            selectedId = null; sceneRenderer = null;
            document = GenshinVegetationSourceReader.Load(sourcePath);
            var existing = previous != null && previous.SourceKey == document.key ? previous :
                AssetDatabase.LoadAssetAtPath<GenshinVegetationData>(assetPath);
            if (existing != null && existing.SourceKey == document.key) data = existing;
            RefreshPoints();
        }

        private void RefreshPoints()
        {
            points = data != null ? data.GetInstances() : document != null && document.decoded ? document.instances :
                Array.Empty<GenshinVegetationData.Instance>();
            sceneRenderer = data == null ? null : Object.FindObjectsOfType<GenshinVegetationRenderer>(true)
                .FirstOrDefault(r => r.Data == data && r.gameObject.scene.path == data.SourceScene);
            SceneView.RepaintAll(); Repaint();
        }

        private Matrix4x4 PreviewMatrix => sceneRenderer == null ? Matrix4x4.identity : sceneRenderer.transform.localToWorldMatrix;

        private void DrawScene(SceneView view)
        {
            if (!preview || EditorApplication.isPlayingOrWillChangePlaymode) { EndBrushStroke(); return; }
            CheckBrushRelease(Event.current);
            Matrix4x4 matrix = PreviewMatrix;
            if (tab == 2 && data != null && brushMode != 0 && paintSurface != null)
            {
                Event input = Event.current;
                if (!input.alt && paintSurface.Raycast(HandleUtility.GUIPointToWorldRay(input.mousePosition), out RaycastHit hit, 10000))
                {
                    if (HandleBrushInput(input, hit, matrix)) return;
                }
            }
            if (tab == 2 && brushMode != 0) return;
            for (int i = 0; i < points.Length; i++)
            {
                var point = points[i];
                if (points.Length > 2000 && i % Mathf.CeilToInt(points.Length / 2000f) != 0 && point.id != selectedId) continue;
                Vector3 world = matrix.MultiplyPoint3x4(point.position);
                Handles.color = point.id == selectedId ? Color.yellow : new Color(0.2f, 1, 0.65f, 1);
                float size = HandleUtility.GetHandleSize(world) * 0.035f;
                if (Handles.Button(world, Quaternion.identity, size, size, Handles.DotHandleCap)) { selectedId = point.id; Repaint(); }
                if (tab != 2 || data == null || brushMode != 0 || point.id != selectedId) continue;
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(world, Quaternion.identity);
                if (EditorGUI.EndChangeCheck()) Run(() => { point.position = matrix.inverse.MultiplyPoint3x4(moved); ChangeInstance(point, false); });
            }
        }

        private void PaintAt(RaycastHit hit, Matrix4x4 matrix)
        {
            if (paintSurface == null || paintSurface.gameObject.scene != TargetScene())
                throw new InvalidOperationException("Choose a planting surface in the loaded target scene.");
            prototypeIndex = Mathf.Clamp(prototypeIndex, 0, data.Prototypes.Length - 1);
            Undo.RecordObject(data, "Edit vegetation at surface");
            if (brushMode == 1)
            {
                var value = new GenshinVegetationData.Instance { id = "local/" + Guid.NewGuid().ToString("N"),
                    prototype = data.Prototypes[prototypeIndex].key, position = matrix.inverse.MultiplyPoint3x4(hit.point),
                    rotation = Quaternion.Inverse(matrix.rotation) * Quaternion.FromToRotation(Vector3.up, hit.normal),
                    scale = new Vector3(newScale, newScale * GenshinVegetationBrush.HeightFromPressure(rangeBrush, lastStrokePressure), newScale) };
                brushStroke.ApplyTint(ref value, hit);
                data.SetEdit(value);
            }
            else
                foreach (var value in points)
                    if (Vector3.Distance(matrix.MultiplyPoint3x4(value.position), hit.point) <= eraseRadius) data.SetEdit(value, true);
            Changed();
        }

        private void ChangeInstance(GenshinVegetationData.Instance value, bool deleted)
        {
            GenshinVegetationData.ValidateInstance(value);
            Undo.RecordObject(data, "Edit vegetation instance");
            data.SetEdit(value, deleted); Changed();
        }

        private void Changed() { EditorUtility.SetDirty(data); RefreshPoints(); }
        private void UndoRedo() { EndBrushStroke(false); if (data != null) data.NotifyChanged(); RefreshPoints(); }
        private void SaveData() { if (data == null) throw new InvalidOperationException("Select a vegetation data asset."); AssetDatabase.SaveAssetIfDirty(data); message = "植被数据及局部修改已保存。"; }
        private Scene TargetScene() { return data == null ? default : SceneManager.GetSceneByPath(data.SourceScene); }

        private void FramePoints()
        {
            if (points.Length == 0 || SceneView.lastActiveSceneView == null) return;
            Bounds bounds = new Bounds(PreviewMatrix.MultiplyPoint3x4(points[0].position), Vector3.one);
            foreach (var point in points) bounds.Encapsulate(PreviewMatrix.MultiplyPoint3x4(point.position));
            SceneView.lastActiveSceneView.Frame(bounds, false);
        }

        internal static GenshinVegetationRenderer ApplyToScene(GenshinVegetationData source, Scene scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() || !scene.isLoaded || scene.path != source.SourceScene)
                throw new InvalidOperationException("Load the source layout's target scene in Edit mode first.");
            var instances = source.GetInstances();
            if (instances.Length > 0) GenshinVegetationData.Validate(source.Prototypes, instances);
            var matches = Object.FindObjectsOfType<GenshinVegetationRenderer>(true).Where(r => r.Data == source && r.gameObject.scene == scene).ToArray();
            if (matches.Length > 1) throw new InvalidOperationException("Multiple renderers already reference this source in the target scene.");
            GenshinVegetationRenderer renderer;
            if (matches.Length == 1) renderer = matches[0];
            else
            {
                var root = new GameObject("Vegetation_" + source.name);
                SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "Import source vegetation");
                renderer = Undo.AddComponent<GenshinVegetationRenderer>(root);
            }
            Undo.RecordObject(renderer, "Bind vegetation block");
            var serialized = new SerializedObject(renderer);
            var cullingShader = serialized.FindProperty("cullingShader");
            if (cullingShader.objectReferenceValue == null)
            {
                cullingShader.objectReferenceValue = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Shader/GenshinVegetationCull.compute");
                serialized.ApplyModifiedProperties();
            }
            renderer.Configure(source); renderer.Rebuild();
            if (!string.IsNullOrEmpty(renderer.ValidationError)) throw new InvalidOperationException(renderer.ValidationError);
            EditorSceneManager.MarkSceneDirty(scene);
            return renderer;
        }

        private void Run(Action action)
        {
            error = null; message = null;
            try { action(); }
            catch (Exception exception) { error = exception.Message; }
            Repaint();
        }
    }
}

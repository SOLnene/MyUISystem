using System;
using System.Collections.Generic;
using System.Linq;
using Game.Vegetation;
using UnityEditor;
using UnityEngine;
using EditorEventType = UnityEngine.EventType;

namespace Game.EditorTools
{
    internal sealed partial class GenshinVegetationWorkflowWindow
    {
        [SerializeField] private GenshinVegetationBrush.Settings rangeBrush = new GenshinVegetationBrush.Settings();
        [SerializeField] private SceneAsset authoredScene;
        [SerializeField] private string authoredPath = "Assets/Art/Map/Genshin/Mondstadt/Terrain/GenshinVegetation_Painted.asset";
        [SerializeField] private GenshinVegetationData.Prototype[] authoredPalette = Array.Empty<GenshinVegetationData.Prototype>();
        private bool showLayerCreation;
        private int editPage;
        private GenshinVegetationBrush.Stroke brushStroke;
        private GenshinVegetationData strokeData;
        private Collider strokeSurface;
        private int strokeMode;
        private int strokeGroup = -1;
        private int strokeControl;
        private int strokeChanges;
        private Vector3 lastStrokePoint;
        private float lastStrokePressure = -1;

        private bool IsAuthored => data != null && data.SourceKey != null && data.SourceKey.StartsWith("painted/", StringComparison.Ordinal);

        private void DrawLayerCreation()
        {
            showLayerCreation = EditorGUILayout.Foldout(showLayerCreation, "新建独立植被绘制图层", true);
            if (!showLayerCreation) return;
            if (authoredScene == null && data != null) authoredScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(data.SourceScene);
            authoredScene = (SceneAsset)EditorGUILayout.ObjectField("绘制目标地图", authoredScene, typeof(SceneAsset), false);
            authoredPath = EditorGUILayout.TextField("新图层资产路径", authoredPath);
            if (GUILayout.Button("复制当前草种绑定"))
            {
                var palette = data != null ? data.Prototypes : document != null && document.decoded ? document.prototypes : null;
                if (palette != null) authoredPalette = Array.ConvertAll(palette, p => new GenshinVegetationData.Prototype {
                    key = p.key, mesh = p.mesh, material = p.material });
            }
            int count = EditorGUILayout.IntSlider("图层草种数量", authoredPalette.Length, 1, 16);
            if (count != authoredPalette.Length) Array.Resize(ref authoredPalette, count);
            for (int i = 0; i < authoredPalette.Length; i++)
            {
                if (authoredPalette[i] == null) authoredPalette[i] = new GenshinVegetationData.Prototype { key = "Plant" + (i + 1) };
                var entry = authoredPalette[i];
                EditorGUILayout.LabelField("草种 " + (i + 1), EditorStyles.boldLabel);
                entry.key = EditorGUILayout.TextField("草种标识", entry.key);
                entry.mesh = (Mesh)EditorGUILayout.ObjectField("草种网格", entry.mesh, typeof(Mesh), false);
                entry.material = (Material)EditorGUILayout.ObjectField("草种材质", entry.material, typeof(Material), false);
            }
            using (new EditorGUI.DisabledGroupScope(authoredScene == null))
                if (GUILayout.Button("创建空白绘制图层")) Run(() => {
                    EndBrushStroke();
                    data = GenshinVegetationBrush.CreateLayer(AssetDatabase.GenerateUniqueAssetPath(authoredPath),
                        AssetDatabase.GetAssetPath(authoredScene), authoredPalette);
                    assetPath = AssetDatabase.GetAssetPath(data); selectedId = null; RefreshPoints();
                    message = "已创建空白图层。打开目标地图并创建批量绘制节点后，可在局部编辑页范围种植。";
                });
            EditorGUILayout.HelpBox("新图层从零个实例开始。草种网格与材质使用已保存的项目资源；绘制结果单独保存。", MessageType.Info);
        }

        private void DrawRangeBrush()
        {
            EditorGUILayout.LabelField("范围种植设置", EditorStyles.boldLabel);
            rangeBrush.radius = EditorGUILayout.Slider("种植半径", rangeBrush.radius, 0.1f, 10);
            rangeBrush.density = EditorGUILayout.Slider("种植密度 / 平方米", rangeBrush.density, 0.1f, 50);
            rangeBrush.minimumSpacing = EditorGUILayout.Slider("最小植株间距", rangeBrush.minimumSpacing, 0, 3);
            float low = EditorGUILayout.FloatField("最小随机缩放", rangeBrush.scaleRange.x);
            float high = EditorGUILayout.FloatField("最大随机缩放", rangeBrush.scaleRange.y);
            rangeBrush.scaleRange = new Vector2(low, high);
            DrawPlantAppearance();
            rangeBrush.maximumSlope = EditorGUILayout.Slider("最大种植坡度", rangeBrush.maximumSlope, 0, 90);
            rangeBrush.alignToNormal = EditorGUILayout.Toggle("沿表面法线生长", rangeBrush.alignToNormal);
            rangeBrush.edgeFalloff = EditorGUILayout.Slider("笔刷边缘衰减", rangeBrush.edgeFalloff, 0, 1);
            rangeBrush.clusterStrength = EditorGUILayout.Slider("成簇强度", rangeBrush.clusterStrength, 0, 1);
            rangeBrush.clusterSize = EditorGUILayout.Slider("簇团尺寸", rangeBrush.clusterSize, 0.2f, 20);
            rangeBrush.seed = EditorGUILayout.IntField("随机种子", rangeBrush.seed);
            EditorGUILayout.LabelField("草种比例（0 表示不种植）", EditorStyles.boldLabel);
            foreach (var prototype in data.Prototypes)
            {
                var weight = rangeBrush.weights.Find(w => w.key == prototype.key);
                if (weight == null) { weight = new GenshinVegetationBrush.Weight { key = prototype.key }; rangeBrush.weights.Add(weight); }
                weight.value = EditorGUILayout.Slider(prototype.key, weight.value, 0, 10);
                if (rangeBrush.mask != null && rangeBrush.maskControlsSpecies)
                    weight.maskChannel = EditorGUILayout.Popup(prototype.key + " 分布通道", weight.maskChannel + 1,
                        new[] { "自动", "R", "G", "B", "A" }) - 1;
            }
            EditorGUILayout.LabelField("道路 / 建筑禁种区域", EditorStyles.boldLabel);
            int blockers = EditorGUILayout.IntSlider("排除 Collider 数量", rangeBrush.blockers.Count, 0, 16);
            while (rangeBrush.blockers.Count < blockers) rangeBrush.blockers.Add(null);
            while (rangeBrush.blockers.Count > blockers) rangeBrush.blockers.RemoveAt(rangeBrush.blockers.Count - 1);
            for (int i = 0; i < rangeBrush.blockers.Count; i++)
                rangeBrush.blockers[i] = (Collider)EditorGUILayout.ObjectField("排除区域 " + (i + 1), rangeBrush.blockers[i], typeof(Collider), true);
            rangeBrush.mask = (Texture2D)EditorGUILayout.ObjectField("种植遮罩（可选）", rangeBrush.mask, typeof(Texture2D), false);
            if (rangeBrush.mask != null)
            {
                rangeBrush.maskControlsSpecies = EditorGUILayout.Toggle("按 RGBA 分配草种", rangeBrush.maskControlsSpecies);
                if (rangeBrush.maskControlsSpecies)
                {
                    EditorGUILayout.HelpBox("各草种按所选通道及草种比例采样；自动依次使用 R/G/B/A。有效通道中的最大值控制总密度，所有有效通道为黑色时不种植。", MessageType.Info);
                    EditorGUILayout.HelpBox("分布图使用图层坐标与下方覆盖范围，范围外不种植；需开启 Read/Write。A 通道表示透明度，没有透明通道的图片通常为 1。", MessageType.Info);
                }
                rangeBrush.maskOrigin = EditorGUILayout.Vector2Field("遮罩原点 XZ", rangeBrush.maskOrigin);
                rangeBrush.maskSize = EditorGUILayout.Vector2Field("遮罩覆盖尺寸 XZ", rangeBrush.maskSize);
                if (!rangeBrush.maskControlsSpecies)
                    EditorGUILayout.HelpBox("遮罩使用图层坐标：白色允许种植，黑色禁止，灰色降低密度；覆盖范围外不种植。纹理需开启 Read/Write。", MessageType.Info);
            }
            EditorGUILayout.HelpBox("左键拖动连续种植，范围擦除也支持拖动；一整笔可一次撤销。固定种子控制候选落点，已有植株按最小间距保留。", MessageType.Info);
        }

        private void DrawPlantAppearance()
        {
            rangeBrush.usePenPressure = EditorGUILayout.Toggle("压感控制草高", rangeBrush.usePenPressure);
            if (rangeBrush.usePenPressure)
            {
                float minimumHeight = EditorGUILayout.Slider("轻压高度倍率", rangeBrush.pressureHeightRange.x, 0.05f, 3);
                float maximumHeight = EditorGUILayout.Slider("重压高度倍率", rangeBrush.pressureHeightRange.y, minimumHeight, 3);
                rangeBrush.pressureHeightRange = new Vector2(minimumHeight, maximumHeight);
                EditorGUILayout.HelpBox("数位笔压力只影响新植株高度，鼠标保持原高度；已种植的实例不会随参数变化。", MessageType.Info);
            }
            rangeBrush.paintTint = EditorGUILayout.Toggle("启用植株染色", rangeBrush.paintTint);
            if (rangeBrush.paintTint)
            {
                rangeBrush.sampleSurfaceColor = EditorGUILayout.Toggle("草根采样地表底色", rangeBrush.sampleSurfaceColor);
                if (!rangeBrush.sampleSurfaceColor) rangeBrush.rootTint = EditorGUILayout.ColorField("草根颜色", rangeBrush.rootTint);
                rangeBrush.tipTint = EditorGUILayout.ColorField("草尖颜色", rangeBrush.tipTint);
                rangeBrush.tintStrength = EditorGUILayout.Slider("染色强度", rangeBrush.tintStrength, 0, 1);
                rangeBrush.tintPower = EditorGUILayout.Slider("颜色渐变指数", rangeBrush.tintPower, 0.1f, 4);
                EditorGUILayout.HelpBox("染色仅作用于新增植株并随实例保存。自动采样地表底色，不包含灯光和阴影；也可关闭采样指定草根色。", MessageType.Info);
            }
        }

        private void CheckBrushRelease(Event input)
        {
            if (brushStroke == null) return;
            if (!preview || tab != 2 || brushMode != strokeMode || data != strokeData || paintSurface != strokeSurface || input.alt)
            { EndBrushStroke(); return; }
            if ((input.rawType == EditorEventType.MouseUp && input.button == 0) ||
                (input.type == EditorEventType.KeyDown && input.keyCode == KeyCode.Escape))
            { EndBrushStroke(); input.Use(); }
        }

        private bool HandleBrushInput(Event input, RaycastHit hit, Matrix4x4 matrix)
        {
            int control = GUIUtility.GetControlID("GenshinVegetationBrush".GetHashCode(), FocusType.Passive);
            Handles.color = brushMode == 2 ? Color.red : Color.green;
            float radius = brushMode == 3 ? rangeBrush.radius : brushMode == 2 ? eraseRadius : 0.2f;
            Handles.DrawWireDisc(hit.point, brushMode == 3 ? Vector3.up : hit.normal, radius);
            if (input.type == EditorEventType.Layout && !input.alt) HandleUtility.AddDefaultControl(control);
            if (input.alt || input.button != 0) return false;
            if (input.type == EditorEventType.MouseDown && GUIUtility.hotControl == 0 && HandleUtility.nearestControl == control)
            {
                Run(() => {
                    BeginBrushStroke(matrix);
                    strokeControl = control; GUIUtility.hotControl = control;
                    lastStrokePoint = hit.point;
                    lastStrokePressure = BrushPressure(input);
                    if (brushMode == 1) { PaintAt(hit, matrix); strokeChanges++; }
                    else StampBrush(hit.point, lastStrokePressure);
                });
                if (brushStroke == null) return false;
                input.Use(); return true;
            }
            if (input.type == EditorEventType.MouseDrag && brushStroke != null && GUIUtility.hotControl == strokeControl)
            {
                Run(() => {
                    try { if (brushMode != 1) PaintStrokeSegment(hit.point, BrushPressure(input)); }
                    catch { EndBrushStroke(); throw; }
                });
                input.Use(); return true;
            }
            if (input.type == EditorEventType.MouseMove) SceneView.RepaintAll();
            return false;
        }

        private void BeginBrushStroke(Matrix4x4 matrix)
        {
            EndBrushStroke();
            var stroke = new GenshinVegetationBrush.Stroke(data, paintSurface, matrix, rangeBrush, brushMode != 3);
            Undo.IncrementCurrentGroup(); strokeGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Paint vegetation stroke");
            Undo.RegisterCompleteObjectUndo(data, "Paint vegetation stroke");
            brushStroke = stroke; strokeData = data; strokeSurface = paintSurface; strokeMode = brushMode; strokeChanges = 0;
        }

        private static float BrushPressure(Event input) => input.pointerType == PointerType.Pen ? input.pressure : -1;

        private void StampBrush(Vector3 center, float pressure)
        {
            int changed = brushMode == 3 ? brushStroke.Plant(center, pressure) : brushStroke.Erase(center, eraseRadius);
            strokeChanges += changed;
            if (changed > 0) Changed();
        }

        private void PaintStrokeSegment(Vector3 end, float pressure)
        {
            float radius = brushMode == 3 ? rangeBrush.radius : eraseRadius;
            float step = Mathf.Max(0.05f, Mathf.Min(radius * 0.35f, 1));
            float distance = new Vector2(end.x - lastStrokePoint.x, end.z - lastStrokePoint.z).magnitude;
            int samples = Mathf.Max(1, Mathf.CeilToInt(distance / step));
            if (samples > 256) throw new InvalidOperationException("The cursor crossed too far in one update; continue with a shorter stroke.");
            int before = strokeChanges;
            for (int i = 1; i <= samples; i++)
            {
                Vector3 point = Vector3.Lerp(lastStrokePoint, end, (float)i / samples);
                if (!GenshinVegetationBrush.TrySurfaceHit(paintSurface, point, out RaycastHit hit)) continue;
                float samplePressure = pressure < 0 || lastStrokePressure < 0 ? pressure :
                    Mathf.Lerp(lastStrokePressure, pressure, (float)i / samples);
                strokeChanges += brushMode == 3 ? brushStroke.Plant(hit.point, samplePressure) : brushStroke.Erase(hit.point, eraseRadius);
            }
            lastStrokePoint = end;
            lastStrokePressure = pressure;
            if (strokeChanges != before) Changed();
        }

        private void EndBrushStroke(bool collapse = true)
        {
            if (brushStroke == null) return;
            int group = strokeGroup;
            if (GUIUtility.hotControl == strokeControl && strokeControl != 0) GUIUtility.hotControl = 0;
            brushStroke.Dispose();
            brushStroke = null; strokeData = null; strokeSurface = null; strokeGroup = -1; strokeControl = 0;
            if (collapse && group >= 0) { Undo.FlushUndoRecordObjects(); Undo.CollapseUndoOperations(group); Undo.IncrementCurrentGroup(); }
            if (strokeChanges > 0) message = "本笔处理 " + strokeChanges + " 个实例；可一次撤销，保存局部修改后持久化。";
        }

        private void DrawEditPage()
        {
            int pages = Mathf.Max(1, Mathf.CeilToInt(data.EditCount / 50f));
            editPage = Mathf.Clamp(editPage, 0, pages - 1);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledGroupScope(editPage == 0)) if (GUILayout.Button("上一页")) editPage--;
                EditorGUILayout.LabelField((editPage + 1) + " / " + pages, GUILayout.Width(65));
                using (new EditorGUI.DisabledGroupScope(editPage == pages - 1)) if (GUILayout.Button("下一页")) editPage++;
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using EditorEventType = UnityEngine.EventType;
using Random = System.Random;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
internal sealed class GenshinGrassPatch : MonoBehaviour
{
    [Header("Surface And Assets")]
    [SerializeField] private MeshCollider surface;
    [SerializeField] private Texture2D densityMap;
    [SerializeField] private Mesh grassMesh;
    [SerializeField] private Material grassMaterial;
    [SerializeField] private Material groundMaterial;

    [Header("Distribution")]
    [SerializeField] private Vector2 areaSize = new Vector2(20, 20);
    [SerializeField, Range(1, 30)] private float density = 8;
    [SerializeField] private Vector2 scaleRange = new Vector2(0.24f, 0.36f);
    [SerializeField] private int seed = 7351;
    [SerializeField, Min(0)] private float edgeInset = 0.24f;
    [SerializeField, Min(1)] private float maximumDistance = 60;
    [SerializeField] private bool animateWind = true;

    [Header("Density Brush")]
    [SerializeField] private bool paintDensity;
    [SerializeField, Min(0.1f)] private float brushRadius = 1.2f;
    [SerializeField, Range(0.01f, 1)] private float brushStrength = 0.35f;
    [SerializeField, Range(0, 1)] private float brushTarget = 1;
    [SerializeField, HideInInspector] private int instanceCount;

    private sealed class Batch
    {
        internal Matrix4x4[] matrices;
        internal MaterialPropertyBlock properties;
        internal Bounds bounds;
    }

    private struct Instance
    {
        internal Matrix4x4 matrix;
        internal Vector4 groundUV;
        internal Vector3 position;
    }

    private readonly List<Batch> batches = new List<Batch>();
    private readonly Plane[] frustumPlanes = new Plane[6];
    private static readonly int GroundTextureId = Shader.PropertyToID("_GroundTex");
    private static readonly int GroundUVId = Shader.PropertyToID("_GroundUV");
    private static readonly int GrassTimeId = Shader.PropertyToID("_GrassTime");
    private bool rebuildPending;
    private Matrix4x4 surfaceMatrix;

    private void OnEnable()
    {
        Camera.onPreCull += DrawGrass;
        rebuildPending = true;
#if UNITY_EDITOR
        SceneView.duringSceneGui += PaintSceneDensity;
        Undo.undoRedoPerformed += OnDensityUndoRedo;
#endif
    }

    private void OnDisable()
    {
        Camera.onPreCull -= DrawGrass;
        batches.Clear();
#if UNITY_EDITOR
        SceneView.duringSceneGui -= PaintSceneDensity;
        Undo.undoRedoPerformed -= OnDensityUndoRedo;
        FinishBrushStroke();
#endif
    }

    private void OnValidate()
    {
        // Inspector validation can run during deserialization; defer physics and texture access to Update.
        rebuildPending = true;
    }

    private void Update()
    {
        if (transform.hasChanged || (surface != null && surface.transform.localToWorldMatrix != surfaceMatrix))
            rebuildPending = true;
        if (rebuildPending)
            RebuildInstances();
    }

    [ContextMenu("Rebuild Grass")]
    private void RebuildInstances()
    {
        rebuildPending = false;
        batches.Clear();
        instanceCount = 0;
        // A newly added component has no assets yet. Validate configuration once, not on every draw.
        if (surface == null || densityMap == null || grassMesh == null || grassMaterial == null || groundMaterial == null)
            return;
        if (!densityMap.isReadable || !SystemInfo.supportsInstancing || !grassMaterial.enableInstancing)
        {
            Debug.LogError("Grass requires a readable density map and an instanced material.", this);
            return;
        }

        surfaceMatrix = surface.transform.localToWorldMatrix;
        transform.hasChanged = false;
        Vector2 size = new Vector2(Mathf.Max(0.1f, areaSize.x), Mathf.Max(0.1f, areaSize.y));
        int columns = Mathf.CeilToInt(size.x * Mathf.Sqrt(Mathf.Max(1, density)));
        int rows = Mathf.CeilToInt(size.y * Mathf.Sqrt(Mathf.Max(1, density)));
        Color32[] pixels = densityMap.GetPixels32();
        Vector2 mainScale = groundMaterial.GetTextureScale("_MainTex");
        Vector2 mainOffset = groundMaterial.GetTextureOffset("_MainTex");
        var random = new Random(seed);
        var cells = new Dictionary<Vector2Int, List<Instance>>();

        for (int z = 0; z < rows; z++)
        for (int x = 0; x < columns; x++)
        {
            // Consume a fixed number of random values per candidate so brushing never reshuffles surviving grass.
            float u = (x + 0.1f + (float)random.NextDouble() * 0.8f) / columns;
            float v = (z + 0.1f + (float)random.NextDouble() * 0.8f) / rows;
            float acceptance = (float)random.NextDouble();
            float yaw = (float)random.NextDouble() * 360;
            float scale = Mathf.Lerp(scaleRange.x, scaleRange.y, (float)random.NextDouble());
            int pixelX = Mathf.Min(densityMap.width - 1, (int)(u * densityMap.width));
            int pixelY = Mathf.Min(densityMap.height - 1, (int)(v * densityMap.height));
            if (acceptance >= pixels[pixelY * densityMap.width + pixelX].r / 255f)
                continue;

            Vector3 point = transform.position + new Vector3((u - 0.5f) * size.x, 0, (v - 0.5f) * size.y);
            if (!TrySurfacePoint(point, out RaycastHit hit))
                continue;
            if (edgeInset > 0 && (!TrySurfacePoint(point + Vector3.right * edgeInset, out _) ||
                !TrySurfacePoint(point - Vector3.right * edgeInset, out _) ||
                !TrySurfacePoint(point + Vector3.forward * edgeInset, out _) ||
                !TrySurfacePoint(point - Vector3.forward * edgeInset, out _)))
                continue;

            Vector2 uv = Vector2.Scale(hit.textureCoord, mainScale) + mainOffset;
            Vector2Int cell = new Vector2Int(Mathf.FloorToInt((point.x - transform.position.x) / 4),
                Mathf.FloorToInt((point.z - transform.position.z) / 4));
            if (!cells.TryGetValue(cell, out List<Instance> instances))
                cells.Add(cell, instances = new List<Instance>());
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0, yaw, 0);
            instances.Add(new Instance
            {
                matrix = Matrix4x4.TRS(hit.point, rotation, Vector3.one * Mathf.Max(0.01f, scale)),
                groundUV = new Vector4(uv.x, uv.y, 0, 0),
                position = hit.point
            });
        }

        foreach (List<Instance> instances in cells.Values)
        for (int start = 0; start < instances.Count; start += 256)
        {
            int count = Mathf.Min(256, instances.Count - start);
            var matrices = new Matrix4x4[count];
            var groundUVs = new Vector4[count];
            var bounds = new Bounds(instances[start].position, Vector3.zero);
            for (int i = 0; i < count; i++)
            {
                Instance instance = instances[start + i];
                matrices[i] = instance.matrix;
                groundUVs[i] = instance.groundUV;
                bounds.Encapsulate(instance.position);
            }
            // Include the full source mesh and wind displacement in CPU and renderer culling bounds.
            bounds.Expand((grassMesh.bounds.extents.magnitude + grassMesh.bounds.center.magnitude) *
                Mathf.Max(scaleRange.x, scaleRange.y) * 2 + 1);
            var properties = new MaterialPropertyBlock();
            properties.SetVectorArray(GroundUVId, groundUVs);
            properties.SetTexture(GroundTextureId, groundMaterial.GetTexture("_MainTex"));
            batches.Add(new Batch { matrices = matrices, properties = properties, bounds = bounds });
            instanceCount += count;
        }
    }

    private bool TrySurfacePoint(Vector3 point, out RaycastHit hit)
    {
        Ray ray = new Ray(new Vector3(point.x, surface.bounds.max.y + 3, point.z), Vector3.down);
        if (!surface.Raycast(ray, out hit, surface.bounds.size.y + 6))
            return false;
        // Reject roofs, paving, and props covering this grass surface without changing scene colliders.
        return !Physics.Raycast(ray, out RaycastHit first, surface.bounds.size.y + 6,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || first.collider == surface;
    }

    private void DrawGrass(Camera camera)
    {
        if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView)
            return;
        GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);
        float time = animateWind ? Time.realtimeSinceStartup : 0;
        foreach (Batch batch in batches)
        {
            if (batch.bounds.SqrDistance(camera.transform.position) > maximumDistance * maximumDistance ||
                !GeometryUtility.TestPlanesAABB(frustumPlanes, batch.bounds))
                continue;
            batch.properties.SetFloat(GrassTimeId, time);
            var parameters = new RenderParams(grassMaterial)
            {
                camera = camera,
                worldBounds = batch.bounds,
                matProps = batch.properties,
                layer = gameObject.layer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                lightProbeUsage = LightProbeUsage.Off
            };
            Graphics.RenderMeshInstanced(parameters, grassMesh, 0, batch.matrices);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 1, 0.6f, 0.8f);
        Gizmos.DrawWireCube(transform.position, new Vector3(areaSize.x, 0.2f, areaSize.y));
    }

#if UNITY_EDITOR
    private int brushControl;

    private void PaintSceneDensity(SceneView view)
    {
        if (!paintDensity || Selection.activeGameObject != gameObject || Application.isPlaying)
            return;
        Event input = Event.current;
        if (input.type == EditorEventType.MouseUp && brushControl != 0)
        {
            FinishBrushStroke();
            input.Use();
            return;
        }
        if (input.alt || surface == null || densityMap == null)
            return;
        int control = GUIUtility.GetControlID(FocusType.Passive);
        if (input.type == EditorEventType.Layout)
            HandleUtility.AddDefaultControl(control);
        if (!surface.Raycast(HandleUtility.GUIPointToWorldRay(input.mousePosition), out RaycastHit hit, 10000))
            return;
        Vector2 uv = new Vector2((hit.point.x - transform.position.x) / areaSize.x + 0.5f,
            (hit.point.z - transform.position.z) / areaSize.y + 0.5f);
        if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1)
            return;
        Handles.color = input.shift ? Color.red : Color.green;
        Handles.DrawWireDisc(hit.point, hit.normal, brushRadius);
        if (input.button == 0 && input.type == EditorEventType.MouseDown)
        {
            Undo.RegisterCompleteObjectUndo(densityMap, "Paint Grass Density");
            GUIUtility.hotControl = brushControl = control;
        }
        if (brushControl != 0 && input.button == 0 &&
            (input.type == EditorEventType.MouseDown || input.type == EditorEventType.MouseDrag))
        {
            PaintDensityAt(uv, input.shift ? 0 : brushTarget);
            input.Use();
        }
        if (input.type == EditorEventType.MouseMove)
            view.Repaint();
    }

    private void PaintDensityAt(Vector2 center, float targetDensity)
    {
        Color32[] pixels = densityMap.GetPixels32();
        for (int y = 0; y < densityMap.height; y++)
        for (int x = 0; x < densityMap.width; x++)
        {
            Vector2 delta = new Vector2(((x + 0.5f) / densityMap.width - center.x) * areaSize.x,
                ((y + 0.5f) / densityMap.height - center.y) * areaSize.y);
            float weight = Mathf.Clamp01(1 - delta.magnitude / brushRadius) * brushStrength;
            int index = y * densityMap.width + x;
            byte value = (byte)Mathf.RoundToInt(Mathf.Lerp(pixels[index].r / 255f, targetDensity, weight) * 255);
            pixels[index] = new Color32(value, value, value, 255);
        }
        densityMap.SetPixels32(pixels);
        densityMap.Apply(false);
        EditorUtility.SetDirty(densityMap);
    }

    private void FinishBrushStroke()
    {
        if (brushControl == 0)
            return;
        if (GUIUtility.hotControl == brushControl)
            GUIUtility.hotControl = 0;
        brushControl = 0;
        AssetDatabase.SaveAssetIfDirty(densityMap);
        rebuildPending = true;
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
    }

    private void OnDensityUndoRedo()
    {
        rebuildPending = true;
        AssetDatabase.SaveAssetIfDirty(densityMap);
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
    }
#endif
}

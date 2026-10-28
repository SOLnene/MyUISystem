using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Vegetation
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class GenshinVegetationRenderer : MonoBehaviour
    {
        [SerializeField] private GenshinVegetationData data;
        [SerializeField, Min(1)] private float maximumDistance = 150;
        [SerializeField, Min(0)] private float windBoundsPadding = 1;
        // Opt in per layer so existing scenes keep their previous no-caster behavior.
        [SerializeField] private bool castShadows;
        [SerializeField] private bool gpuCulling = true;
        [SerializeField] private ComputeShader cullingShader;

        private sealed class Batch
        {
            public Mesh mesh;
            public Material material;
            public Matrix4x4[] matrices;
            public Bounds bounds;
            public MaterialPropertyBlock properties;
            public GenshinVegetationGpuBatch gpu;
        }

        private readonly List<Batch> batches = new List<Batch>();
        private readonly Plane[] planes = new Plane[6];
        private readonly Vector4[] planeVectors = new Vector4[6];
        private static readonly int TimeId = Shader.PropertyToID("_GrassTime");
        private static readonly int RootTintId = Shader.PropertyToID("_InstanceRootTint");
        private static readonly int TipTintId = Shader.PropertyToID("_InstanceTipTint");
        private static readonly int PaintHeightId = Shader.PropertyToID("_PaintHeightRange");
        private GenshinVegetationData lastData;
        private int lastRevision = -1;
        private bool rebuildPending;
        public GenshinVegetationData Data => data;
        public int InstanceCount { get; private set; }
        public int BatchCount => batches.Count;
        public string ValidationError { get; private set; }

        public void Configure(GenshinVegetationData source)
        {
            data = source;
            rebuildPending = true;
        }

        private void OnEnable()
        {
            Camera.onPreCull += Draw;
            rebuildPending = true;
        }

        private void OnDisable()
        {
            Camera.onPreCull -= Draw;
            ReleaseBatches();
        }

        private void OnValidate() { rebuildPending = true; }

        private void Update()
        {
            if (data != lastData || (data != null && data.Revision != lastRevision) || transform.hasChanged)
                rebuildPending = true;
            if (rebuildPending) Rebuild();
        }

        public void Rebuild()
        {
            ReleaseBatches();
            InstanceCount = 0;
            ValidationError = null;
            rebuildPending = false;
            lastData = data;
            lastRevision = data == null ? -1 : data.Revision;
            transform.hasChanged = false;
            if (data == null) return;
            try
            {
                GenshinVegetationData.Instance[] instances = data.GetInstances();
                // An intentionally erased block is valid and must not resurrect source instances.
                if (instances.Length == 0) return;
                GenshinVegetationData.Validate(data.Prototypes, instances);
                var byKey = new Dictionary<string, GenshinVegetationData.Prototype>();
                foreach (var prototype in data.Prototypes) byKey.Add(prototype.key, prototype);
                var groups = new Dictionary<(string, int, int, int), List<(Matrix4x4 matrix, GenshinVegetationData.Instance instance)>>();
                foreach (var instance in instances)
                {
                    Matrix4x4 matrix = transform.localToWorldMatrix *
                        Matrix4x4.TRS(instance.position, instance.rotation, instance.scale);
                    Vector3 point = matrix.GetColumn(3);
                    bool gpu = UsesGpuCulling(byKey[instance.prototype].material);
                    float blockSize = gpu ? 64 : 4;
                    var key = (instance.prototype, Mathf.FloorToInt(point.x / blockSize),
                        gpu ? 0 : Mathf.FloorToInt(point.y / 4), Mathf.FloorToInt(point.z / blockSize));
                    if (!groups.TryGetValue(key, out var group))
                        groups.Add(key, group = new List<(Matrix4x4, GenshinVegetationData.Instance)>());
                    group.Add((matrix, instance));
                }
                foreach (var group in groups)
                {
                    var prototype = byKey[group.Key.Item1];
                    bool gpu = UsesGpuCulling(prototype.material);
                    int batchSize = gpu ? 16384 : 256;
                    for (int start = 0; start < group.Value.Count; start += batchSize)
                    {
                        var items = group.Value.GetRange(start, Mathf.Min(batchSize, group.Value.Count - start));
                        Matrix4x4[] matrices = items.ConvertAll(item => item.matrix).ToArray();
                        var properties = new MaterialPropertyBlock();
                        // Flower meshes share the grass material but need their own root-to-tip normalization.
                        properties.SetVector(PaintHeightId, new Vector4(prototype.mesh.bounds.min.y,
                            1 / Mathf.Max(0.001f, prototype.mesh.bounds.size.y), 0, 0));
                        var roots = items.ConvertAll(item => TintVector(item.instance.rootTint, item.instance.tintStrength)).ToArray();
                        var tips = items.ConvertAll(item => TintVector(item.instance.tipTint, item.instance.tintPower)).ToArray();
                        if (!gpu)
                        {
                            properties.SetVectorArray(RootTintId, roots);
                            properties.SetVectorArray(TipTintId, tips);
                        }
                        Bounds bounds = TransformedBounds(prototype.mesh.bounds, matrices[0]);
                        for (int i = 1; i < matrices.Length; i++) bounds.Encapsulate(TransformedBounds(prototype.mesh.bounds, matrices[i]));
                        bounds.Expand(windBoundsPadding * 2);
                        batches.Add(new Batch { mesh = prototype.mesh, material = prototype.material,
                            matrices = matrices, bounds = bounds, properties = properties,
                            gpu = gpu ? new GenshinVegetationGpuBatch(cullingShader, prototype.mesh, matrices, roots, tips, windBoundsPadding, properties) : null });
                    }
                }
                InstanceCount = instances.Length;
            }
            catch (Exception exception)
            {
                ReleaseBatches();
                ValidationError = exception.Message;
                Debug.LogError("Vegetation block cannot render: " + exception.Message, this);
            }
        }

        private bool UsesGpuCulling(Material material) => gpuCulling && cullingShader != null &&
            SystemInfo.supportsComputeShaders && (material.shader.name == "Game/Scene/Genshin Instanced Grass" ||
            material.shader.name == "Game/Scene/Genshin Native Vegetation");

        private void ReleaseBatches()
        {
            foreach (Batch batch in batches) batch.gpu?.Dispose();
            batches.Clear();
        }

        private static Vector4 TintVector(Color stored, float parameter)
        {
            // ColorField stores sRGB colors; vector arrays bypass Unity's automatic color conversion.
            Color color = QualitySettings.activeColorSpace == ColorSpace.Linear ? stored.linear : stored;
            return new Vector4(color.r, color.g, color.b, parameter);
        }

        private static Bounds TransformedBounds(Bounds local, Matrix4x4 matrix)
        {
            Vector3 center = matrix.MultiplyPoint3x4(local.center);
            Vector3 x = matrix.MultiplyVector(new Vector3(local.extents.x, 0, 0));
            Vector3 y = matrix.MultiplyVector(new Vector3(0, local.extents.y, 0));
            Vector3 z = matrix.MultiplyVector(new Vector3(0, 0, local.extents.z));
            return new Bounds(center, new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z)) * 2);
        }

        private void Draw(Camera camera)
        {
            // Asset edits can repaint a Scene view without an ExecuteAlways Update, so rebuild before culling too.
            if (rebuildPending || data != lastData || (data != null && data.Revision != lastRevision) || transform.hasChanged)
                Rebuild();
            if ((camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) ||
                (camera.cullingMask & (1 << gameObject.layer)) == 0 || !SystemInfo.supportsInstancing) return;
            GeometryUtility.CalculateFrustumPlanes(camera, planes);
            for (int i = 0; i < planes.Length; i++)
                planeVectors[i] = new Vector4(planes[i].normal.x, planes[i].normal.y, planes[i].normal.z, planes[i].distance);
            foreach (Batch batch in batches)
            {
                if (batch.bounds.SqrDistance(camera.transform.position) > maximumDistance * maximumDistance ||
                    !GeometryUtility.TestPlanesAABB(planes, batch.bounds)) continue;
                batch.properties.SetFloat(TimeId, Time.realtimeSinceStartup);
                if (batch.gpu != null)
                {
                    batch.gpu.Cull(planeVectors, camera.transform.position, maximumDistance);
                    Graphics.DrawMeshInstancedIndirect(batch.mesh, 0, batch.material, batch.bounds,
                        batch.gpu.Arguments, 0, batch.properties, castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off, true,
                        gameObject.layer, camera, LightProbeUsage.Off);
                    continue;
                }
                var parameters = new RenderParams(batch.material) { camera = camera,
                    worldBounds = batch.bounds, matProps = batch.properties, layer = gameObject.layer,
                    shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off, receiveShadows = true,
                    lightProbeUsage = LightProbeUsage.Off };
                Graphics.RenderMeshInstanced(parameters, batch.mesh, 0, batch.matrices);
            }
        }
    }
}

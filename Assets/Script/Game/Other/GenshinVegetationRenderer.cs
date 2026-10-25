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

        private sealed class Batch
        {
            public Mesh mesh;
            public Material material;
            public Matrix4x4[] matrices;
            public Bounds bounds;
            public MaterialPropertyBlock properties;
        }

        private readonly List<Batch> batches = new List<Batch>();
        private readonly Plane[] planes = new Plane[6];
        private static readonly int TimeId = Shader.PropertyToID("_GrassTime");
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
            batches.Clear();
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
            batches.Clear();
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
                var groups = new Dictionary<(string, int, int, int), List<Matrix4x4>>();
                foreach (var instance in instances)
                {
                    Matrix4x4 matrix = transform.localToWorldMatrix *
                        Matrix4x4.TRS(instance.position, instance.rotation, instance.scale);
                    Vector3 point = matrix.GetColumn(3);
                    var key = (instance.prototype, Mathf.FloorToInt(point.x / 4),
                        Mathf.FloorToInt(point.y / 4), Mathf.FloorToInt(point.z / 4));
                    if (!groups.TryGetValue(key, out List<Matrix4x4> group))
                        groups.Add(key, group = new List<Matrix4x4>());
                    group.Add(matrix);
                }
                foreach (var group in groups)
                {
                    var prototype = byKey[group.Key.Item1];
                    for (int start = 0; start < group.Value.Count; start += 256)
                    {
                        Matrix4x4[] matrices = group.Value.GetRange(start, Mathf.Min(256, group.Value.Count - start)).ToArray();
                        Bounds bounds = TransformedBounds(prototype.mesh.bounds, matrices[0]);
                        for (int i = 1; i < matrices.Length; i++) bounds.Encapsulate(TransformedBounds(prototype.mesh.bounds, matrices[i]));
                        bounds.Expand(windBoundsPadding * 2);
                        batches.Add(new Batch { mesh = prototype.mesh, material = prototype.material,
                            matrices = matrices, bounds = bounds, properties = new MaterialPropertyBlock() });
                    }
                }
                InstanceCount = instances.Length;
            }
            catch (Exception exception)
            {
                batches.Clear();
                ValidationError = exception.Message;
                Debug.LogError("Vegetation block cannot render: " + exception.Message, this);
            }
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
            foreach (Batch batch in batches)
            {
                if (batch.bounds.SqrDistance(camera.transform.position) > maximumDistance * maximumDistance ||
                    !GeometryUtility.TestPlanesAABB(planes, batch.bounds)) continue;
                batch.properties.SetFloat(TimeId, Time.realtimeSinceStartup);
                var parameters = new RenderParams(batch.material) { camera = camera,
                    worldBounds = batch.bounds, matProps = batch.properties, layer = gameObject.layer,
                    shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true,
                    lightProbeUsage = LightProbeUsage.Off };
                Graphics.RenderMeshInstanced(parameters, batch.mesh, 0, batch.matrices);
            }
        }
    }
}

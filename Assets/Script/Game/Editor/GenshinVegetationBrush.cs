using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Vegetation;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    internal static class GenshinVegetationBrush
    {
        [Serializable]
        internal sealed class Weight
        {
            public string key;
            public float value = 1;
        }

        [Serializable]
        internal sealed class Settings
        {
            public float radius = 2;
            public float density = 6;
            public float minimumSpacing = 0.25f;
            public Vector2 scaleRange = new Vector2(0.8f, 1.2f);
            public float maximumSlope = 40;
            public bool alignToNormal = true;
            public float edgeFalloff = 0.25f;
            public float clusterStrength = 0.3f;
            public float clusterSize = 3;
            public int seed = 7351;
            public List<Weight> weights = new List<Weight>();
            public List<Collider> blockers = new List<Collider>();
            public Texture2D mask;
            public Vector2 maskOrigin;
            public Vector2 maskSize = new Vector2(64, 64);
        }

        internal sealed class Stroke
        {
            private struct Placed
            {
                internal Vector3 position;
                internal GenshinVegetationData.Instance instance;
            }

            private readonly GenshinVegetationData data;
            private readonly Collider surface;
            private readonly Settings settings;
            private readonly Matrix4x4 worldToLocal;
            private readonly Dictionary<Vector3Int, List<Placed>> cells = new Dictionary<Vector3Int, List<Placed>>();
            private readonly HashSet<string> erased = new HashSet<string>();
            private readonly float[] weights;
            private readonly float totalWeight;
            private readonly float cellSize;

            internal Stroke(GenshinVegetationData data, Collider surface, Matrix4x4 localToWorld, Settings settings, bool eraseOnly = false)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || data == null || surface == null ||
                    !surface.enabled || !surface.gameObject.activeInHierarchy || surface.gameObject.scene.path != data.SourceScene)
                    throw new InvalidOperationException("Choose an enabled surface in the loaded target scene in Edit mode.");
                if (!Finite(settings.minimumSpacing) || settings.minimumSpacing < 0 || Mathf.Abs(localToWorld.determinant) < 0.000001f)
                    throw new InvalidOperationException("Brush spacing and renderer transform must be valid.");
                this.data = data; this.surface = surface; this.settings = settings;
                worldToLocal = localToWorld.inverse;
                cellSize = Mathf.Max(1, settings.minimumSpacing);
                foreach (var point in data.GetInstances()) AddToIndex(point, localToWorld.MultiplyPoint3x4(point.position));
                if (eraseOnly) return;
                if (!Finite(settings.radius) || settings.radius <= 0 || settings.radius > 20 ||
                    !Finite(settings.density) || settings.density <= 0 || settings.density > 100 ||
                    !Finite(settings.scaleRange.x) || !Finite(settings.scaleRange.y) || settings.scaleRange.x <= 0 ||
                    settings.scaleRange.y < settings.scaleRange.x || !Finite(settings.maximumSlope) || settings.maximumSlope < 0 || settings.maximumSlope > 90 ||
                    !Finite(settings.edgeFalloff) || settings.edgeFalloff < 0 || settings.edgeFalloff > 1 ||
                    !Finite(settings.clusterStrength) || settings.clusterStrength < 0 || settings.clusterStrength > 1 ||
                    !Finite(settings.clusterSize) || settings.clusterSize <= 0)
                    throw new InvalidOperationException("Brush size, density, scale, slope and clustering must be within their displayed ranges.");
                if (Mathf.Pow(2 * settings.radius * Mathf.Sqrt(settings.density) + 3, 2) > 25000)
                    throw new InvalidOperationException("Reduce brush radius or density: a stamp supports up to 25000 candidate cells.");
                if (settings.mask != null && (!settings.mask.isReadable || !Finite(settings.maskSize.x) ||
                    !Finite(settings.maskSize.y) || settings.maskSize.x <= 0 || settings.maskSize.y <= 0 ||
                    !Finite(settings.maskOrigin.x) || !Finite(settings.maskOrigin.y)))
                    throw new InvalidOperationException("The planting mask must be readable and have a finite origin and positive size.");
                foreach (var blocker in settings.blockers)
                    if (blocker != null && (blocker == surface || blocker.gameObject.scene != surface.gameObject.scene))
                        throw new InvalidOperationException("Exclusion colliders must belong to the target scene and differ from the planting surface.");
                weights = data.Prototypes.Select(p => settings.weights.Find(w => w.key == p.key)?.value ?? 1).ToArray();
                if (weights.Any(w => !Finite(w) || w < 0) || !Finite(weights.Sum()) || weights.Sum() <= 0)
                    throw new InvalidOperationException("At least one grass type must have a positive finite weight.");
                totalWeight = weights.Sum();
            }

            internal int Plant(Vector3 center)
            {
                float step = 1 / Mathf.Sqrt(settings.density);
                int minimumX = Mathf.FloorToInt((center.x - settings.radius) / step);
                int maximumX = Mathf.FloorToInt((center.x + settings.radius) / step);
                int minimumZ = Mathf.FloorToInt((center.z - settings.radius) / step);
                int maximumZ = Mathf.FloorToInt((center.z + settings.radius) / step);
                int count = 0;
                for (int z = minimumZ; z <= maximumZ; z++)
                for (int x = minimumX; x <= maximumX; x++)
                {
                    // World-grid candidates are stable across stamps; a stationary or slower cursor cannot keep stacking plants.
                    var sample = new Vector3((x + Random(x, z, 1)) * step, center.y, (z + Random(x, z, 2)) * step);
                    float distance = new Vector2(sample.x - center.x, sample.z - center.z).magnitude / settings.radius;
                    if (distance > 1 || !TrySurfaceHit(surface, sample, out RaycastHit hit) ||
                        Vector3.Angle(Vector3.up, hit.normal) > settings.maximumSlope || IsBlocked(hit.point)) continue;
                    float chance = settings.edgeFalloff <= 0 ? 1 : Mathf.Clamp01((1 - distance) / settings.edgeFalloff);
                    float noise = Mathf.PerlinNoise(sample.x / settings.clusterSize + (settings.seed & 1023),
                        sample.z / settings.clusterSize + ((settings.seed >> 10) & 1023));
                    chance *= Mathf.Lerp(1, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.25f, 0.75f, noise)), settings.clusterStrength);
                    Vector3 local = worldToLocal.MultiplyPoint3x4(hit.point);
                    if (settings.mask != null)
                    {
                        float u = (local.x - settings.maskOrigin.x) / settings.maskSize.x;
                        float v = (local.z - settings.maskOrigin.y) / settings.maskSize.y;
                        if (u < 0 || u > 1 || v < 0 || v > 1) continue;
                        chance *= Mathf.Clamp01(settings.mask.GetPixelBilinear(u, v).r);
                    }
                    if (Random(x, z, 3) >= chance || Nearby(hit.point, Mathf.Max(0.001f, settings.minimumSpacing)).Any()) continue;
                    float pick = Random(x, z, 4) * totalWeight;
                    int kind = 0;
                    for (; kind < weights.Length - 1; kind++) { pick -= weights[kind]; if (pick < 0) break; }
                    Vector3 up = worldToLocal.MultiplyVector(settings.alignToNormal ? hit.normal : Vector3.up).normalized;
                    var value = new GenshinVegetationData.Instance { id = "local/" + Guid.NewGuid().ToString("N"),
                        prototype = data.Prototypes[kind].key, position = local,
                        rotation = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.AngleAxis(Random(x, z, 5) * 360, Vector3.up),
                        scale = Vector3.one * Mathf.Lerp(settings.scaleRange.x, settings.scaleRange.y, Random(x, z, 6)) };
                    data.SetEdit(value); AddToIndex(value, hit.point); count++;
                }
                return count;
            }

            internal int Erase(Vector3 center, float radius)
            {
                if (!Finite(radius) || radius <= 0 || radius > 20) throw new InvalidOperationException("Invalid eraser radius.");
                int count = 0;
                foreach (var point in Nearby(center, radius))
                    if (erased.Add(point.instance.id)) { data.SetEdit(point.instance, true); count++; }
                return count;
            }

            private bool IsBlocked(Vector3 point)
            {
                foreach (var blocker in settings.blockers)
                {
                    if (blocker == null || !blocker.enabled || !blocker.gameObject.activeInHierarchy) continue;
                    Bounds bounds = blocker.bounds;
                    if (point.x < bounds.min.x || point.x > bounds.max.x || point.z < bounds.min.z || point.z > bounds.max.z) continue;
                    // Project the actual collider footprint, rather than excluding the whole AABB of a curved road mesh.
                    var ray = new Ray(new Vector3(point.x, bounds.max.y + 0.1f, point.z), Vector3.down);
                    if (blocker.Raycast(ray, out RaycastHit hit, bounds.size.y + 0.2f) && hit.point.y >= point.y - 0.25f) return true;
                }
                return false;
            }

            private Vector3Int Cell(Vector3 point) => new Vector3Int(Mathf.FloorToInt(point.x / cellSize),
                Mathf.FloorToInt(point.y / cellSize), Mathf.FloorToInt(point.z / cellSize));

            private void AddToIndex(GenshinVegetationData.Instance value, Vector3 world)
            {
                Vector3Int key = Cell(world);
                if (!cells.TryGetValue(key, out List<Placed> bucket)) cells.Add(key, bucket = new List<Placed>());
                bucket.Add(new Placed { instance = value, position = world });
            }

            private IEnumerable<Placed> Nearby(Vector3 center, float radius)
            {
                Vector3Int first = Cell(center - Vector3.one * radius), last = Cell(center + Vector3.one * radius);
                for (int z = first.z; z <= last.z; z++)
                for (int y = first.y; y <= last.y; y++)
                for (int x = first.x; x <= last.x; x++)
                    if (cells.TryGetValue(new Vector3Int(x, y, z), out List<Placed> bucket))
                        foreach (var point in bucket)
                            if ((point.position - center).sqrMagnitude < radius * radius) yield return point;
            }

            private float Random(int x, int z, uint channel)
            {
                uint hash = unchecked((uint)settings.seed ^ (uint)x * 0x9e3779b9u ^ (uint)z * 0x85ebca6bu ^ channel * 0xc2b2ae35u);
                hash ^= hash >> 16; hash = unchecked(hash * 0x7feb352du); hash ^= hash >> 15;
                hash = unchecked(hash * 0x846ca68bu); hash ^= hash >> 16;
                return (hash & 0xffffff) / 16777216f;
            }
        }

        internal static bool TrySurfaceHit(Collider surface, Vector3 point, out RaycastHit hit)
        {
            Bounds bounds = surface.bounds;
            return surface.Raycast(new Ray(new Vector3(point.x, bounds.max.y + 0.1f, point.z), Vector3.down),
                out hit, bounds.size.y + 0.2f);
        }

        internal static GenshinVegetationData CreateLayer(string path, string scene, GenshinVegetationData.Prototype[] palette)
        {
            path = path.Replace('\\', '/');
            if (EditorApplication.isPlayingOrWillChangePlaymode || !path.StartsWith("Assets/Art/Map/Genshin/", StringComparison.Ordinal) ||
                path.Contains("..") || !path.EndsWith(".asset", StringComparison.Ordinal) ||
                !AssetDatabase.IsValidFolder(Path.GetDirectoryName(path).Replace('\\', '/')) || File.Exists(path) ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null || palette == null || palette.Length == 0)
                throw new InvalidOperationException("Choose a new .asset in an existing map folder, a project scene and a grass palette.");
            // Validate bindings without inserting a fictitious source instance into an authored layer.
            GenshinVegetationData.Validate(palette, new[] { new GenshinVegetationData.Instance { id = "binding-check",
                prototype = palette[0]?.key, rotation = Quaternion.identity, scale = Vector3.one } });
            if (palette.Any(p => !EditorUtility.IsPersistent(p.mesh) || !EditorUtility.IsPersistent(p.material)))
                throw new InvalidOperationException("Grass palette bindings must be saved project assets.");
            var data = ScriptableObject.CreateInstance<GenshinVegetationData>();
            try
            {
                // An authored layer has zero extracted records. Every brush stroke is stored through the existing local-edit API.
                var serialized = new SerializedObject(data);
                serialized.FindProperty("sourceKey").stringValue = "painted/" + Guid.NewGuid().ToString("N");
                serialized.FindProperty("sourceHash").stringValue = "authored-v1";
                serialized.FindProperty("sourceScene").stringValue = scene;
                var bindings = serialized.FindProperty("prototypes"); bindings.arraySize = palette.Length;
                for (int i = 0; i < palette.Length; i++)
                {
                    var entry = bindings.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("key").stringValue = palette[i].key;
                    entry.FindPropertyRelative("mesh").objectReferenceValue = palette[i].mesh;
                    entry.FindPropertyRelative("material").objectReferenceValue = palette[i].material;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(data, path); AssetDatabase.SaveAssetIfDirty(data);
                return data;
            }
            catch { if (!AssetDatabase.Contains(data)) UnityEngine.Object.DestroyImmediate(data); throw; }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

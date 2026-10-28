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
            public int maskChannel = -1;
        }

        [Serializable]
        internal sealed class Settings
        {
            public float radius = 2;
            public float density = 6;
            public float minimumSpacing = 0.25f;
            public Vector2 scaleRange = new Vector2(0.8f, 1.2f);
            public bool usePenPressure = true;
            public Vector2 pressureHeightRange = new Vector2(0.25f, 1.5f);
            public bool paintTint;
            public bool sampleSurfaceColor = true;
            public Color rootTint = new Color(0.3f, 0.45f, 0.2f, 1);
            public Color tipTint = new Color(0.6f, 0.8f, 0.3f, 1);
            public float tintStrength = 1;
            public float tintPower = 1;
            public float maximumSlope = 40;
            public bool alignToNormal = true;
            public float edgeFalloff = 0.25f;
            public float clusterStrength = 0.3f;
            public float clusterSize = 3;
            public int seed = 7351;
            public List<Weight> weights = new List<Weight>();
            public List<Collider> blockers = new List<Collider>();
            public Texture2D mask;
            public bool maskControlsSpecies;
            public Vector2 maskOrigin;
            public Vector2 maskSize = new Vector2(64, 64);
        }

        internal sealed class Stroke : IDisposable
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
            private readonly int[] maskChannels;
            private readonly float totalWeight;
            private readonly float cellSize;
            private GenshinVegetationSurfaceSampler colorSampler;

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
                if (settings.usePenPressure && (!Finite(settings.pressureHeightRange.x) || !Finite(settings.pressureHeightRange.y) ||
                    settings.pressureHeightRange.x <= 0 || settings.pressureHeightRange.y < settings.pressureHeightRange.x))
                    throw new InvalidOperationException("Pressure height limits must be finite, positive and ordered.");
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
                maskChannels = data.Prototypes.Select((p, i) => {
                    int channel = settings.weights.Find(w => w.key == p.key)?.maskChannel ?? -1;
                    return channel < 0 ? Mathf.Min(i, 3) : channel;
                }).ToArray();
                if (settings.mask != null && settings.maskControlsSpecies && maskChannels.Any(c => c > 3))
                    throw new InvalidOperationException("Distribution channels must be R, G, B or A.");
                if (weights.Any(w => !Finite(w) || w < 0) || !Finite(weights.Sum()) || weights.Sum() <= 0)
                    throw new InvalidOperationException("At least one grass type must have a positive finite weight.");
                totalWeight = weights.Sum();
            }

            internal int Plant(Vector3 center, float pressure = -1)
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
                    Color distribution = Color.white;
                    float availableWeight = totalWeight;
                    if (settings.mask != null)
                    {
                        float u = (local.x - settings.maskOrigin.x) / settings.maskSize.x;
                        float v = (local.z - settings.maskOrigin.y) / settings.maskSize.y;
                        if (u < 0 || u > 1 || v < 0 || v > 1) continue;
                        distribution = settings.mask.GetPixelBilinear(u, v);
                        if (settings.maskControlsSpecies)
                        {
                            availableWeight = 0;
                            float coverage = 0;
                            for (int i = 0; i < weights.Length; i++)
                            {
                                float channel = Mathf.Clamp01(distribution[maskChannels[i]]);
                                availableWeight += weights[i] * channel;
                                if (weights[i] > 0) coverage = Mathf.Max(coverage, channel);
                            }
                            // Coverage controls total density; weighted channels choose the species independently.
                            // A fully red region stays dense even when other palette entries use G/B/A.
                            chance *= coverage;
                        }
                        else chance *= Mathf.Clamp01(distribution.r);
                    }
                    if (availableWeight <= 0 || Random(x, z, 3) >= chance || Nearby(hit.point, Mathf.Max(0.001f, settings.minimumSpacing)).Any()) continue;
                    float pick = Random(x, z, 4) * availableWeight;
                    int kind = 0;
                    for (; kind < weights.Length - 1; kind++)
                    {
                        float weight = weights[kind];
                        if (settings.mask != null && settings.maskControlsSpecies)
                            weight *= Mathf.Clamp01(distribution[maskChannels[kind]]);
                        pick -= weight;
                        if (pick < 0) break;
                    }
                    Vector3 up = worldToLocal.MultiplyVector(settings.alignToNormal ? hit.normal : Vector3.up).normalized;
                    var value = new GenshinVegetationData.Instance { id = "local/" + Guid.NewGuid().ToString("N"),
                        prototype = data.Prototypes[kind].key, position = local,
                        rotation = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.AngleAxis(Random(x, z, 5) * 360, Vector3.up),
                        scale = Vector3.one * Mathf.Lerp(settings.scaleRange.x, settings.scaleRange.y, Random(x, z, 6)) };
                    value.scale.y *= HeightFromPressure(settings, pressure);
                    ApplyTint(ref value, hit);
                    data.SetEdit(value); AddToIndex(value, hit.point); count++;
                }
                return count;
            }

            internal void ApplyTint(ref GenshinVegetationData.Instance value, RaycastHit hit)
            {
                if (!settings.paintTint) return;
                if (!Finite(settings.tintStrength) || settings.tintStrength < 0 || settings.tintStrength > 1 ||
                    !Finite(settings.tintPower) || settings.tintPower <= 0)
                    throw new InvalidOperationException("Tint strength and gradient must be finite and within their ranges.");
                Color root = settings.rootTint;
                if (settings.sampleSurfaceColor)
                {
                    if (colorSampler == null) colorSampler = new GenshinVegetationSurfaceSampler(surface);
                    root = colorSampler.Sample(hit);
                }
                value.rootTint = root;
                value.tipTint = settings.tipTint;
                value.tintStrength = settings.tintStrength;
                value.tintPower = settings.tintPower;
            }

            public void Dispose()
            {
                colorSampler?.Dispose();
                colorSampler = null;
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

        internal static float HeightFromPressure(Settings settings, float pressure)
        {
            // A negative value denotes a mouse event; it must keep the original height even when pen limits change.
            return !settings.usePenPressure || !Finite(pressure) || pressure < 0 ? 1 :
                Mathf.Lerp(settings.pressureHeightRange.x, settings.pressureHeightRange.y, Mathf.Clamp01(pressure));
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    internal static class GenshinRegionImporter
    {
        private const string ManifestPath = "Tools/GenshinRegionManifest.json";

        [MenuItem("Tools/Genshin Region/Build Latest Extraction")]
        private static void BuildLatest()
        {
            var manifest = JObject.Parse(File.ReadAllText(ProjectFile(ManifestPath)));
            var profile = (string)manifest["activeProfile"];
            var folder = ProjectFile((string)manifest["stagingRoot"] + "/" + profile);
            var plan = Directory.GetFiles(folder, "import_plan.json", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal).LastOrDefault();
            if (plan == null)
                throw new InvalidOperationException("Run Tools/ExtractGenshinRegion.py --mode all first.");
            Debug.Log("Region scene saved: " + BuildFromPlan(plan));
        }

        private static string BuildFromPlan(string planPath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Build the region while the Editor is idle.");
            var plan = JObject.Parse(File.ReadAllText(planPath));
            if ((int)plan["schemaVersion"] != 1)
                throw new InvalidDataException("Unsupported region plan version.");
            var profile = (JObject)plan["profile"];
            var files = ((JArray)plan["files"]).Cast<JObject>().ToDictionary(file => (string)file["name"]);
            foreach (var file in files.Values)
            {
                var assetPath = (string)file["assetPath"];
                if (!assetPath.StartsWith("Assets/Art/Model/", StringComparison.Ordinal) ||
                    HashFile(ProjectFile(assetPath)) != (string)file["sha256"])
                    throw new InvalidDataException("Project asset differs from its audited export: " + assetPath);
            }
            foreach (var key in new[] { "assetRoot", "materialRoot", "sceneRoot" })
                if (!AssetDatabase.IsValidFolder((string)plan[key]))
                    throw new DirectoryNotFoundException((string)plan[key]);
            foreach (var file in files.Values)
                ConfigureImport(file);

            var original = SceneManager.GetActiveScene();
            var originalDirty = original.isDirty;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var root = new GameObject("RecoveredRegion_" + (string)profile["id"]);
                var size = (float)profile["blockSize"];
                foreach (JObject entry in (JArray)profile["meshes"])
                    InstantiatePart(root.transform, entry, files, plan, size);
                Physics.SyncTransforms();
                var validation = ValidateSurface(root, size);
                validation["minimumCoverage"] = (float)profile["minimumSurfaceCoverage"];
                validation["passed"] = (float)validation["coverage"] >= (float)validation["minimumCoverage"];
                CreateView(root);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.58f, 0.64f, 0.72f);
                RenderSettings.fog = false;
                RenderSettings.skybox = null;

                // Rebuilds create a new scene instead of overwriting a scene the
                // user may have edited after an earlier import.
                var scenePath = AssetDatabase.GenerateUniqueAssetPath((string)plan["sceneRoot"] + "/GenshinRegion_" + (string)profile["id"] + ".unity");
                if (!EditorSceneManager.SaveScene(scene, scenePath))
                    throw new IOException("Could not save the generated region scene.");
                var report = new JObject
                {
                    ["scene"] = scenePath,
                    ["profile"] = profile.DeepClone(),
                    ["surface"] = validation,
                    ["meshFilterCount"] = root.GetComponentsInChildren<MeshFilter>().Length,
                    ["colliderCount"] = root.GetComponentsInChildren<MeshCollider>().Length,
                    ["triangleCount"] = root.GetComponentsInChildren<MeshFilter>().Sum(filter => filter.sharedMesh.triangles.Length / 3),
                    ["originalScene"] = original.path,
                    ["originalDirtyBefore"] = originalDirty,
                    ["originalDirtyAfter"] = original.isDirty,
                    ["addedToBuildSettings"] = false
                };
                var reportPath = Path.Combine(Path.GetDirectoryName(planPath), "unity_build_" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ") + ".json");
                File.WriteAllText(reportPath, report.ToString(Formatting.Indented));
                Debug.Log("Region validation report: " + reportPath);
                return scenePath;
            }
            finally
            {
                // The active UI scene may contain unsaved work. Only the newly
                // generated scene is saved and closed by this workflow.
                SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void ConfigureImport(JObject file)
        {
            var path = (string)file["assetPath"];
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if ((string)file["kind"] == "Mesh")
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                if (!importer.isReadable || importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.globalScale != 1f)
                {
                    importer.isReadable = true;
                    importer.materialImportMode = ModelImporterMaterialImportMode.None;
                    importer.globalScale = 1f;
                    importer.SaveAndReimport();
                }
            }
            else
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer.textureType != TextureImporterType.Default || !importer.sRGBTexture ||
                    importer.wrapMode != TextureWrapMode.Clamp || importer.maxTextureSize < 4096)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = true;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.maxTextureSize = 4096;
                    importer.SaveAndReimport();
                }
            }
        }

        private static void InstantiatePart(Transform parent, JObject entry, Dictionary<string, JObject> files, JObject plan, float blockSize)
        {
            var source = files[(string)entry["name"]];
            var textureSource = files[(string)entry["texture"]];
            var model = AssetDatabase.LoadAssetAtPath<GameObject>((string)source["assetPath"]);
            var instance = Object.Instantiate(model, parent);
            instance.name = (string)entry["role"] + "_" + (string)entry["name"];
            var position = (JArray)entry["position"];
            instance.transform.localPosition = new Vector3((float)position[0], (float)position[1], (float)position[2]);
            var materialPath = (string)plan["materialRoot"] + "/GenshinRegion_" + (string)plan["profile"]["id"] + "_" +
                               (string)entry["role"] + "_" + ((string)textureSource["sha256"]).Substring(0, 8) + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>((string)textureSource["assetPath"]);
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Glossiness", 0.05f);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            var filters = instance.GetComponentsInChildren<MeshFilter>();
            var overrides = (entry["meshOverrides"] as JArray)?.Cast<JObject>().ToArray() ?? Array.Empty<JObject>();
            foreach (var part in overrides)
                if (!filters.Any(filter => filter.name == (string)part["meshName"]))
                    throw new InvalidDataException("Configured mesh override was not found: " + (string)part["meshName"]);
            for (var index = 0; index < filters.Length; index++)
            {
                var filter = filters[index];
                if ((string)entry["uvMode"] == "blockPlanar")
                {
                    var meshPath = (string)plan["assetRoot"] + "/GenshinRegion_" + (string)plan["profile"]["id"] +
                                   "_Terrain_" + ((string)source["sha256"]).Substring(0, 8) + "_" + index + ".asset";
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (mesh == null)
                    {
                        mesh = Object.Instantiate(filter.sharedMesh);
                        mesh.name = "RegionTerrain_" + index;
                        var vertices = mesh.vertices;
                        var uv = new Vector2[vertices.Length];
                        for (var vertex = 0; vertex < vertices.Length; vertex++)
                        {
                            // HLOD UVs do not map the block BaseMap. Derive UVs
                            // after Unity has restored the OBJ coordinate axes.
                            var local = instance.transform.InverseTransformPoint(filter.transform.TransformPoint(vertices[vertex]));
                            uv[vertex] = new Vector2(local.x / blockSize, local.z / blockSize);
                        }
                        mesh.uv = uv;
                        AssetDatabase.CreateAsset(mesh, meshPath);
                    }
                    filter.sharedMesh = mesh;
                }
                var partOverride = overrides.SingleOrDefault(part => (string)part["meshName"] == filter.name);
                var partMaterial = material;
                var enableCollider = true;
                if (partOverride != null)
                {
                    var points = filter.sharedMesh.vertices;
                    var triangles = filter.sharedMesh.triangles;
                    if ((bool?)partOverride["requireHorizontal"] == true)
                        for (var triangle = 0; triangle < triangles.Length; triangle += 3)
                            if (Mathf.Abs(points[triangles[triangle]].y - points[triangles[triangle + 1]].y) > 0.005f ||
                                Mathf.Abs(points[triangles[triangle]].y - points[triangles[triangle + 2]].y) > 0.005f)
                                throw new InvalidDataException("Water override contains a nonhorizontal triangle: " + filter.name);
                    var color = (JArray)partOverride["color"];
                    var colorKey = string.Join("_", color.Values<float>().Select(value => Mathf.RoundToInt(value * 10000f)));
                    var overridePath = (string)plan["materialRoot"] + "/GenshinRegion_" + (string)plan["profile"]["id"] +
                                       "_" + (string)partOverride["role"] + "_" + colorKey + ".mat";
                    partMaterial = AssetDatabase.LoadAssetAtPath<Material>(overridePath);
                    if (partMaterial == null)
                    {
                        partMaterial = new Material(Shader.Find("Standard"));
                        partMaterial.name = "Region" + (string)partOverride["role"];
                        partMaterial.color = new Color((float)color[0], (float)color[1], (float)color[2], (float)color[3]);
                        partMaterial.SetFloat("_Glossiness", 0.8f);
                        partMaterial.SetFloat("_Metallic", 0f);
                        AssetDatabase.CreateAsset(partMaterial, overridePath);
                    }
                    // Water comes from an existing source submesh. Only its
                    // material changes; it must not become a walkable floor.
                    enableCollider = (bool?)partOverride["collider"] ?? true;
                    filter.gameObject.name = (string)partOverride["role"] + "_" + filter.name;
                }
                var renderer = filter.GetComponent<MeshRenderer>();
                renderer.sharedMaterials = Enumerable.Repeat(partMaterial, filter.sharedMesh.subMeshCount).ToArray();
                if (enableCollider)
                {
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                }
            }
        }

        private static JObject ValidateSurface(GameObject root, float size)
        {
            var colliders = root.GetComponentsInChildren<MeshCollider>();
            var bounds = GetBounds(root);
            var misses = new JArray();
            var hits = 0;
            const int resolution = 41;
            for (var x = 0; x < resolution; x++)
            for (var z = 0; z < resolution; z++)
            {
                var px = Mathf.Lerp(1f, size - 1f, x / (float)(resolution - 1));
                var pz = Mathf.Lerp(1f, size - 1f, z / (float)(resolution - 1));
                var ray = new Ray(new Vector3(px, bounds.max.y + 100f, pz), Vector3.down);
                var found = false;
                foreach (var collider in colliders)
                {
                    RaycastHit hit;
                    if (!collider.Raycast(ray, out hit, bounds.size.y + 200f))
                        continue;
                    found = true;
                    break;
                }
                if (found) hits++; else misses.Add(new JArray(px, pz));
            }
            return new JObject
            {
                ["samples"] = resolution * resolution,
                ["hits"] = hits,
                ["coverage"] = hits / (float)(resolution * resolution),
                ["misses"] = misses,
                ["boundsMin"] = new JArray(bounds.min.x, bounds.min.y, bounds.min.z),
                ["boundsMax"] = new JArray(bounds.max.x, bounds.max.y, bounds.max.z),
                ["meaning"] = "Vertical surface coverage, not a character movement or navigation test."
            };
        }

        private static Bounds GetBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0)
                throw new InvalidDataException("Region has no rendered meshes.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static void CreateView(GameObject root)
        {
            var bounds = GetBounds(root);
            var light = new GameObject("RegionSun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            var camera = new GameObject("RegionOverviewCamera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.aspect = 1.6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.45f, 0.65f, 0.8f);
            camera.nearClipPlane = 1f;
            camera.farClipPlane = 10000f;
            camera.transform.position = bounds.center + new Vector3(-900f, 900f, -1100f);
            camera.transform.LookAt(bounds.center);
            float horizontal = 0f, vertical = 0f;
            for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
            for (var z = -1; z <= 1; z += 2)
            {
                var corner = Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                horizontal = Mathf.Max(horizontal, Mathf.Abs(Vector3.Dot(corner, camera.transform.right)));
                vertical = Mathf.Max(vertical, Mathf.Abs(Vector3.Dot(corner, camera.transform.up)));
            }
            camera.orthographicSize = Mathf.Max(vertical, horizontal / camera.aspect) * 1.05f;
        }

        private static string ProjectFile(string relative)
        {
            var root = Directory.GetParent(Application.dataPath).FullName;
            var path = Path.GetFullPath(Path.Combine(root, relative));
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Path leaves the Unity project: " + relative);
            return path;
        }

        private static string HashFile(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}

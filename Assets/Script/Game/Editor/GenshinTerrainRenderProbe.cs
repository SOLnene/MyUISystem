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
    internal static class GenshinTerrainRenderProbe
    {
        private const string Run = ".codex_tmp_frame_analysis/genshin_terrain_render_20261021";
        private const string SourceScene = "Assets/Scenes/GenshinTestArchive/GenshinRegion_mondstadt_2_-1 1.unity";
        private const string ShaderPath = "Assets/Shader/GenshinStylizedTerrain.shader";
        private const string ModelRoot = "Assets/Art/Map/Genshin/Mondstadt/Terrain/Meshes/";
        private const string TextureRoot = "Assets/Art/Map/Genshin/Mondstadt/Terrain/Textures/";
        private const string MaterialRoot = "Assets/Art/Map/Genshin/Mondstadt/Materials/";

        [MenuItem("Tools/Genshin Region/Build Stylized Terrain Probe")]
        private static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Build the terrain probe while the Editor is idle.");
            AssetDatabase.ImportAsset(ShaderPath, ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            RequireShader(shader);
            var bindings = JObject.Parse(File.ReadAllText(Path.Combine(Run, "texture_bindings.json")));
            var texturePaths = new Dictionary<string, string>();
            foreach (JArray record in (JArray)bindings["textures"])
            {
                var name = (string)record[0];
                var path = TextureRoot + name + ".png";
                CopyVerified(Path.Combine(Run, "textures/Texture2D", name + ".png"), path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var atlas = name.Contains("TerrainMesh");
                var packed = name.EndsWith("_Normal", StringComparison.Ordinal);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = !atlas && !packed;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false;
                importer.wrapMode = atlas ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                importer.mipmapEnabled = !atlas;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                texturePaths.Add(record[3].ToString(), path);
            }
            var sourceMaterials = new List<Material>();
            var materialPaths = new List<string>();
            foreach (var path in Directory.GetFiles(Path.Combine(Run, "materials/Material"), "*.json").OrderBy(p => p, StringComparer.Ordinal))
            {
                var source = JObject.Parse(File.ReadAllText(path));
                var material = new Material(shader) { name = "GenshinLayers_" + (string)source["m_Name"] };
                var properties = (JObject)source["m_SavedProperties"];
                foreach (var property in ((JObject)properties["m_TexEnvs"]).Properties())
                {
                    var reference = property.Value["m_Texture"];
                    if ((bool)reference["IsNull"] || !material.HasProperty(property.Name))
                        continue;
                    material.SetTexture(property.Name, AssetDatabase.LoadAssetAtPath<Texture2D>(texturePaths[reference["m_PathID"].ToString()]));
                }
                foreach (var property in ((JObject)properties["m_Floats"]).Properties())
                    if (material.HasProperty(property.Name))
                        material.SetFloat(property.Name, (float)property.Value);
                material.SetFloat("_LayerMode", 1f);
                var target = AssetDatabase.GenerateUniqueAssetPath(MaterialRoot + material.name + ".mat");
                AssetDatabase.CreateAsset(material, target);
                sourceMaterials.Add(material);
                materialPaths.Add(target);
            }
            var hlod = new Material(sourceMaterials[0]) { name = "GenshinStylized_mondstadt_2_-1" };
            hlod.SetFloat("_LayerMode", 0f);
            hlod.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureRoot + "BigWorldTerrain_2_-1.bin_BaseMap.png"));
            var hlodPath = AssetDatabase.GenerateUniqueAssetPath(MaterialRoot + hlod.name + ".mat");
            AssetDatabase.CreateAsset(hlod, hlodPath);
            materialPaths.Add(hlodPath);
            AssetDatabase.SaveAssets();

            var original = SceneManager.GetActiveScene();
            var originalDirty = original.isDirty;
            var originalRootCount = original.rootCount;
            var sourceHash = Hash(SourceScene);
            var scene = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var renderers = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MeshRenderer>()).ToArray();
                var terrain = renderers.Where(renderer => renderer.name.StartsWith("Stages_BigWorld_2_-1_Constant_HLOD_Lod0_", StringComparison.Ordinal)).ToArray();
                if (terrain.Length != 2)
                    throw new InvalidDataException("Expected the two audited HLOD terrain renderers.");
                foreach (var renderer in terrain)
                    renderer.sharedMaterial = hlod;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.66f, 0.73f, 0.8f);
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = 1200f;
                RenderSettings.fogEndDistance = 8000f;
                RenderSettings.fogColor = new Color(0.65f, 0.79f, 0.87f);
                var camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>()).Single();
                camera.backgroundColor = RenderSettings.fogColor;
                var sun = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Light>()).Single(light => light.type == LightType.Directional);
                sun.shadows = LightShadows.Soft;
                var scenePath = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/GenshinTerrainStylized_mondstadt_2_-1.unity");
                if (!EditorSceneManager.SaveScene(scene, scenePath))
                    throw new IOException("Could not save the terrain rendering probe.");
                var screenshots = Capture(scene, "stylized");
                RequireShader(shader);
                var layerSampleScene = BuildLayerSample(sourceMaterials[0]);
                var report = new
                {
                    scene = scenePath,
                    layerSampleScene,
                    sourceScene = SourceScene,
                    sourceSceneHashUnchanged = sourceHash == Hash(SourceScene),
                    originalScene = original.path,
                    originalDirtyBefore = originalDirty,
                    originalDirtyAfter = original.isDirty,
                    originalRootsBefore = originalRootCount,
                    originalRootsAfter = original.rootCount,
                    originalShaderName = "miHoYo/Terrain/Terrain Mesh BlendPass",
                    rawShaderBytes = new FileInfo(Path.Combine(Run, "terrain_shader_serialized.dat")).Length,
                    decompiledPrograms = 166,
                    shaderSupported = shader.isSupported,
                    shaderMessages = ShaderUtil.GetShaderMessages(shader).Select(message => new { message.message, severity = message.severity.ToString() }).ToArray(),
                    recoveredMaterialTemplates = sourceMaterials.Count,
                    materials = materialPaths,
                    styledTerrainRenderers = terrain.Length,
                    meshCount = renderers.Length,
                    triangleCount = renderers.Sum(renderer => renderer.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3),
                    screenshots,
                    limitations = "Port of recovered layer blending with replacement Built-in lighting. The region uses its verified HLOD BaseMap; near-mesh atlas assignments and placement remain unverified."
                };
                File.WriteAllText(Path.Combine(Run, "unity_render_audit.json"), JsonConvert.SerializeObject(report, Formatting.Indented));
                Debug.Log("Stylized terrain probe saved: " + scenePath);
            }
            finally
            {
                // The active UI scene can contain unsaved user work. Save and close
                // only this independently opened copy of the recovered region.
                SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static string BuildLayerSample(Material material)
        {
            const string name = "BigWorld_2_-1_TerrainMesh_DM_0_Lod0";
            var meshPath = ModelRoot + name + ".obj";
            CopyVerified(Path.Combine(Run, "meshes/Mesh", name + ".obj"), meshPath);
            AssetDatabase.ImportAsset(meshPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(meshPath);
            importer.isReadable = true;
            importer.globalScale = 1f;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
            var original = SceneManager.GetActiveScene();
            var dirty = original.isDirty;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(meshPath));
                foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
                    renderer.sharedMaterial = material;
                var bounds = model.GetComponentInChildren<MeshRenderer>().bounds;
                var sun = new GameObject("SampleSun").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
                sun.color = new Color(1f, 0.96f, 0.88f);
                sun.intensity = 1.15f;
                sun.shadows = LightShadows.Soft;
                var camera = new GameObject("LayerSampleCamera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.scene = scene;
                camera.fieldOfView = 52f;
                camera.aspect = 1.6f;
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 1000f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.65f, 0.79f, 0.87f);
                camera.transform.position = bounds.center + new Vector3(-75f, 68f, -95f);
                camera.transform.LookAt(bounds.center);
                RenderSettings.skybox = null;
                RenderSettings.fog = false;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.66f, 0.73f, 0.8f);
                var scenePath = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/GenshinTerrainLayerSample_mondstadt_2_-1.unity");
                if (!EditorSceneManager.SaveScene(scene, scenePath))
                    throw new IOException("Could not save the terrain layer sample.");
                Capture(scene, "original_layers", true);
                RequireShader(material.shader);
                File.WriteAllText(Path.Combine(Run, "layer_sample_audit.json"), JsonConvert.SerializeObject(new
                {
                    scene = scenePath, mesh = meshPath, material = AssetDatabase.GetAssetPath(material),
                    originalLayerMode = material.GetFloat("_LayerMode"),
                    triangles = model.GetComponentInChildren<MeshFilter>().sharedMesh.triangles.Length / 3,
                    shaderMessages = ShaderUtil.GetShaderMessages(material.shader).Select(message => message.message).ToArray(),
                    originalDirtyBefore = dirty, originalDirtyAfter = original.isDirty,
                    limitation = "Material evaluation sample at local origin; original world placement and renderer-material association remain unverified."
                }, Formatting.Indented));
                return scenePath;
            }
            finally
            {
                SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static string[] Capture(Scene source, string prefix, bool layerSample = false)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (var root in source.GetRootGameObjects())
                    SceneManager.MoveGameObjectToScene(Object.Instantiate(root), preview);
                var camera = preview.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>()).Single();
                camera.scene = preview;
                camera.enabled = false;
                if (layerSample)
                {
                    var bounds = preview.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MeshRenderer>()).Single().bounds;
                    var whole = Path.Combine(Run, prefix + "_close.png");
                    Render(camera, whole);
                    camera.transform.position = bounds.center + new Vector3(0f, 21f, -40f);
                    camera.transform.LookAt(bounds.center + new Vector3(0f, 3f, 20f));
                    var detail = Path.Combine(Run, prefix + "_detail.png");
                    Render(camera, detail);
                    return new[] { whole, detail };
                }
                var overview = Path.Combine(Run, prefix + "_overview.png");
                Render(camera, overview);
                camera.orthographic = false;
                camera.fieldOfView = 52f;
                camera.nearClipPlane = 0.3f;
                camera.transform.position = new Vector3(670f, 291f, 445f);
                camera.transform.LookAt(new Vector3(725f, 237f, 560f));
                var close = Path.Combine(Run, prefix + "_terrain_close.png");
                Render(camera, close);
                return new[] { overview, close };
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void Render(Camera camera, string path)
        {
            var target = RenderTexture.GetTemporary(1280, 800, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(image);
            }
        }

        private static void RequireShader(Shader shader)
        {
            if (shader == null || !shader.isSupported || ShaderUtil.GetShaderMessages(shader).Any(message => message.severity.ToString() == "Error"))
                throw new InvalidOperationException("The stylized terrain Shader did not compile successfully.");
        }

        private static void CopyVerified(string source, string destination)
        {
            if (File.Exists(destination))
            {
                if (Hash(source) != Hash(destination))
                    throw new IOException("Existing resource differs from extraction: " + destination);
                return;
            }
            File.Copy(source, destination);
        }

        private static string Hash(string path)
        {
            using (var algorithm = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "");
        }
    }
}

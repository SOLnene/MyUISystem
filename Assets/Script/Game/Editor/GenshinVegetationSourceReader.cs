using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Game.Vegetation;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    internal static class GenshinVegetationSourceReader
    {
        internal sealed class Document
        {
            internal string path;
            internal string key;
            internal string hash;
            internal string scene;
            internal Vector3 origin;
            internal bool decoded;
            internal int declaredCount;
            internal string summary;
            internal GenshinVegetationData.Prototype[] prototypes = Array.Empty<GenshinVegetationData.Prototype>();
            internal GenshinVegetationData.Instance[] instances = Array.Empty<GenshinVegetationData.Instance>();
        }

        internal static Document Load(string path)
        {
            path = ProjectFile(path);
            byte[] bytes = File.ReadAllBytes(path);
            // Reject ambiguous text encodings instead of silently rewriting source evidence.
            string text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
            var root = JObject.Parse(text);
            var document = new Document { path = path, hash = Hash(bytes) };
            if (root["cells"] is JArray cells) return ReadNativeReport(document, cells);
            if (root["meshes"] is JArray && root["material"] is JObject)
                ReadStreamPlan(document, root);
            else
                ReadDecodedPlan(document, root);
            ValidateRecords(document);
            document.summary = "已解码布局：" + document.instances.Length + " 个实例，" + document.prototypes.Length + " 种植被。";
            return document;
        }

        private static void ReadStreamPlan(Document document, JObject root)
        {
            document.scene = Required(root, "scene");
            document.key = "streamgen/" + document.scene + "/" + Required(root, "rootName");
            document.origin = Vector(root["worldOrigin"], "worldOrigin");
            Material material = ProjectAsset<Material>(Required(root["material"], "path"));
            document.prototypes = ((JArray)root["meshes"]).Select(p => new GenshinVegetationData.Prototype {
                key = Required(p, "name"), mesh = ProjectAsset<Mesh>(Required(p, "path")), material = material
            }).ToArray();
            document.instances = ((JArray)root["instances"]).Select(p => new GenshinVegetationData.Instance {
                id = document.key + "/" + Required(p, "stream") + "/" + Required(p, "instance"),
                prototype = Required(p, "name"), position = Vector(p["position"], "position") - document.origin,
                rotation = Quaternion.Euler(Vector(p["rotation"], "rotation")), scale = Vector(p["scale"], "scale")
            }).ToArray();
            document.decoded = true;
            document.declaredCount = document.instances.Length;
        }

        private static void ReadDecodedPlan(Document document, JObject root)
        {
            if ((int?)root["schemaVersion"] != 1 || (string)root["decodeStatus"] != "verified")
                throw new InvalidDataException("Expected schemaVersion=1 and decodeStatus=verified. Packed or guessed transforms cannot be imported.");
            document.key = Required(root, "sourceKey");
            document.scene = Required(root, "scene");
            document.origin = Vector(root["worldOrigin"], "worldOrigin");
            string space = Required(root, "coordinateSpace");
            if (space != "sourceWorld" && space != "project")
                throw new InvalidDataException("coordinateSpace must be sourceWorld or project.");
            document.prototypes = ((JArray)root["prototypes"]).Select(p => new GenshinVegetationData.Prototype {
                key = Required(p, "key"), mesh = ProjectAsset<Mesh>(Required(p, "mesh")),
                material = ProjectAsset<Material>(Required(p, "material"))
            }).ToArray();
            document.instances = ((JArray)root["instances"]).Select(p => new GenshinVegetationData.Instance {
                id = document.key + "/" + Required(p, "id"), prototype = Required(p, "prototype"),
                position = Vector(p["position"], "position") - (space == "sourceWorld" ? document.origin : Vector3.zero),
                rotation = Rotation(p["rotation"]), scale = Vector(p["scale"], "scale")
            }).ToArray();
            document.decoded = true;
            document.declaredCount = document.instances.Length;
        }

        private static void ValidateRecords(Document document)
        {
            if (document.instances.Length == 0 || document.prototypes.Length == 0)
                throw new InvalidDataException("The source contains no decoded vegetation.");
            if (!document.scene.StartsWith("Assets/", StringComparison.Ordinal) ||
                !document.scene.EndsWith(".unity", StringComparison.Ordinal) || document.scene.Contains(".."))
                throw new InvalidDataException("A project scene path is required.");
            var keys = new HashSet<string>();
            foreach (var prototype in document.prototypes)
                if (!keys.Add(prototype.key)) throw new InvalidDataException("Duplicate prototype key: " + prototype.key);
            var ids = new HashSet<string>();
            foreach (var instance in document.instances)
            {
                GenshinVegetationData.ValidateInstance(instance);
                if (!ids.Add(instance.id) || !keys.Contains(instance.prototype))
                    throw new InvalidDataException("Duplicate source ID or unresolved grass type: " + instance.id);
            }
        }

        private static Document ReadNativeReport(Document document, JArray cells)
        {
            var lines = new List<string>();
            if (cells.Count == 0) throw new InvalidDataException("Native report contains no cells.");
            foreach (JObject cell in cells)
            {
                byte[] raw = File.ReadAllBytes(ProjectFile(Required(cell["source"], "rawPath")));
                using (var stream = new MemoryStream(raw, false))
                using (var reader = new BinaryReader(stream, new UTF8Encoding(false, true)))
                {
                    int length = reader.ReadInt32();
                    if (length < 1 || length > 512 || length > stream.Length - 4)
                        throw new InvalidDataException("Invalid native grass name length.");
                    string name = new UTF8Encoding(false, true).GetString(reader.ReadBytes(length));
                    stream.Position = (stream.Position + 3) & ~3L;
                    int version = reader.ReadInt32();
                    int x = reader.ReadInt32();
                    int z = reader.ReadInt32();
                    int declared = reader.ReadInt32();
                    reader.ReadInt32();
                    int palette = reader.ReadInt32();
                    if (version != 1 || declared < 1 || declared > 2000000 || palette < 0 || palette > declared)
                        throw new InvalidDataException("Unsupported native grass header: " + name);
                    stream.Position += palette * 8L;
                    if (stream.Position > stream.Length - 4) throw new InvalidDataException("Truncated grass palette.");
                    int count = reader.ReadInt32();
                    long profileOffset = stream.Position + count * 28L;
                    if (count != declared || profileOffset > stream.Length - 4 ||
                        name != (string)cell["name"] || count != (int)cell["recordArrayCount"] ||
                        profileOffset != (long)cell["profileTableOffset"])
                        throw new InvalidDataException("Native record boundary differs from the audited report: " + name);
                    stream.Position = profileOffset;
                    int profiles = reader.ReadInt32();
                    if (profiles != (int)cell["profileTableCount"])
                        throw new InvalidDataException("Native grass profile count mismatch.");
                    foreach (JObject profile in (JArray)cell["profiles"])
                    {
                        long offset = (long)profile["offset"];
                        if (offset < profileOffset + 4 || offset > stream.Length - 12)
                            throw new InvalidDataException("Profile reference outside native data.");
                        stream.Position = offset;
                        if (reader.ReadInt32() != (int)profile["fileId"] ||
                            reader.ReadInt64().ToString() != (string)profile["pathId"])
                            throw new InvalidDataException("Native grass profile pointer mismatch.");
                    }
                    document.declaredCount += count;
                    lines.Add(name + "：" + count + " 条，原点 (" + x + ", " + z + ")，" + profiles + " 种配置；二进制边界通过。");
                }
            }
            document.key = "native-grass/" + string.Join("/", cells.Select(c => (string)c["source"]["cab"]));
            document.decoded = false;
            document.summary = string.Join("\n", lines) + "\n压缩位置、旋转、缩放和逐实例草种尚未验证。仅供检查，禁止生成猜测布局。";
            return document;
        }

        internal static GenshinVegetationData Import(Document document, string assetPath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !document.decoded)
                throw new InvalidOperationException("Only verified decoded layouts can be imported while the Editor is idle.");
            assetPath = MapAssetPath(assetPath);
            if (!assetPath.EndsWith(".asset", StringComparison.Ordinal) ||
                !AssetDatabase.IsValidFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/')))
                throw new InvalidDataException("Choose an existing map asset folder and an .asset filename.");
            GenshinVegetationData.Validate(document.prototypes, document.instances);
            foreach (var prototype in document.prototypes)
            {
                if (!EditorUtility.IsPersistent(prototype.mesh) || !EditorUtility.IsPersistent(prototype.material))
                    throw new InvalidDataException("Save Mesh and Material bindings as project assets before importing.");
                MapAssetPath(AssetDatabase.GetAssetPath(prototype.mesh));
                MapAssetPath(AssetDatabase.GetAssetPath(prototype.material));
            }
            var asset = AssetDatabase.LoadAssetAtPath<GenshinVegetationData>(assetPath);
            if (asset == null && File.Exists(ProjectFile(assetPath)))
                throw new InvalidOperationException("The target is an existing asset of another type.");
            bool created = asset == null;
            if (created) asset = ScriptableObject.CreateInstance<GenshinVegetationData>();
            try
            {
                if (!created) Undo.RecordObject(asset, "Reimport vegetation source");
                asset.ReplaceSource(document.key, document.hash, document.scene, document.origin,
                    document.prototypes, document.instances);
                if (created) AssetDatabase.CreateAsset(asset, assetPath);
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
                return asset;
            }
            catch
            {
                if (created && !AssetDatabase.Contains(asset)) UnityEngine.Object.DestroyImmediate(asset);
                throw;
            }
        }

        internal static string ProjectFile(string path)
        {
            return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(Directory.GetParent(Application.dataPath).FullName, path));
        }

        private static T ProjectAsset<T>(string path) where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(MapAssetPath(path));
        }

        private static string MapAssetPath(string path)
        {
            path = path.Replace('\\', '/');
            if (!path.StartsWith("Assets/Art/Map/Genshin/", StringComparison.Ordinal) || path.Contains(".."))
                throw new InvalidDataException("Vegetation resources must reference project map assets: " + path);
            return path;
        }

        private static string Required(JToken token, string key)
        {
            string value = (string)token?[key];
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidDataException("Missing field: " + key);
            return value;
        }

        private static Vector3 Vector(JToken token, string field)
        {
            if (!(token is JArray values) || values.Count != 3) throw new InvalidDataException("Expected three values for " + field);
            return new Vector3((float)values[0], (float)values[1], (float)values[2]);
        }

        private static Quaternion Rotation(JToken token)
        {
            if (!(token is JArray values) || values.Count != 4) throw new InvalidDataException("Expected quaternion rotation [x,y,z,w].");
            return new Quaternion((float)values[0], (float)values[1], (float)values[2], (float)values[3]);
        }

        private static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}

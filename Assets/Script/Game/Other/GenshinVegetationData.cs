using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Vegetation
{
    public sealed class GenshinVegetationData : ScriptableObject
    {
        [Serializable]
        public sealed class Prototype
        {
            public string key;
            public Mesh mesh;
            public Material material;
        }

        [Serializable]
        public struct Instance
        {
            public string id;
            public string prototype;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
            public Color rootTint;
            public Color tipTint;
            // Zero keeps assets authored before per-instance coloring on the material's original appearance.
            public float tintStrength;
            public float tintPower;
        }

        [Serializable]
        public struct Edit
        {
            public Instance value;
            public bool added;
            public bool deleted;
        }

        [SerializeField] private string sourceKey;
        [SerializeField] private string sourceHash;
        [SerializeField] private string sourceScene;
        [SerializeField] private Vector3 worldOrigin;
        [SerializeField] private Prototype[] prototypes = Array.Empty<Prototype>();
        [SerializeField] private Instance[] sourceInstances = Array.Empty<Instance>();
        [SerializeField] private List<Edit> edits = new List<Edit>();

        public string SourceKey => sourceKey;
        public string SourceHash => sourceHash;
        public string SourceScene => sourceScene;
        public Vector3 WorldOrigin => worldOrigin;
        public Prototype[] Prototypes => prototypes;
        public Instance[] SourceInstances => sourceInstances;
        public int EditCount => edits.Count;
        public int Revision { get; private set; }
        public Edit[] GetEdits() { return edits.ToArray(); }

        public void ReplaceSource(string key, string hash, string scene, Vector3 origin,
            Prototype[] bindings, Instance[] instances)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(hash) || !Finite(origin))
                throw new InvalidOperationException("Source identity, hash and finite map origin are required.");
            if (!string.IsNullOrEmpty(sourceKey) && sourceKey != key)
                throw new InvalidOperationException("Choose a new asset for a different vegetation source.");
            Validate(bindings, instances);
            var prototypeKeys = new HashSet<string>();
            foreach (Prototype binding in bindings) prototypeKeys.Add(binding.key);
            foreach (Edit edit in edits)
                if (!prototypeKeys.Contains(edit.value.prototype))
                    throw new InvalidOperationException("An existing edit references a removed prototype: " + edit.value.prototype);
            // Source IDs survive reimports; manual edits are never baked back into the source array.
            sourceKey = key;
            sourceHash = hash;
            sourceScene = scene;
            worldOrigin = origin;
            // The editor document remains editable before reimport; it must not share mutable bindings with this asset.
            prototypes = Array.ConvertAll(bindings, binding => new Prototype {
                key = binding.key, mesh = binding.mesh, material = binding.material });
            sourceInstances = (Instance[])instances.Clone();
            NotifyChanged();
        }

        public Instance[] GetInstances()
        {
            var changes = new Dictionary<string, Edit>(StringComparer.Ordinal);
            foreach (Edit edit in edits) changes[edit.value.id] = edit;
            var result = new List<Instance>(sourceInstances.Length + edits.Count);
            foreach (Instance source in sourceInstances)
            {
                if (!changes.TryGetValue(source.id, out Edit edit)) result.Add(source);
                else if (!edit.deleted) result.Add(edit.value);
            }
            foreach (Edit edit in edits)
                if (edit.added && !edit.deleted) result.Add(edit.value);
            return result.ToArray();
        }

        public int OrphanedEditCount()
        {
            var ids = new HashSet<string>();
            foreach (Instance source in sourceInstances) ids.Add(source.id);
            int count = 0;
            foreach (Edit edit in edits) if (!edit.added && !ids.Contains(edit.value.id)) count++;
            return count;
        }

        public void SetEdit(Instance value, bool deleted = false)
        {
            ValidateInstance(value);
            if (Array.FindIndex(prototypes, p => p.key == value.prototype) < 0)
                throw new InvalidOperationException("Unknown vegetation prototype: " + value.prototype);
            int existing = edits.FindIndex(e => e.value.id == value.id);
            bool added = existing >= 0 ? edits[existing].added :
                Array.FindIndex(sourceInstances, i => i.id == value.id) < 0;
            if (added && !value.id.StartsWith("local/", StringComparison.Ordinal))
                throw new InvalidOperationException("New instances require a local/ identity.");
            var change = new Edit { value = value, added = added, deleted = deleted };
            if (existing < 0) edits.Add(change);
            else edits[existing] = change;
            NotifyChanged();
        }

        public void Restore(string id)
        {
            edits.RemoveAll(e => e.value.id == id);
            NotifyChanged();
        }

        public void NotifyChanged() { Revision++; }
        private void OnValidate() { NotifyChanged(); }

        public static void Validate(Prototype[] bindings, Instance[] instances)
        {
            if (bindings == null || instances == null || instances.Length == 0)
                throw new InvalidOperationException("A decoded source must contain prototypes and instances.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (Prototype binding in bindings)
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.key) || !keys.Add(binding.key))
                    throw new InvalidOperationException("Prototype identities must be nonempty and unique.");
                if (binding.mesh == null || binding.material == null)
                    throw new InvalidOperationException("Bind a project Mesh and Material for " + binding.key);
                if (binding.mesh.subMeshCount != 1 || !binding.material.enableInstancing)
                    throw new InvalidOperationException("This importer requires one submesh and an instanced material: " + binding.key);
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (Instance instance in instances)
            {
                ValidateInstance(instance);
                if (!ids.Add(instance.id) || !keys.Contains(instance.prototype))
                    throw new InvalidOperationException("Duplicate instance ID or unresolved prototype: " + instance.id);
            }
        }

        public static void ValidateInstance(Instance instance)
        {
            if (string.IsNullOrWhiteSpace(instance.id) || string.IsNullOrWhiteSpace(instance.prototype) ||
                !Finite(instance.position) || !Finite(instance.scale) ||
                Mathf.Abs(instance.scale.x) < 0.000001f || Mathf.Abs(instance.scale.y) < 0.000001f ||
                Mathf.Abs(instance.scale.z) < 0.000001f)
                throw new InvalidOperationException("Invalid vegetation transform or identity: " + instance.id);
            Quaternion q = instance.rotation;
            float length = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            if (float.IsNaN(length) || float.IsInfinity(length) || Mathf.Abs(length - 1) > 0.001f)
                throw new InvalidOperationException("Rotation must be a normalized quaternion: " + instance.id);
            if (float.IsNaN(instance.tintStrength) || float.IsInfinity(instance.tintStrength) ||
                instance.tintStrength < 0 || instance.tintStrength > 1 ||
                (instance.tintStrength > 0 && (!Finite(new Vector3(instance.rootTint.r, instance.rootTint.g, instance.rootTint.b)) ||
                !Finite(new Vector3(instance.tipTint.r, instance.tipTint.g, instance.tipTint.b)) ||
                float.IsNaN(instance.tintPower) || float.IsInfinity(instance.tintPower) || instance.tintPower <= 0)))
                throw new InvalidOperationException("Invalid vegetation tint: " + instance.id);
        }

        private static bool Finite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

internal sealed class WorldSwordTrail
{
    const int Capacity = 32;
    const int MaxSubdivisions = 8;
    const float Lifetime = .14f;
    const float MinSampleInterval = 1f / 150f;
    // Dawn's guard and tip are on the prefab's local Z axis. Extend the rim slightly past the tip.
    static readonly Vector3 BladeBase = new Vector3(0f, 0f, .22f);
    static readonly Vector3 BladeTip = new Vector3(0f, 0f, .98f);

    readonly Transform weapon;
    readonly Material material;
    readonly Mesh mesh;
    readonly List<Sample> samples = new List<Sample>(Capacity);
    readonly List<Vector3> vertices = new List<Vector3>(Capacity * MaxSubdivisions * 2);
    readonly List<Vector2> uvs = new List<Vector2>(Capacity * MaxSubdivisions * 2);
    readonly List<int> indices = new List<int>((Capacity - 1) * MaxSubdivisions * 6);

    struct Sample
    {
        internal Vector3 bladeBase, bladeTip;
        internal float time;
    }

    internal WorldSwordTrail(Transform weapon, Material material)
    {
        this.weapon = weapon;
        this.material = material;
        mesh = new Mesh { name = "World Sword Trail" };
        mesh.MarkDynamic();
    }

    internal void Clear() => samples.Clear();

    internal void SampleBlade(float now)
    {
        Vector3 tip = weapon.TransformPoint(BladeTip);
        if (samples.Count > 0)
        {
            Sample last = samples[samples.Count - 1];
            if (now - last.time < MinSampleInterval || (tip - last.bladeTip).sqrMagnitude < .0001f)
                return;
        }
        if (samples.Count == Capacity) samples.RemoveAt(0);
        samples.Add(new Sample { bladeBase = weapon.TransformPoint(BladeBase), bladeTip = tip, time = now });
    }

    internal void Draw(float now)
    {
        while (samples.Count > 0 && now - samples[0].time >= Lifetime) samples.RemoveAt(0);
        if (samples.Count < 2) return;

        vertices.Clear();
        uvs.Clear();
        indices.Clear();
        // The strip stays where the blade passed, even while the player continues moving.
        Sample previous = samples[0];
        AddBladePair(previous.bladeBase, previous.bladeTip, (now - previous.time) / Lifetime);
        for (int i = 1; i < samples.Count; i++)
        {
            Sample next = samples[i];
            Vector3 from = previous.bladeTip - previous.bladeBase;
            Vector3 to = next.bladeTip - next.bladeBase;
            int steps = Mathf.Clamp(Mathf.CeilToInt(Vector3.Angle(from, to) / 12f), 1, MaxSubdivisions);
            // Interpolate the blade's rotation so fast swings form an arc between evaluated poses.
            for (int step = 1; step <= steps; step++)
            {
                float t = (float)step / steps;
                Vector3 bladeBase = Vector3.Lerp(previous.bladeBase, next.bladeBase, t);
                Vector3 tip = bladeBase + Vector3.Slerp(from, to, t);
                float age = (now - Mathf.Lerp(previous.time, next.time, t)) / Lifetime;
                AddBladePair(bladeBase, tip, age);
            }
            previous = next;
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(indices, 0);
        Graphics.DrawMesh(mesh, Matrix4x4.identity, material, weapon.gameObject.layer, null, 0, null,
            ShadowCastingMode.Off, false, null, LightProbeUsage.Off);
    }

    void AddBladePair(Vector3 bladeBase, Vector3 tip, float age)
    {
        int start = vertices.Count - 2;
        vertices.Add(Vector3.Lerp(bladeBase, tip, .2f + age * .65f));
        vertices.Add(tip);
        uvs.Add(new Vector2(age, 0f));
        uvs.Add(new Vector2(age, 1f));
        if (start < 0) return;

        indices.Add(start);
        indices.Add(start + 1);
        indices.Add(start + 2);
        indices.Add(start + 1);
        indices.Add(start + 3);
        indices.Add(start + 2);
    }

    internal void Dispose() => Object.Destroy(mesh);
}

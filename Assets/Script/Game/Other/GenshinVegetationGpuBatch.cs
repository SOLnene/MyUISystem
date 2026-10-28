using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Game.Vegetation
{
    internal sealed class GenshinVegetationGpuBatch : IDisposable
    {
        // Keep this 192-byte record aligned with both HLSL declarations; colors are already in shader color space.
        [StructLayout(LayoutKind.Sequential)]
        private struct Instance
        {
            public Matrix4x4 objectToWorld;
            public Matrix4x4 worldToObject;
            public Vector4 rootTint;
            public Vector4 tipTint;
            public Vector4 boundsCenter;
            public Vector4 boundsExtents;
        }

        private static readonly int InstancesId = Shader.PropertyToID("_GrassGpuInstances");
        private static readonly int VisibleId = Shader.PropertyToID("_GrassVisibleIndices");
        private static readonly int PlanesId = Shader.PropertyToID("_GrassCullPlanes");
        private static readonly int CameraId = Shader.PropertyToID("_GrassCullCamera");
        private static readonly int CountId = Shader.PropertyToID("_GrassInstanceCount");
        private readonly ComputeShader shader;
        private readonly int kernel;
        private readonly int count;
        private ComputeBuffer instances;
        private ComputeBuffer visible;
        private ComputeBuffer arguments;

        internal ComputeBuffer Arguments => arguments;

        internal GenshinVegetationGpuBatch(ComputeShader shader, Mesh mesh, Matrix4x4[] matrices,
            Vector4[] roots, Vector4[] tips, float padding, MaterialPropertyBlock properties)
        {
            this.shader = shader;
            kernel = shader.FindKernel("CullInstances");
            count = matrices.Length;
            try
            {
                var records = new Instance[count];
                Bounds local = mesh.bounds;
                for (int i = 0; i < count; i++)
                {
                    Matrix4x4 matrix = matrices[i];
                    Vector3 x = matrix.MultiplyVector(new Vector3(local.extents.x, 0, 0));
                    Vector3 y = matrix.MultiplyVector(new Vector3(0, local.extents.y, 0));
                    Vector3 z = matrix.MultiplyVector(new Vector3(0, 0, local.extents.z));
                    Vector3 center = matrix.MultiplyPoint3x4(local.center);
                    Vector3 extents = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                        Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                        Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z)) + Vector3.one * padding;
                    records[i] = new Instance { objectToWorld = matrix, worldToObject = matrix.inverse,
                        rootTint = roots[i], tipTint = tips[i],
                        boundsCenter = new Vector4(center.x, center.y, center.z, 0),
                        boundsExtents = new Vector4(extents.x, extents.y, extents.z, 0) };
                }
                instances = new ComputeBuffer(count, Marshal.SizeOf<Instance>(), ComputeBufferType.Structured);
                visible = new ComputeBuffer(count, sizeof(uint), ComputeBufferType.Append);
                arguments = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
                instances.SetData(records);
                arguments.SetData(new uint[] { mesh.GetIndexCount(0), 0, mesh.GetIndexStart(0), (uint)mesh.GetBaseVertex(0), 0 });
                properties.SetBuffer(InstancesId, instances);
                properties.SetBuffer(VisibleId, visible);
            }
            catch { Dispose(); throw; }
        }

        internal void Cull(Vector4[] planes, Vector3 cameraPosition, float maximumDistance)
        {
            // Reset for every camera; a zero-visible view must never reuse the previous camera's count.
            visible.SetCounterValue(0);
            shader.SetInt(CountId, count);
            shader.SetVectorArray(PlanesId, planes);
            shader.SetVector(CameraId, new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, maximumDistance * maximumDistance));
            shader.SetBuffer(kernel, InstancesId, instances);
            shader.SetBuffer(kernel, VisibleId, visible);
            shader.Dispatch(kernel, (count + 63) / 64, 1, 1);
            // Indexed indirect arguments store instanceCount at byte offset 4. No GPU readback is needed to draw.
            ComputeBuffer.CopyCount(visible, arguments, sizeof(uint));
        }

        public void Dispose()
        {
            instances?.Release(); instances = null;
            visible?.Release(); visible = null;
            arguments?.Release(); arguments = null;
        }
    }
}

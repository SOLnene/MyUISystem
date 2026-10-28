using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.EditorTools
{
    internal sealed class GenshinVegetationSurfaceSampler : IDisposable
    {
        private readonly Collider surface;
        private readonly Dictionary<Texture, Texture2D> snapshots = new Dictionary<Texture, Texture2D>();

        internal GenshinVegetationSurfaceSampler(Collider surface) { this.surface = surface; }

        internal Color Sample(RaycastHit hit)
        {
            Color color;
            if (surface is TerrainCollider terrainCollider)
                color = SampleTerrain(terrainCollider, hit.point);
            else
            {
                Material material = SurfaceMaterial(hit);
                if (material.shader.name == "Game/Terrain/Genshin Recovered Terrain")
                    color = SampleLayers(material, hit, true);
                else if (material.shader.name == "Game/Terrain/Genshin Stylized" && material.GetFloat("_LayerMode") > 0.5f)
                    color = SampleLayers(material, hit, false);
                else
                {
                    Color tint = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") :
                        material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                    color = QualitySettings.activeColorSpace == ColorSpace.Linear ? tint.linear : tint;
                    foreach (string property in new[] { "_BaseMap", "_MainTex", "_DiffuseTex" })
                    {
                        if (!material.HasProperty(property) || material.GetTexture(property) == null) continue;
                        RequireMeshUV();
                        Vector2 uv = Vector2.Scale(hit.textureCoord, material.GetTextureScale(property)) + material.GetTextureOffset(property);
                        color *= Read(material.GetTexture(property), uv, Color.white);
                        break;
                    }
                }
            }
            // Store ColorField/sRGB values; the renderer converts them once when uploading vector arrays.
            color = QualitySettings.activeColorSpace == ColorSpace.Linear ? color.gamma : color;
            color.a = 1;
            return color;
        }

        private Material SurfaceMaterial(RaycastHit hit)
        {
            Renderer renderer = surface.GetComponent<Renderer>();
            if (renderer == null) throw new InvalidOperationException("The planting collider needs a Renderer for automatic color sampling; otherwise choose a manual root color.");
            int slot = 0;
            if (surface is MeshCollider collider && collider.sharedMesh != null)
            {
                Mesh mesh = collider.sharedMesh;
                int firstTriangle = 0;
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    int triangles = (int)mesh.GetIndexCount(i) / 3;
                    if (hit.triangleIndex >= firstTriangle && hit.triangleIndex < firstTriangle + triangles) { slot = i; break; }
                    firstTriangle += triangles;
                }
            }
            Material[] materials = renderer.sharedMaterials;
            if (slot >= materials.Length || materials[slot] == null)
                throw new InvalidOperationException("The planting surface has no material for the hit triangle.");
            return materials[slot];
        }

        private void RequireMeshUV()
        {
            if (!(surface is MeshCollider))
                throw new InvalidOperationException("Texture sampling needs a MeshCollider or TerrainCollider; otherwise choose a manual root color.");
        }

        private Color SampleLayers(Material material, RaycastHit hit, bool recovered)
        {
            RequireMeshUV();
            Color control0 = Read(material.GetTexture(recovered ? "_Control0" : "_SplatMap"), hit.textureCoord, new Color(1, 0, 0, 0));
            Color control1 = recovered ? Read(material.GetTexture("_Control1"), hit.textureCoord, Color.clear) : Color.clear;
            Color tint = Read(material.GetTexture(recovered ? "_TerrainTint" : "_TintMap"), hit.textureCoord, Color.white) * material.GetFloat("_TintEnhanceFactor");
            Vector4 origin = recovered ? material.GetVector("_SourceOrigin") : Vector4.zero;
            Vector2 xz = new Vector2(hit.point.x + origin.x, hit.point.z + origin.z);
            int count = recovered ? 7 : 3;
            var albedos = new Color[count];
            var heights = new float[count];
            var tintFactors = new float[count];
            float maximum = 0;
            for (int i = 0; i < count; i++)
            {
                int index = i + 1;
                string prefix = "_Texture_" + index;
                float tile = material.GetFloat(recovered ? prefix + "_Tiling" : "_TexTill" + index);
                albedos[i] = Read(material.GetTexture(recovered ? prefix + "_Albedo" : "_Albedo" + index), xz / Mathf.Max(0.01f, tile), Color.white);
                float weight = i < 4 ? control0[i] : control1[i - 4];
                heights[i] = weight > 0.001f ? albedos[i].a + weight * 1.2f : 0;
                maximum = Mathf.Max(maximum, heights[i]);
                tintFactors[i] = material.GetFloat(recovered ? prefix + "_TintEnabled" : "_UseTint" + index);
            }
            Color result = Color.clear;
            float sum = 0.001f;
            for (int i = 0; i < count; i++)
            {
                float weight = Mathf.Max(0, heights[i] - (maximum - 0.2f));
                result += albedos[i] * Color.LerpUnclamped(Color.white, tint, tintFactors[i]) * weight;
                sum += weight;
            }
            return result / sum;
        }

        private Color SampleTerrain(TerrainCollider collider, Vector3 point)
        {
            TerrainData data = collider.terrainData;
            if (data == null || data.alphamapLayers == 0) throw new InvalidOperationException("The planting terrain has no painted layers.");
            Vector3 local = collider.transform.InverseTransformPoint(point);
            float u = Mathf.Clamp01(local.x / data.size.x), v = Mathf.Clamp01(local.z / data.size.z);
            float[,,] weights = data.GetAlphamaps(Mathf.Min(data.alphamapWidth - 1, (int)(u * data.alphamapWidth)),
                Mathf.Min(data.alphamapHeight - 1, (int)(v * data.alphamapHeight)), 1, 1);
            TerrainLayer[] layers = data.terrainLayers;
            Color result = Color.clear;
            for (int i = 0; i < layers.Length; i++)
            {
                TerrainLayer layer = layers[i];
                if (layer == null || weights[0, 0, i] <= 0) continue;
                Vector2 uv = new Vector2((local.x + layer.tileOffset.x) / Mathf.Max(0.01f, layer.tileSize.x),
                    (local.z + layer.tileOffset.y) / Mathf.Max(0.01f, layer.tileSize.y));
                Color sample = Read(layer.diffuseTexture, uv, Color.white);
                Vector4 low = layer.diffuseRemapMin, high = layer.diffuseRemapMax;
                sample = new Color(Mathf.LerpUnclamped(low.x, high.x, sample.r), Mathf.LerpUnclamped(low.y, high.y, sample.g),
                    Mathf.LerpUnclamped(low.z, high.z, sample.b), 1);
                result += sample * weights[0, 0, i];
            }
            return result;
        }

        private Color Read(Texture texture, Vector2 uv, Color fallback)
        {
            if (texture == null) return fallback;
            if (!snapshots.TryGetValue(texture, out Texture2D copy))
            {
                // GPU copies honor source sRGB flags without changing any texture's Read/Write importer setting.
                RenderTexture temporary = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                RenderTexture previous = RenderTexture.active;
                bool previousSRGBWrite = GL.sRGBWrite;
                try
                {
                    GL.sRGBWrite = false;
                    Graphics.Blit(texture, temporary);
                    RenderTexture.active = temporary;
                    copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, true)
                    { hideFlags = HideFlags.HideAndDontSave, wrapModeU = texture.wrapModeU, wrapModeV = texture.wrapModeV };
                    copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0, false);
                    snapshots.Add(texture, copy);
                }
                catch { if (copy != null) UnityEngine.Object.DestroyImmediate(copy); throw; }
                finally
                {
                    GL.sRGBWrite = previousSRGBWrite;
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(temporary);
                }
            }
            // CPU bilinear coordinates address texel origins; GPU normalized UVs address texel centers.
            return copy.GetPixelBilinear(uv.x - 0.5f / copy.width, uv.y - 0.5f / copy.height);
        }

        public void Dispose()
        {
            foreach (Texture2D copy in snapshots.Values) UnityEngine.Object.DestroyImmediate(copy);
            snapshots.Clear();
        }
    }
}

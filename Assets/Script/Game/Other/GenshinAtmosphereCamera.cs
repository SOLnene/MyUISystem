using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[ExecuteAlways]
[ImageEffectAllowedInSceneView]
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
internal sealed class GenshinAtmosphereCamera : MonoBehaviour
{
    [SerializeField] private Light sceneSun;
    [SerializeField] private Material skyMaterial;
    [SerializeField] private Material gradingMaterial;

    private static readonly int SunDirectionId = Shader.PropertyToID("_ES_SunDirection");
    private static readonly int CaptureModeId = Shader.PropertyToID("_GenshinGroundCaptureMode");
    private static readonly int CaptureViewId = Shader.PropertyToID("_GenshinGroundCaptureView");
    private static readonly int CaptureProjectionId = Shader.PropertyToID("_GenshinGroundCaptureVP");
    private static readonly int GroundNormalsId = Shader.PropertyToID("_GenshinGroundNormals");
    private static readonly int GroundAlbedoDepthId = Shader.PropertyToID("_GenshinGroundAlbedoDepth");
    private static readonly int GroundCaptureEnabledId = Shader.PropertyToID("_GenshinGroundCaptureEnabled");

    private readonly List<Renderer> groundRenderers = new List<Renderer>();
    private readonly Dictionary<Material, Material> captureMaterials = new Dictionary<Material, Material>();
    private Camera renderCamera;
    private CommandBuffer groundCapture;
    private RenderTexture groundNormals;
    private RenderTexture groundAlbedoDepth;
    private Scene groundScene;

    private void OnEnable()
    {
        renderCamera = GetComponent<Camera>();
        groundCapture = new CommandBuffer { name = "Genshin Ground Material Capture" };
        renderCamera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, groundCapture);
    }

    private void OnPreCull()
    {
        // Use the scene light for both Game and Scene views, rather than a second independent sun.
        Vector3 direction = -sceneSun.transform.forward;
        skyMaterial.SetVector(SunDirectionId, new Vector4(direction.x, direction.y, direction.z, 0));
        UpdateGroundCapture();
    }

    private void UpdateGroundCapture()
    {
        // A camera without a valid capture must retain the existing material path.
        Shader.SetGlobalFloat(GroundCaptureEnabledId, 0);
        groundCapture.Clear();
        Scene sourceScene = sceneSun.gameObject.scene;
        if (!sourceScene.isLoaded)
            return;

        // Region heightfields are static; cache their renderers once per owning scene.
        if (groundScene != sourceScene)
        {
            ReleaseCaptureMaterials();
            groundRenderers.Clear();
            groundScene = sourceScene;
            foreach (GameObject root in sourceScene.GetRootGameObjects())
            {
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material != null && material.shader.name == "Game/Terrain/Genshin Recovered Terrain")
                        {
                            groundRenderers.Add(renderer);
                            break;
                        }
                    }
                }
            }
        }

        int width = renderCamera.pixelWidth;
        int height = renderCamera.pixelHeight;
        if (groundRenderers.Count == 0 || width <= 0 || height <= 0)
            return;
        if (groundNormals == null || groundNormals.width != width || groundNormals.height != height)
        {
            ReleaseGroundTargets();
            groundNormals = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            {
                name = "Genshin Ground Normals", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            groundAlbedoDepth = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear)
            {
                name = "Genshin Ground Albedo Depth", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            groundNormals.Create();
            groundAlbedoDepth.Create();
        }

        foreach (Renderer renderer in groundRenderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly)
                continue;
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null || material.shader.name != "Game/Terrain/Genshin Recovered Terrain")
                    continue;
                if (!captureMaterials.TryGetValue(material, out Material capture))
                {
                    capture = new Material(material) { hideFlags = HideFlags.HideAndDontSave };
                    captureMaterials.Add(material, capture);
                }
                // Keep live material edits in sync without changing the source asset.
                capture.CopyPropertiesFromMaterial(material);
            }
        }

        Matrix4x4 view = renderCamera.worldToCameraMatrix;
        groundCapture.SetGlobalMatrix(CaptureViewId, view);
        groundCapture.SetGlobalMatrix(CaptureProjectionId, GL.GetGPUProjectionMatrix(renderCamera.projectionMatrix, true) * view);
        for (int mode = 1; mode <= 2; mode++)
        {
            groundCapture.SetGlobalFloat(CaptureModeId, mode);
            groundCapture.SetRenderTarget(mode == 1 ? groundNormals : groundAlbedoDepth);
            groundCapture.SetViewport(new Rect(0, 0, width, height));
            groundCapture.ClearRenderTarget(true, true, Color.clear);
            foreach (Renderer renderer in groundRenderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly)
                    continue;
                Material[] materials = renderer.sharedMaterials;
                for (int submesh = 0; submesh < materials.Length; submesh++)
                {
                    Material material = materials[submesh];
                    if (material != null && captureMaterials.TryGetValue(material, out Material capture))
                        groundCapture.DrawRenderer(renderer, capture, submesh, 0);
                }
            }
        }
        groundCapture.SetGlobalFloat(CaptureModeId, 0);
        groundCapture.SetGlobalTexture(GroundNormalsId, groundNormals);
        groundCapture.SetGlobalTexture(GroundAlbedoDepthId, groundAlbedoDepth);
        groundCapture.SetGlobalFloat(GroundCaptureEnabledId, 1);
        groundCapture.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
        groundCapture.SetViewport(renderCamera.pixelRect);
    }

    private void OnPostRender()
    {
        // Global bindings are valid only during this camera's draw, including Scene view copies.
        Shader.SetGlobalFloat(GroundCaptureEnabledId, 0);
    }

    private void OnDisable()
    {
        if (groundCapture != null)
        {
            renderCamera.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, groundCapture);
            groundCapture.Release();
            groundCapture = null;
        }
        Shader.SetGlobalFloat(GroundCaptureEnabledId, 0);
        Shader.SetGlobalFloat(CaptureModeId, 0);
        Shader.SetGlobalTexture(GroundNormalsId, null);
        Shader.SetGlobalTexture(GroundAlbedoDepthId, null);
        ReleaseGroundTargets();
        ReleaseCaptureMaterials();
        groundRenderers.Clear();
        groundScene = default;
    }

    private void ReleaseGroundTargets()
    {
        foreach (RenderTexture target in new[] { groundNormals, groundAlbedoDepth })
        {
            if (target == null)
                continue;
            target.Release();
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
        groundNormals = null;
        groundAlbedoDepth = null;
    }

    private void ReleaseCaptureMaterials()
    {
        foreach (Material material in captureMaterials.Values)
        {
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        }
        captureMaterials.Clear();
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Graphics.Blit(source, destination, gradingMaterial);
    }
}

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class UIBackdropCaptureService :
    SingletonMono<UIBackdropCaptureService>
{
    // The command buffer writes one frame of the UI camera into this persistent result texture.
    static readonly int BlurTemporaryId =
        Shader.PropertyToID("_UIBackdropBlurTemporary");
    static readonly int BlurRadiusId = Shader.PropertyToID("_BlurRadius");
    static readonly int TextureSourceId = Shader.PropertyToID("_UIBackdropTextureSource");
    static readonly int SaturationId = Shader.PropertyToID("_Saturation");
    static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
    static readonly int TintId = Shader.PropertyToID("_Tint");
    static readonly int TintStrengthId =
        Shader.PropertyToID("_TintStrength");
    static readonly int BackdropTexelSizeId =
        Shader.PropertyToID("_BackdropTexelSize");

    CommandBuffer captureCommandBuffer;
    Camera uiCamera;
    RenderTexture outputTexture;
    bool isCapturing;
    bool capturePending;
    bool captureCompleted;
    CommandBuffer gameCaptureCommandBuffer;
    Camera gameCamera;
    RenderTexture gameOutputTexture;
    Material gameBlurMaterial;
    Material gameBlurTemplate;
    bool isGameCapturing, hasGameSnapshot;

    // This cache belongs to a UI browsing session; individual pages only borrow its texture.
    public RenderTexture GameBackdrop => hasGameSnapshot ? gameOutputTexture : null;

    internal void Configure(Camera camera)
    {
        if (uiCamera == camera)
        {
            return;
        }

        // UIManager creates the camera dynamically. Keep the capture backend bound to that camera
        // and execute the capture after its normal rendering has finished.
        DetachCommandBuffer();
        uiCamera = camera;
        EnsureCommandBuffer();
        uiCamera.AddCommandBuffer(
            CameraEvent.AfterEverything,
            captureCommandBuffer);
    }

    void OnEnable()
    {
        // The post-render callback only signals completion; the actual GPU work is recorded above.
        Camera.onPostRender -= HandleCameraPostRender;
        Camera.onPostRender += HandleCameraPostRender;
    }

    void OnDisable()
    {
        Camera.onPostRender -= HandleCameraPostRender;
        capturePending = false;
        // Discard recorded commands when the service is disabled so a later request starts cleanly.
        captureCommandBuffer?.Clear();
        gameCaptureCommandBuffer?.Clear();
    }

    // Records one capture request, waits until the configured camera has rendered it, and returns
    // the persistent RT that can be assigned directly to a RawImage.
    public async UniTask<RenderTexture> CaptureCompositeAsync(
        Material blurMaterial,
        UIBackdropCaptureProfile profile,
        CancellationToken cancellationToken = default)
    {
        if (blurMaterial == null)
        {
            throw new ArgumentNullException(nameof(blurMaterial));
        }

        if (blurMaterial.passCount < 2)
        {
            throw new InvalidOperationException(
                "UI backdrop blur material requires two shader passes.");
        }

        if (uiCamera == null)
        {
            throw new InvalidOperationException(
                "UI backdrop capture camera is not configured.");
        }

        // Only one request may rewrite outputTexture at a time.
        await UniTask.WaitWhile(
            () => isCapturing,
            cancellationToken: cancellationToken);

        isCapturing = true;

        try
        {
            // Capture at the profile scale instead of allocating a full-resolution blur target.
            var outputWidth = Mathf.Max(
                1,
                Mathf.RoundToInt(Screen.width * profile.ResolutionScale));
            var outputHeight = Mathf.Max(
                1,
                Mathf.RoundToInt(Screen.height * profile.ResolutionScale));

            EnsureOutput(outputWidth, outputHeight);
            PrepareCapture(captureCommandBuffer, outputTexture, blurMaterial, profile);
            captureCompleted = false;
            capturePending = true;

            // CommandBuffer execution happens during the camera render, so polling must yield to
            // the frame loop until Camera.onPostRender confirms that pass has completed.
            while (!captureCompleted)
            {
                await UniTask.WaitForEndOfFrame(
                    this,
                    cancellationToken);
            }

            return outputTexture;
        }
        finally
        {
            capturePending = false;
            captureCompleted = false;
            // Clear only the recorded commands. outputTexture remains alive for the popup view.
            captureCommandBuffer?.Clear();
            isCapturing = false;
        }
    }

    
    public async UniTask<RenderTexture> CaptureGameAsync(
        Camera source,
        Material blurTemplate,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Navigation inside an existing UI session keeps its original world frame.
        if (hasGameSnapshot && gameCamera == source && uiCamera != null)
        {
            foreach (var view in uiCamera.transform.parent.GetComponentsInChildren<UIView>())
                if (view.Handle != null && view.Handle.uiType != UIType.HubRoot)
                    return gameOutputTexture;
        }
        if (source == null || !source.isActiveAndEnabled)
            throw new InvalidOperationException("Game backdrop requires a rendering world camera.");
        if (blurTemplate == null || blurTemplate.passCount < 2)
            throw new ArgumentException("Game backdrop requires a two-pass blur material.", nameof(blurTemplate));

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, this.GetCancellationTokenOnDestroy());
        await UniTask.WaitWhile(() => isGameCapturing, cancellationToken: cancellation.Token);
        isGameCapturing = true;
        hasGameSnapshot = false;
        RenderTexture frame = null;
        RenderTexture previousTarget = source.targetTexture;
        try
        {
            if (gameCaptureCommandBuffer == null)
                gameCaptureCommandBuffer = new CommandBuffer { name = "Game Backdrop Capture" };
            if (gameCamera != source)
            {
                gameCamera = source;
            }
            // Reward capture may run in the same frame with different styling, so keep uniforms separate.
            if (gameBlurTemplate != blurTemplate || gameBlurMaterial == null)
            {
                if (gameBlurMaterial != null) Destroy(gameBlurMaterial);
                gameBlurTemplate = blurTemplate;
                gameBlurMaterial = new Material(blurTemplate) { name = "Game Backdrop Blur" };
            }
            var profile = UIBackdropCaptureProfile.GameScreen;
            int width = Mathf.Max(1, Mathf.RoundToInt(source.pixelWidth * profile.ResolutionScale));
            int height = Mathf.Max(1, Mathf.RoundToInt(source.pixelHeight * profile.ResolutionScale));
            if (gameOutputTexture == null || gameOutputTexture.width != width ||
                gameOutputTexture.height != height || !gameOutputTexture.IsCreated())
            {
                DestroyOutput(gameOutputTexture);
                gameOutputTexture = CreateOutput(width, height, $"GameBackdrop_{width}x{height}");
            }
            // Capture after movement/animation/camera LateUpdate, before opening the first page.
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellation.Token);
            if (!isActiveAndEnabled || source == null || !source.isActiveAndEnabled)
                throw new InvalidOperationException("Game backdrop camera stopped before capture completed.");
            frame = RenderTexture.GetTemporary(source.pixelWidth, source.pixelHeight, 24,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            // An explicit target includes this camera's image effects and avoids sampling a backbuffer.
            source.targetTexture = frame;
            source.Render();
            source.targetTexture = previousTarget;
            PrepareCapture(gameCaptureCommandBuffer, gameOutputTexture, gameBlurMaterial, profile, frame, true);
            Graphics.ExecuteCommandBuffer(gameCaptureCommandBuffer);
            cancellation.Token.ThrowIfCancellationRequested();
            hasGameSnapshot = true;
            return gameOutputTexture;
        }
        finally
        {
            if (source != null) source.targetTexture = previousTarget;
            if (frame != null) RenderTexture.ReleaseTemporary(frame);
            gameCaptureCommandBuffer?.Clear();
            isGameCapturing = false;
        }
    }

    void LateUpdate()
    {
        if (gameCamera != null || gameOutputTexture == null || isGameCapturing) return;
        // The world scene has gone away. Do not expose its cached frame to the next session.
        hasGameSnapshot = false;
        DestroyOutput(gameOutputTexture);
        gameOutputTexture = null;
    }

    void PrepareCapture(
        CommandBuffer commands,
        RenderTexture output,
        Material material,
        UIBackdropCaptureProfile profile,
        RenderTargetIdentifier? input = null,
        bool textureSource = false)
    {
        // Material properties are configured immediately; their values are consumed when the
        // command buffer is executed later in the source camera's AfterEverything stage.
        material.SetFloat(BlurRadiusId, profile.BlurRadius);
        material.SetFloat(SaturationId, profile.Saturation);
        material.SetFloat(BrightnessId, profile.Brightness);
        material.SetColor(TintId, profile.Tint);
        material.SetFloat(TintStrengthId, profile.TintStrength);
        material.SetVector(
            BackdropTexelSizeId,
            new Vector4(
                1f / output.width,
                1f / output.height,
                output.width,
                output.height));

        // A command buffer stores commands between frames, so remove the previous request before
        // recording the next one.
        commands.Clear();
        commands.GetTemporaryRT(
            BlurTemporaryId,
            output.width,
            output.height,
            0,
            FilterMode.Bilinear,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Default);
        
        commands.BeginSample("UI Backdrop Capture");
        RenderTargetIdentifier source = input ?? BuiltinRenderTextureType.CameraTarget;
        bool normalizeTexture = textureSource && SystemInfo.graphicsUVStartsAtTop;
        if (normalizeTexture)
        {
            // The existing shader normalizes CameraTarget UVs; match that convention for an RT input.
            commands.GetTemporaryRT(TextureSourceId, output.width, output.height, 0, FilterMode.Bilinear,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            commands.Blit(source, TextureSourceId, new Vector2(1f, -1f), new Vector2(0f, 1f));
            source = TextureSourceId;
        }
        // Pass 0 samples the final UI camera color and performs the horizontal blur. CameraTarget
        // is normalized in this pass because its UV orientation differs from a RenderTexture.
        commands.Blit(
            source,
            BlurTemporaryId,
            material,
            0);
        // Pass 1 performs the vertical blur and the final saturation/brightness/tint adjustment.
        commands.Blit(
            BlurTemporaryId,
            output,
            material,
            1);
        
        
        commands.EndSample("UI Backdrop Capture");
        // ReleaseTemporaryRT is itself recorded; the temporary target is released after the GPU
        // has consumed it, while output remains persistent.
        commands.ReleaseTemporaryRT(BlurTemporaryId);
        if (normalizeTexture) commands.ReleaseTemporaryRT(TextureSourceId);
    }

    void HandleCameraPostRender(Camera renderedCamera)
    {
        if (!capturePending || renderedCamera != uiCamera)
        {
            return;
        }
        
        // This callback is the completion signal for the async request. It does not copy or clear
        // the RT; those operations were already recorded in the command buffer.
        capturePending = false;
        captureCompleted = true;
    }

    void EnsureCommandBuffer()
    {
        if (captureCommandBuffer != null)
        {
            return;
        }

        captureCommandBuffer = new CommandBuffer
        {
            // A stable name makes this capture visible in Frame Debugger and GPU captures.
            name = "UI Backdrop Capture"
        };
    }

    void DetachCommandBuffer()
    {
        if (uiCamera == null || captureCommandBuffer == null)
        {
            return;
        }

        // Remove the old binding before switching cameras or destroying the service.
        uiCamera.RemoveCommandBuffer(
            CameraEvent.AfterEverything,
            captureCommandBuffer);
    }

    void EnsureOutput(int width, int height)
    {
        if (outputTexture != null &&
            outputTexture.width == width &&
            outputTexture.height == height &&
            outputTexture.IsCreated())
        {
            return;
        }

        // Reuse the persistent RT while the resolution is unchanged; recreate it only when the
        // screen/profile size changes.
        DestroyOutput(outputTexture);
        outputTexture = CreateOutput(width, height, $"UIBackdrop_{width}x{height}");
    }

    static RenderTexture CreateOutput(int width, int height, string textureName)
    {
        var output = new RenderTexture(
            width,
            height,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Default)
        {
            name = textureName,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false
        };
        output.Create();
        return output;
    }

    static void DestroyOutput(RenderTexture output)
    {
        if (output == null)
        {
            return;
        }

        output.Release();
        Destroy(output);
    }

    void OnDestroy()
    {
        // The command buffer and persistent RT are owned by this service and are released together.
        Camera.onPostRender -= HandleCameraPostRender;
        DetachCommandBuffer();
        captureCommandBuffer?.Release();
        DestroyOutput(outputTexture);
        gameCaptureCommandBuffer?.Release();
        DestroyOutput(gameOutputTexture);
        if (gameBlurMaterial != null) Destroy(gameBlurMaterial);
    }
}

using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
    ScriptableRenderContext context;
    Camera camera;
    const string bufferName = "Render Camera";
    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };
    CullingResults cullingResults;

    Lighting lighting = new Lighting();

    PostFXStack postFXStack = new PostFXStack();

    Material material;

    static ShaderTagId
        unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit"),
        litShaderTag = new ShaderTagId("CustomLit");

    //static int frameBufferId = Shader.PropertyToID("_CameraFrameBuffer");
    static int bufferSizeId = Shader.PropertyToID("_CameraBufferSize");
    static int colorAttachmentId = Shader.PropertyToID("_CameraColorAttachment");
    static int depthAttachmentId = Shader.PropertyToID("_CameraDepthAttachment");
    static int colorTextureId = Shader.PropertyToID("_CameraColorTexture");
    static int depthTextureId = Shader.PropertyToID("_CameraDepthTexture");
    static int sourceTextureId = Shader.PropertyToID("_SourceTexture");
    static int srcBlendId = Shader.PropertyToID("_CameraSrcBlend");
    static int dstBlendId = Shader.PropertyToID("_CameraDstBlend");

    static bool copyTextureSupported = SystemInfo.copyTextureSupport > CopyTextureSupport.None;

    bool useColorTexture;
    bool useDepthTexture;
    bool useIntermediateBuffer;

    bool useHDR;
    bool useScaledRendering;

    static CameraSettings defaultCameraSettings = new CameraSettings();
    CameraSettings cameraSettings;
    CameraBufferSettings bufferSettings;
    float renderScale;

    Vector2Int bufferSize;

    Texture2D missingTexture;

    public const float renderScaleMin = 0.1f, renderScaleMax = 2f;

    public CameraRenderer(Shader shader)
    {
        material = CoreUtils.CreateEngineMaterial(shader);
        missingTexture = new Texture2D(1, 1)
        {
            hideFlags = HideFlags.HideAndDontSave,
            name = "Missing"
        };
        missingTexture.SetPixel(0, 0, Color.white * 0.5f);
        missingTexture.Apply(true, true);
    }

    public void Dispose()
    {
        if(rayTracingAccelerationStructure != null)
        {
            rayTracingAccelerationStructure.Release();
            rayTracingAccelerationStructure = null;
        }
        CoreUtils.Destroy(material);
        CoreUtils.Destroy(missingTexture);
    }

    RayTracingAccelerationStructure rayTracingAccelerationStructure = null;

    public void Render(
        ScriptableRenderContext context,
        Camera camera,
        CameraBufferSettings bufferSettings,
        bool useDynamicBatching,
        bool useGPUInstancing,
        bool useLightsPerObject,
        ShadowSettings shadowSettings,
        PostFXSettings postFXSettings,
        int colorLUTResolution,
        RayTracingSettings rayTracingSettings
    ) {
        this.context = context;
        this.camera = camera;
        this.bufferSettings = bufferSettings;

        var crpCamera = camera.GetComponent<CustomRenderPipelineCamera>();
        CameraSettings cameraSettings = crpCamera ? crpCamera.Settings : defaultCameraSettings;
        this.cameraSettings = cameraSettings;

        if(camera.cameraType == CameraType.Reflection)
        {
            useColorTexture = bufferSettings.copyColorReflection;
            useDepthTexture = bufferSettings.copyDepthReflections;
        } else
        {
            useColorTexture = bufferSettings.copyColor && cameraSettings.copyColor;
            useDepthTexture = bufferSettings.copyDepth && cameraSettings.copyDepth;
        }

        if(cameraSettings.overridePostFX)
        {
            postFXSettings = cameraSettings.postFXSettings;
        }

        if(!Cull(shadowSettings.maxDistance))
        {
            return;
        }
        useHDR = bufferSettings.allowHDR && camera.allowHDR;
        if(useScaledRendering)
        {
            renderScale = Mathf.Clamp(renderScale, renderScaleMin, renderScaleMax);
            bufferSize.x = (int)(camera.pixelWidth * renderScale);
            bufferSize.y = (int)(camera.pixelHeight * renderScale);
        } else
        {
            bufferSize.x = camera.pixelWidth;
            bufferSize.y = camera.pixelHeight;
        }

        buffer.BeginSample(SampleName);
        buffer.SetGlobalVector(bufferSizeId, new Vector4(
            1f / bufferSize.x, 1f / bufferSize.y,
            bufferSize.x, bufferSize.y
        ));
        ExecuteBuffer();
        lighting.Setup(
            context,
            cullingResults,
            shadowSettings,
            useLightsPerObject,
            cameraSettings.maskLights ? cameraSettings.renderingLayerMask : -1
        );
        postFXStack.Setup(
            context,
            camera,
            bufferSize,
            postFXSettings,
            useHDR,
            colorLUTResolution,
            cameraSettings.finalBlendMode,
            bufferSettings.bicubicRescaling
        );
        buffer.EndSample(SampleName);
        Setup();
        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing, useLightsPerObject, cameraSettings.renderingLayerMask);
        DrawUnsupportedShaders();
        DrawGizmosBeforeFX();
        if(postFXStack.IsActive)
        {
            postFXStack.Render(colorAttachmentId);
        }
        else if(useIntermediateBuffer)
        {
            DrawFinal(cameraSettings.finalBlendMode);
            ExecuteBuffer();
        }
        DrawGizmosAfterFX();

        // Ray Tracing
        // TODO: Remove from here
        if (rayTracingSettings.use)
        {
            if (rayTracingAccelerationStructure == null)
            {
                RayTracingAccelerationStructure.RASSettings settings = new RayTracingAccelerationStructure.RASSettings();
                settings.rayTracingModeMask = RayTracingAccelerationStructure.RayTracingModeMask.Everything;
                settings.managementMode = RayTracingAccelerationStructure.ManagementMode.Automatic;
                settings.layerMask = 255;

                rayTracingAccelerationStructure = new RayTracingAccelerationStructure(settings);
            }

            rayTracingAccelerationStructure.Build();

            buffer.GetTemporaryRT(Shader.PropertyToID("RayTracingRenderTarget"), camera.pixelWidth, camera.pixelHeight,
                0, FilterMode.Bilinear, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_SRGB, 1, true);
            buffer.SetRayTracingShaderPass(rayTracingSettings.shader, "PathTracing");
            buffer.SetRayTracingTextureParam(
                rayTracingSettings.shader,
                Shader.PropertyToID("RenderTarget"),
                Shader.PropertyToID("RayTracingRenderTarget"));
            buffer.SetRayTracingAccelerationStructure(
                rayTracingSettings.shader,
                Shader.PropertyToID("g_AccelStructure"),
                rayTracingAccelerationStructure);
            buffer.SetRayTracingFloatParam(
                rayTracingSettings.shader,
                Shader.PropertyToID("g_AspectRatio"),
                (float)camera.pixelWidth / (float)camera.pixelHeight);
            buffer.SetRayTracingTextureParam(
                rayTracingSettings.shader,
                Shader.PropertyToID("unity_SpecCube0"),
                rayTracingSettings.sky
            );

            buffer.DispatchRays(
                rayTracingSettings.shader,
                "MyRaygenShader",
                (uint)camera.pixelWidth,
                (uint)camera.pixelHeight,
                1,
                camera);

            Draw(Shader.PropertyToID("RayTracingRenderTarget"), BuiltinRenderTextureType.CameraTarget);
            buffer.ReleaseTemporaryRT(Shader.PropertyToID("RayTracingRenderTarget"));
        }

        Cleanup();
        Submit();
    }

    bool Cull(float maxShadowDistance)
    {
        renderScale = cameraSettings.GetRenderScale(bufferSettings.renderScale);
        useScaledRendering = renderScale < 0.99f || renderScale > 1.01f;
        PrepareBuffer();
        PrepareForSceneWindow();
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }

    void Setup()
    {
        context.SetupCameraProperties(camera);
        CameraClearFlags flags = camera.clearFlags;

        useIntermediateBuffer =
               useScaledRendering
            || useColorTexture
            || useDepthTexture
            || postFXStack.IsActive;

        if(useIntermediateBuffer)
        {
            if(flags > CameraClearFlags.Color)
            {
                flags = CameraClearFlags.Color;
            }
            buffer.GetTemporaryRT(colorAttachmentId, bufferSize.x, bufferSize.y,
                0, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            buffer.GetTemporaryRT(depthAttachmentId, bufferSize.x, bufferSize.y,
                32, FilterMode.Point, RenderTextureFormat.Depth);
            buffer.SetRenderTarget(
                colorAttachmentId,
                RenderBufferLoadAction.DontCare,
                RenderBufferStoreAction.Store,
                depthAttachmentId,
                RenderBufferLoadAction.DontCare,
                RenderBufferStoreAction.Store
            );
        }
        buffer.ClearRenderTarget(
            flags <= CameraClearFlags.Depth,
            flags == CameraClearFlags.Color,
            flags == CameraClearFlags.Color ?
                camera.backgroundColor.linear : Color.clear
        );
        buffer.BeginSample(SampleName);
        buffer.SetGlobalTexture(colorTextureId, missingTexture);
        buffer.SetGlobalTexture(depthTextureId, missingTexture);
        ExecuteBuffer();
    }

    void Cleanup()
    {
        lighting.Cleanup();
        if(useIntermediateBuffer)
        {
            buffer.ReleaseTemporaryRT(colorAttachmentId);
            buffer.ReleaseTemporaryRT(depthAttachmentId);

            if(useColorTexture)
            {
                buffer.ReleaseTemporaryRT(colorTextureId);
            }
            if(useDepthTexture)
            {
                buffer.ReleaseTemporaryRT(depthTextureId);
            }
        }
    }

    void CopyAttachments()
    {
        if(useColorTexture)
        {
            buffer.GetTemporaryRT(colorTextureId, bufferSize.x, bufferSize.y,
                0, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            if(copyTextureSupported)
            {
                buffer.CopyTexture(colorAttachmentId, colorTextureId);
            } else
            {
                Draw(colorAttachmentId, colorTextureId);
            }
        }

        if(useDepthTexture)
        {
            buffer.GetTemporaryRT(depthTextureId, bufferSize.x, bufferSize.y,
                32, FilterMode.Point, RenderTextureFormat.Depth);
            if(copyTextureSupported)
            {
                buffer.CopyTexture(depthAttachmentId, depthTextureId);
            } else
            {
                Draw(depthAttachmentId, depthTextureId, true);
                
            }
        }
        if(!copyTextureSupported)
        {
            buffer.SetRenderTarget(
                colorAttachmentId, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                depthAttachmentId, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
        }
        ExecuteBuffer();
    }

    void Submit()
    {
        buffer.EndSample(SampleName);
        ExecuteBuffer();
        context.Submit();
    }

    void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject, int renderingLayerMask)
    {
        PerObjectData lightsPerObjectFlags = useLightsPerObject ?
            PerObjectData.LightData | PerObjectData.LightIndices :
            PerObjectData.None;
        var sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };
        var drawingSettings = new DrawingSettings(
            unlitShaderTagId, sortingSettings
        ) {
            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useGPUInstancing,
            perObjectData = PerObjectData.Lightmaps |
                PerObjectData.ShadowMask |
                PerObjectData.LightProbe |
                PerObjectData.OcclusionProbe |
                PerObjectData.LightProbeProxyVolume |
                PerObjectData.OcclusionProbeProxyVolume |
                PerObjectData.ReflectionProbes |
                lightsPerObjectFlags
        };
        drawingSettings.SetShaderPassName(1, litShaderTag);
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque, renderingLayerMask: (uint)renderingLayerMask);

        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

        context.DrawSkybox(camera);
        if(useColorTexture || useDepthTexture)
        {
            CopyAttachments();
        }

        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;

        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }

    void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, bool isDepth = false)
    {
        buffer.SetGlobalTexture(sourceTextureId, from);
        buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.DrawProcedural(Matrix4x4.identity, material, isDepth ? 1 : 0, MeshTopology.Triangles, 3);
    }

    void DrawFinal(CameraSettings.FinalBlendMode finalBlendMode)
    {
        buffer.SetGlobalFloat(srcBlendId, (float)finalBlendMode.source);
        buffer.SetGlobalFloat(dstBlendId, (float)finalBlendMode.destination);
        buffer.SetGlobalTexture(sourceTextureId, colorAttachmentId);
        buffer.SetRenderTarget(
            BuiltinRenderTextureType.CameraTarget,
            finalBlendMode.destination == BlendMode.Zero ? RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load,
            RenderBufferStoreAction.Store
        );
        buffer.SetViewport(camera.pixelRect);
        buffer.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3);
        buffer.SetGlobalFloat(srcBlendId, 1f);
        buffer.SetGlobalFloat(dstBlendId, 0f);
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
}

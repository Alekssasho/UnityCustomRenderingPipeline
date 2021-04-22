using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using System;

public class CameraPathTracerRenderer : CameraRenderer
{
    RenderGraph renderGraph;

    RayTracingAccelerationStructure rayTracingAccelerationStructure = null;

    ComputeBuffer lightsBuffer = null;

    RenderTexture accumulationTexture = null;
    RTHandle accumulationHandle;

    uint accumulatedFrames = 0;

    Matrix4x4 lastCameraMatrix;

    struct Light
    {
        public Vector3 color;
        public Vector3 direction;
        public Vector3 position;
        public uint type;
    }

    public CameraPathTracerRenderer(Shader shader) : base(shader)
    {
        renderGraph = new RenderGraph("Path Tracing Render Graph");
    }

    public override void Dispose()
    {
        if (rayTracingAccelerationStructure != null)
        {
            rayTracingAccelerationStructure.Release();
            rayTracingAccelerationStructure = null;
        }

        if(lightsBuffer != null)
        {
            lightsBuffer.Release();
            lightsBuffer = null;
        }

        renderGraph.Cleanup();
        renderGraph = null;
        base.Dispose();
    }

    public override void Render(
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
    )
    {
        base.SetupForRender(
            context,
            camera,
            bufferSettings,
            useDynamicBatching,
            useGPUInstancing,
            useLightsPerObject,
            shadowSettings,
            postFXSettings,
            colorLUTResolution,
            rayTracingSettings);

        if (rayTracingAccelerationStructure == null)
        {
            RayTracingAccelerationStructure.RASSettings settings = new RayTracingAccelerationStructure.RASSettings();
            settings.rayTracingModeMask = RayTracingAccelerationStructure.RayTracingModeMask.Everything;
            settings.managementMode = RayTracingAccelerationStructure.ManagementMode.Automatic;
            settings.layerMask = 255;

            rayTracingAccelerationStructure = new RayTracingAccelerationStructure(settings);
        }

        rayTracingAccelerationStructure.Build();

        if (lightsBuffer == null || lightsBuffer.count != cullingResults.visibleLights.Length)
        {
            if(lightsBuffer != null)
            {
                lightsBuffer.Release();
            }
            lightsBuffer = new ComputeBuffer(cullingResults.visibleLights.Length, 3 * 3 * 4 + 4);
        }

        if(accumulationTexture == null || accumulationTexture.width != camera.pixelWidth || accumulationTexture.height != camera.pixelHeight)
        {
            accumulationTexture = new RenderTexture(camera.pixelWidth, camera.pixelHeight, 0);
            accumulationHandle = RTHandles.Alloc(accumulationTexture);
        }

        Light[] lights = new Light[cullingResults.visibleLights.Length];
        for(int i = 0; i < cullingResults.visibleLights.Length; i++)
        {
            VisibleLight visibleLight = cullingResults.visibleLights[i];
            lights[i] = new Light()
            {
                color = new Vector3(visibleLight.finalColor.r, visibleLight.finalColor.g, visibleLight.finalColor.b),
                direction = visibleLight.localToWorldMatrix.GetColumn(2),
                position = new Vector3(0.0f, 0.0f, 0.0f),
                type = 0,
            };
        }

        lightsBuffer.SetData(lights);

        if(lastCameraMatrix != camera.cameraToWorldMatrix)
        {
            accumulatedFrames = 0;
        }

        var renderGraphParams = new RenderGraphParameters()
        {
            scriptableRenderContext = context,
            commandBuffer = buffer,
            currentFrameIndex = 0
        };
        renderGraph.Begin(renderGraphParams);

        var desc = new TextureDesc(camera.pixelWidth, camera.pixelHeight);
        desc.enableRandomWrite = true;
        desc.colorFormat = GraphicsFormat.R8G8B8A8_SRGB;
        TextureHandle rtResult = renderGraph.CreateTexture(desc);

        TextureHandle accumulatedResult = renderGraph.ImportTexture(accumulationHandle);
        TextureHandle backbuffer = renderGraph.ImportBackbuffer(BuiltinRenderTextureType.CameraTarget);

        AddRTPass(renderGraph, rtResult, rayTracingSettings, camera, accumulatedFrames);
        AddAccumulatePass(renderGraph, rtResult, accumulatedResult, accumulatedFrames);
        AddDrawPass(renderGraph, accumulatedResult, backbuffer);

        renderGraph.Execute();
        renderGraph.EndFrame();

        base.DrawUnsupportedShadersNonPartial();
        base.DrawGizmos();

        Cleanup();
        Submit();

        accumulatedFrames++;
        lastCameraMatrix = camera.cameraToWorldMatrix;
    }

    class AccumulatePassData
    {
        public TextureHandle srcTexture;
        public TextureHandle dstTexture;
        public uint frameIndex;
    }

    private void AddAccumulatePass(RenderGraph renderGraph, TextureHandle rtResult, TextureHandle accumulatedResult, uint frameIndex)
    {
        using (var builder = renderGraph.AddRenderPass<AccumulatePassData>("Accumulate Path Tracing Pass", out var passData))
        {
            passData.srcTexture = builder.ReadTexture(rtResult);
            passData.dstTexture = builder.WriteTexture(accumulatedResult);
            passData.frameIndex = frameIndex;

            builder.SetRenderFunc((AccumulatePassData data, RenderGraphContext ctx) =>
            {
                var materialPropertyBlock = ctx.renderGraphPool.GetTempMaterialPropertyBlock();
                materialPropertyBlock.SetTexture("_SourceTexture", data.srcTexture);
                materialPropertyBlock.SetFloat("g_FrameIndex", passData.frameIndex);

                ctx.cmd.SetRenderTarget(data.dstTexture);
                ctx.cmd.DrawProcedural(Matrix4x4.identity, material, 2, MeshTopology.Triangles, 3, 1, materialPropertyBlock);
            });
        };
    }

    class DrawPassData
    {
        public TextureHandle srcTexture;
        public TextureHandle dstTexture;
    }

    void AddDrawPass(RenderGraph renderGraph, TextureHandle srcTexture, TextureHandle dstTexture)
    {
        using (var builder = renderGraph.AddRenderPass<DrawPassData>("Draw Pass", out var passData))
        {
            passData.srcTexture = builder.ReadTexture(srcTexture);
            passData.dstTexture = builder.WriteTexture(dstTexture);

            builder.SetRenderFunc((DrawPassData data, RenderGraphContext ctx) =>
            {
                var materialPropertyBlock = ctx.renderGraphPool.GetTempMaterialPropertyBlock();
                materialPropertyBlock.SetTexture("_SourceTexture", data.srcTexture);

                // This for some reason don't work if set from materialPropertyBlock
                ctx.cmd.SetGlobalFloat(Shader.PropertyToID("_CameraSrcBlend"), (float)BlendMode.One);
                ctx.cmd.SetGlobalFloat(Shader.PropertyToID("_CameraDstBlend"), (float)BlendMode.Zero);

                ctx.cmd.SetRenderTarget(
                    data.dstTexture,
                    RenderBufferLoadAction.DontCare,
                    RenderBufferStoreAction.Store);
                ctx.cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1, materialPropertyBlock);
            });
        };
    }

    class RTPassData
    {
        public TextureHandle dstTexture;
        public RayTracingShader shader;
        public Cubemap sky;
        public float aspectRatio;
        public ComputeBuffer lightsBuffer;
        public uint frameIndex;
    }
    void AddRTPass(RenderGraph renderGraph, TextureHandle dstTexture, RayTracingSettings rtSettings, Camera camera, uint frameIndex)
    {
        using (var builder = renderGraph.AddRenderPass<RTPassData>("RT Pass", out var passData))
        {
            passData.shader = rtSettings.shader;
            passData.sky = rtSettings.sky;
            passData.dstTexture = builder.WriteTexture(dstTexture);
            passData.aspectRatio = (float)camera.pixelWidth / (float)camera.pixelHeight;
            passData.lightsBuffer = lightsBuffer;
            passData.frameIndex = frameIndex;

            builder.SetRenderFunc((RTPassData data, RenderGraphContext ctx) =>
            {
                ctx.cmd.SetRayTracingShaderPass(data.shader, "PathTracing");
                ctx.cmd.SetRayTracingTextureParam(
                    data.shader,
                    Shader.PropertyToID("RenderTarget"),
                    data.dstTexture);
                ctx.cmd.SetRayTracingAccelerationStructure(
                    data.shader,
                    Shader.PropertyToID("g_AccelStructure"),
                    rayTracingAccelerationStructure);
                ctx.cmd.SetRayTracingFloatParam(
                    data.shader,
                    Shader.PropertyToID("g_AspectRatio"),
                    data.aspectRatio);
                ctx.cmd.SetRayTracingTextureParam(
                    data.shader,
                    Shader.PropertyToID("unity_SpecCube0"),
                    data.sky
                );
                ctx.cmd.SetRayTracingFloatParam(
                    data.shader,
                    Shader.PropertyToID("g_FrameSeed"),
                    data.frameIndex);
                data.shader.SetBuffer(Shader.PropertyToID("g_Lights"), data.lightsBuffer);

                ctx.cmd.DispatchRays(
                    data.shader,
                    "MyRaygenShader",
                    (uint)camera.pixelWidth,
                    (uint)camera.pixelHeight,
                    1);
            });
        };
    }
}

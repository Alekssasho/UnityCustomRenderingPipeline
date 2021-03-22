using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

public class CameraPathTracerRenderer : CameraRenderer
{
    RenderGraph renderGraph;

    RayTracingAccelerationStructure rayTracingAccelerationStructure = null;

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

        //buffer.GetTemporaryRT(Shader.PropertyToID("RayTracingRenderTarget"), camera.pixelWidth, camera.pixelHeight,
        //    0, FilterMode.Bilinear, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_SRGB, 1, true);
        //buffer.SetRayTracingShaderPass(rayTracingSettings.shader, "PathTracing");
        //buffer.SetRayTracingTextureParam(
        //    rayTracingSettings.shader,
        //    Shader.PropertyToID("RenderTarget"),
        //    Shader.PropertyToID("RayTracingRenderTarget"));
        //buffer.SetRayTracingAccelerationStructure(
        //    rayTracingSettings.shader,
        //    Shader.PropertyToID("g_AccelStructure"),
        //    rayTracingAccelerationStructure);
        //buffer.SetRayTracingFloatParam(
        //    rayTracingSettings.shader,
        //    Shader.PropertyToID("g_AspectRatio"),
        //    (float)camera.pixelWidth / (float)camera.pixelHeight);
        //buffer.SetRayTracingTextureParam(
        //    rayTracingSettings.shader,
        //    Shader.PropertyToID("unity_SpecCube0"),
        //    rayTracingSettings.sky
        //);

        //buffer.DispatchRays(
        //    rayTracingSettings.shader,
        //    "MyRaygenShader",
        //    (uint)camera.pixelWidth,
        //    (uint)camera.pixelHeight,
        //    1,
        //    camera);

        //ExecuteBuffer();

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

        AddRTPass(renderGraph, rtResult, rayTracingSettings, camera);
        AddDrawPass(renderGraph, rtResult);
        renderGraph.Execute();
        renderGraph.EndFrame();

        //Draw(Shader.PropertyToID("RayTracingRenderTarget"), BuiltinRenderTextureType.CameraTarget);
        //buffer.ReleaseTemporaryRT(Shader.PropertyToID("RayTracingRenderTarget"));

        base.DrawUnsupportedShadersNonPartial();
        base.DrawGizmos();

        Cleanup();
        Submit();
    }


    class DrawPassData
    {
        public TextureHandle srcTexture;
        public TextureHandle dstTexture;
    }

    void AddDrawPass(RenderGraph renderGraph, TextureHandle srcTexture)
    {
        using (var builder = renderGraph.AddRenderPass<DrawPassData>("Draw Pass", out var passData))
        {
            passData.srcTexture = builder.ReadTexture(srcTexture);

            builder.SetRenderFunc((DrawPassData data, RenderGraphContext ctx) =>
            {
                var materialPropertyBlock = ctx.renderGraphPool.GetTempMaterialPropertyBlock();
                materialPropertyBlock.SetTexture("_SourceTexture", data.srcTexture);

                CoreUtils.SetRenderTarget(
                    ctx.cmd,
                    BuiltinRenderTextureType.CameraTarget,
                    RenderBufferLoadAction.DontCare,
                    RenderBufferStoreAction.Store,
                    ClearFlag.None,
                    Color.black);
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
    }
    void AddRTPass(RenderGraph renderGraph, TextureHandle dstTexture, RayTracingSettings rtSettings, Camera camera)
    {
        using (var builder = renderGraph.AddRenderPass<RTPassData>("RT Pass", out var passData))
        {
            passData.shader = rtSettings.shader;
            passData.sky = rtSettings.sky;
            passData.dstTexture = builder.WriteTexture(dstTexture);
            passData.aspectRatio = (float)camera.pixelWidth / (float)camera.pixelHeight;

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

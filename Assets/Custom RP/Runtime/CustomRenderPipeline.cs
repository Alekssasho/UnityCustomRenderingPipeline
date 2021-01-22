using UnityEngine;
using UnityEngine.Rendering;

public partial class CustomRenderPipeline : RenderPipeline
{
    CameraRenderer renderer;
    bool useDynamicBatching, useGPUinstancing, useLightsPerObject;
    ShadowSettings shadowSettings;
    PostFXSettings postFXSettings;
    CameraBufferSettings cameraBufferSettings;
    int colorLUTResolution;

    public CustomRenderPipeline(
        CameraBufferSettings cameraBufferSettings,
        bool useDynamicBatching, bool useGPUinstancing, bool useSRPBatcher,
        bool useLightsPerObject, ShadowSettings shadowSettings,
        PostFXSettings postFXSettings,
        int colorLUTResolution,
        Shader cameraRendererShader
    ) {
        this.cameraBufferSettings = cameraBufferSettings;
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUinstancing = useGPUinstancing;
        this.shadowSettings = shadowSettings;
        this.useLightsPerObject = useLightsPerObject;
        this.postFXSettings = postFXSettings;
        this.colorLUTResolution = colorLUTResolution;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        GraphicsSettings.lightsUseLinearIntensity = true;

        InitializeForEditor();

        renderer = new CameraRenderer(cameraRendererShader);
    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach (Camera camera in cameras)
        {
            renderer.Render(context, camera, cameraBufferSettings, useDynamicBatching, useGPUinstancing, useLightsPerObject,
                shadowSettings, postFXSettings, colorLUTResolution);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        DisposeForEditor();
        renderer.Dispose();
    }

}

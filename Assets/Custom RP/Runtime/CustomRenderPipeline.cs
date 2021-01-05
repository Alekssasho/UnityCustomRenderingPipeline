using UnityEngine;
using UnityEngine.Rendering;

public partial class CustomRenderPipeline : RenderPipeline
{
    CameraRenderer renderer = new CameraRenderer();
    bool useDynamicBatching, useGPUinstancing, useLightsPerObject;
    ShadowSettings shadowSettings;
    PostFXSettings postFXSettings;

    public CustomRenderPipeline(
        bool useDynamicBatching, bool useGPUinstancing, bool useSRPBatcher,
        bool useLightsPerObject, ShadowSettings shadowSettings,
        PostFXSettings postFXSettings
    ) {
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUinstancing = useGPUinstancing;
        this.shadowSettings = shadowSettings;
        this.useLightsPerObject = useLightsPerObject;
        this.postFXSettings = postFXSettings;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        GraphicsSettings.lightsUseLinearIntensity = true;

        InitializeForEditor();
    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach (Camera camera in cameras)
        {
            renderer.Render(context, camera, useDynamicBatching, useGPUinstancing, useLightsPerObject,
                shadowSettings, postFXSettings);
        }
    }

}

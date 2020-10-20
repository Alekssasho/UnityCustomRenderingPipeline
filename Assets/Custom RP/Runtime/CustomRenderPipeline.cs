using UnityEngine;
using UnityEngine.Rendering;

public class CustomRenderPipeline : RenderPipeline
{
    CameraRenderer renderer = new CameraRenderer();
    bool useDynamicBatching, useGPUinstancing;
    ShadowSettings shadowSettings;

    public CustomRenderPipeline(
        bool useDynamicBatching, bool useGPUinstancing, bool useSRPBatcher,
        ShadowSettings shadowSettings
    ) {
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUinstancing = useGPUinstancing;
        this.shadowSettings = shadowSettings;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        GraphicsSettings.lightsUseLinearIntensity = true;
    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach (Camera camera in cameras)
        {
            renderer.Render(context, camera, useDynamicBatching, useGPUinstancing,
                shadowSettings);
        }
    }

}

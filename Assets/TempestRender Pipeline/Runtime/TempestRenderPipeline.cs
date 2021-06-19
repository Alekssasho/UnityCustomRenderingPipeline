using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

public class TempestRenderPipeline : RenderPipeline
{
    public TempestRenderPipeline()
    {

    }

    static ShaderTagId mainOpaqueShader = new ShaderTagId("TempestMainOpaque");

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach (Camera camera in cameras)
        {
            camera.TryGetCullingParameters(out ScriptableCullingParameters p);
            CullingResults cullingResults = context.Cull(ref p);

            context.SetupCameraProperties(camera);

            var sortingSettings = new SortingSettings(camera)
            {
                criteria = SortingCriteria.CommonOpaque
            };
            var drawingSettings = new DrawingSettings(mainOpaqueShader, sortingSettings);
            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
            context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
            context.DrawSkybox(camera);

#if UNITY_EDITOR
            DrawUnsupportedShaders(camera, context, cullingResults);
            if (Handles.ShouldRenderGizmos())
            {
                context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
                context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
            }
#endif
            context.Submit();
        }
    }

#if UNITY_EDITOR
    static Material errorMaterial;

    static ShaderTagId[] legacyShaderTagIds =
    {
        new ShaderTagId("Always"),
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM"),
        new ShaderTagId("CustomLit"),
        new ShaderTagId("SRPDefaultUnlit"),
    };

    void DrawUnsupportedShaders(Camera camera, ScriptableRenderContext context, CullingResults cullingResults)
    {
        if (errorMaterial == null)
        {
            errorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
        }

        var drawingSettings = new DrawingSettings(legacyShaderTagIds[0], new SortingSettings(camera))
        {
            overrideMaterial = errorMaterial
        };
        for (int i = 1; i < legacyShaderTagIds.Length; i++)
        {
            drawingSettings.SetShaderPassName(i, legacyShaderTagIds[i]);
        }
        var filteringSettings = FilteringSettings.defaultValue;
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }
#endif
}

using System;
using UnityEngine.Rendering;
using UnityEngine;

[Serializable]
public class CameraSettings
{
    [Serializable]
    public struct FinalBlendMode
    {
        public BlendMode source, destination;
    }

    public FinalBlendMode finalBlendMode = new FinalBlendMode
    {
        source = BlendMode.One,
        destination = BlendMode.Zero
    };

    [RenderingLayerMaskField]
    public int renderingLayerMask = -1;

    public bool maskLights = false;

    public bool overridePostFX = false;
    public PostFXSettings postFXSettings = default;

    public bool copyColor = true;
    public bool copyDepth = true;

    public enum RenderScaleMode { Inherit, Multiply, Override }
    public RenderScaleMode renderScaleMode = RenderScaleMode.Inherit;

    [Range(CameraRenderer.renderScaleMin, CameraRenderer.renderScaleMax)]
    public float renderScale = 1f;

    public float GetRenderScale(float scale)
    {
        return renderScaleMode == RenderScaleMode.Inherit ? scale :
            renderScaleMode == RenderScaleMode.Override ? renderScale :
            scale * renderScale;
    }
}

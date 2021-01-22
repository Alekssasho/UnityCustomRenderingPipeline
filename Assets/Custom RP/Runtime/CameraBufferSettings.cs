using UnityEngine;

[System.Serializable]
public class CameraBufferSettings
{
    public bool allowHDR;
    public bool copyColor;
    public bool copyColorReflection;
    public bool copyDepth;
    public bool copyDepthReflections;

    [Range(CameraRenderer.renderScaleMin, CameraRenderer.renderScaleMax)]
    public float renderScale;

    public enum BicubicRescalingMode { Off, UpOnly, UpAndDown }
    public BicubicRescalingMode bicubicRescaling;
}

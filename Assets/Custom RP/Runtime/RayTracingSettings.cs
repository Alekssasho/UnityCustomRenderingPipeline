using UnityEngine;
using UnityEngine.Experimental.Rendering;

[System.Serializable]
public class RayTracingSettings
{
    public bool use = false;
    public RayTracingShader shader;
    public Cubemap sky;
}
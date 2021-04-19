#ifndef CUSTOM_PATH_TRACING_PASS_INCLUDED
#define CUSTOM_PATH_TRACING_PASS_INCLUDED

// Engine includes
#include "UnityRaytracingMeshUtils.cginc"
#include "../ShaderLibrary/CommonPathTracing.hlsl"

struct AttributeData
{
	float2 barycentrics;
};

struct Vertex
{
    float3 position;
    float3 normal;
    float2 uv;
};

Vertex FetchVertex(uint vertexIndex)
{
    Vertex v;
    v.position = UnityRayTracingFetchVertexAttribute3(vertexIndex, kVertexAttributePosition);
    v.normal = UnityRayTracingFetchVertexAttribute3(vertexIndex, kVertexAttributeNormal);
    v.uv = UnityRayTracingFetchVertexAttribute2(vertexIndex, kVertexAttributeTexCoord0);
    return v;
}

Vertex InterpolateVertices(Vertex v0, Vertex v1, Vertex v2, float3 barycentrics)
{
    Vertex v;
#define INTERPOLATE_ATTRIBUTE(attr) v.attr = v0.attr * barycentrics.x + v1.attr * barycentrics.y + v2.attr * barycentrics.z
    INTERPOLATE_ATTRIBUTE(position);
    INTERPOLATE_ATTRIBUTE(normal);
    INTERPOLATE_ATTRIBUTE(uv);
    return v;
}

[shader("closesthit")]
void ClosestHitMain(inout PathTracingRayPayload payload : SV_RayPayload, AttributeData attribs : SV_IntersectionAttributes)
{
	uint3 triangleIndices = UnityRayTracingFetchTriangleIndices(PrimitiveIndex());
    Vertex v0, v1, v2;
    v0 = FetchVertex(triangleIndices.x);
    v1 = FetchVertex(triangleIndices.y);
    v2 = FetchVertex(triangleIndices.z);

    float3 barycentricCoords = float3(1.0 - attribs.barycentrics.x - attribs.barycentrics.y, attribs.barycentrics.x, attribs.barycentrics.y);
    Vertex v = InterpolateVertices(v0, v1, v2, barycentricCoords);

    v.uv = TransformBaseUV(v.uv);
    float4 map = _BaseMap.SampleLevel(sampler_BaseMap, v.uv, 0);
    float4 color = INPUT_PROP(_BaseColor);
    payload.color = (map * color).xyz;
    payload.normal = v.normal;

    payload.hitType = HitType::LitSurface;
}

#endif
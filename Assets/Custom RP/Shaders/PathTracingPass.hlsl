#ifndef CUSTOM_PATH_TRACING_PASS_INCLUDED
#define CUSTOM_PATH_TRACING_PASS_INCLUDED

struct RayPayload {
	float4 color;
};

struct AttributeData
{
	float2 barycentrics;
};

[shader("closesthit")]
void ClosestHitMain(inout RayPayload payload : SV_RayPayload, AttributeData attribs : SV_IntersectionAttributes)
{
	float4 color = INPUT_PROP(_BaseColor);
	payload.color = color;
}

#endif
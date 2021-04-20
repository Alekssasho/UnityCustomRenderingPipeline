#ifndef CUSTOM_COMMON_PATH_TRACING_INCLUDED
#define CUSTOM_COMMON_PATH_TRACING_INCLUDED

enum HitType : uint
{
	Environment = 0,
	LitSurface = 1,
};

struct PathTracingRayPayload {
	float3 color;
	float3 normal;
	float3 position;
	uint hitType;
};

struct PathTracingShadowRayPayload
{
	bool hit;
};

#endif
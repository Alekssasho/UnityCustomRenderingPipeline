#ifndef CUSTOM_BRDF_INCLUDED
#define CUSTOM_BRDF_INCLUDED

struct BRDF
{
	float3 diffuse;
	float3 specular;
	float roughness;
};

#define MIN_REFLECTIVITY 0.04

float OneMinusReflectivity(float metallic)
{
	float range = 1.0 - MIN_REFLECTIVITY;
	return range - metallic * range;
}

BRDF GetBRDF(Surface surface)
{
	float oneMinusReflectivity = OneMinusReflectivity(surface.metallic);
	float perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(surface.smoothness);
	
	BRDF brdf;
	brdf.diffuse = surface.color * oneMinusReflectivity;
	brdf.specular = lerp(MIN_REFLECTIVITY, surface.color, surface.metallic);
	brdf.roughness = PerceptualRoughnessToRoughness(perceptualRoughness);
	return brdf;
}

#endif
#ifndef GENSHIN_RECOVERED_LIGHTING_INCLUDED
#define GENSHIN_RECOVERED_LIGHTING_INCLUDED

float3 GenshinRawNormal(float4 value, float strength)
{
    float2 xy = (value.rg * 2 - 1) * strength;
    return float3(xy, sqrt(1 - saturate(dot(xy, xy))));
}

float3 GenshinReorientNormal(float3 baseNormal, float3 detailNormal)
{
    float3 t = baseNormal + float3(0, 0, 1);
    float3 u = detailNormal * float3(-1, -1, 1);
    return normalize(t * dot(t, u) - u * t.z);
}

float3 GenshinLayerBlend(float3 baseValue, float3 layerValue, float weight, float method)
{
    // Native mode 0 interpolates, mode 1 multiplies, and mode 2 selects the darker sample.
    if (method > 1.5)
    {
        float3 luma = float3(0.0396819152, 0.45802179, 0.00609653955);
        layerValue = dot(baseValue, luma) < dot(layerValue, luma) ? baseValue : layerValue;
    }
    else
        layerValue *= lerp(float3(1, 1, 1), baseValue, method);
    return lerp(baseValue, layerValue, weight);
}

float3 GenshinSourceSpecular(float3 f0, float smoothness, float nh, float lh)
{
    // Forward programs cap the GGX distribution at 12 before the Fresnel contribution.
    float roughness = max(0.001, 1 - smoothness);
    float roughness4 = roughness * roughness;
    roughness4 *= roughness4;
    float denominator = nh * nh * (roughness4 - 1) + 1;
    float distribution = min(12, roughness4 / max(denominator * denominator, 1e-8) * 0.318309873);
    float grazing = pow(1 - lh, 5) * (2 - 2 * roughness4);
    return distribution * (f0 + (1 - f0) * grazing);
}

float3 GenshinSourceLighting(float3 diffuse, float3 f0, float smoothness,
    float3 normal, float3 worldPosition, float attenuation)
{
    float3 light = normalize(UnityWorldSpaceLightDir(worldPosition));
    float3 view = normalize(UnityWorldSpaceViewDir(worldPosition));
    float3 halfDirection = normalize(light + view);
    float nl = saturate(dot(normal, light));
    float nh = saturate(dot(normal, halfDirection));
    float lh = saturate(dot(light, halfDirection));
    float3 specular = GenshinSourceSpecular(f0, smoothness, nh, lh);
    // Unity SH and shadow coordinates bridge client-specific scene lighting buffers.
    float3 indirect = max(0, ShadeSH9(float4(normal, 1)));
    return (diffuse + f0) * indirect + (diffuse + specular) * _LightColor0.rgb * nl * attenuation;
}
#endif

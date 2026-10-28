#ifndef GENSHIN_NATIVE_VEGETATION_CORE_INCLUDED
#define GENSHIN_NATIVE_VEGETATION_CORE_INCLUDED

sampler2D _Noisemap;
float _GrassTime, _WindStrength, _WindSpeed, _NoiseScale;
float _ScaleRandom, _ScaleShift, _ScaleUnit;
float _VegetationBend, _PetalFlat, _Billboard, _CustomRotate;
float4 _LargeWindParam, _SmallWindParam, _GlobalWind;

// Shared by source shaders -2259022730218094418 / -150854655841348770.
// UV0 is the blade root XZ; UV1 stores the per-blade phase and bend weight.
float3 GenshinNativeDeform(float3 position, float3 root, float2 windData,
    float matrixYToX, float2 objectAxis, float2 cameraRight)
{
    float3 offset = position - root;
    objectAxis *= rsqrt(max(0.000001, dot(objectAxis, objectAxis)));
    cameraRight *= rsqrt(max(0.000001, dot(cameraRight, cameraRight)));
    float2 facing = cameraRight * _Billboard + float2(0, -_CustomRotate);
    float across = offset.x * objectAxis.x - offset.z * objectAxis.y;
    float along = dot(offset.zx, objectAxis);
    float2 rotated = float2(across * facing.y - along * facing.x,
        across * facing.x + along * facing.y) + offset.xz;
    offset.xz -= rotated * (_Billboard + _CustomRotate);

    float phase = frac(matrixYToX - windData.x);
    float variation = ((phase - 0.5) * _ScaleRandom + 1) * _ScaleUnit + _ScaleShift;
    float weight = variation * saturate(windData.y);
    float3 direction = _GlobalWind.xyz * rsqrt(max(0.001, dot(_GlobalWind.xyz, _GlobalWind.xyz)));
    float time = _LargeWindParam.x + _GrassTime * _WindSpeed;
    float2 noiseUV = root.xz / max(0.001, _NoiseScale) - time * direction.xz;
    float3 noise = tex2Dlod(_Noisemap, float4(noiseUV, 0, 0)).rgb - 0.5;
    float2 drift = noise.xy + direction.xz * (noise.z + _VegetationBend);
    drift *= _LargeWindParam.y * _LargeWindParam.z * weight * _WindStrength;

    float lengthBeforeBending = length(offset);
    float3 petal = offset * float3(1 - _PetalFlat, 1, 1 - _PetalFlat);
    float3 bent = petal + float3(drift.x, 0, drift.y);
    bent *= lengthBeforeBending * rsqrt(max(0.001, dot(bent, bent)));
    float3 result = offset + bent - petal;
    // The source scales height linearly and narrows XZ with a squared correction.
    result.y += (variation - 1) * offset.y;
    result.xz -= min(1, (variation - 1) * (variation - 1)) * offset.xz;
    return root + result;
}

float3 GenshinNativeSurfaceNormal(float seed, float amount)
{
    float2 jitter = (frac(seed * float2(22, 33)) - 0.5) * amount;
    return normalize(float3(jitter.x, 1, jitter.y));
}

float3 GenshinNativeGrassColor(float3 root, float3 tip, float blend, float height,
    float tipLighting, float death)
{
    float3 color = lerp(root, tip, blend);
    // Source grass PS28 applies the fixed withered color before height-dependent tip gain.
    color = lerp(color, float3(0.0700690001, 0.0560160019, 0.0378829986), saturate(death));
    return color * (1 + tipLighting * saturate(height - 0.6));
}

float3 GenshinNativeFlowerColor(float3 textureColor, float3 tint, float strength, float death)
{
    float3 color = textureColor * lerp(float3(1, 1, 1), tint, strength);
    // Flower PS83 preserves source brightness while transitioning to the withered palette.
    float3 withered = (length(color) * 0.8 + 0.6) * float3(0.0700690001, 0.0560160019, 0.0378829986);
    return lerp(color, withered, saturate(death));
}

#endif

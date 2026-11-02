Shader "Game/Terrain/Genshin Recovered Terrain"
{
    Properties
    {
        [NoScaleOffset] _Control0 ("Original Layer Weights 1-4", 2D) = "red" {}
        [NoScaleOffset] _Control1 ("Original Layer Weights 5-7", 2D) = "black" {}
        [NoScaleOffset] _TerrainTint ("Original Terrain Tint", 2D) = "white" {}
        _SourceOrigin ("Original World Origin", Vector) = (2048, 0, -1024, 0)
        _TintEnhanceFactor ("Terrain Tint Multiplier", Float) = 1
        [NoScaleOffset] _Texture_1_Albedo ("1 Grass Albedo / Height", 2D) = "white" {}
        [NoScaleOffset] _Texture_2_Albedo ("2 Dry Soil Albedo / Height", 2D) = "white" {}
        [NoScaleOffset] _Texture_3_Albedo ("3 Wet Soil Albedo / Height", 2D) = "white" {}
        [NoScaleOffset] _Texture_4_Albedo ("4 Gravel Albedo / Height", 2D) = "white" {}
        [NoScaleOffset] _Texture_5_Albedo ("5 Sand Albedo / Height", 2D) = "white" {}
        [NoScaleOffset] _Texture_6_Albedo ("6 Dirt Albedo / Height", 2D) = "white" {}
        [NoScaleOffset] _Texture_7_Albedo ("7 Stone Albedo / Height", 2D) = "white" {}
        [NoScaleOffset] _Texture_1_Normal ("1 Packed Normal / Height / Smoothness", 2D) = "gray" {}
        [NoScaleOffset] _Texture_2_Normal ("2 Packed Normal / Height / Smoothness", 2D) = "gray" {}
        [NoScaleOffset] _Texture_3_Normal ("3 Packed Normal / Height / Smoothness", 2D) = "gray" {}
        [NoScaleOffset] _Texture_4_Normal ("4 Packed Normal / Height / Smoothness", 2D) = "gray" {}
        [NoScaleOffset] _Texture_5_Normal ("5 Packed Normal / Height / Smoothness", 2D) = "gray" {}
        [NoScaleOffset] _Texture_6_Normal ("6 Packed Normal / Height / Smoothness", 2D) = "gray" {}
        [NoScaleOffset] _Texture_7_Normal ("7 Packed Normal / Height / Smoothness", 2D) = "gray" {}
        _Texture_1_Tiling ("1 World Tile Size", Float) = 8
        _Texture_2_Tiling ("2 World Tile Size", Float) = 8
        _Texture_3_Tiling ("3 World Tile Size", Float) = 8
        _Texture_4_Tiling ("4 World Tile Size", Float) = 8
        _Texture_5_Tiling ("5 World Tile Size", Float) = 14
        _Texture_6_Tiling ("6 World Tile Size", Float) = 6
        _Texture_7_Tiling ("7 World Tile Size", Float) = 16
        _Texture_1_Normal_Power ("1 Normal Power", Float) = 1
        _Texture_2_Normal_Power ("2 Normal Power", Float) = 1
        _Texture_3_Normal_Power ("3 Normal Power", Float) = 1
        _Texture_4_Normal_Power ("4 Normal Power", Float) = 1
        _Texture_5_Normal_Power ("5 Normal Power", Float) = 0.4
        _Texture_6_Normal_Power ("6 Normal Power", Float) = 1
        _Texture_7_Normal_Power ("7 Normal Power", Float) = 1
        [Toggle] _Texture_1_TintEnabled ("1 Use Terrain Tint", Float) = 1
        [Toggle] _Texture_2_TintEnabled ("2 Use Terrain Tint", Float) = 0
        [Toggle] _Texture_3_TintEnabled ("3 Use Terrain Tint", Float) = 0
        [Toggle] _Texture_4_TintEnabled ("4 Use Terrain Tint", Float) = 0
        [Toggle] _Texture_5_TintEnabled ("5 Use Terrain Tint", Float) = 1
        [Toggle] _Texture_6_TintEnabled ("6 Use Terrain Tint", Float) = 0
        [Toggle] _Texture_7_TintEnabled ("7 Use Terrain Tint", Float) = 0
        _Terrain_Smoothness ("Terrain Smoothness", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            #include "GenshinRecoveredLighting.cginc"

            // Share repeat/clamp samplers while retaining each texture's color space.
            // Separate samplers for all seven albedo/normal pairs exceed D3D11's limit.
            UNITY_DECLARE_TEX2D(_Control0);
            UNITY_DECLARE_TEX2D_NOSAMPLER(_Control1);
            UNITY_DECLARE_TEX2D_NOSAMPLER(_TerrainTint);
            UNITY_DECLARE_TEX2D(_Texture_1_Albedo);
            UNITY_DECLARE_TEX2D_NOSAMPLER(_Texture_2_Albedo);
            UNITY_DECLARE_TEX2D_NOSAMPLER(_Texture_3_Albedo);
            UNITY_DECLARE_TEX2D_NOSAMPLER(_Texture_4_Albedo);
            UNITY_DECLARE_TEX2D_NOSAMPLER(_Texture_5_Albedo);
            UNITY_DECLARE_TEX2D_NOSAMPLER(_Texture_6_Albedo);
            UNITY_DECLARE_TEX2D_NOSAMPLER(_Texture_7_Albedo);
            UNITY_DECLARE_TEX2D_NOSAMPLER(_Texture_1_Normal);
            UNITY_DECLARE_TEX2D_NOSAMPLER(_Texture_2_Normal);
            UNITY_DECLARE_TEX2D_NOSAMPLER(_Texture_3_Normal);
            UNITY_DECLARE_TEX2D_NOSAMPLER(_Texture_4_Normal);
            UNITY_DECLARE_TEX2D_NOSAMPLER(_Texture_5_Normal);
            UNITY_DECLARE_TEX2D_NOSAMPLER(_Texture_6_Normal);
            UNITY_DECLARE_TEX2D_NOSAMPLER(_Texture_7_Normal);
            float4 _SourceOrigin;
            float _TintEnhanceFactor, _Terrain_Smoothness;
            float _GenshinGroundCaptureMode;
            float4x4 _GenshinGroundCaptureView, _GenshinGroundCaptureVP;
            float _Texture_1_Tiling, _Texture_2_Tiling, _Texture_3_Tiling, _Texture_4_Tiling;
            float _Texture_5_Tiling, _Texture_6_Tiling, _Texture_7_Tiling;
            float _Texture_1_Normal_Power, _Texture_2_Normal_Power, _Texture_3_Normal_Power;
            float _Texture_4_Normal_Power, _Texture_5_Normal_Power, _Texture_6_Normal_Power, _Texture_7_Normal_Power;
            float _Texture_1_TintEnabled, _Texture_2_TintEnabled, _Texture_3_TintEnabled;
            float _Texture_4_TintEnabled, _Texture_5_TintEnabled, _Texture_6_TintEnabled, _Texture_7_TintEnabled;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float3 worldTangent : TEXCOORD3;
                float3 worldBitangent : TEXCOORD4;
                SHADOW_COORDS(5)
                UNITY_FOG_COORDS(6)
            };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                // Offscreen material data uses the owning camera's explicit projection.
                if (_GenshinGroundCaptureMode > 0.5)
                    o.pos = mul(_GenshinGroundCaptureVP, float4(o.worldPos, 1));
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                // Preserve the native XZ terrain frame, including its slope-dependent lengths.
                o.worldTangent = float3(o.worldNormal.y, -o.worldNormal.x, 0);
                o.worldBitangent = cross(o.worldTangent, o.worldNormal);
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 control0 = UNITY_SAMPLE_TEX2D(_Control0, i.uv);
                float4 control1 = UNITY_SAMPLE_TEX2D_SAMPLER(_Control1, _Control0, i.uv);
                float3 tint = UNITY_SAMPLE_TEX2D_SAMPLER(_TerrainTint, _Control0, i.uv).rgb * _TintEnhanceFactor;
                // The recovered mesh was rebased. Restore the source origin for tile phase.
                float2 sourceXZ = i.worldPos.xz + _SourceOrigin.xz;
                float weights[7] = { control0.r, control0.g, control0.b, control0.a,
                    control1.r, control1.g, control1.b };
                float4 albedo[7];
                float4 packed[7];
                float normalPower[7] = { _Texture_1_Normal_Power, _Texture_2_Normal_Power,
                    _Texture_3_Normal_Power, _Texture_4_Normal_Power, _Texture_5_Normal_Power,
                    _Texture_6_Normal_Power, _Texture_7_Normal_Power };
                float useTint[7] = { _Texture_1_TintEnabled, _Texture_2_TintEnabled,
                    _Texture_3_TintEnabled, _Texture_4_TintEnabled, _Texture_5_TintEnabled,
                    _Texture_6_TintEnabled, _Texture_7_TintEnabled };

                #define SAMPLE_LAYER(N, INDEX) \
                    albedo[INDEX] = UNITY_SAMPLE_TEX2D_SAMPLER(_Texture_##N##_Albedo, _Texture_1_Albedo, sourceXZ / _Texture_##N##_Tiling); \
                    packed[INDEX] = UNITY_SAMPLE_TEX2D_SAMPLER(_Texture_##N##_Normal, _Texture_1_Albedo, sourceXZ / _Texture_##N##_Tiling);
                SAMPLE_LAYER(1, 0)
                SAMPLE_LAYER(2, 1)
                SAMPLE_LAYER(3, 2)
                SAMPLE_LAYER(4, 3)
                SAMPLE_LAYER(5, 4)
                SAMPLE_LAYER(6, 5)
                SAMPLE_LAYER(7, 6)
                #undef SAMPLE_LAYER

                // Native programs independently height-blend albedo (A) and normals (B).
                // A layer absent from the control maps must not raise either threshold.
                float colorHeight[7], normalHeight[7];
                float maxColor = 0, maxNormal = 0;
                [unroll] for (int layer = 0; layer < 7; ++layer)
                {
                    float present = weights[layer] > 0.001 ? 1 : 0;
                    colorHeight[layer] = (albedo[layer].a + weights[layer] * 1.2) * present;
                    normalHeight[layer] = (packed[layer].b + weights[layer] * 1.2) * present;
                    maxColor = max(maxColor, colorHeight[layer]);
                    maxNormal = max(maxNormal, normalHeight[layer]);
                }
                float3 diffuse = 0, detailNormal = 0;
                float colorSum = 0.001, normalSum = 0.001, smoothness = 0;
                [unroll] for (int blendLayer = 0; blendLayer < 7; ++blendLayer)
                {
                    float cw = max(0, colorHeight[blendLayer] - (maxColor - 0.2));
                    float nw = max(0, normalHeight[blendLayer] - (maxNormal - 0.2));
                    diffuse += albedo[blendLayer].rgb * lerp(float3(1, 1, 1), tint, useTint[blendLayer]) * cw;
                    // Keep RG/height/smoothness intact: these are Default textures, not Unity normal maps.
                    detailNormal += GenshinRawNormal(packed[blendLayer], normalPower[blendLayer]) * nw;
                    smoothness += min(packed[blendLayer].a, blendLayer == 6 ? 1.0 : 0.5) * nw;
                    colorSum += cw;
                    normalSum += nw;
                }
                diffuse /= colorSum;
                detailNormal /= normalSum;
                smoothness = saturate(smoothness / normalSum * _Terrain_Smoothness);
                float3 normal = normalize(detailNormal.x * i.worldTangent +
                    detailNormal.y * i.worldBitangent + detailNormal.z * i.worldNormal);
                if (_GenshinGroundCaptureMode > 0.5)
                {
                    if (_GenshinGroundCaptureMode > 1.5)
                        return float4(diffuse, -mul(_GenshinGroundCaptureView, float4(i.worldPos, 1)).z);
                    return float4(normal, smoothness);
                }
                UNITY_LIGHT_ATTENUATION(attenuation, i, i.worldPos);
                float3 lighting = GenshinSourceLighting(diffuse, float3(0.04, 0.04, 0.04),
                    smoothness, normal, i.worldPos, attenuation);
                lighting += GenshinEnvironmentSpecular(float3(0.04, 0.04, 0.04),
                    smoothness, normal, i.worldPos, attenuation);
                float4 color = float4(lighting, 1);
                UNITY_APPLY_FOG(i.fogCoord, color);
                return color;
            }
            ENDCG
        }
        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }
    FallBack "Diffuse"
}

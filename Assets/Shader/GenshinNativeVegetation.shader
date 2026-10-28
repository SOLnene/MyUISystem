Shader "Game/Scene/Genshin Native Vegetation"
{
    Properties
    {
        [Toggle] _UseTexture ("Textured Flowers", Float) = 0
        _MainTex ("Source Color / Alpha", 2D) = "white" {}
        _CutOff ("Source Alpha Cutoff", Range(0,1)) = 0.5
        _ColorBase ("Fallback Root Color", Color) = (0.34,0.53,0.27,1)
        _ColorLerp ("Fallback Tip Color", Color) = (0.34,0.53,0.27,1)
        _RootColorScale ("Source Root Gradient Scale", Float) = 0.2
        _RootColorOffset ("Source Root Gradient Offset", Float) = -0.519
        _GrassTipLighting ("Source Tip Lighting", Float) = 1.18
        _DeathState ("Source Withered State", Range(0,1)) = 0
        _RandomNormalScale ("Source Normal Variation", Float) = 0.2
        _Noisemap ("Source World Wind Noise", 2D) = "gray" {}
        _NoiseScale ("Source Noise World Scale", Float) = 30
        _WindStrength ("Wind Multiplier", Range(0,2)) = 1
        _WindSpeed ("Wind Time Rate", Float) = 0.1
        _GlobalWind ("World Wind Direction", Vector) = (1,0,0.4,0)
        _LargeWindParam ("Source Wind Phase / Amplitude / Scale", Vector) = (0,0.55,1,0)
        _SmallWindParam ("Reserved Source Small Wind", Vector) = (0,0,0,0)
        _ScaleRandom ("Source Blade Size Variation", Float) = 1
        _ScaleShift ("Source Blade Size Shift", Float) = 0
        _ScaleUnit ("Source Blade Size Unit", Float) = 0.33333
        _VegetationBend ("Source Directional Bend", Float) = 0
        _PetalFlat ("Source Petal Flattening", Range(0,1)) = 0
        _Billboard ("Source Camera Facing", Range(0,1)) = 0
        _CustomRotate ("Source Rotation Override", Range(0,1)) = 0
        _FadeStart ("Fade Start (m)", Float) = 40
        _FadeEnd ("Fade End (m)", Float) = 60
        [HideInInspector] _InstanceRootTint ("Painted Root / Strength", Vector) = (0,0,0,0)
        [HideInInspector] _InstanceTipTint ("Painted Tip / Power", Vector) = (0,0,0,1)
        [HideInInspector] _PaintHeightRange ("Painted Height Range", Vector) = (0,1,0,0)
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Cull Off
        CGINCLUDE
        #include "UnityCG.cginc"
        #include "GenshinNativeVegetationCore.cginc"
        sampler2D _MainTex;
        float4 _MainTex_ST, _ColorBase, _ColorLerp, _PaintHeightRange;
        float _UseTexture, _CutOff, _RootColorScale, _RootColorOffset;
        float _GrassTipLighting, _DeathState, _RandomNormalScale, _FadeStart, _FadeEnd;
        UNITY_INSTANCING_BUFFER_START(Grass)
            UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceRootTint)
            UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceTipTint)
        UNITY_INSTANCING_BUFFER_END(Grass)

        #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
        // Matches the existing 192-byte GPU instance protocol.
        struct GrassGpuInstance
        {
            float4x4 objectToWorld;
            float4x4 worldToObject;
            float4 rootTint;
            float4 tipTint;
            float4 boundsCenter;
            float4 boundsExtents;
        };
        StructuredBuffer<GrassGpuInstance> _GrassGpuInstances;
        StructuredBuffer<uint> _GrassVisibleIndices;
        #endif
        void SetupNativeGrass()
        {
            #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
            GrassGpuInstance item = _GrassGpuInstances[_GrassVisibleIndices[unity_InstanceID]];
            unity_ObjectToWorld = item.objectToWorld;
            unity_WorldToObject = item.worldToObject;
            _InstanceRootTint = item.rootTint;
            _InstanceTipTint = item.tipTint;
            #endif
        }
        struct NativeInput
        {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            float2 uv : TEXCOORD0;
            float2 windData : TEXCOORD1;
            float2 sourceUV : TEXCOORD2;
            float2 preservedRoot : TEXCOORD7;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        float3 NativeWorldPosition(NativeInput input)
        {
            // Imported textured native meshes retain the original root UV0 in UV7.
            float2 rootUV = _UseTexture > 0.5 ? input.preservedRoot : input.uv;
            float3 root = mul(unity_ObjectToWorld, float4(rootUV.x, 0, rootUV.y, 1)).xyz;
            float3 position = mul(unity_ObjectToWorld, input.vertex).xyz;
            return GenshinNativeDeform(position, root, input.windData, unity_ObjectToWorld._m01,
                float2(unity_ObjectToWorld._m00, unity_ObjectToWorld._m02),
                float2(UNITY_MATRIX_V._m02, UNITY_MATRIX_V._m00));
        }
        float NativeFade()
        {
            float3 origin = mul(unity_ObjectToWorld, float4(0,0,0,1)).xyz;
            return 1 - smoothstep(_FadeStart, max(_FadeStart + 0.01, _FadeEnd), distance(origin, _WorldSpaceCameraPos));
        }
        void NativeClip(float2 uv, float fade, float2 pixel)
        {
            if (_UseTexture > 0.5) clip(tex2D(_MainTex, uv).a - _CutOff);
            // Source flower PS83 uses this 8x8 ordered coverage pattern.
            static const float coverage[64] = {
                1,49,13,61,4,52,16,64,33,17,45,29,36,20,48,32,
                9,57,5,53,12,60,8,56,41,25,37,21,44,28,40,24,
                3,51,15,63,2,50,14,62,35,19,47,31,34,18,46,30,
                11,59,7,55,10,58,6,54,43,27,39,23,42,26,38,22
            };
            uint2 cell = (uint2)pixel & 7;
            if (fade < 0.95) clip(fade - coverage[cell.y * 8 + cell.x] / 64.0);
        }
        ENDCG
        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma target 3.5
            #pragma target 4.5 PROCEDURAL_INSTANCING_ON
            #pragma vertex NativeVertex
            #pragma fragment NativeFragment
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:SetupNativeGrass
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            struct NativeOutput
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float3 parameters : TEXCOORD3;
                float4 rootTint : TEXCOORD4;
                float4 tipTint : TEXCOORD5;
                SHADOW_COORDS(6)
                UNITY_FOG_COORDS(7)
            };
            NativeOutput NativeVertex(NativeInput v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                NativeOutput o;
                o.worldPos = NativeWorldPosition(v);
                o.pos = UnityWorldToClipPos(o.worldPos);
                float seed = unity_ObjectToWorld._m01 + unity_ObjectToWorld._m11 - v.windData.x;
                o.normal = GenshinNativeSurfaceNormal(seed, _RandomNormalScale);
                o.uv = TRANSFORM_TEX(v.sourceUV, _MainTex);
                o.parameters = float3(saturate(v.windData.y),
                    saturate((v.vertex.y - _PaintHeightRange.x) * _PaintHeightRange.y), NativeFade());
                o.rootTint = UNITY_ACCESS_INSTANCED_PROP(Grass, _InstanceRootTint);
                o.tipTint = UNITY_ACCESS_INSTANCED_PROP(Grass, _InstanceTipTint);
                v.vertex = mul(unity_WorldToObject, float4(o.worldPos, 1));
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            float4 NativeFragment(NativeOutput i) : SV_Target
            {
                NativeClip(i.uv, i.parameters.z, i.pos.xy);
                float blend = saturate(i.parameters.x / max(0.0001, _RootColorScale) + _RootColorOffset);
                float strength = saturate(i.rootTint.w);
                float power = max(0.01, i.tipTint.w);
                float3 albedo;
                if (_UseTexture > 0.5)
                {
                    // Brush tint modulates the atlas so petals and stems retain their source detail.
                    float3 tint = lerp(i.rootTint.rgb, i.tipTint.rgb, pow(i.parameters.y, power));
                    albedo = GenshinNativeFlowerColor(tex2D(_MainTex, i.uv).rgb, tint, strength, _DeathState);
                }
                else
                {
                    // Native instance colors enter the root/tip gradient before withering and tip gain.
                    float3 root = lerp(_ColorBase.rgb, i.rootTint.rgb, strength);
                    float3 tip = lerp(_ColorLerp.rgb, i.tipTint.rgb, strength);
                    blend = lerp(blend, pow(blend, power), strength);
                    albedo = GenshinNativeGrassColor(root, tip, blend, i.parameters.x, _GrassTipLighting, _DeathState);
                }
                float3 normal = normalize(i.normal);
                float3 light = normalize(UnityWorldSpaceLightDir(i.worldPos));
                UNITY_LIGHT_ATTENUATION(attenuation, i, i.worldPos);
                // Forward adapter: the source writes material/normal data into its deferred buffers.
                float3 color = albedo * (max(0, ShadeSH9(float4(normal, 1))) +
                    _LightColor0.rgb * saturate(dot(normal, light)) * attenuation);
                float4 result = float4(color, 1);
                UNITY_APPLY_FOG(i.fogCoord, result);
                return result;
            }
            ENDCG
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            CGPROGRAM
            #pragma target 3.5
            #pragma target 4.5 PROCEDURAL_INSTANCING_ON
            #pragma vertex NativeShadowVertex
            #pragma fragment NativeShadowFragment
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:SetupNativeGrass
            struct NativeShadowOutput
            {
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD1;
                float fade : TEXCOORD2;
            };
            NativeShadowOutput NativeShadowVertex(NativeInput v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                NativeShadowOutput o;
                float3 position = NativeWorldPosition(v);
                o.uv = TRANSFORM_TEX(v.sourceUV, _MainTex);
                o.fade = NativeFade();
                v.vertex = mul(unity_WorldToObject, float4(position, 1));
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }
            float4 NativeShadowFragment(NativeShadowOutput i) : SV_Target
            {
                NativeClip(i.uv, i.fade, i.pos.xy);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
    Fallback Off
}

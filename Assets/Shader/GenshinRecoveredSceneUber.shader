Shader "Game/Scene/Genshin Recovered Scene Uber"
{
    Properties
    {
        _Color ("Main Color", Color) = (1,1,1,1)
        _MainTex ("Main Diffuse", 2D) = "white" {}
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _DetailMask ("SMBE Mask", 2D) = "black" {}
        _NormalStrength ("Normal Strength", Float) = 1
        [Toggle] _EnableAlphaTest ("Alpha Test", Float) = 0
        _CutOff ("Alpha Cutoff", Float) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode ("Cull Mode", Float) = 2
        [Toggle] _EnableSecondTex ("Secondary Layer", Float) = 0
        _SecondaryTex ("Secondary Diffuse", 2D) = "white" {}
        [Normal] _SecondaryBumpMap ("Secondary Normal", 2D) = "bump" {}
        _SecondaryDetailMask ("Secondary SMBE", 2D) = "black" {}
        _UVSec ("Secondary UV Set", Float) = 0
        [Toggle] _SecondaryClamp ("Clamp Secondary UV", Float) = 0
        _BlendValueFrom ("Vertex Alpha Blend Source", Float) = 0
        _InvertBlend ("Invert Blend", Float) = 0
        _DiffuseBlendIntensity ("Diffuse Blend Intensity", Float) = 1
        _DiffuseBlendMethod ("Diffuse Blend Method", Float) = 0
        _MaskBlendIntensity ("Mask Blend Intensity", Float) = 1
        _MaskBlendMethod ("Mask Blend Method", Float) = 0
        _NormalBlendIntensity ("Normal Blend Intensity", Float) = 1
        _NormalBlendMethod ("Normal Blend Method", Float) = 0
        _BrickBlend ("Brick Blend", Float) = 0
        _BrickRandomClip ("Brick Clip", Float) = 0
        _BrickSmoothStepStart ("Brick Blend Start", Float) = 0
        _BrickSmoothStepEnd ("Brick Blend End", Float) = 1
        _UseMainMaskAlphaAsEmission ("Main Mask Emission", Float) = 1
        _UseSecondaryMaskAlphaAsEmission ("Secondary Mask Emission", Float) = 1
        [Toggle] _EnablePOM ("Parallax Occlusion", Float) = 0
        _POMHeightMap ("Height Map", 2D) = "white" {}
        _POMHeight ("POM Height", Float) = 0
        _POMUVMinStep ("POM Max UV Step", Float) = 0.01
        _POMLODBias ("POM LOD Bias", Float) = -2
        _POMNormalScale ("POM Normal Scale", Float) = 1
        _POMShadowScale ("POM Shadow Scale", Float) = 1
        _POMShadowBias ("POM Shadow Bias", Float) = -0.02
        _POMShadowTransitionRange ("POM Shadow Transition", Float) = 0.1
        Emission_Type ("Emission Type", Float) = 0
        [HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,1)
        _EmissionStrength ("Emission Strength", Float) = 1
        _EmissionTimeStrength ("Day / Night Emission", Range(0,1)) = 0
        _EnableCubeReflection ("Environment Reflection", Float) = 0
        _CubeMapIntensity ("Environment Intensity", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull [_CullMode]
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            #include "GenshinRecoveredLighting.cginc"
            sampler2D _MainTex, _BumpMap, _DetailMask, _SecondaryTex, _SecondaryBumpMap, _SecondaryDetailMask, _POMHeightMap;
            float4 _MainTex_ST, _MainTex_TexelSize, _SecondaryTex_ST, _SecondaryTex_TexelSize, _Color, _EmissionColor;
            float _NormalStrength, _EnableAlphaTest, _CutOff, _EnableSecondTex, _UVSec, _SecondaryClamp, _BlendValueFrom, _InvertBlend;
            float _DiffuseBlendIntensity, _DiffuseBlendMethod, _MaskBlendIntensity, _MaskBlendMethod;
            float _NormalBlendIntensity, _NormalBlendMethod, _BrickBlend, _BrickRandomClip, _BrickSmoothStepStart, _BrickSmoothStepEnd;
            float _UseMainMaskAlphaAsEmission, _UseSecondaryMaskAlphaAsEmission;
            float _EnablePOM, _POMHeight, _POMUVMinStep, _POMLODBias, _POMNormalScale;
            float _POMShadowScale, _POMShadowBias, _POMShadowTransitionRange;
            float Emission_Type, _EmissionStrength, _EmissionTimeStrength, _EnableCubeReflection, _CubeMapIntensity;
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 normal : TEXCOORD2;
                float4 tangent : TEXCOORD3;
                float4 color : COLOR0;
                SHADOW_COORDS(4)
                UNITY_FOG_COORDS(5)
            };
            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.tangent = float4(UnityObjectToWorldDir(v.tangent.xyz), v.tangent.w * unity_WorldTransformParams.w);
                o.uv.xy = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv.zw = lerp(v.uv, v.uv1, _UVSec) * _SecondaryTex_ST.xy + _SecondaryTex_ST.zw;
                o.color = v.color;
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            float2 ParallaxUV(float2 uv, float3 viewTS, out float lod)
            {
                lod = 0;
                if (_EnablePOM < 0.5 || _POMHeight <= 0) return uv;
                float2 textureUV = uv * _MainTex_TexelSize.zw;
                float2 gradientX = ddx(textureUV), gradientY = ddy(textureUV);
                lod = 0.5 * log2(max(1e-8,max(dot(gradientX,gradientX),dot(gradientY,gradientY)))) + _POMLODBias;
                // Native traversal uses 4..20 steps and refines the first height crossing.
                int steps = (int)(20 - 16 * saturate(viewTS.z));
                float layerStep = 1.0 / steps;
                float stepLength = min(_POMUVMinStep, _POMHeight / max(0.01, viewTS.z) * layerStep);
                float2 delta = -viewTS.xy * stepLength;
                float2 current = uv - 0.95 * steps * delta;
                float height = tex2D(_POMHeightMap, current).r;
                float previousHeight = height;
                current += delta;
                height = tex2D(_POMHeightMap,current).r;
                float layer = 1-layerStep;
                [loop] for (int stepIndex=0; stepIndex<20; stepIndex++)
                {
                    if (stepIndex >= steps || height >= layer) break;
                    previousHeight = height;
                    layer -= layerStep;
                    current += delta;
                    height = tex2Dlod(_POMHeightMap,float4(current,0,lod)).r;
                }
                float afterCrossing = height - layer;
                float beforeCrossing = layer + layerStep - previousHeight;
                return current - delta * afterCrossing / max(0.00001, afterCrossing + beforeCrossing);
            }
            float ParallaxShadow(float2 uv, float3 lightTS, float3 viewTS, float lod)
            {
                if (_EnablePOM < 0.5 || _POMHeight <= 0 || lightTS.z <= 0.06) return 1;
                float height = tex2Dlod(_POMHeightMap,float4(uv,0,lod)).r;
                float remaining = 1 - height;
                float viewStep = _POMHeight / max(0.01,viewTS.z) / (int)(20-16*saturate(viewTS.z));
                float rayHeight = _POMHeight * saturate(_POMUVMinStep / max(0.001,viewStep));
                float2 delta = lightTS.xy * remaining * rayHeight / max(0.01, 4 * lightTS.z);
                delta = min(delta * 2, _POMUVMinStep);
                float occlusion = 0;
                [unroll] for (int sampleIndex=1; sampleIndex<=4; sampleIndex++)
                {
                    float sampleHeight = tex2Dlod(_POMHeightMap,float4(uv+sampleIndex*delta,0,lod)).r;
                    float difference = sampleHeight - height - remaining * 0.25 * sampleIndex - _POMShadowBias;
                    occlusion = max(occlusion, smoothstep(0, max(0.0001, _POMShadowTransitionRange), difference));
                }
                return 1 - occlusion * saturate((lightTS.z - 0.06) / 0.14) * _POMShadowScale;
            }
            float4 frag(v2f i) : SV_Target
            {
                float3 normal = normalize(i.normal);
                float3 tangent = normalize(i.tangent.xyz);
                float3 bitangent = cross(normal, tangent) * i.tangent.w;
                float3 view = normalize(UnityWorldSpaceViewDir(i.worldPos));
                float3 light = normalize(UnityWorldSpaceLightDir(i.worldPos));
                float3 viewTS = float3(dot(view,tangent),dot(view,bitangent),dot(view,normal));
                float3 lightTS = float3(dot(light,tangent),dot(light,bitangent),dot(light,normal));
                float lod;
                float2 uv = ParallaxUV(i.uv.xy, viewTS, lod);
                float4 mainSample = (_EnablePOM > 0.5 ? tex2Dlod(_MainTex,float4(uv,0,lod)) : tex2D(_MainTex,uv)) * _Color;
                if (_EnableAlphaTest > 0.5) clip(mainSample.a - _CutOff);
                float3 albedo = mainSample.rgb;
                float4 mask = tex2D(_DetailMask, uv);
                float4 normalSample = _EnablePOM > 0.5 ? tex2Dlod(_BumpMap,float4(uv,0,lod)) : tex2D(_BumpMap,uv);
                float3 normalTS = UnpackNormalWithScale(normalSample, _NormalStrength);
                float emissionMask = mask.a * _UseMainMaskAlphaAsEmission;
                if (_EnableSecondTex > 0.5)
                {
                    // Clamp atlases contain transparent borders; wrapping them repeats emblems along long banners.
                    float2 layerUV = _SecondaryClamp > 0.5 ? clamp(i.uv.zw,0.5*_SecondaryTex_TexelSize.xy,1-0.5*_SecondaryTex_TexelSize.xy) : i.uv.zw;
                    float2 layerDx = ddx(i.uv.zw), layerDy = ddy(i.uv.zw);
                    float4 layerMask = tex2Dgrad(_SecondaryDetailMask,layerUV,layerDx,layerDy);
                    float weight = lerp(lerp(layerMask.b, 1-layerMask.b, _InvertBlend), i.color.a, _BlendValueFrom);
                    if (_BrickBlend > 0.5)
                        weight *= step(_BrickRandomClip, layerMask.a * _UseSecondaryMaskAlphaAsEmission) *
                            smoothstep(_BrickSmoothStepStart, max(_BrickSmoothStepStart+0.0001,_BrickSmoothStepEnd),weight);
                    float3 layer = tex2Dgrad(_SecondaryTex,layerUV,layerDx,layerDy).rgb * _Color.rgb;
                    albedo = GenshinLayerBlend(albedo, layer, weight * _DiffuseBlendIntensity, _DiffuseBlendMethod);
                    mask.rgb = GenshinLayerBlend(mask.rgb, layerMask.rgb, weight * _MaskBlendIntensity, _MaskBlendMethod);
                    float3 layerNormal = UnpackNormal(tex2Dgrad(_SecondaryBumpMap,layerUV,layerDx,layerDy));
                    float normalScale = _NormalBlendMethod > 1.5 ? i.color.b : _NormalBlendMethod;
                    float3 combined = float3(normalTS.xy * normalScale + layerNormal.xy, layerNormal.z);
                    normalTS = normalize(lerp(normalTS, combined, weight * _NormalBlendIntensity));
                    emissionMask = lerp(emissionMask, layerMask.a * _UseSecondaryMaskAlphaAsEmission, weight);
                }
                normalTS = normalize(lerp(float3(0,0,1),normalTS,_POMNormalScale));
                normal = normalize(tangent * normalTS.x + bitangent * normalTS.y + normal * normalTS.z);
                float metallic = saturate(mask.g);
                float3 f0 = lerp(float3(0.04,0.04,0.04),albedo,metallic);
                float3 diffuse = albedo * (0.96 - 0.96 * metallic);
                UNITY_LIGHT_ATTENUATION(attenuation, i, i.worldPos);
                attenuation *= ParallaxShadow(uv, lightTS, viewTS, lod);
                float3 lighting = GenshinSourceLighting(diffuse,f0,clamp(mask.r,0.001,0.999),normal,i.worldPos,attenuation);
                if (_EnableCubeReflection > 0.5)
                {
                    float4 reflected = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0,reflect(-view,normal),(1-mask.r)*6);
                    lighting += DecodeHDR(reflected,unity_SpecCube0_HDR) * f0 * _CubeMapIntensity;
                }
                if (Emission_Type > 0.5)
                    lighting += _EmissionColor.rgb * _EmissionStrength * emissionMask * (Emission_Type > 1.5 ? _EmissionTimeStrength : 1);
                float4 output = float4(lighting,1);
                UNITY_APPLY_FOG(i.fogCoord,output);
                return output;
            }
            ENDCG
        }
        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            CGPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST, _Color;
            float _EnableAlphaTest, _CutOff;
            struct shadowInput
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct shadowOutput { V2F_SHADOW_CASTER; float2 uv : TEXCOORD1; };
            shadowOutput vertShadow(shadowInput v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                shadowOutput o;
                o.uv = TRANSFORM_TEX(v.uv,_MainTex);
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o);
                return o;
            }
            float4 fragShadow(shadowOutput i) : SV_Target
            {
                if (_EnableAlphaTest > 0.5) clip(tex2D(_MainTex,i.uv).a*_Color.a-_CutOff);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}

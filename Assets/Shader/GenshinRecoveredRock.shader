Shader "Game/Scene/Genshin Recovered Rock"
{
    Properties
    {
        _MainColor ("Main Color", Color) = (1,1,1,1)
        _StoneDiffuse ("Stone Diffuse / Smoothness", 2D) = "white" {}
        _StoneNormal ("Stone Packed Normal RG", 2D) = "gray" {}
        _StoneNormalScale ("Stone Normal Scale", Float) = 1
        [Toggle] _UseMediumDetail ("Medium Detail", Float) = 0
        _MediumDetailDiffuse ("Medium Detail Diffuse", 2D) = "white" {}
        _MediumDetailMask ("Medium Detail Mask", 2D) = "black" {}
        _MediumDetailNormal_MIDDLE ("Medium Raw Normal", 2D) = "gray" {}
        _MediumDetailDiffuseDistanceInverse ("Medium Distance Inverse", Float) = 0.002
        _MediumDetailDiffuseFade ("Medium Fade", Float) = 1
        _MediumDetailNormalScale ("Medium Normal Scale", Float) = 0.7
        _MediumDarkenIntensity ("Medium Darken Intensity", Float) = 0
        [Toggle] _UseNearDetailDiffuse ("Near Detail Diffuse", Float) = 0
        _NearDetailDiffuse ("Near Detail Diffuse", 2D) = "white" {}
        _NearDetailMask_MIDDLE ("Near Detail Mask", 2D) = "black" {}
        _NearDetailNormal_MIDDLE ("Near Raw Normal", 2D) = "gray" {}
        _NearDetailDiffuseDistanceInverse ("Near Distance Inverse", Float) = 0.03333222
        _NearDetailDiffuseFade ("Near Fade", Float) = 0.25
        _NearDetailNormalScale ("Near Normal Scale", Float) = 1
        _NearDarkenIntensity ("Near Darken Intensity", Float) = 0
        _GrassDiffuse ("Grass Diffuse", 2D) = "white" {}
        _GrassMask ("Grass Mask", 2D) = "black" {}
        _GrassNormalMix ("Grass Normal Mix", Float) = -1
        _UseTintGrassDiffuse ("Use Grass Tint", Float) = 0
        _TintGrassDiffuseColor ("Grass Tint", Color) = (1,1,1,1)
        _DiffuseBlendIntensity ("Diffuse Blend Intensity", Float) = 1
        _DiffuseBlendMethod ("Diffuse Blend Method", Float) = 0
        _MaskBlendIntensity ("Mask Blend Intensity", Float) = 1
        _MaskBlendMethod ("Mask Blend Method", Float) = 0
        _VertRForMedium ("Vertex R For Detail", Float) = 0
        _EnableFixedTiling ("Fixed Tiling", Float) = 0
        _UseGrassSecondaryUV ("Grass Secondary UV", Float) = 0
        _UseMediumDetailSecondaryUV ("Medium Secondary UV", Float) = 0
        _UseNearDetailSecondaryUV ("Near Secondary UV", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
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

            sampler2D _StoneDiffuse, _StoneNormal, _MediumDetailDiffuse, _MediumDetailMask, _MediumDetailNormal_MIDDLE;
            sampler2D _NearDetailDiffuse, _NearDetailMask_MIDDLE, _NearDetailNormal_MIDDLE, _GrassDiffuse, _GrassMask;
            float4 _StoneDiffuse_ST, _MediumDetailDiffuse_ST, _NearDetailDiffuse_ST, _GrassDiffuse_ST;
            float4 _MainColor, _TintGrassDiffuseColor;
            float _StoneNormalScale, _UseMediumDetail, _MediumDetailDiffuseDistanceInverse, _MediumDetailDiffuseFade;
            float _MediumDetailNormalScale, _MediumDarkenIntensity, _UseNearDetailDiffuse, _NearDetailDiffuseDistanceInverse;
            float _NearDetailDiffuseFade, _NearDetailNormalScale, _NearDarkenIntensity, _GrassNormalMix, _UseTintGrassDiffuse;
            float _DiffuseBlendIntensity, _DiffuseBlendMethod, _MaskBlendIntensity, _MaskBlendMethod;
            float _VertRForMedium, _EnableFixedTiling, _UseGrassSecondaryUV, _UseMediumDetailSecondaryUV, _UseNearDetailSecondaryUV;

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
                float4 detailUV : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                float3 normal : TEXCOORD3;
                float4 tangent : TEXCOORD4;
                float4 color : COLOR0;
                SHADOW_COORDS(5)
                UNITY_FOG_COORDS(6)
            };
            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.tangent = float4(UnityObjectToWorldDir(v.tangent.xyz), v.tangent.w * unity_WorldTransformParams.w);
                float scale = _EnableFixedTiling > 0.5 ? 1 : 0.33 *
                    (length(unity_ObjectToWorld._m00_m01_m02) + length(unity_ObjectToWorld._m10_m11_m12) + length(unity_ObjectToWorld._m20_m21_m22));
                o.uv.xy = lerp(v.uv, v.uv1, _UseGrassSecondaryUV) * scale * _GrassDiffuse_ST.xy + _GrassDiffuse_ST.zw;
                o.uv.zw = TRANSFORM_TEX(v.uv, _StoneDiffuse);
                o.detailUV.xy = lerp(v.uv, v.uv1, _UseMediumDetailSecondaryUV) * scale * _MediumDetailDiffuse_ST.xy + _MediumDetailDiffuse_ST.zw;
                o.detailUV.zw = lerp(v.uv, v.uv1, _UseNearDetailSecondaryUV) * scale * _NearDetailDiffuse_ST.xy + _NearDetailDiffuse_ST.zw;
                o.color = v.color;
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            float DetailFade(float inverseDistance, float exponent, float distanceToCamera)
            {
                return min(1, pow(saturate(inverseDistance * distanceToCamera) + 0.0001, exponent));
            }
            float4 frag(v2f i) : SV_Target
            {
                float distanceToCamera = distance(_WorldSpaceCameraPos, i.worldPos);
                float mediumFade = DetailFade(_MediumDetailDiffuseDistanceInverse, _MediumDetailDiffuseFade, distanceToCamera);
                float nearFade = DetailFade(_NearDetailDiffuseDistanceInverse, _NearDetailDiffuseFade, distanceToCamera);
                float detailMask = i.color.r;
                if (_VertRForMedium > 0.5)
                {
                    mediumFade = max(mediumFade, 1 - detailMask);
                    nearFade = max(nearFade, 1 - detailMask);
                    detailMask = 1;
                }
                float4 stone = tex2D(_StoneDiffuse, i.uv.zw);
                float3 normalTS = GenshinRawNormal(tex2D(_StoneNormal, i.uv.zw), _StoneNormalScale);
                float3 mask = float3(stone.a, 0, 0);
                if (_UseMediumDetail > 0.5)
                {
                    float3 medium = tex2D(_MediumDetailDiffuse, i.detailUV.xy).rgb;
                    float3 darker = dot(stone.rgb, float3(0.0396819152,0.45802179,0.00609653955)) <
                        dot(medium, float3(0.0396819152,0.45802179,0.00609653955)) ? stone.rgb : medium;
                    medium = lerp(medium, lerp(stone.rgb, darker, _MediumDarkenIntensity), step(0.0001, _MediumDarkenIntensity));
                    stone.rgb = lerp(medium, stone.rgb, mediumFade);
                    mask = lerp(tex2D(_MediumDetailMask, i.detailUV.xy).rgb, mask, mediumFade);
                    float3 mediumNormal = tex2D(_MediumDetailNormal_MIDDLE, i.detailUV.xy).xyz * 2 - 1;
                    mediumNormal.xy *= (1 - mediumFade) * detailMask * _MediumDetailNormalScale;
                    normalTS = GenshinReorientNormal(normalTS, mediumNormal);
                }
                if (_UseNearDetailDiffuse > 0.5)
                {
                    float3 nearDiffuse = tex2D(_NearDetailDiffuse, i.detailUV.zw).rgb;
                    float3 darker = dot(stone.rgb, float3(0.0396819152,0.45802179,0.00609653955)) <
                        dot(nearDiffuse, float3(0.0396819152,0.45802179,0.00609653955)) ? stone.rgb : nearDiffuse;
                    float3 detail = lerp(stone.rgb * nearDiffuse * 4, lerp(stone.rgb, darker, _NearDarkenIntensity), step(0.0001, _NearDarkenIntensity));
                    stone.rgb = lerp(detail, stone.rgb, nearFade);
                }
                float3 nearNormal = tex2D(_NearDetailNormal_MIDDLE, i.detailUV.zw).xyz * 2 - 1;
                nearNormal.xy *= (1 - nearFade) * detailMask * _NearDetailNormalScale;
                normalTS = GenshinReorientNormal(normalTS, nearNormal);
                mask = lerp(tex2D(_NearDetailMask_MIDDLE, i.detailUV.zw).rgb, mask, nearFade);

                // Authored vertex alpha is the stone weight; using slope here loses the source grass boundaries.
                float grassWeight = 1 - i.color.a;
                grassWeight = abs(grassWeight) < 0.001 ? 0 : saturate(grassWeight);
                float3 grassMask = tex2D(_GrassMask, i.uv.xy).rgb;
                float3 grass = _UseTintGrassDiffuse > 0.5 ? grassMask.g * _TintGrassDiffuseColor.rgb : tex2D(_GrassDiffuse, i.uv.xy).rgb;
                float3 albedo = GenshinLayerBlend(stone.rgb * _MainColor.rgb, grass, grassWeight * _DiffuseBlendIntensity, _DiffuseBlendMethod);
                mask = GenshinLayerBlend(mask, grassMask, grassWeight * _MaskBlendIntensity, _MaskBlendMethod);
                normalTS.xy *= 1 + grassWeight * _GrassNormalMix;
                normalTS.z = sqrt(1 - saturate(dot(normalTS.xy, normalTS.xy)));
                float3 normal = normalize(i.normal);
                float3 tangent = normalize(i.tangent.xyz);
                float3 bitangent = cross(normal, tangent) * i.tangent.w;
                normal = normalize(tangent * normalTS.x + bitangent * normalTS.y + normal * normalTS.z);
                UNITY_LIGHT_ATTENUATION(attenuation, i, i.worldPos);
                float4 color = float4(GenshinSourceLighting(albedo, float3(0.04,0.04,0.04), clamp(mask.r,0.001,0.999), normal, i.worldPos, attenuation), 1);
                UNITY_APPLY_FOG(i.fogCoord, color);
                return color;
            }
            ENDCG
        }
        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }
    FallBack "Diffuse"
}

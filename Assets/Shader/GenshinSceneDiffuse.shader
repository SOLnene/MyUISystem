Shader "Game/Scene/Genshin Recovered Diffuse"
{
    Properties
    {
        _Color ("Main Color", Color) = (1, 1, 1, 1)
        _MainTex ("Base RGB Atlas", 2D) = "white" {}
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 2)) = 0
        [Toggle] _EnableAlphaTest ("Alpha Test", Float) = 0
        _CutOff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode ("Cull Mode", Float) = 2
        [Enum(None, 0, Normal, 1, Time, 2)] Emission_Type ("Emission Type", Float) = 2
        [HDR] _EmissionColor ("Emission Color", Color) = (0.67, 0.497, 0.18858123, 1)
        _EmissionStrength ("Emission Intensity", Float) = 4
        _EmissionTimeStrength ("Time Emission Strength", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200
        Cull [_CullMode]
        Pass
        {
            Name "FORWARD"
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

            sampler2D _MainTex, _BumpMap;
            float4 _MainTex_ST, _BumpMap_ST;
            half4 _Color, _EmissionColor;
            float Emission_Type, _EmissionStrength, _EmissionTimeStrength;
            float _NormalStrength, _EnableAlphaTest, _CutOff;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                half3 worldNormal : TEXCOORD2;
                half3 vertexIndirect : TEXCOORD3;
                float2 lightmapUV : TEXCOORD4;
                SHADOW_COORDS(5)
                UNITY_FOG_COORDS(6)
                half4 worldTangent : TEXCOORD7;
                half emissionMask : COLOR0;
            };

            v2f vert(appdata v)
            {
                // Select the instance transform before computing world-space attributes.
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.uv = float4(TRANSFORM_TEX(v.uv, _MainTex), TRANSFORM_TEX(v.uv, _BumpMap));
                o.worldTangent = half4(UnityObjectToWorldDir(v.tangent.xyz), v.tangent.w * unity_WorldTransformParams.w);
                o.lightmapUV = v.lightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
                // The recovered forward variant interpolates quadratic SH terms;
                // its linear terms are evaluated with the fragment normal.
                o.vertexIndirect = SHEvalLinearL2(half4(o.worldNormal, 1));
                #ifdef VERTEXLIGHT_ON
                o.vertexIndirect += Shade4PointLights(
                    unity_4LightPosX0, unity_4LightPosY0, unity_4LightPosZ0,
                    unity_LightColor[0].rgb, unity_LightColor[1].rgb,
                    unity_LightColor[2].rgb, unity_LightColor[3].rgb,
                    unity_4LightAtten0, o.worldPos, o.worldNormal);
                #endif
                o.emissionMask = v.color.a;
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 baseSample = tex2D(_MainTex, i.uv.xy) * _Color;
                if (_EnableAlphaTest > 0.5)
                    clip(baseSample.a - _CutOff);
                half3 albedo = baseSample.rgb;
                half3 normal = normalize(i.worldNormal);
                if (_NormalStrength > 0 && dot(i.worldTangent.xyz, i.worldTangent.xyz) > 0.01)
                {
                    // Decode Unity's platform normal-map encoding before applying tangent-space lighting.
                    half3 tangentNormal = UnpackNormalWithScale(tex2D(_BumpMap, i.uv.zw), _NormalStrength);
                    half3 tangent = normalize(i.worldTangent.xyz);
                    half3 bitangent = cross(normal, tangent) * i.worldTangent.w;
                    normal = normalize(tangentNormal.x * tangent + tangentNormal.y * bitangent + tangentNormal.z * normal);
                }
                UNITY_LIGHT_ATTENUATION(attenuation, i, i.worldPos);
                half diffuse = max(0, dot(normal, normalize(UnityWorldSpaceLightDir(i.worldPos))));
                half3 indirect;
                #ifdef LIGHTMAP_ON
                indirect = DecodeLightmap(UNITY_SAMPLE_TEX2D(unity_Lightmap, i.lightmapUV));
                #else
                indirect = max(0, SHEvalLinearL0L1(half4(normal, 1)) + i.vertexIndirect);
                #endif
                half3 color = albedo * (indirect + _LightColor0.rgb * diffuse * attenuation);
                // Original EMISSION_TYPE_TIME multiplies vertex alpha, not atlas alpha.
                // Decode its deferred HDR contribution directly into this forward adapter.
                if (Emission_Type > 0.5)
                {
                    half timeStrength = Emission_Type > 1.5 ? _EmissionTimeStrength : 1;
                    color += _EmissionColor.rgb * _EmissionStrength * timeStrength * i.emissionMask;
                }
                half4 output = half4(color, 1);
                // The client uses its own shadow projection and final composition.
                // Unity shadow coordinates and fog supply those boundaries in this project.
                UNITY_APPLY_FOG(i.fogCoord, output);
                return output;
            }
            ENDCG
        }
        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }
    FallBack "Diffuse"
}

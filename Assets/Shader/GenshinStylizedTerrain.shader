Shader "Game/Terrain/Genshin Stylized"
{
    Properties
    {
        _MainTex ("HLOD Base Map", 2D) = "white" {}
        [Toggle] _LayerMode ("Use Original Splat UVs", Float) = 0
        _Albedo1 ("Layer 1 Albedo / Height", 2D) = "white" {}
        _Albedo2 ("Layer 2 Albedo / Height", 2D) = "white" {}
        _Albedo3 ("Layer 3 Albedo / Height", 2D) = "white" {}
        _Normal1 ("Layer 1 Packed Normal", 2D) = "gray" {}
        _Normal2 ("Layer 2 Packed Normal", 2D) = "gray" {}
        _Normal3 ("Layer 3 Packed Normal", 2D) = "gray" {}
        _SplatMap ("RGB Layer Weights", 2D) = "white" {}
        _TintMap ("Linear Terrain Tint", 2D) = "white" {}
        _TexTill1 ("Layer 1 World Tile Size", Float) = 8
        _TexTill2 ("Layer 2 World Tile Size", Float) = 8
        _TexTill3 ("Layer 3 World Tile Size", Float) = 16
        _NrmPow1 ("Layer 1 Normal Scale", Range(0, 2)) = 1
        _NrmPow2 ("Layer 2 Normal Scale", Range(0, 2)) = 1
        _NrmPow3 ("Layer 3 Normal Scale", Range(0, 2)) = 1
        _TintEnhanceFactor ("Original Tint Multiplier", Float) = 2.8
        _UseTint1 ("Tint Layer 1", Range(0, 1)) = 1
        _UseTint2 ("Tint Layer 2", Range(0, 1)) = 0
        _UseTint3 ("Tint Layer 3", Range(0, 1)) = 0
        _NormalStrength ("Detail Normal Strength", Range(0, 1)) = 0.45
        _DetailStrength ("HLOD Detail Strength", Range(0, 1)) = 0.35
        _DetailFade ("Detail Fade Start / End", Vector) = (40, 240, 0, 0)
        _ShadowTint ("Shadow Color", Color) = (0.58, 0.70, 0.82, 1)
        _LightTint ("Sunlit Color", Color) = (1.04, 1.02, 0.96, 1)
        _RampThreshold ("Diffuse Ramp Center", Range(-1, 1)) = 0.1
        _RampWidth ("Diffuse Ramp Softness", Range(0.01, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            #include "GenshinRecoveredLighting.cginc"

            sampler2D _MainTex, _Albedo1, _Albedo2, _Albedo3;
            sampler2D _Normal1, _Normal2, _Normal3, _SplatMap, _TintMap;
            float _LayerMode, _TexTill1, _TexTill2, _TexTill3;
            float _NrmPow1, _NrmPow2, _NrmPow3;
            float _TintEnhanceFactor, _UseTint1, _UseTint2, _UseTint3;
            float _NormalStrength, _DetailStrength, _RampThreshold, _RampWidth;
            float4 _DetailFade, _ShadowTint, _LightTint;

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
                SHADOW_COORDS(3)
                UNITY_FOG_COORDS(4)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.uv = v.uv;
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float3 DecodePackedNormal(float4 packed, float strength)
            {
                // The client stores normal XY in RG; B/A contain other material data.
                // Unity's DXT5nm importer would move these channels and corrupt this decode.
                float2 xy = (packed.rg * 2 - 1) * strength;
                return float3(xy, sqrt(saturate(1 - dot(xy, xy))));
            }

            float3 HeightWeights(float3 splat, float3 height)
            {
                // Recovered from the terrain pixel program: absent layers must not
                // contribute their texture height to the shared 0.2 blend threshold.
                float3 present = step(0.001, splat);
                float3 candidate = (height + splat * 1.2) * present;
                float threshold = max(candidate.x, max(candidate.y, candidate.z)) - 0.2;
                float3 weights = max(0, candidate - threshold) * present;
                return weights / (dot(weights, float3(1, 1, 1)) + 0.001);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv1 = i.worldPos.xz / max(_TexTill1, 0.01);
                float2 uv2 = i.worldPos.xz / max(_TexTill2, 0.01);
                float2 uv3 = i.worldPos.xz / max(_TexTill3, 0.01);
                float4 a = tex2D(_Albedo1, uv1);
                float4 b = tex2D(_Albedo2, uv2);
                float4 c = tex2D(_Albedo3, uv3);
                float3 n1 = DecodePackedNormal(tex2D(_Normal1, uv1), _NrmPow1);
                float3 n2 = DecodePackedNormal(tex2D(_Normal2, uv2), _NrmPow2);
                float3 n3 = DecodePackedNormal(tex2D(_Normal3, uv3), _NrmPow3);
                float3 normal = normalize(i.worldNormal);
                float3 albedo;
                float3 detailNormal;
                if (_LayerMode > 0.5)
                {
                    float3 splat = tex2D(_SplatMap, i.uv).rgb;
                    float3 weights = HeightWeights(splat, float3(a.a, b.a, c.a));
                    float3 tint = tex2D(_TintMap, i.uv).rgb * _TintEnhanceFactor;
                    albedo = a.rgb * lerp(float3(1, 1, 1), tint, _UseTint1) * weights.x
                           + b.rgb * lerp(float3(1, 1, 1), tint, _UseTint2) * weights.y
                           + c.rgb * lerp(float3(1, 1, 1), tint, _UseTint3) * weights.z;
                    float3 normalWeights = HeightWeights(splat, float3(n1.z, n2.z, n3.z));
                    detailNormal = normalize(n1 * normalWeights.x + n2 * normalWeights.y + n3 * normalWeights.z);
                }
                else
                {
                    // HLOD UVs address a baked block map, not a packed near-terrain atlas.
                    // Keep that verified mapping; world-space detail only adds local texture.
                    albedo = tex2D(_MainTex, i.uv).rgb;
                    float rock = smoothstep(0.25, 0.65, 1 - abs(normal.y));
                    float detail = lerp(dot(a.rgb, float3(0.3, 0.59, 0.11)) / 0.35,
                                        dot(c.rgb, float3(0.3, 0.59, 0.11)) / 0.25, rock);
                    float fade = 1 - smoothstep(_DetailFade.x, _DetailFade.y, distance(_WorldSpaceCameraPos, i.worldPos));
                    albedo *= lerp(1, clamp(detail, 0.65, 1.35), _DetailStrength * fade);
                    detailNormal = normalize(lerp(float3(0, 0, 1), lerp(n1, n3, rock), fade));
                }
                float3 axis = abs(normal.x) > 0.99 ? float3(0, 0, 1) : float3(1, 0, 0);
                float3 tangent = normalize(axis - normal * dot(axis, normal));
                float3 bitangent = normalize(cross(tangent, normal));
                float3 perturbed = normalize(detailNormal.x * tangent + detailNormal.y * bitangent + detailNormal.z * normal);
                normal = normalize(lerp(normal, perturbed, _NormalStrength));
                UNITY_LIGHT_ATTENUATION(attenuation, i, i.worldPos);
                fixed4 color = fixed4(GenshinSourceLighting(albedo, float3(0.04,0.04,0.04), 0.292, normal, i.worldPos, attenuation), 1);
                UNITY_APPLY_FOG(i.fogCoord, color);
                return color;
            }
            ENDCG
        }
        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }
    FallBack "Diffuse"
}

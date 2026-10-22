Shader "Game/Scene/Genshin Recovered Water"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.25,0.60,0.65,1)
        _DeepColor ("Deep Color", Color) = (0.075,0.33,0.43,1)
        _Normal01 ("Wave Normal 1 Raw RGB", 2D) = "gray" {}
        _Normal02 ("Wave Normal 2 Raw RGB", 2D) = "gray" {}
        _NormalMapScale ("Wave Normal Scale", Float) = 0.6
        _Normal01_U_Speed ("Wave 1 U Speed", Float) = 0.01
        _Normal01_VSpeed ("Wave 1 V Speed", Float) = -0.01
        _Normal02_U_Speed ("Wave 2 U Speed", Float) = -0.02
        _Normal02_V_Speed ("Wave 2 V Speed", Float) = 0.02
        _ProjectUVScale ("World Projection Scale", Float) = 0.13
        _SourceWorldOffset ("Recovered World Offset", Vector) = (0,0,0,0)
        _ShoreTransparency ("Shore Transparency", Float) = 0.4
        _ShoreFade ("Shore Fade", Float) = 0.27
        _DeepColorDepth ("Deep Color Depth Inverse", Float) = 0.06896552
        _DeepColorFade ("Deep Color Fade", Float) = 0.6
        _FresnelPower ("Fresnel Power", Float) = 5
        _ReflectionIntensity ("Reflection Intensity", Float) = 1
        _RefractionIntensity ("Refraction Intensity", Float) = 0.05
        _SpecularIntensity ("Sun Specular Intensity", Float) = 19.8
        _SpecularPower ("Sun Specular Power", Float) = 0.93
        _Gloss ("Sun Gloss", Float) = 217.6
        _DepthFoamMask ("Depth Foam Mask", 2D) = "white" {}
        _DepthFoamColor ("Depth Foam Color", Color) = (0.75,0.80,0.8,0.15)
        _DepthFoamExtent ("Depth Foam Extent", Float) = 0.13
        _DepthFoamWidth ("Depth Foam Width", Float) = 3.31
        _DepthFoamSpeed ("Depth Foam Speed", Float) = 3.23
        [Toggle] _UseCameraDepth ("Camera Provides Depth Texture", Float) = 0
        _FallbackDepth ("Depth Without Camera Texture", Float) = 12
        _FlowMode ("World Water / UV Fall / UV Foam", Float) = 0
        _FlowTex ("Flow Opacity", 2D) = "white" {}
        _FlowSpeed ("Flow UV Speed", Vector) = (0,1,0,0)
        _Opacity ("Opacity", Range(0,1)) = 0.71
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        GrabPass { "_GenshinWaterSceneColor" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            ZWrite Off
            Cull [_Cull]
            Blend SrcAlpha OneMinusSrcAlpha
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
            sampler2D _Normal01, _Normal02, _DepthFoamMask, _FlowTex, _GenshinWaterSceneColor;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4 _CameraDepthTexture_TexelSize, _Normal01_ST, _Normal02_ST, _DepthFoamMask_ST, _FlowTex_ST;
            float4 _ShallowColor, _DeepColor, _DepthFoamColor, _FlowSpeed, _SourceWorldOffset;
            float _NormalMapScale, _Normal01_U_Speed, _Normal01_VSpeed, _Normal02_U_Speed, _Normal02_V_Speed, _ProjectUVScale;
            float _ShoreTransparency, _ShoreFade, _DeepColorDepth, _DeepColorFade, _FresnelPower, _ReflectionIntensity, _RefractionIntensity;
            float _SpecularIntensity, _SpecularPower, _Gloss, _DepthFoamExtent, _DepthFoamWidth, _DepthFoamSpeed;
            float _UseCameraDepth, _FallbackDepth, _FlowMode, _Opacity;
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float4 grabPos : TEXCOORD3;
                float2 uv : TEXCOORD4;
                SHADOW_COORDS(5)
                UNITY_FOG_COORDS(6)
                float eyeDepth : TEXCOORD7;
            };
            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld,v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.screenPos = ComputeScreenPos(o.pos);
                o.grabPos = ComputeGrabScreenPos(o.pos);
                o.uv = v.uv;
                o.eyeDepth = -UnityObjectToViewPos(v.vertex).z;
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o,o.pos);
                return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                float time = _Time.y;
                float2 worldUV = (i.worldPos.xz+_SourceWorldOffset.xz) * _ProjectUVScale;
                float2 uv1 = worldUV + time * float2(_Normal01_U_Speed,_Normal01_VSpeed);
                float2 uv2 = worldUV + time * float2(_Normal02_U_Speed,_Normal02_V_Speed);
                if (_FlowMode > 0.5)
                {
                    uv1 = TRANSFORM_TEX(i.uv,_Normal01) + time * float2(_Normal01_U_Speed,_Normal01_VSpeed);
                    uv2 = TRANSFORM_TEX(i.uv,_Normal02) + time * float2(_Normal02_U_Speed,_Normal02_V_Speed);
                }
                float3 wave1 = tex2D(_Normal01,uv1).xyz * 2 - 1;
                float3 wave2 = tex2D(_Normal02,uv2).xyz * 2 - 1;
                float2 waveXY = (wave1.xy + wave2.xy) * _NormalMapScale;
                float3 wave = normalize(float3(waveXY,max(0.01,wave1.z+wave2.z)));
                float3 normal = normalize(i.normal);
                float3 axis = abs(normal.x) > 0.99 ? float3(0,0,1) : float3(1,0,0);
                float3 tangent = normalize(axis-normal*dot(axis,normal));
                float3 bitangent = normalize(cross(tangent,normal));
                normal = normalize(tangent*wave.x+bitangent*wave.y+normal*wave.z);
                float3 view = normalize(UnityWorldSpaceViewDir(i.worldPos));
                float3 light = normalize(UnityWorldSpaceLightDir(i.worldPos));
                bool hasDepth = _UseCameraDepth > 0.5 && _CameraDepthTexture_TexelSize.z > 16;
                float depth = _FallbackDepth;
                if (hasDepth)
                {
                    float rawDepth = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screenPos));
                    depth = max(0,LinearEyeDepth(rawDepth)-i.eyeDepth);
                }
                float shore = min(1,pow(saturate(_ShoreTransparency*depth)+0.0001,_ShoreFade));
                float deep = min(1,pow(saturate(_DeepColorDepth*depth)+0.0001,_DeepColorFade));
                float3 surface = lerp(_ShallowColor.rgb,_DeepColor.rgb,deep);
                float nv = saturate(dot(normal,view));
                // The water program uses a linear Fresnel gate, not the Standard BRDF sun reflection.
                float fresnel = max(0.05,1-_FresnelPower*nv);
                float4 environmentSample = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0,reflect(-view,normal),1);
                float3 environment = DecodeHDR(environmentSample,unity_SpecCube0_HDR);
                float environmentWeight = step(0.0001,dot(environment,environment));
                environment = lerp(max(0,ShadeSH9(float4(0,1,0,1)))*0.5,environment,environmentWeight);
                surface = lerp(surface,environment,saturate(fresnel*_ReflectionIntensity));
                UNITY_LIGHT_ATTENUATION(attenuation,i,i.worldPos);
                float nl = saturate(dot(normal,light));
                float nh = saturate(dot(normal,normalize(light+view)));
                float highlight = exp2((nh-1)*(_Gloss+1)*1.4427);
                highlight *= max(0.05,_SpecularPower*nl-1)*max(0.05,1-_SpecularPower*nv);
                highlight *= saturate(_ShoreTransparency*(depth*0.1-0.1));
                surface += _LightColor0.rgb * highlight * _SpecularIntensity * attenuation;
                float foam = 0;
                if (hasDepth)
                {
                    float noise = tex2D(_DepthFoamMask,worldUV*_DepthFoamMask_ST.xy+_DepthFoamMask_ST.zw+time*_DepthFoamSpeed*0.01).r;
                    foam = saturate(1-depth/max(0.001,_DepthFoamExtent)) * smoothstep(0.35,0.75,noise);
                    surface = lerp(surface,_DepthFoamColor.rgb,foam);
                }
                float alpha = shore;
                if (_FlowMode > 0.5)
                {
                    float flow = tex2D(_FlowTex,TRANSFORM_TEX(i.uv,_FlowTex)+time*_FlowSpeed.xy).r;
                    alpha = _Opacity * (_FlowMode > 1.5 ? flow : saturate(0.4+0.6*flow));
                    if (_FlowMode > 1.5) surface = lerp(_DeepColor.rgb,_ShallowColor.rgb,saturate(flow));
                }
                else
                {
                    float4 grab = i.grabPos;
                    grab.xy += wave.xy * _RefractionIntensity * grab.w;
                    float3 behind = tex2Dproj(_GenshinWaterSceneColor,UNITY_PROJ_COORD(grab)).rgb;
                    surface = lerp(behind,surface,saturate(alpha+foam));
                    alpha = 1;
                }
                float4 color = float4(surface,alpha);
                UNITY_APPLY_FOG(i.fogCoord,color);
                return color;
            }
            ENDCG
        }
    }
    FallBack Off
}

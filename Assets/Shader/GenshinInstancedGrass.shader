Shader "Game/Scene/Genshin Instanced Grass"
{
    Properties
    {
        _DiffuseTex ("Original Grass Diffuse", 2D) = "white" {}
        _MaskTex ("Original LSAB (B Cutout)", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        _RootTint ("Root Tint", Color) = (0.85,0.95,0.85,1)
        _TipTint ("Tip Tint", Color) = (0.85,0.95,0.8,1)
        [HideInInspector] _InstanceRootTint ("Painted Root / Strength", Vector) = (0,0,0,0)
        [HideInInspector] _InstanceTipTint ("Painted Tip / Gradient Power", Vector) = (0,0,0,1)
        [HideInInspector] _PaintHeightRange ("Painted Height Origin / Reciprocal Range", Vector) = (0,1,0,0)
        _RootShade ("Root Occlusion", Range(0,1)) = 0.75
        _BladeRoot ("Source Root Height", Float) = -0.06083
        _BladeHeight ("Source Tip Height", Float) = 1.32607
        _WindStrength ("Wind Strength (m)", Range(0,0.2)) = 0.035
        _WindDirection ("Wind Direction", Vector) = (1,0,0.4,0)
        _FadeStart ("Fade Start (m)", Float) = 40
        _FadeEnd ("Fade End (m)", Float) = 60
        _GroundBlend ("Compatibility Ground Blend", Range(0,1)) = 0
        _LeafColor ("Source Leaf Color", Color) = (0.54901963,0.54901963,0.54901963,1)
        _LeafColor2 ("Source Leaf Color Lerp", Color) = (0,0,0,1)
        _BranchColor ("Source Branch Color", Color) = (1,1,1,1)
        _TopColor ("Source Top Color", Color) = (0,0,0,1)
        _AOColor ("Source AO Color", Color) = (0,0,0,1)
        _DarkColor ("Source Dark Color", Color) = (0,0,0,1)
        _TopColorVector ("Source Top Direction", Vector) = (1,1,1,1)
        _TopColorScale ("Source Top Scale", Float) = 0
        _TopColorOffset ("Source Top Offset", Float) = -1
        _CutOffset ("Source View Cutoff Offset", Float) = 1
        _ClipEnhance ("Source Distance Cutoff", Float) = 0
        _ClipEnhanceDistance ("Source Cutoff Distance", Float) = 100
        _BranchDontUseAoColor ("Source Branch AO Bypass", Range(0,1)) = 0
        _SpecularPower ("Source Specular Power", Float) = 1
        _SpecularScale ("Source Specular Scale", Float) = 1
        _LTDistortion ("Source LT Distortion", Float) = 1
        _LTPower ("Source LT Power", Float) = 2.9
        _LTScale ("Source LT Scale", Float) = 0.05
        _WindState ("Runtime Wind State", Range(0,2.4)) = 1
        _WindPhase ("Runtime Wind Phase", Float) = 0
        _BendSpeed ("Source Bend Speed Min", Float) = 0.18
        _BendSpeedMax ("Source Bend Speed Max", Float) = 0.18
        _SideBendVariance ("Source Bend Variance", Float) = 0.6
        _SideBendIntensity ("Source Bend Intensity Min", Float) = 0.183
        _SideBendIntensityMax ("Source Bend Intensity Max", Float) = 0.5
        _SideBendSoftness ("Source Bend Softness", Float) = 0.44
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Cull Off
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MaskTex;
        float4 _WindDirection;
        float _Cutoff, _WindStrength, _FadeStart, _FadeEnd, _GrassTime;
        float _CutOffset, _ClipEnhance, _ClipEnhanceDistance;
        float _WindState, _WindPhase, _BendSpeed, _BendSpeedMax, _SideBendVariance;
        float _SideBendIntensity, _SideBendIntensityMax, _SideBendSoftness;
        UNITY_INSTANCING_BUFFER_START(Grass)
            UNITY_DEFINE_INSTANCED_PROP(float4, _GroundUV)
            UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceRootTint)
            UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceTipTint)
        UNITY_INSTANCING_BUFFER_END(Grass)

        #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
        // 192-byte layout matches the compute culler and GenshinVegetationGpuBatch.Instance.
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

        void SetupVisibleGrass()
        {
            #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
            GrassGpuInstance instance = _GrassGpuInstances[_GrassVisibleIndices[unity_InstanceID]];
            unity_ObjectToWorld = instance.objectToWorld;
            unity_WorldToObject = instance.worldToObject;
            _InstanceRootTint = instance.rootTint;
            _InstanceTipTint = instance.tipTint;
            _GroundUV = 0;
            #endif
        }

        float3 SourceGrassWind(float3 position, float3 root, float2 packedNormal)
        {
            if (_WindState <= 0 || _WindStrength <= 0)
                return position;

            // Source VS 5/255 decodes a blade direction from UV1, not from its height.
            float hemisphere = sign(packedNormal.x - 1.5);
            packedNormal.x -= saturate(hemisphere) * 3;
            float3 bladeDirection = float3(packedNormal,
                hemisphere * sqrt(max(0.0001,1 - dot(packedNormal,packedNormal))));
            float directionX = UnityObjectToWorldNormal(bladeDirection).x;
            float state = _WindState * 0.416667014;
            float speed = lerp(_BendSpeed,_BendSpeedMax,state);
            float intensity = lerp(_SideBendIntensity,_SideBendIntensityMax,state);
            float phase = 2 * _SideBendVariance * directionX + _GrassTime * speed +
                (_BendSpeedMax - _BendSpeed) * _WindPhase;
            float triangleWave = abs(frac(phase * 2 - 0.5) * 2 - 1);
            float wave = triangleWave * triangleWave * (12 - 8 * triangleWave) - 2;
            wave *= step(-0.99,bladeDirection.z);
            float3 offset = position - root;
            float sourceLength = length(offset);
            float amplitude = 0.3 * pow(max(1e-8,0.3 * length(offset.xz)),_SideBendSoftness);
            // Keep the existing amplitude control; runtime client wind direction/state are unavailable.
            offset.xz += normalize(_WindDirection.xz + float2(0.0001,0)) * wave * amplitude *
                intensity * (_WindStrength / 0.035);
            offset *= 0.7 * sourceLength / max(1e-6,length(offset)) + 0.3;
            return root + offset;
        }
        float GrassDistanceFade(float3 root)
        {
            return 1 - smoothstep(_FadeStart,max(_FadeStart + 0.01,_FadeEnd),distance(_WorldSpaceCameraPos,root));
        }
        void ClipGrassSurface(float4 mask, float3 position, float3 view, float3 encodedNormal, float fade, float2 pixel)
        {
            float viewCutoff = lerp(saturate(abs(dot(normalize(encodedNormal),view)) - 0.1),1,_CutOffset);
            // The native binding is _CutOff; preserve this project's existing _Cutoff property name.
            clip(mask.b + saturate(distance(_WorldSpaceCameraPos,position) /
                (_ClipEnhanceDistance + 0.0001)) * _ClipEnhance - _Cutoff * viewCutoff);
            float dither = frac(52.9829189 * frac(dot(pixel,float2(0.06711056,0.00583715))));
            clip(fade - max(0.0001,dither));
        }
        ENDCG
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma target 3.5
            #pragma target 4.5 PROCEDURAL_INSTANCING_ON
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:SetupVisibleGrass
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            #include "GenshinRecoveredLighting.cginc"

            sampler2D _DiffuseTex, _GroundTex;
            float4 _RootTint, _TipTint;
            float4 _PaintHeightRange;
            float _RootShade, _BladeRoot, _BladeHeight;
            float4 _LeafColor, _LeafColor2, _BranchColor, _TopColor, _AOColor, _DarkColor, _TopColorVector;
            float _GroundBlend, _TopColorScale, _TopColorOffset;
            float _BranchDontUseAoColor, _SpecularPower, _SpecularScale, _LTDistortion, _LTPower, _LTScale;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float2 windData : TEXCOORD1;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float4 normal : TEXCOORD2;
                float3 ground : TEXCOORD3;
                float2 heightFade : TEXCOORD4;
                float4 colorData : COLOR0;
                float4 paintedRoot : TEXCOORD7;
                float4 paintedTip : TEXCOORD8;
                float paintedHeight : TEXCOORD9;
                SHADOW_COORDS(5)
                UNITY_FOG_COORDS(6)
            };

            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o;
                float3 root = mul(unity_ObjectToWorld,float4(0,0,0,1)).xyz;
                float3 position = mul(unity_ObjectToWorld,v.vertex).xyz;
                float height = saturate((v.vertex.y - _BladeRoot) / max(0.001,_BladeHeight - _BladeRoot));
                o.paintedRoot = UNITY_ACCESS_INSTANCED_PROP(Grass,_InstanceRootTint);
                o.paintedTip = UNITY_ACCESS_INSTANCED_PROP(Grass,_InstanceTipTint);
                o.paintedHeight = saturate((v.vertex.y - _PaintHeightRange.x) * _PaintHeightRange.y);
                position = SourceGrassWind(position,root,v.windData);
                o.pos = UnityWorldToClipPos(position);
                o.worldPos = position;
                o.normal.xyz = UnityObjectToWorldNormal(v.normal);
                o.normal.w = saturate(dot(v.normal,normalize(_TopColorVector.xyz)) * _TopColorScale + _TopColorOffset);
                o.colorData = float4(UnityObjectToWorldNormal(v.color.rgb * 2 - 1),v.color.a);
                o.uv = v.uv;
                float2 groundUV = UNITY_ACCESS_INSTANCED_PROP(Grass,_GroundUV).xy;
                o.ground = tex2Dlod(_GroundTex,float4(groundUV,0,3)).rgb;
                o.heightFade = float2(height,GrassDistanceFade(root));
                v.vertex = mul(unity_WorldToObject,float4(position,1));
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o,o.pos);
                return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                float4 mask = tex2D(_MaskTex,i.uv);
                float3 view = normalize(UnityWorldSpaceViewDir(i.worldPos));
                ClipGrassSurface(mask,i.worldPos,view,i.colorData.xyz,i.heightFade.y,i.pos.xy);
                // LSAB = leaf interpolation, specular, alpha cutout, leaf/branch selection.
                float3 tint = lerp(_BranchColor.rgb,lerp(_LeafColor.rgb,_LeafColor2.rgb,mask.r),mask.a);
                float3 original = tex2D(_DiffuseTex,i.uv).rgb * tint;
                float specularMask = mask.a * mask.g;
                float3 albedo = lerp(original,mask.a * _TopColor.rgb,i.normal.w * specularMask);
                if (max(max(abs(_AOColor.r),abs(_AOColor.g)),abs(_AOColor.b)) > 0.0001)
                    albedo = lerp(_AOColor.rgb,albedo,i.colorData.a);
                float height = smoothstep(0,0.9,i.heightFade.x);
                float3 groundBlend = lerp(i.ground * _RootTint.rgb,albedo * _TipTint.rgb,height) * lerp(_RootShade,1,height);
                albedo = lerp(albedo,groundBlend,_GroundBlend);
                float paintedBlend = pow(saturate(i.paintedHeight),max(0.01,i.paintedTip.a));
                albedo = lerp(albedo,lerp(i.paintedRoot.rgb,i.paintedTip.rgb,paintedBlend),saturate(i.paintedRoot.a));
                UNITY_LIGHT_ATTENUATION(attenuation,i,i.worldPos);
                float3 normal = normalize(i.normal.xyz);
                float3 light = normalize(UnityWorldSpaceLightDir(i.worldPos));
                float nl = dot(normal,light);
                if (dot(mask.a * _DarkColor.rgb,float3(0.229,0.587,0.114)) > 0)
                    albedo = lerp(albedo,mask.a * _DarkColor.rgb,saturate(-nl));
                float3 shaded = albedo * (1 + normal.y);
                shaded = lerp(shaded,original,_BranchDontUseAoColor * (1 - mask.a));
                // Native PS 177 packs this view-dependent coefficient into deferred material data.
                float lt = pow(saturate(dot(view,-(light + normal * _LTDistortion))) + 0.0001,_LTPower);
                float response = max(0.01,lerp(1,25 * _LTScale * lt,i.colorData.a));
                float reflectivity = floor(clamp(0.04 * (response - 1),0.01,1) * 127) / 127;
                float3 halfDirection = normalize(light + view);
                float3 specular = GenshinSourceSpecular(reflectivity.xxx,saturate(_SpecularPower * 0.05),
                    saturate(dot(normal,halfDirection)),saturate(dot(light,halfDirection))) * specularMask * _SpecularScale;
                float3 top = _TopColor.rgb * mask.a;
                float3 color = (shaded + top) * max(0,ShadeSH9(float4(normal,1))) +
                    (shaded + top * 0.318309873 + specular) * _LightColor0.rgb * saturate(nl) * attenuation;
                // Forward adapter for the recovered LT coefficient; the client's deferred resolve is absent.
                color += albedo * _LightColor0.rgb * (0.04 * 25 * _LTScale * lt * i.colorData.a) * attenuation;
                float4 output = float4(color,1);
                UNITY_APPLY_FOG(i.fogCoord,output);
                return output;
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
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:SetupVisibleGrass

            struct shadowInput
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float2 windData : TEXCOORD1;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct shadowOutput
            {
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                float3 encodedNormal : TEXCOORD3;
                float fade : TEXCOORD4;
            };
            shadowOutput shadowVert(shadowInput v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                shadowOutput o;
                float3 root = mul(unity_ObjectToWorld,float4(0,0,0,1)).xyz;
                float3 position = SourceGrassWind(mul(unity_ObjectToWorld,v.vertex).xyz,root,v.windData);
                // Both passes use the same deformation and LSAB B cutout, so moving leaves keep their shadow silhouette.
                v.vertex = mul(unity_WorldToObject,float4(position,1));
                o.uv = v.uv;
                o.worldPos = position;
                o.encodedNormal = UnityObjectToWorldNormal(v.color.rgb * 2 - 1);
                o.fade = GrassDistanceFade(root);
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }
            float4 shadowFrag(shadowOutput i) : SV_Target
            {
                ClipGrassSurface(tex2D(_MaskTex,i.uv),i.worldPos,normalize(UnityWorldSpaceViewDir(i.worldPos)),
                    i.encodedNormal,i.fade,i.pos.xy);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
    FallBack Off
}

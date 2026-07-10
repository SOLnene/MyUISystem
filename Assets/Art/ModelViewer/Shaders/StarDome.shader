Shader "ModelViewer/StarDome"
{
    Properties
    {
        _MainTex ("Star Texture", 2D) = "black" {}
        _NebulaTex ("Nebula Noise", 2D) = "gray" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(0, 4)) = 1
        _OverlayColor ("Overlay Color", Color) = (0.03, 0.15, 0.34, 1)
        _OverlayStrength ("Overlay Strength", Range(0, 1)) = 1
        _NebulaColor ("Nebula Color", Color) = (0.16, 0.34, 0.65, 1)
        _NebulaStrength ("Nebula Strength", Range(0, 1)) = 0.22
        _NebulaThreshold ("Nebula Threshold", Range(0, 1)) = 0.38
        _NebulaDistortion ("Nebula Distortion", Range(0, 0.1)) = 0.025
        _NebulaSpeedA ("Nebula Speed A", Vector) = (0.0012, 0.00008, 0, 0)
        _NebulaSpeedB ("Nebula Speed B", Vector) = (-0.00055, 0.00011, 0, 0)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Opaque"
            "IgnoreProjector" = "True"
        }

        Cull [_Cull]
        ZWrite Off
        ZTest LEqual
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"

            struct AppData
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _NebulaTex;
            float4 _NebulaTex_ST;
            fixed4 _Tint;
            half _Intensity;
            fixed4 _OverlayColor;
            half _OverlayStrength;
            fixed4 _NebulaColor;
            half _NebulaStrength;
            half _NebulaThreshold;
            half _NebulaDistortion;
            float4 _NebulaSpeedA;
            float4 _NebulaSpeedB;

            Varyings Vert(AppData input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                fixed3 stars = tex2D(_MainTex, input.uv).rgb * _Tint.rgb * _Intensity;
                float2 nebulaUV = input.uv * _NebulaTex_ST.xy + _NebulaTex_ST.zw;
                float2 flowUV = nebulaUV * 2.0 + _Time.y * _NebulaSpeedB.xy;
                fixed2 flow = tex2D(_NebulaTex, flowUV).rg * 2.0 - 1.0;
                float2 densityUV = nebulaUV + _Time.y * _NebulaSpeedA.xy + flow * _NebulaDistortion;
                fixed3 noise = tex2D(_NebulaTex, densityUV).rgb;
                half density = dot(noise, fixed3(0.3333, 0.3333, 0.3333));
                density = smoothstep(_NebulaThreshold, 1.0, density) * _NebulaStrength;

                fixed3 color = stars + _OverlayColor.rgb * _OverlayStrength;
                color += _NebulaColor.rgb * density;
                return fixed4(color, 1);
            }
            ENDCG
        }
    }

    Fallback Off
}

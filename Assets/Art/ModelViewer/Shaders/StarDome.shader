Shader "ModelViewer/StarDome"
{
    Properties
    {
        _MainTex ("Star Texture", 2D) = "black" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(0, 4)) = 1
        _OverlayColor ("Overlay Color", Color) = (0.03, 0.15, 0.34, 1)
        _OverlayStrength ("Overlay Strength", Range(0, 1)) = 1
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
            fixed4 _Tint;
            half _Intensity;
            fixed4 _OverlayColor;
            half _OverlayStrength;

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
                fixed3 color = stars + _OverlayColor.rgb * _OverlayStrength;
                return fixed4(color, 1);
            }
            ENDCG
        }
    }

    Fallback Off
}

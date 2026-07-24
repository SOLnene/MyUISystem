Shader "Unlit/PlaneReflection"
{
    Properties
    {
        [Toggle(_PLANE_REFLECTION)] _UseReflection ("Use Reflection", Float) = 1
        _ReflectionTex ("Reflection RT", 2D) = "white" {}
        _Blur ("Blur", Range(0,0.01)) = 0.003
        _Darkness ("Darkness", Range(0,1)) = 0.6
        _FadeStart ("Fade Start", Range(0,1)) = 0.1
        _FadeEnd ("Fade End", Range(0,1)) = 0.9
        _GradientBottomColor ("Gradient Bottom Color", Color) = (0.42,0.36,0.58,0.9)
        _GradientTopColor ("Gradient Top Color", Color) = (0.24,0.21,0.4,0)
        _GradientStart ("Gradient Start", Range(0,1)) = 0
        _GradientEnd ("Gradient End", Range(0,1)) = 0.65
      
    }

    SubShader
    {
        Tags { "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local_fragment _ _PLANE_REFLECTION

            #include "UnityCG.cginc"

            sampler2D _ReflectionTex;
            float _Blur;
            float _Darkness;
            float _FadeStart;
            float _FadeEnd;
            fixed4 _GradientBottomColor;
            fixed4 _GradientTopColor;
            float _GradientStart;
            float _GradientEnd;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos: TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
            
                // 垂直镜像
                o.uv = float2(v.uv.x, 1 - v.uv.y);
                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 screenUV = i.screenPos.xy / i.screenPos.w;

#if defined(_PLANE_REFLECTION)
                float2 finalUV = screenUV;
                //float4 refl = tex2D(_ReflectionTex, finalUV);
               // 4. 边缘裁切：防止采样到 RT 的边缘重复
                /*if(finalUV.y > 1.0 || finalUV.y < 0.0 || finalUV.x > 1.0 || finalUV.x < 0.0) {
                    return fixed4(0,0,0,0);
                }*/
                // 简单4采样模糊
                float4 col =
                    tex2D(_ReflectionTex, finalUV + float2(_Blur,0)) +
                    tex2D(_ReflectionTex, finalUV - float2(_Blur,0)) +
                    tex2D(_ReflectionTex, finalUV + float2(0,_Blur)) +
                    tex2D(_ReflectionTex, finalUV - float2(0,_Blur));

                col *= 0.25;

                // 变暗
                col.rgb *= (1-_Darkness);

                // 距离渐隐
                float fadeDistance = length(uv * 2.0 - 1.0);
                float fade = 1.0 - smoothstep(_FadeStart, _FadeEnd, fadeDistance);
                
                col.a *= fade;

                return col;
#else
                float gradient = smoothstep(_GradientStart, _GradientEnd, screenUV.y);
                return lerp(_GradientBottomColor, _GradientTopColor, gradient);
#endif
            }

            ENDCG
        }
    }
}

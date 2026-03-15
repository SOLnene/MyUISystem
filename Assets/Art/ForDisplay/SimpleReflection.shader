Shader "Unlit/SimpleReflection"
{
    Properties
    {
        _ReflectionTex ("Reflection RT", 2D) = "white" {}
        _Darkness ("Darkness", Range(0,1)) = 0.6
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _ReflectionTex;
            float _Darkness;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // 直接使用 Plane 自身的 UV，不再计算屏幕坐标
                o.uv = v.texcoord; 
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 直接采样，这样倒影就死死地贴在 Plane 表面
                fixed4 refl = tex2D(_ReflectionTex, i.uv);
                
                // 简单的底部渐隐（如果是正交相机，需要根据画面调整）
                float fade = smoothstep(0.0, 0.8, i.uv.y);
                
                refl.rgb *= _Darkness;
                refl.a *= fade;
                return refl;
            }
            ENDCG
        }
    }
}
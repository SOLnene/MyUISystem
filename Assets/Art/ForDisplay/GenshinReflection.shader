Shader "Unlit/GenshinReflection"
{
    Properties
    {
        _ReflectionTex ("Reflection RT", 2D) = "white" {}
        // 控制倒影的整体透明度
        _Alpha ("Reflection Alpha", Range(0, 1)) = 0.5 
        // 渐隐的起始和结束点
        _FadeStart ("Fade Start", Range(0, 1)) = 0.5
        _FadeEnd ("Fade End", Range(0, 1)) = 0.0
        _Distance("Distance", Range(0, 1)) = 0.2
    }

    SubShader
    {
        // 透明渲染队列，确保能看到背后的星空
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off // 双面渲染

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _ReflectionTex;
            float _Alpha;
            float _FadeStart;
            float _FadeEnd;
            float _Distance;
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                // 计算屏幕投影坐标
                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. 屏幕坐标除以w，得到0-1的采样UV
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                
                // 2. 采样RT（此时RT的背景必须是透明的）
                fixed4 refl = tex2D(_ReflectionTex, screenUV);

                // 3. 计算脚底向外的渐隐 (Fade out)
                // 假设Plane的中心是脚底，计算离中心的距离
                float dist = distance(i.uv, float2(0.5, 0.5)); 
                // 距离中心越远，越接近0（透明）
                float fade = smoothstep(_FadeStart, _FadeEnd, dist);

                // 4. 混合最终颜色
                fixed4 col = refl;
                // 乘上总透明度和渐隐系数
                col.a *= _Alpha * fade; 

                return col;
            }
            ENDCG
        }
    }
}
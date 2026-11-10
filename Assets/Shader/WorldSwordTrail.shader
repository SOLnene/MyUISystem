Shader "MyUISystem/World Sword Trail"
{
    SubShader
    {
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct Varyings
            {
                float4 position : SV_POSITION;
                half2 uv : TEXCOORD0;
            };

            Varyings vert(appdata_base input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord.xy;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half age = input.uv.x;
                half across = input.uv.y;
                half fade = smoothstep(0.0h, 0.08h, age) * (1.0h - smoothstep(0.2h, 1.0h, age));
                half shape = smoothstep(0.0h, 0.65h, across) * (1.0h - smoothstep(0.92h, 1.0h, across));
                half rim = smoothstep(0.72h, 0.89h, across) * (1.0h - smoothstep(0.91h, 1.0h, across));
                half alpha = fade * max(shape * 0.5h, rim * 0.92h);
                half3 color = lerp(half3(1.0h, 0.43h, 0.07h), half3(1.65h, 1.35h, 0.75h), rim);
                return half4(color * alpha, alpha);
            }
            ENDCG
        }
    }
}

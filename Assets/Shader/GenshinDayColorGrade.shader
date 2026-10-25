Shader "Hidden/Game/Genshin Day Color Grade"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Exposure ("Exposure EV", Range(-4, 4)) = -0.35
        _Contrast ("Midgray Contrast", Range(0.5, 1.5)) = 1.04
        _Saturation ("Saturation", Range(0, 2)) = 1.02
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Exposure, _Contrast, _Saturation;

            float4 frag(v2f_img input) : SV_Target
            {
                float4 source = tex2D(_MainTex, input.uv);
                float3 color = max(0, source.rgb * exp2(_Exposure));
                float luminance = dot(color, float3(0.2126, 0.7152, 0.0722));
                color = max(0, lerp(luminance.xxx, color, _Saturation));
                // Pivot in log exposure so contrast retains dim surfaces instead of clipping them to black.
                color = 0.18 * pow(max(color / 0.18, 0.00001), _Contrast);
                // Approximate display transform, not the unrecovered MHYColorGrading pipeline.
                // Narkowicz ACES fit (CC0): https://knarkowicz.wordpress.com/2016/01/06/aces-filmic-tone-mapping-curve/
                color = saturate((color * (2.51 * color + 0.03)) / (color * (2.43 * color + 0.59) + 0.14));
                return float4(color, source.a);
            }
            ENDCG
        }
    }
    FallBack Off
}

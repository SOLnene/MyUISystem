Shader "Hidden/UI/UIBackdropBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlurRadius ("Blur Radius", Float) = 2.25
        _Saturation ("Saturation", Range(0, 1)) = 0.28
        _Brightness ("Brightness", Range(0, 2)) = 0.72
        _Tint ("Tint", Color) = (0.75, 0.78, 0.95, 1)
        _TintStrength ("Tint Strength", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _BackdropTexelSize;
            float _BlurRadius;

            v2f_img Vert(appdata_img input)
            {
                v2f_img output = vert_img(input);
                // CameraTarget and a RenderTexture use different vertical conventions on
                // top-origin graphics APIs. Normalize the source before sampling it.
                #if UNITY_UV_STARTS_AT_TOP
                output.uv.y = 1 - output.uv.y;
                #endif
                return output;
            }

            fixed4 frag(v2f_img input) : SV_Target
            {
                float2 offset = float2(
                    _BackdropTexelSize.x * _BlurRadius,
                    0);
                fixed4 color = tex2D(_MainTex, input.uv) * 0.227027;
                color += tex2D(_MainTex, input.uv + offset) * 0.1945946;
                color += tex2D(_MainTex, input.uv - offset) * 0.1945946;
                color += tex2D(_MainTex, input.uv + offset * 2) * 0.1216216;
                color += tex2D(_MainTex, input.uv - offset * 2) * 0.1216216;
                color += tex2D(_MainTex, input.uv + offset * 3) * 0.054054;
                color += tex2D(_MainTex, input.uv - offset * 3) * 0.054054;
                color += tex2D(_MainTex, input.uv + offset * 4) * 0.016216;
                color += tex2D(_MainTex, input.uv - offset * 4) * 0.016216;
                color.a = 1;
                return color;
            }
            ENDCG
        }

        Pass
        {
            // The second pass reads the normalized temporary RT, so it only performs the vertical
            // blur and final color treatment; it must not flip the UVs a second time.
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _BackdropTexelSize;
            float _BlurRadius;
            float _Saturation;
            float _Brightness;
            fixed4 _Tint;
            float _TintStrength;

            fixed4 frag(v2f_img input) : SV_Target
            {
                float2 offset = float2(
                    0,
                    _BackdropTexelSize.y * _BlurRadius);
                fixed4 color = tex2D(_MainTex, input.uv) * 0.227027;
                color += tex2D(_MainTex, input.uv + offset) * 0.1945946;
                color += tex2D(_MainTex, input.uv - offset) * 0.1945946;
                color += tex2D(_MainTex, input.uv + offset * 2) * 0.1216216;
                color += tex2D(_MainTex, input.uv - offset * 2) * 0.1216216;
                color += tex2D(_MainTex, input.uv + offset * 3) * 0.054054;
                color += tex2D(_MainTex, input.uv - offset * 3) * 0.054054;
                color += tex2D(_MainTex, input.uv + offset * 4) * 0.016216;
                color += tex2D(_MainTex, input.uv - offset * 4) * 0.016216;

                float luminance = dot(
                    color.rgb,
                    float3(0.2126, 0.7152, 0.0722));
                color.rgb = lerp(
                    luminance.xxx,
                    color.rgb,
                    saturate(_Saturation));
                color.rgb *= _Brightness;
                color.rgb = lerp(
                    color.rgb,
                    color.rgb * _Tint.rgb,
                    saturate(_TintStrength));
                color.a = 1;
                return color;
            }
            ENDCG
        }
    }
}

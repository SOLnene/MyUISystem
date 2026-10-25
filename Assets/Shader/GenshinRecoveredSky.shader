Shader "Game/Sky/Genshin Recovered Sky"
{
    Properties
    {
        [NoScaleOffset] _SkyGradientTex ("Original Sky Gradient", 2D) = "white" {}
        [NoScaleOffset] _CloudDensityTex ("Original Cloud Density", 2D) = "gray" {}
        [NoScaleOffset] _CloudWispsTex ("Original Cloud Wisps", 2D) = "black" {}
        [HDR] _ES_TopFrontColor ("Top Toward Sun", Color) = (0.24, 0.49, 0.85, 1)
        [HDR] _ES_TopBackColor ("Top Away From Sun", Color) = (0.16, 0.39, 0.77, 1)
        [HDR] _ES_BottomFrontColor ("Horizon Toward Sun", Color) = (0.76, 0.85, 0.91, 1)
        [HDR] _ES_BottomBackColor ("Horizon Away From Sun", Color) = (0.57, 0.74, 0.88, 1)
        _ES_SkyFrontAndBackBlendFactor ("Directional Blend", Range(0, 1)) = 0.5
        _ES_BottomColorHeight ("Horizon Gradient Height", Range(0.01, 1)) = 0.7
        [HDR] _ES_HorizonHaloColor ("Horizon Halo Color", Color) = (0.79, 0.83, 0.79, 1)
        _ES_HorizonHaloIntensity ("Horizon Halo Intensity", Range(0, 2)) = 0.2
        _ES_HorizonHaloHeight ("Horizon Halo Height", Range(0.01, 1)) = 0.22
        _ES_SunDirection ("Direction To Sun", Vector) = (0.3, 0.8, 0.4, 0)
        [HDR] _ES_SunHaloColor ("Sun Halo Color", Color) = (1, 0.88, 0.68, 1)
        _ES_SunHaloIntensity ("Sun Halo Intensity", Range(0, 1)) = 0.08
        _ES_SunHaloSize ("Sun Halo Exponent", Float) = 900
        _CloudThreshold ("Cloud Density Threshold", Range(0, 1)) = 0.24
        _CloudSmoothness ("Cloud Edge Width", Range(0.01, 0.5)) = 0.18
        _CloudOpacity ("Cloud Opacity", Range(0, 1)) = 0.85
        _CloudWispsOpacity ("Wisps Opacity", Range(0, 2)) = 0.65
        [HDR] _CloudLightColor ("Cloud Light Color", Color) = (0.94, 0.96, 0.98, 1)
        [HDR] _CloudDarkColor ("Cloud Shade Color", Color) = (0.57, 0.69, 0.83, 1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _SkyGradientTex, _CloudDensityTex, _CloudWispsTex;
            float4 _ES_TopFrontColor, _ES_TopBackColor, _ES_BottomFrontColor, _ES_BottomBackColor;
            float4 _ES_HorizonHaloColor, _ES_SunHaloColor, _ES_SunDirection;
            float _ES_SkyFrontAndBackBlendFactor, _ES_BottomColorHeight;
            float _ES_HorizonHaloIntensity, _ES_HorizonHaloHeight, _ES_SunHaloIntensity, _ES_SunHaloSize;
            float4 _CloudLightColor, _CloudDarkColor;
            float _CloudThreshold, _CloudSmoothness, _CloudOpacity, _CloudWispsOpacity;

            struct v2f
            {
                float4 position : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f output;
                output.position = UnityObjectToClipPos(vertex);
                output.direction = vertex.xyz;
                return output;
            }

            float4 frag(v2f input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float3 sun = normalize(_ES_SunDirection.xyz);
                float sunDot = dot(direction, sun);
                float height = abs(asin(clamp(direction.y, -1, 1))) * 0.636619806;

                // Ported from the original atmosphere vertex program: cubic directional blend,
                // with independent R horizon-color and G horizon-halo lookup curves.
                float front = pow(max(0, 1 - _ES_SkyFrontAndBackBlendFactor + sunDot * _ES_SkyFrontAndBackBlendFactor), 3);
                float3 top = lerp(_ES_TopBackColor.rgb, _ES_TopFrontColor.rgb, front);
                float3 bottom = lerp(_ES_BottomBackColor.rgb, _ES_BottomFrontColor.rgb, front);
                float gradient = tex2Dlod(_SkyGradientTex, float4(height / max(0.0001, _ES_BottomColorHeight), 0.5, 0, 0)).r;
                float halo = tex2Dlod(_SkyGradientTex, float4(height / max(0.0001, _ES_HorizonHaloHeight), 0.5, 0, 0)).g;
                float directionalHalo = smoothstep(0.3, 1, saturate(sunDot * 0.5 + 0.5));
                directionalHalo = lerp(directionalHalo, 1, smoothstep(0.2, 0.5, abs(sun.y)));
                float3 color = lerp(top, bottom, gradient);
                color += _ES_HorizonHaloColor.rgb * (_ES_HorizonHaloIntensity * halo * directionalHalo);

                // The native halo combines three exponents; the daytime profile values are approximations.
                float mu = max(0.00001, saturate(sunDot * 0.5 + 0.5));
                float exponent = _ES_SunHaloSize * abs(direction.y);
                float sunHalo = pow(mu, exponent) + 0.12 * pow(mu, exponent * 0.1) + 0.03 * pow(mu, exponent * 0.01);
                color += _ES_SunHaloColor.rgb * (_ES_SunHaloIntensity * sunHalo * smoothstep(0, 1, max(0, sunDot)));

                // Original density/wisps textures with a fixed weather-map substitute and planar sky projection.
                // This does not reconstruct the client's animated weather map or cloud meshes.
                float2 cloudUV = direction.xz / (max(0, direction.y) + 0.18) * 0.18;
                float3 density0 = tex2D(_CloudDensityTex, cloudUV).rgb;
                float3 density1 = tex2D(_CloudDensityTex, cloudUV * 1.37 + float2(0.19, 0.31)).rgb;
                float density = density0.b * density1.b + 0.15 * (density0.r - 0.5);
                float cloud = saturate((density - _CloudThreshold) / max(0.01, _CloudSmoothness)) * _CloudOpacity;
                float2 wispsUV = float2(atan2(direction.z, direction.x) * 0.159154943 + 0.5, height * 0.8);
                float wisps = tex2D(_CloudWispsTex, wispsUV).a * _CloudWispsOpacity;
                float horizonFade = smoothstep(0.035, 0.25, direction.y);
                float3 cloudColor = lerp(_CloudDarkColor.rgb, _CloudLightColor.rgb, saturate(density * 2));
                color = lerp(color, cloudColor, saturate(cloud + wisps) * horizonFade);
                return float4(color, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}

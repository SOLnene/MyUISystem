Shader "ModelViewer/StarTwinkleParticle"
{
    Properties
    {
        _MainTex ("Star Texture", 2D) = "white" {}
        _CycleDuration ("Cycle Duration", Range(0.5, 10)) = 3.2
        _PulseSharpness ("Pulse Sharpness", Range(1, 8)) = 3
        _MinScale ("Min Scale", Range(0, 1)) = 0.25
        _MaxScale ("Max Scale", Range(1, 2)) = 1.15
        _MinBrightness ("Min Brightness", Range(0, 1)) = 0
        _MaxBrightness ("Max Brightness", Range(0, 4)) = 1.4
        _RegionFrequency ("Region Frequency", Range(0.5, 8)) = 2.5
        _LocalPhaseJitter ("Local Phase Jitter", Range(0, 0.5)) = 0.15
        _ColorA ("Cool White", Color) = (0.86, 0.95, 1, 1)
        _ColorB ("Cyan", Color) = (0.38, 1, 0.94, 1)
        _ColorC ("Blue", Color) = (0.38, 0.68, 1, 1)
        _ColorD ("Pink", Color) = (1, 0.48, 0.78, 1)
        _ColorE ("Gold", Color) = (1, 0.82, 0.38, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background+10"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Blend One One
            ColorMask RGB
            Cull Off
            ZWrite Off
            ZTest LEqual
            Lighting Off

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"

            struct AppData
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 center : TEXCOORD1;
                float stableRandom : TEXCOORD2;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed3 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half _CycleDuration;
            half _PulseSharpness;
            half _MinScale;
            half _MaxScale;
            half _MinBrightness;
            half _MaxBrightness;
            half _RegionFrequency;
            half _LocalPhaseJitter;
            fixed4 _ColorA;
            fixed4 _ColorB;
            fixed4 _ColorC;
            fixed4 _ColorD;
            fixed4 _ColorE;

            float Hash13(float3 value)
            {
                value = frac(value * 0.1031);
                value += dot(value, value.yzx + 33.33);
                return frac((value.x + value.y) * value.z);
            }

            float ValueNoise(float3 value)
            {
                float3 cell = floor(value);
                float3 weight = frac(value);
                weight = weight * weight * (3.0 - 2.0 * weight);

                float x00 = lerp(Hash13(cell), Hash13(cell + float3(1, 0, 0)), weight.x);
                float x10 = lerp(Hash13(cell + float3(0, 1, 0)), Hash13(cell + float3(1, 1, 0)), weight.x);
                float x01 = lerp(Hash13(cell + float3(0, 0, 1)), Hash13(cell + float3(1, 0, 1)), weight.x);
                float x11 = lerp(Hash13(cell + float3(0, 1, 1)), Hash13(cell + float3(1, 1, 1)), weight.x);
                return lerp(lerp(x00, x10, weight.y), lerp(x01, x11, weight.y), weight.z);
            }

            fixed3 SelectPalette(float randomValue)
            {
                if (randomValue < 0.2)
                    return _ColorA.rgb;
                if (randomValue < 0.4)
                    return _ColorB.rgb;
                if (randomValue < 0.6)
                    return _ColorC.rgb;
                if (randomValue < 0.8)
                    return _ColorD.rgb;
                return _ColorE.rgb;
            }

            Varyings Vert(AppData input)
            {
                Varyings output;
                float3 direction = normalize(input.center);
                float regionPhase = ValueNoise(direction * _RegionFrequency);
                float localPhase = (input.stableRandom - 0.5) * _LocalPhaseJitter;
                float timeline = _Time.y / max(_CycleDuration, 0.01) + regionPhase + localPhase;
                float cycle = floor(timeline);
                float phase = frac(timeline);
                float pulse = pow(max(sin(phase * UNITY_PI), 0.0), _PulseSharpness);

                float scale = lerp(_MinScale, _MaxScale, pulse);
                float3 vertex = input.center + (input.vertex.xyz - input.center) * scale;
                float colorRandom = Hash13(float3(input.stableRandom * 97.13, cycle, cycle * 0.173));
                fixed3 palette = SelectPalette(colorRandom);
                half brightness = lerp(_MinBrightness, _MaxBrightness, pulse);

                output.position = UnityObjectToClipPos(float4(vertex, 1));
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = palette * input.color.rgb * brightness;
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                fixed starAlpha = tex2D(_MainTex, input.uv).a;
                return fixed4(input.color * starAlpha, 0);
            }
            ENDCG
        }
    }

    Fallback Off
}

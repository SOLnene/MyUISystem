Shader "UI/TabSelectionFlow"
{
    Properties
    {
        _Color ("Particle Visibility", Color) = (1, 1, 1, 1)
        [HDR] _CoreColor ("Core Color", Color) = (0.75, 0.94, 1, 1)
        [HDR] _GlowColor ("Glow Color", Color) = (0.38, 0.68, 0.82, 1)
        _Amplitude ("Curve Amplitude", Range(0, 0.35)) = 0.13
        _WaveCount ("Wave Count", Range(0.1, 3)) = 0.85
        _FlowSpeed ("Flow Cycles Per Second", Range(0, 2)) = 0.34
        _PhaseOffset ("Second Curve Phase", Range(0, 3.14)) = 0.45
        _LineWidth ("Core Width", Range(0.001, 0.1)) = 0.014
        _GlowWidth ("Glow Width", Range(0.01, 0.3)) = 0.075
        _CoreStrength ("Core Strength", Range(0, 3)) = 0.9
        _GlowStrength ("Glow Strength", Range(0, 1)) = 0.2
        _RibbonStrength ("Ribbon Fill", Range(0, 1)) = 0.07
        [HideInInspector] _FlowTime ("Unscaled Flow Time", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha One
        ColorMask RGB

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            struct VertexInput
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VertexOutput
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            half4 _CoreColor;
            half4 _GlowColor;
            float _Amplitude;
            float _WaveCount;
            float _FlowSpeed;
            float _PhaseOffset;
            float _LineWidth;
            float _GlowWidth;
            float _CoreStrength;
            float _GlowStrength;
            float _RibbonStrength;
            float _FlowTime;

            VertexOutput Vert(VertexInput input)
            {
                VertexOutput output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = UnityObjectToClipPos(input.position);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            float SoftLine(float distanceToCurve, float width)
            {
                float distanceInWidths = distanceToCurve / max(width, 0.0001);
                return exp2(-2.0 * distanceInWidths * distanceInWidths);
            }

            half4 Frag(VertexOutput input) : SV_Target
            {
                float x = saturate(input.uv.x);
                // The envelope stays fixed while the sampled curve phase moves through it.
                float span = sin(x * UNITY_PI);
                float endFade = smoothstep(0.0, 0.1, x) * (1.0 - smoothstep(0.68, 1.0, x));
                float phase = (x * _WaveCount - _FlowTime * _FlowSpeed) * 2.0 * UNITY_PI;
                float amplitude = _Amplitude * span;
                float firstCurve = 0.5 + amplitude * sin(phase);
                float secondCurve = 0.5 - amplitude * sin(phase + _PhaseOffset) * 0.85;
                float firstDistance = abs(input.uv.y - firstCurve);
                float secondDistance = abs(input.uv.y - secondCurve);

                // Keep the moving core at least a fraction of a screen pixel wide to prevent shimmer.
                float coreWidth = max(_LineWidth * (0.25 + span * 0.75), fwidth(input.uv.y) * 0.65);
                float core = SoftLine(firstDistance, coreWidth) + SoftLine(secondDistance, coreWidth) * 0.7;
                float glowWidth = _GlowWidth * (0.3 + span * 0.7);
                float glow = SoftLine(firstDistance, glowWidth) + SoftLine(secondDistance, glowWidth) * 0.65;
                float ribbonCenter = (firstCurve + secondCurve) * 0.5;
                float ribbonWidth = abs(firstCurve - secondCurve) * 0.5 + coreWidth;
                float ribbon = SoftLine(abs(input.uv.y - ribbonCenter), ribbonWidth);

                half3 light = _CoreColor.rgb * core * _CoreStrength;
                light += _GlowColor.rgb * (glow * _GlowStrength + ribbon * _RibbonStrength);
                return half4(light * input.color.rgb, endFade * input.color.a);
            }
            ENDCG
        }
    }
}

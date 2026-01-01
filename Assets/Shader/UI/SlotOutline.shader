Shader "Unlit/SlotOutline"
{
    //目前无效
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineWidth ("Outline Width", Float) = 2.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
                fixed4 color    : COLOR;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                half2 texcoord  : TEXCOORD0;
                fixed4 color    : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _OutlineColor;
            float _OutlineWidth;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.texcoord) * i.color;

                // 采样周围像素，形成描边
                float outline = 0;
                float2 offset = _OutlineWidth * _MainTex_TexelSize.xy;

                outline += tex2D(_MainTex, i.texcoord + float2(offset.x,0)).a;
                outline += tex2D(_MainTex, i.texcoord + float2(-offset.x,0)).a;
                outline += tex2D(_MainTex, i.texcoord + float2(0,offset.y)).a;
                outline += tex2D(_MainTex, i.texcoord + float2(0,-offset.y)).a;

                // 如果当前像素透明，但周围有像素 → 画描边
                if (col.a < 0.1 && outline > 0)
                {
                    return _OutlineColor;
                }

                return col;
            }
            ENDCG
        }
    }
}

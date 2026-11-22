Shader "WashGame/LiveWashScreen"
{
    Properties
    {
        _MainTex ("Live camera", 2D) = "white" {}
        _BlurredTex ("Blurred camera", 2D) = "white" {}
        _Mask ("Soap R / towel G", 2D) = "black" {}
        _Blur ("Blur enabled", Float) = 1
        _Radius ("Blur radius", Float) = 7
        _Foam ("Foam", Float) = 0
        _FullFoam ("Full screen soap", Float) = 0
        _RainTex ("Rain sprite sheet", 2D) = "black" {}
        _RainFrame ("Rain frame UV", Vector) = (0,0,1,1)
        _Wet ("Rain visible", Float) = 0
        _Wiping ("Wiping", Float) = 0
        _Aspect ("Aspect", Float) = 1.7778
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex, _Mask, _BlurredTex, _RainTex;
            float4 _RainFrame;
            float4 _MainTex_TexelSize;
            float _Blur, _Radius, _Foam, _Wet, _Wiping, _Aspect, _FullFoam;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }
            float4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 mask = tex2D(_Mask, uv).rg;
                float cleared = mask.g * _Wiping;
                float3 sharp = tex2D(_MainTex, uv).rgb;
                float3 blurred = tex2D(_BlurredTex, uv).rgb;
                float3 col = lerp(sharp, blurred, _Blur * (1 - cleared));
                if (_Wet > 0)
                {
                    float4 rain = tex2D(_RainTex, _RainFrame.xy + uv * _RainFrame.zw);
                    col = lerp(col, rain.rgb, rain.a * (0.5 - cleared));
                }
                float foam = lerp(mask.r, 1, _FullFoam) * _Foam;
                col = lerp(col, float3(1,1,1), foam*.5);
                return float4(col, 1);
            }
            ENDHLSL
        }
    }
}

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
        _Wet ("Water", Float) = 0
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
            sampler2D _MainTex, _Mask, _BlurredTex;
            float4 _MainTex_TexelSize;
            float _Blur, _Radius, _Foam, _Wet, _Wiping, _Aspect, _FullFoam;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 mask = tex2D(_Mask, uv).rg;
                float cleared = mask.g * _Wiping;
                float3 sharp = tex2D(_MainTex, uv).rgb;
                float3 blurred = tex2D(_BlurredTex, uv).rgb;
                float3 col = lerp(sharp, blurred, _Blur * (1 - cleared));
                // Stable screen-space droplets, with a dark refracted edge and a small highlight.
                float2 p = uv * float2(_Aspect, 1) * 12;
                float2 cell = floor(p), f = frac(p);
                float h = hash(cell);
                float2 center = float2(.25 + .5*h, .25 + .5*hash(cell + 7));
                float2 d = (f - center) * float2(1, .75);
                float r = .07 + .13*h;
                float dist = length(d);
                float drop = (1 - smoothstep(r-.015, r, dist)) * step(.38, h) * _Wet * (1-cleared);
                float rim = smoothstep(r*.6, r*.95, dist);
                col = lerp(col, col * .65 + float3(.12,.19,.23), drop * (.15+.6*rim));
                float glint = 1-smoothstep(.008,.04,length(d-float2(-r*.3,r*.35)));
                col += glint * drop * .7;
                float2 bubbles = uv * float2(_Aspect, 1) * 78;
                float2 b = frac(bubbles)-.5;
                float br = .27+.15*hash(floor(bubbles));
                float ring = 1-smoothstep(.015,.06,abs(length(b)-br));
                float foam = lerp(mask.r, 1, _FullFoam) * _Foam;
                col = lerp(col, float3(1,1,1), foam*.5);
                return float4(col, 1);
            }
            ENDHLSL
        }
    }
}

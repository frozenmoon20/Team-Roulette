// 미니게임 2 원형 시야: 화면을 검게 덮고 가운데에 둥근 구멍을 뚫는다.
// _Radius가 0이면 완전히 캄캄, 1.15 정도면 화면 전체가 보인다. (단위: 화면 높이)
// RawImage의 Material로 쓴다. 값은 EyeOpen.cs가 매 프레임 넣어 준다.
Shader "UI/Iris"
{
    Properties
    {
         [PerRendererData] _MainTex ("Texture (안 씀, RawImage용)", 2D) = "white" {}
        _Radius   ("Radius (구멍 크기)", Range(0, 1.5)) = 0
        _Softness ("Softness (가장자리 흐림)", Range(0.001, 0.5)) = 0.08
        _Aspect   ("Aspect (화면 가로/세로)", Float) = 1.7778
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Radius;
            float _Softness;
            float _Aspect;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 화면 가운데로부터의 거리 (가로를 화면 비율만큼 늘려서 찌그러지지 않은 원으로)
                float2 d = i.uv - 0.5;
                d.x *= _Aspect;
                float dist = length(d);

                // 구멍 안쪽은 투명(0), 바깥은 검정(1), 경계는 부드럽게
                float a = smoothstep(_Radius - _Softness, _Radius, dist);
                return fixed4(0, 0, 0, a * i.color.a);
            }
            ENDCG
        }
    }
}
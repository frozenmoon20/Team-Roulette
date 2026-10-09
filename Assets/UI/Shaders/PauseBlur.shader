Shader "TeamRoulette/UI/PauseBlur"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 d = _MainTex_TexelSize.xy * 2;
                fixed4 c = tex2D(_MainTex, i.uv) * 4;
                c += tex2D(_MainTex, i.uv + float2(d.x, 0)) * 2;
                c += tex2D(_MainTex, i.uv - float2(d.x, 0)) * 2;
                c += tex2D(_MainTex, i.uv + float2(0, d.y)) * 2;
                c += tex2D(_MainTex, i.uv - float2(0, d.y)) * 2;
                c += tex2D(_MainTex, i.uv + d);
                c += tex2D(_MainTex, i.uv - d);
                c += tex2D(_MainTex, i.uv + float2(d.x, -d.y));
                c += tex2D(_MainTex, i.uv + float2(-d.x, d.y));
                return c / 16;
            }
            ENDHLSL
        }
    }
}

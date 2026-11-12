Shader "WashGame/SeparableBlur"
{
    Properties { _MainTex ("Source", 2D) = "white" {} _Radius ("Radius", Float) = 7 }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment blurH
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_TexelSize; float _Radius;
            float4 blurH(v2f_img i) : SV_Target
            {
                float4 sum=0;float total=0;
                [unroll] for(int x=-4;x<=4;x++)
                {float w=exp(-.125*x*x);sum+=tex2D(_MainTex,i.uv+float2(x*abs(_MainTex_TexelSize.x)*_Radius*.5,0))*w;total+=w;}
                return sum/total;
            }
            ENDHLSL
        }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment blurV
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_TexelSize; float _Radius;
            float4 blurV(v2f_img i) : SV_Target
            {
                float4 sum=0;float total=0;
                [unroll] for(int y=-4;y<=4;y++)
                {float w=exp(-.125*y*y);sum+=tex2D(_MainTex,i.uv+float2(0,y*abs(_MainTex_TexelSize.y)*_Radius*.125))*w;total+=w;}
                return sum/total;
            }
            ENDHLSL
        }
    }
}

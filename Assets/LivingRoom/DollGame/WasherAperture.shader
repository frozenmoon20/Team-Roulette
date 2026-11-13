Shader "TeamRoulette/DollWasherAperture" {
 Properties{_BaseMap("Texture",2D)="white"{} _BaseColor("Color",Color)=(1,1,1,1) _DrumCenter("Center",Vector)=(0,0,0,0) _DrumAxis("Axis",Vector)=(0,0,1,0) _DrumRadius("Radius",Float)=.22}
 SubShader{Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"} Pass{Tags{"LightMode"="UniversalForward"} Cull Back ZWrite On ZTest LEqual
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseMap_ST,_BaseColor,_DrumCenter,_DrumAxis;float _DrumRadius;
 CBUFFER_END
 struct A{float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;};
 struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;float2 uv:TEXCOORD2;};
 V vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);o.uv=TRANSFORM_TEX(a.uv,_BaseMap);return o;}
 half4 frag(V i):SV_Target{float3 d=i.w-_DrumCenter.xyz;float z=dot(d,_DrumAxis.xyz);float radial=length(d-z*_DrumAxis.xyz);if(radial<_DrumRadius && z>-.36)discard;Light l=GetMainLight();half3 lit=SampleSH(normalize(i.n))+l.color*saturate(dot(normalize(i.n),l.direction));return half4(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb*max(lit,.18),1);}
 ENDHLSL
 }}
}

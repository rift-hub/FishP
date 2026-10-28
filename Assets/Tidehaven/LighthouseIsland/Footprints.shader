Shader "Tidehaven/Pooled Sand Footprints" {
 SubShader {Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" "RenderType"="Transparent"} Pass {
 Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off Offset -1,-1
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A{float4 p:POSITION;float2 uv:TEXCOORD0;float2 birth:TEXCOORD1;};
 struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;float2 birth:TEXCOORD1;};
 V vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;o.birth=i.birth;return o;}
 half4 frag(V i):SV_Target {float2 p=(i.uv-.5)*float2(.24,.42);float sole=length((p-float2(0,.045))/float2(.074,.13));float heel=length((p+float2(0,.112))/float2(.058,.058));float d=min(sole,heel);float age=_Time.y-i.birth.x;float fade=(1-smoothstep(7,12,age))*i.birth.y;float inside=1-smoothstep(.72,1.02,d);float rim=smoothstep(.85,1.05,d)*(1-smoothstep(1.05,1.28,d));float3 c=lerp(float3(.20,.14,.075),float3(.82,.69,.44),rim);return half4(c,(inside*.40+rim*.24)*fade);}
 ENDHLSL
 }} }

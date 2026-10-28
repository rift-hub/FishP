Shader "Tidehaven/InkOutline" {
 Properties { _Width("Width",Float)=0.012 _Color("Ink",Color)=(0.035,0.045,0.04,1) }
 SubShader {Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Geometry+1"} Pass {Cull Front ZWrite On
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float _Width;float4 _Color;
 struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
 struct V {float4 positionCS:SV_POSITION;};
 V vert(A v){V o;float3 p=TransformObjectToWorld(v.positionOS.xyz);p+=TransformObjectToWorldNormal(v.normalOS)*_Width;o.positionCS=TransformWorldToHClip(p);return o;}
 half4 frag(V i):SV_Target{return _Color;}
 ENDHLSL
 }} }

Shader "Tidehaven/Painted Lagoon"
{
 Properties {
  _Shallow("Clear green shallows",Color)=(.35,.77,.64,1)
  _Deep("Turquoise lagoon",Color)=(.22,.65,.52,1)
  _Density("Depth tint",Range(.1,3))=.85
  _WaveHeight("Swell height",Range(0,.2))=.045
  _ContactWidth("Rock whitewater depth",Range(.05,1))=.48
  _FoamColor("Warm whitewater",Color)=(.94,.96,.90,1)
  _WavePeriod("Swash cycle",Float)=6
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
  Pass {
   Tags {"LightMode"="UniversalForward"}
   Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _Shallow,_Deep,_FoamColor;
   float _Density,_WaveHeight,_ContactWidth,_WavePeriod;
   CBUFFER_END
   float Shore(float2 p){float2 q=float2(p.x,p.y/.84);float a=atan2(q.y,q.x);return length(q)-(19.5+2.1*sin(3*a+.5)+1.4*cos(5*a-.7)+.7*sin(7*a));}
   float2 Hash2(float2 p){return frac(sin(float2(dot(p,float2(127.1,311.7)),dot(p,float2(269.5,183.3))))*43758.5453);}
   float Noise(float2 p){float2 q=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(Hash2(q).x,Hash2(q+float2(1,0)).x,f.x),lerp(Hash2(q+float2(0,1)).x,Hash2(q+1).x,f.x),f.y);}
   float FoamNoise(float2 p){return Noise(p)*.55+Noise(p*2.13+7.4)*.30+Noise(p*4.27-3.8)*.15;}
   float3 Waves(float2 p,float t){
    float a=dot(p,float2(.78,.63))*.78-t*.72;
    float b=dot(p,float2(-.32,.95))*1.43-t*.91;
    float2 slope=cos(a)*.78*float2(.78,.63)+cos(b)*.50*float2(-.32,.95);
    return float3(sin(a)+sin(b)*.35,slope)*_WaveHeight;
   }
   // Broad, softly warped underwater light patterns, separate from whitewater.
   float Caustic(float2 p){
    float2 q=floor(p),f=frac(p);float near1=9,near2=9;
    [unroll]for(int y=-1;y<=1;y++)[unroll]for(int x=-1;x<=1;x++){
     float2 o=float2(x,y);float2 v=o+.18+Hash2(q+o)*.64-f;float d=dot(v,v);
     if(d<near1){near2=near1;near1=d;}else near2=min(near2,d);
    }
    return 1-smoothstep(.025,.15,sqrt(near2)-sqrt(near1));
   }
   struct A{float4 p:POSITION;};
   struct V{float4 p:SV_POSITION;float3 world:TEXCOORD0;};
   V vert(A i){V o;o.world=TransformObjectToWorld(i.p.xyz);float fade=smoothstep(0,2,Shore(o.world.xz));o.world.y+=Waves(o.world.xz,_Time.y).x*fade;o.p=TransformWorldToHClip(o.world);return o;}
   half4 frag(V i):SV_Target {
    float2 p=i.world.xz;float t=_Time.y;float shore=Shore(p);
    float2 uv=GetNormalizedScreenSpaceUV(i.p);
    float raw=SampleSceneDepth(uv);
    #if !UNITY_REVERSED_Z
     raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
    #endif
    float3 opaqueWS=ComputeWorldSpacePosition(uv,raw,UNITY_MATRIX_I_VP);
    float gap=max(0,i.world.y-opaqueWS.y);
    float3 wave=Waves(p,t);
    float2 small=float2(sin(p.x*4.3+p.y*1.8-t*.9),cos(p.y*3.6-p.x*1.7+t*.73))*.022;
    float3 n=normalize(float3(-wave.y+small.x,1,-wave.z+small.y));
    float3 view=GetWorldSpaceNormalizeViewDir(i.world);
    Light light=GetMainLight(TransformWorldToShadowCoord(i.world));
    float2 refractUV=saturate(uv+n.xz*.005*smoothstep(.06,.7,gap));
    float3 below=SampleSceneColor(refractUV);
    float3 tint=lerp(_Shallow.rgb,_Deep.rgb,smoothstep(.3,4,gap));
    float absorption=(1-exp(-gap*_Density))*.74;
    float3 color=lerp(below,tint,absorption);
    float2 flow=p*.28+float2(t*.018,-t*.014);
    flow+=float2(sin(flow.y*2.5+t*.12),cos(flow.x*2.1-t*.11))*.20;
    float caustic=Caustic(flow);
    color+=float3(.022,.034,.025)*caustic*smoothstep(.12,.7,gap)*(1-smoothstep(3,7,gap))*light.shadowAttenuation;
    color*=.985+wave.x*.32;
    float fresnel=pow(1-saturate(dot(n,view)),4);
    color=lerp(color,SRGBToLinear(float3(.58,.78,.70)),saturate(.025+fresnel*.32));
    float nh=saturate(dot(n,SafeNormalize(view+light.direction)));
    float sparkle=pow(nh,1050)*.12*smoothstep(.45,.65,Noise(p*12+t*.07));
    color+=sparkle*light.color*light.shadowAttenuation;
    // Actual scene geometry provides the waterline; no collider bounds or screen-space halo.
    float3 face=normalize(cross(ddy(opaqueWS),ddx(opaqueWS))+float3(0,.00001,0));
    float obstacle=smoothstep(.10,.32,1-abs(face.y));
    float cycle=frac(t/max(1,_WavePeriod)+atan2(p.y/.84,p.x)*.015);
    float pulse=smoothstep(.14,.48,cycle)*(1-smoothstep(.68,.98,cycle));
    float width=_ContactWidth*(.28+pulse*.90);
    float noise=FoamNoise(p*8.0+float2(t*.16,-t*.12));
    float reach=width*(.72+noise*.70);
    float contact=1-smoothstep(reach*.15,reach,gap);
    float lace=smoothstep(.43,.57,noise+contact*.18);
    float foam=contact*lerp(lace,1,smoothstep(.65,.95,contact))*obstacle;
    foam*=smoothstep(0,.025,gap)*smoothstep(-.1,.15,shore);
    color=lerp(color,_FoamColor.rgb,foam*.94);
    float alpha=max(smoothstep(0,.09,gap),foam);
    return half4(min(color,3),alpha);
   }
   ENDHLSL
  }
 }
}

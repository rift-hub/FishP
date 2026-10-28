Shader "Tidehaven/Island Breaking Surf"
{
    Properties
    {
        _FoamColor ("Warm whitewater", Color) = (.94,.96,.90,1)
        _WavePeriod ("Seconds between swashes", Range(4,10)) = 6
        _FoamWidth ("Maximum whitewater width", Range(.25,1.8)) = 1.05
        _CellScale ("Foam detail scale", Range(2,9)) = 5.5
        _CrestWidth ("Breaking lip width", Range(.015,.10)) = .028
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _FoamColor;
                float _WavePeriod, _FoamWidth, _CellScale, _CrestWidth;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float2 uv:TEXCOORD1; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.world=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(output.world);
                output.uv=input.uv;
                return output;
            }
            float Hash(float2 p)
            {
                p=frac(p*float2(.1031,.1030));
                p+=dot(p,p.yx+33.33);
                return frac((p.x+p.y)*p.x);
            }
            float Noise(float2 p)
            {
                float2 i=floor(p), f=frac(p);
                f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),
                            lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            float Fbm(float2 p)
            {
                return Noise(p)*.56+Noise(float2(p.x+p.y,-p.x+p.y)*1.43+7.1)*.29
                    +Noise(p*4.17+float2(18.3,2.8))*.15;
            }
            half4 frag(Varyings input):SV_Target
            {
                float2 p=input.world.xz;
                float t=_Time.y;
                float coast=input.uv.x*6.2831853;
                // One incoming wave expands into a sheet, recedes, then breaks apart.
                // The small coastal phase offset prevents a whole-island mechanical pulse.
                float phase=frac(t/max(1,_WavePeriod)+sin(coast*3)*.022);
                float advance=smoothstep(.025,.60,phase);
                float retreat=smoothstep(.65,1,phase);
                float front=lerp(-1.65,.62,advance)-retreat*.48;
                float leadingNoise=Noise(p*3.2+float2(t*.035,0));
                float d=input.uv.y+(leadingNoise-.5)*.13+(Noise(p*13.1)-.5)*.033;
                float behind=front-d;
                float aa=max(.004,fwidth(d)*.7);
                float breaking=smoothstep(.10,.36,phase);
                float dissolve=smoothstep(.64,.985,phase);
                float life=smoothstep(.015,.08,phase)*(1-smoothstep(.91,1,phase));
                float width=_FoamWidth*lerp(.13,1.15,smoothstep(.13,.57,phase))
                    *lerp(.82,1.15,Noise(p*1.1));
                float back=saturate(behind/max(.05,width));

                // Warped fractal patches and fine branching contours replace round cellular holes.
                float2 flow=p+float2(t*.041,-t*.053);
                float2 warp=float2(Noise(flow*1.6),Noise(flow*1.8+19.1))-.5;
                float2 q=(flow+warp*.34)*_CellScale;
                float broad=Fbm(q);
                float fine=Noise(q*3.47+float2(t*.07,-t*.055));
                float pattern=broad*.86+fine*.14;
                float patternAA=max(.006,fwidth(pattern)*.75);
                float threshold=.38+back*.24+dissolve*.32-breaking*.055;
                float patches=smoothstep(threshold-patternAA,threshold+patternAA,pattern);
                float laceField=abs(broad-.51+(fine-.5)*.13);
                float laceWidth=lerp(.045,.013,back)*(1-dissolve*.85);
                float laceAA=max(.003,fwidth(laceField)*.7);
                float lace=1-smoothstep(laceWidth-laceAA,laceWidth+laceAA,laceField);
                lace*=smoothstep(.16,.42,phase)*(1-dissolve)
                    *smoothstep(.20,.40,Noise(q*.47+6));
                float sheet=smoothstep(-aa,.035,behind)*(1-smoothstep(width*.72,width+aa,behind));
                float foam=max(patches,lace*.88)*sheet*breaking;
                // A thin lip starts the break; it becomes uneven as whitewater builds.
                float lipWidth=_CrestWidth*lerp(.55,1.2,leadingNoise);
                float lip=1-smoothstep(lipWidth-aa,lipWidth+aa,abs(behind));
                lip*=lerp(.82,.48,breaking)*(1-dissolve*.8);
                lip*=lerp(1,smoothstep(.28,.48,pattern),breaking);
                float alpha=max(foam,lip)*life;

                // Only a short, faint trace of the previous swash remains at the start.
                float residueFront=lerp(.18,-.45,smoothstep(0,.28,phase));
                float residue=1-smoothstep(.012-aa,.012+aa,abs(d-residueFront));
                residue*=.24*(1-smoothstep(.02,.27,phase))*smoothstep(.40,.55,pattern);
                alpha=max(alpha,residue);
                alpha*=smoothstep(-3.8,-3.2,d)*(1-smoothstep(1.1,1.5,d));
                return half4(_FoamColor.rgb,saturate(alpha)*_FoamColor.a);
            }
            ENDHLSL
        }
    }
}

Shader "Tidehaven/Painted Island Ground"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _ShadowTint ("Shadow Tint", Color) = (0.46, 0.58, 0.64, 1)
        _OutlineColor ("Outline Color", Color) = (0.10, 0.14, 0.16, 1)
        _OutlineWidth ("Outline Strength", Range(0, 1)) = 0.55
        _ColorVibrance ("Vibrance Boost", Range(0.8, 1.5)) = 1.16
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _ShadowTint;
            float4 _OutlineColor;
            float _OutlineWidth;
            float _ColorVibrance;
        CBUFFER_END

        float hashGround(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
        float groundNoise(float2 p) {
            float2 q=floor(p), f=frac(p); f=f*f*(3-2*f);
            return lerp(lerp(hashGround(q),hashGround(q+float2(1,0)),f.x),lerp(hashGround(q+float2(0,1)),hashGround(q+1),f.x),f.y);
        }
        // Apply gentle, scale-independent animation in World Space so FBX scale/rotation never distorts meshes
        float3 ApplyWorldAnimation(float3 posWS, float3 pivotWS, float4 animData)
        {
            float mode = animData.x;
            if (mode < 0.5) return posWS;

            float phase = pivotWS.x * 0.45 + pivotWS.z * 0.35;
            float t = _Time.y;

            if (mode < 1.5)
            {
                // Mode 1: Gentle world-space foliage breeze (Palms, Trees, Bushes, Grass tufts)
                float heightAboveBase = max(0.0, posWS.y - pivotWS.y);
                float bendMask = smoothstep(0.15, 2.5, heightAboveBase);
                float detailFlutter = sin(t * 2.6 + posWS.x * 2.2 + posWS.z * 2.2) * 0.012 * saturate(heightAboveBase);
                posWS.x += (sin(t * 1.35 + phase) * 0.045 * bendMask) + detailFlutter;
                posWS.z += (cos(t * 1.05 + phase * 0.85) * 0.035 * bendMask) + detailFlutter * 0.7;
            }
            else if (mode < 2.5)
            {
                // Mode 2: Underwater seagrass gentle current in world space
                float heightAboveBase = max(0.0, posWS.y - pivotWS.y);
                float swayMask = saturate(heightAboveBase * 1.4);
                posWS.x += sin(t * 2.0 + phase + posWS.y * 3.0) * 0.045 * swayMask;
                posWS.z += cos(t * 1.5 + phase * 1.2) * 0.035 * swayMask;
            }
            else
            {
                // Mode 3: Subtle fish swimming wiggle in world space
                float distFromCenter = length(posWS.xz - pivotWS.xz);
                float wiggle = sin(t * 8.5 - distFromCenter * 10.0 + phase * 3.0) * 0.018 * saturate(distFromCenter * 3.5);
                float3 rightWS = TransformObjectToWorldDir(float3(0, 0, 1));
                posWS.xz += rightWS.xz * wiggle;
            }
            return posWS;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
                float4 uv2        : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 posWS      : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 pivotWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                posWS = ApplyWorldAnimation(posWS, pivotWS, input.uv2);

                output.posWS = posWS;
                output.positionCS = TransformWorldToHClip(posWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 N = normalize(input.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.posWS);
                Light mainLight = GetMainLight(shadowCoord);
                float3 L = normalize(mainLight.direction);
                float3 V = GetWorldSpaceNormalizeViewDir(input.posWS);

                bool hasVCol = (dot(input.color.rgb, float3(1, 1, 1)) > 0.001 || input.color.a > 0.001);
                float3 vColLinear = hasVCol ? SRGBToLinear(input.color.rgb) : float3(1, 1, 1);
                float2 p=input.posWS.xz;
                float a=atan2(p.y/.84,p.x);
                float radius=19.5+2.1*sin(3*a+.5)+1.4*cos(5*a-.7)+.7*sin(7*a);
                float edge=radius-length(float2(p.x,p.y/.84));
                float border=4.7+1.1*sin(p.x*.37+p.y*.21)+.7*sin(p.y*.64-p.x*.2);
                float grass=smoothstep(.32,.65,edge-border+(groundNoise(p*3.0)-.5)*.40);
                if(p.y<0 && p.y>-18) grass*=smoothstep(.95,1.22,abs(p.x-1.2*sin(p.y*.33))+(groundNoise(p*3)-.5)*.15);
                // Broad, low-contrast paint patches; fine detail never forms directional grass stripes.
                float macro=groundNoise(p*.75), detail=groundNoise(p*2.7);
                float fine=groundNoise(p*90)-.5;
                float phase=p.x*24+p.y*14+sin(p.y*2.2)*2+groundNoise(p*1.3)*3;
                float ripple=sin(phase)*.018*(.5+.5*groundNoise(p*1.8));
                float3 sand=lerp(float3(.72,.56,.33),float3(.78,.62,.39),macro*.7+detail*.3);
                sand*=1+ripple+fine*.085;
                float wet=smoothstep(-.5,1.15,edge);
                sand*=lerp(float3(.72,.79,.75),float3(1,1,1),wet);
                float patches=groundNoise(p*.9+groundNoise(p*.4)*2)*.6+groundNoise(p*2.2)*.4;
                float3 meadow=lerp(float3(.28,.45,.22),float3(.39,.53,.27),smoothstep(.15,.85,patches));
                meadow*=.98+groundNoise(p*24)*.04;
                float3 baseAlbedo=SRGBToLinear(lerp(sand,meadow,grass))*_BaseColor.rgb;
                if(input.posWS.y<0){
                    float caustic=sin(p.x*3.2+sin(p.y*2+_Time.y*.35))+sin(p.y*3.6+sin(p.x*1.7-_Time.y*.28));
                    baseAlbedo*=1+pow(saturate(caustic*.5),9)*.35*saturate(-input.posWS.y*3);
                }
                float luma = dot(baseAlbedo, float3(0.2126, 0.7152, 0.0722));
                baseAlbedo = max(0.0, lerp(float3(luma, luma, luma), baseAlbedo, _ColorVibrance));

                // Crisp 2-step cel-shaded lighting
                float NdotL = dot(N, L) * 0.5 + 0.5;
                float shadowAtten = lerp(0.08, 1.0, mainLight.shadowAttenuation);
                float toonDiff = smoothstep(0.40, 0.58, NdotL) * shadowAtten;
                float toonHigh = smoothstep(0.76, 0.90, NdotL) * mainLight.shadowAttenuation * 0.16;

                float upFactor = saturate(N.y * 0.5 + 0.5);
                float3 shadowColor = lerp(_ShadowTint.rgb * 0.74, _ShadowTint.rgb * 1.04, upFactor);
                float3 litColor = mainLight.color * .93;
                float3 shading = lerp(shadowColor, litColor, toonDiff);

                shading += SampleSH(N) * 0.12;

                float3 finalRGB = baseAlbedo * shading;

                // Underwater turquoise-sky tinting for submerged coral/reefs/fish/rocks
                if (input.posWS.y < 0.02)
                {
                    float subDepth = saturate((-input.posWS.y) / 2.8);
                    float3 aquaTint = lerp(float3(0.75, 0.96, 0.92), float3(0.42, 0.80, 0.88), subDepth);
                    finalRGB = lerp(finalRGB, finalRGB * aquaTint, saturate((-input.posWS.y) * 1.5) * 0.28);
                }

                // Clean silhouette & facet-edge toon ink accent
                if (_OutlineWidth > 0.001)
                {
                    float NdotV = saturate(dot(N, V));
                    float rimInk = smoothstep(0.28, 0.08, NdotV);
                    float normalEdge = saturate(length(fwidth(N)) * 0.55);
                    float ink = saturate(max(rimInk, normalEdge * 0.42));
                    finalRGB = lerp(finalRGB, _OutlineColor.rgb, ink * 0.55);
                }

                return half4(finalRGB, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 uv2        : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vertShadow(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 pivotWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                posWS = ApplyWorldAnimation(posWS, pivotWS, input.uv2);
                float3 normWS = TransformObjectToWorldNormal(input.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - posWS);
            #else
                float3 lightDir = _LightDirection;
            #endif

                float4 posCS = TransformWorldToHClip(ApplyShadowBias(posWS, normWS, lightDir));
            #if UNITY_REVERSED_Z
                posCS.z = min(posCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                posCS.z = max(posCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                output.positionCS = posCS;
                return output;
            }

            half4 fragShadow(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 uv2        : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vertDepth(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 pivotWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                posWS = ApplyWorldAnimation(posWS, pivotWS, input.uv2);
                output.positionCS = TransformWorldToHClip(posWS);
                return output;
            }

            half4 fragDepth(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}

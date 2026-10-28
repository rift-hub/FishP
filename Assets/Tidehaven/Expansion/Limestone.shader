Shader "Tidehaven/Weathered Limestone"
{
    Properties
    {
        _BaseColor ("Pale limestone", Color) = (0.61, 0.66, 0.62, 1)
        _MossColor ("Meadow moss", Color) = (0.37, 0.49, 0.23, 1)
        _CrackColor ("Crevice tint", Color) = (0.31, 0.39, 0.35, 1)
        _Weathering ("Weathering", Range(0, 1)) = 0.65
        _DetailScale ("Rock detail scale", Range(0.25, 3)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _MossColor;
            float4 _CrackColor;
            float _Weathering;
            float _DetailScale;
        CBUFFER_END

        float RockHash(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float RockNoise(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(RockHash(i), RockHash(i + float2(1, 0)), f.x),
                        lerp(RockHash(i + float2(0, 1)), RockHash(i + 1), f.x), f.y);
        }

        // A few uneven sediment layers, with discontinuous fine fractures.
        // World coordinates keep the scale consistent across differently sized cliffs.
        float2 WeatheredLines(float3 p, float3 n)
        {
            float warp = RockNoise(p.xz * 0.63 + 7.1);
            float strata = p.y * 1.15 + (warp - 0.5) * 0.53
                         + sin(p.x * 0.8 + p.z * 0.63) * 0.09;
            float layerDistance = abs(frac(strata) - 0.5);
            float aa = max(fwidth(strata), 0.003);
            float seam = 1.0 - smoothstep(0.009, 0.017 + aa, layerDistance);
            float broken = smoothstep(0.25, 0.48, RockNoise(p.xz * 1.1 + floor(strata) * 11.7));
            seam *= broken;

            // Blend face projections rather than snapping at a face's orientation.
            float2 faceWeights = pow(abs(n.xz), 5.0);
            faceWeights /= max(faceWeights.x + faceWeights.y, 0.001);
            float face = p.z * faceWeights.x + p.x * faceWeights.y;
            float band = floor(p.y * 0.72);
            float segment = floor(face * 0.48);
            float seed = RockHash(float2(segment, band));
            float yy = frac(p.y * 0.72);
            float zigzag = abs(yy - 0.52) * (seed > 0.5 ? 0.19 : -0.22);
            float fissureDistance = abs(frac(face * 0.48) - (0.23 + seed * 0.52 + zigzag));
            float crackAA = max(fwidth(face * 0.48), 0.002);
            float fissure = 1.0 - smoothstep(0.003, 0.008 + crackAA, fissureDistance);
            fissure *= smoothstep(0.08, 0.22, yy) * (1.0 - smoothstep(0.64, 0.86, yy));
            fissure *= step(0.59, seed);

            float edge = 1.0 - smoothstep(0.025, 0.043 + aa, abs(frac(strata - 0.036) - 0.5));
            return float2(max(seam * 0.68, fissure * 0.65), edge * broken * 0.11);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fog : TEXCOORD2;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 n = normalize(input.normalWS);
                float3 p = input.positionWS * _DetailScale;
                float broad = RockNoise(p.xz * 0.31 + p.y * 0.14);
                float mottling = RockNoise(p.xz * 1.8 + p.y * 0.37);
                float grain = RockNoise(p.xz * 9.0 + p.y * 1.73);
                float2 seams = WeatheredLines(p, n);

                float3 stone = _BaseColor.rgb * (0.90 + broad * 0.16 + (mottling - 0.5) * 0.055);
                stone += (grain - 0.5) * 0.009;
                float rockFace = 1.0 - smoothstep(0.52, 0.88, n.y);
                stone = lerp(stone, _CrackColor.rgb, seams.x * rockFace * _Weathering);
                stone += seams.y * rockFace * _Weathering * _BaseColor.rgb;

                // Painted green vertices identify moss, while upward normals make the cap.
                // Noise softly breaks the cap edge so a green ring does not outline each rock.
                float greenVertex = smoothstep(0.015, 0.10, input.color.g - input.color.r)
                                  * smoothstep(0.015, 0.10, input.color.g - input.color.b);
                float cap = smoothstep(0.48, 0.84, n.y + (mottling - 0.5) * 0.25);
                float mossMask = saturate(cap * 0.90 + greenVertex * 0.50);
                mossMask *= smoothstep(0.21, 0.62, n.y + greenVertex * 0.28 + broad * 0.12);
                float3 moss = _MossColor.rgb * (0.83 + broad * 0.28 + (mottling - 0.5) * 0.12);
                float3 albedo = lerp(stone, moss, mossMask);

                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float wrapped = saturate(dot(n, light.direction) * 0.5 + 0.5);
                float diffuse = 0.39 + 0.61 * smoothstep(0.12, 0.92, wrapped);
                float3 shadow = lerp(float3(0.60, 0.70, 0.69), 1.0, light.shadowAttenuation);
                float3 lit = albedo * (light.color * diffuse * shadow + SampleSH(n) * 0.30);
                return half4(MixFog(lit, input.fog), 1);
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
            #pragma vertex VertShadow
            #pragma fragment FragShadow
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings VertShadow(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                float3 p = TransformObjectToWorld(input.positionOS.xyz);
                float3 n = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirection = normalize(_LightPosition - p);
                #else
                    float3 lightDirection = _LightDirection;
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(p, n, lightDirection));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
            }
            half4 FragShadow(Varyings input) : SV_Target { return 0; }
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
            #pragma vertex VertDepth
            #pragma fragment FragDepth
            #pragma multi_compile_instancing
            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings VertDepth(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            half4 FragDepth(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}

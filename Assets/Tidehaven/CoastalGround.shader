Shader "Tidehaven/Coastal Ground"
{
    Properties
    {
        _BaseColor ("Sand", Color) = (0.88, 0.80, 0.57, 1)
        _MeadowMask ("Meadow coverage", 2D) = "black" {}
        _MeadowMaskRect ("Meadow bounds (min X, min Z, width, depth)", Vector) = (-55, -47.5, 110, 95)
        _UseMeadowMask ("Use meadow coverage texture", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 posWS      : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 color      : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _MeadowMaskRect;
                float _UseMeadowMask;
            CBUFFER_END

            TEXTURE2D(_MeadowMask);
            SAMPLER(sampler_MeadowMask);

            // Global interactive bobber data set by TidehavenGame: (x, z, intensity, active)
            float4 _BobberData;
            // Global interactive sand trail footprint ring buffer: (x, z, age01, active)
            float4 _SandTrailPoints[16];
            float _SandTrailCount;

            float2 Hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            // Returns (f1, f2 - f1) for cellular Voronoi caustics & lacy wave foam
            float2 Voronoi2(float2 uv, float time)
            {
                float2 n = floor(uv);
                float2 f = frac(uv);
                float f1 = 8.0;
                float f2 = 8.0;

                [unroll]
                for (int j = -1; j <= 1; j++)
                {
                    [unroll]
                    for (int i = -1; i <= 1; i++)
                    {
                        float2 g = float2((float)i, (float)j);
                        float2 o = Hash22(n + g);
                        o = 0.5 + 0.40 * sin(time + 6.2831 * o);
                        float2 r = g - f + o;
                        float d = dot(r, r);
                        if (d < f1)
                        {
                            f2 = f1;
                            f1 = d;
                        }
                        else if (d < f2)
                        {
                            f2 = d;
                        }
                    }
                }
                float s1 = sqrt(f1);
                float s2 = sqrt(f2);
                return float2(s1, s2 - s1);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = frac(sin(dot(i, float2(12.9898, 78.233))) * 43758.5453);
                float b = frac(sin(dot(i + float2(1, 0), float2(12.9898, 78.233))) * 43758.5453);
                float c = frac(sin(dot(i + float2(0, 1), float2(12.9898, 78.233))) * 43758.5453);
                float d = frac(sin(dot(i + float2(1, 1), float2(12.9898, 78.233))) * 43758.5453);
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float3 MeadowColor(float2 p, float3 vertexColor)
            {
                // Fresh layered greens, with small painted strokes instead of a flat olive wash.
                float broad = ValueNoise(p * 0.46 + 7.3);
                float mottling = ValueNoise(p * 1.45 + 21.8);
                float2 bent = p + float2(sin(p.y * 0.63), cos(p.x * 0.57)) * 0.16;
                float brush = ValueNoise(float2(bent.x * 7.6 + bent.y * 2.1, bent.y * 2.8));
                float fineBrush = ValueNoise(float2(bent.x * 17.0 - bent.y * 3.1, bent.y * 6.3));
                float3 shade = SRGBToLinear(float3(0.30, 0.48, 0.20));
                float3 leaf = SRGBToLinear(float3(0.38, 0.57, 0.23));
                float3 sun = SRGBToLinear(float3(0.45, 0.64, 0.27));
                float3 paint = lerp(shade, leaf, smoothstep(0.12, 0.88, broad));
                paint = lerp(paint, sun, smoothstep(0.35, 0.82, mottling) * 0.36);
                // Fine blade strokes are tonal detail, never high contrast speckles.
                paint *= 1.0 + (brush - 0.5) * 0.16 + (fineBrush - 0.5) * 0.085;
                return lerp(paint, vertexColor, 0.035);
            }

            float ShoreZ(float x)
            {
                return 1.0 + 6.0 * exp(-pow((x + 17.0) / 6.0, 2.0)) + 0.7 * sin(x * 0.28);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.posWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.posWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 pos = input.posWS;
                float3 N = normalize(input.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(pos));

                float3 vCol = SRGBToLinear(input.color.rgb);
                float shore = ShoreZ(pos.x);
                float shoreDist = pos.z - shore; // >0 on beach/land, <0 in lagoon water

                // Vertex alpha describes the authored meadow/path layout; no fixed village coordinates.
                // Smooth coverage gives the sandy paths a soft, irregular grassy verge.
                float grassCoverage = saturate(input.color.a);
                if (_UseMeadowMask > 0.5)
                {
                    // A high-resolution control map is baked from the same C# layout as collision
                    // and dressing, so smooth path verges do not depend on terrain triangle density.
                    float2 meadowUV = (pos.xz - _MeadowMaskRect.xy) / max(_MeadowMaskRect.zw, float2(0.001, 0.001));
                    grassCoverage = SAMPLE_TEXTURE2D(_MeadowMask, sampler_MeadowMask, meadowUV).r;
                }
                grassCoverage *= lerp(smoothstep(1.8, 3.0, shoreDist), 1.0, smoothstep(1.2, 3.0, pos.y));
                bool isMeadow = grassCoverage > 0.999;

                float3 baseColor = vCol;
                float unlitFoam = 0.0;

                if (!isMeadow)
                {
                    // 1. Rich Textured Sunlit Golden-Cream Beach Sand + Sculpted Wind Ripples & Pebbles
                    float sandMacro = ValueNoise(pos.xz * 0.22 + 17.0);
                    float sandMed = ValueNoise(pos.xz * 1.35 + 5.2);
                    // Directional wind-swept sand waves & ripples
                    float wavePhase = pos.x * 3.6 + sin(pos.z * 1.35 + pos.x * 0.4) * 2.1 + pos.z * 2.5;
                    float sandRipple = (sin(wavePhase) * 0.5 + 0.5);
                    float rippleCrest = smoothstep(0.35, 0.85, sandRipple) * 0.085 - 0.04;
                    // Fine sand grain & small shell/pebble speckles
                    float sandGrain = (ValueNoise(pos.xz * 24.0) - 0.5) * 0.055 + (ValueNoise(pos.xz * 52.0) - 0.5) * 0.035;
                    float pebbleDot = smoothstep(0.79, 0.86, ValueNoise(pos.xz * 11.5)) * 0.085;
                    float shellSpark = smoothstep(0.86, 0.92, ValueNoise(pos.xz * 16.0 + 9.0)) * 0.065;

                    float3 drySandA = SRGBToLinear(float3(0.82, 0.72, 0.49));
                    float3 drySandB = SRGBToLinear(float3(0.91, 0.83, 0.61));
                    float3 drySand = lerp(drySandA, drySandB, sandMacro * 0.65 + sandMed * 0.35);
                    drySand *= (1.0 + rippleCrest + sandGrain - pebbleDot + shellSpark);

                    // Inland sandy vertex colors form the walking paths. A softer earthen tone
                    // distinguishes their worn center from the pale beach without drawing cobbles.
                    float inlandPath = smoothstep(5.0, 10.0, shoreDist) * (1.0 - grassCoverage);
                    float3 trailEarth = SRGBToLinear(float3(0.73, 0.66, 0.47));
                    trailEarth *= 0.96 + sandMed * 0.08 + sandGrain;
                    drySand = lerp(drySand, trailEarth, inlandPath * 0.65);

                    // Interactive Sand Trail / Footprints behind the player as he walks!
                    if (shoreDist > -0.25)
                    {
                        float footIndent = 0.0;
                        float footRim = 0.0;
                        int count = (int)min(16.0, _SandTrailCount);
                        [loop]
                        for (int k = 0; k < count; k++)
                        {
                            float4 fp = _SandTrailPoints[k];
                            if (fp.w > 0.5)
                            {
                                // Infer travel from nearby ring entries, keeping the existing
                                // footprint buffer contract. Two-step spacing averages out left/right offsets.
                                float4 neighbor = _SandTrailPoints[(k + 2) % 16];
                                float2 step = neighbor.xy - fp.xy;
                                float stepLength = length(step);
                                float2 forward = float2(0.0, 1.0);
                                if (neighbor.w > 0.5 && abs(neighbor.z - fp.z) < 0.35 && stepLength > 0.05 && stepLength < 2.0)
                                    forward = step / stepLength;
                                else
                                {
                                    neighbor = _SandTrailPoints[(k + 14) % 16];
                                    step = fp.xy - neighbor.xy;
                                    stepLength = length(step);
                                    if (neighbor.w > 0.5 && abs(neighbor.z - fp.z) < 0.35 && stepLength > 0.05 && stepLength < 2.0)
                                        forward = step / stepLength;
                                }
                                float2 delta = pos.xz - fp.xy;
                                float2 footUV = float2(dot(delta, float2(forward.y, -forward.x)), dot(delta, forward));
                                // Rounded sole and a separate heel indentation, softly eroded with age.
                                float sole = length((footUV - float2(0.0, 0.035)) / float2(0.075, 0.145));
                                float heel = length((footUV + float2(0.0, 0.105)) / float2(0.060, 0.065));
                                float fd = min(sole, heel);
                                float fade = 1.0 - smoothstep(0.25, 1.0, fp.z);
                                footIndent = max(footIndent, (1.0 - smoothstep(0.60, 1.05, fd)) * fade);
                                footRim = max(footRim, smoothstep(0.82, 1.05, fd) * (1.0 - smoothstep(1.05, 1.35, fd)) * fade);
                            }
                        }
                        float3 indentCol = SRGBToLinear(float3(0.63, 0.56, 0.39));
                        float3 rimCol = SRGBToLinear(float3(0.94, 0.87, 0.67));
                        drySand = lerp(drySand, rimCol, footRim * 0.23);
                        drySand = lerp(drySand, indentCol, footIndent * 0.48);
                    }

                    // Dynamic wave runup cycle (4.8s period: wave surges onto sand and recedes)
                    float waveCycle = _Time.y * 1.25 + sin(pos.x * 0.18) * 0.6;
                    float waveSurge = sin(waveCycle);
                    float surgeReach = 0.25 + waveSurge * 0.38;

                    // Wet sand darkens smoothly where the swash reaches
                    float3 wetSand = SRGBToLinear(float3(0.72, 0.63, 0.43)) * (1.0 + sandGrain * 0.6);
                    float wetBlend = smoothstep(-0.35, 0.95, shoreDist);
                    float3 sandCol = lerp(wetSand, drySand, wetBlend);

                    // 2. Water Depth Color Gradient:
                    // Sand-tinted aqua softens into the deeper turquoise lagoon.
                    float depthMeters = max(0.0, -shoreDist);
                    float d1 = smoothstep(0.0, 0.85, depthMeters);
                    float d2 = smoothstep(0.70, 2.25, depthMeters);
                    float d3 = smoothstep(2.00, 3.85, depthMeters);

                    float3 veryShallow = SRGBToLinear(float3(0.64, 0.82, 0.70));
                    float3 midTurquoise = SRGBToLinear(float3(0.38, 0.77, 0.72));
                    float3 deeperTurquoise = SRGBToLinear(float3(0.28, 0.68, 0.68));
                    float3 deepTeal = SRGBToLinear(float3(0.20, 0.55, 0.60));

                    float3 seaColor = lerp(veryShallow, midTurquoise, d1);
                    seaColor = lerp(seaColor, deeperTurquoise, d2);
                    seaColor = lerp(seaColor, deepTeal, d3);

                    // Smooth transition from beach sand into shallow water following the wave surge
                    float waterMask = smoothstep(surgeReach + 0.22, -0.65, shoreDist);
                    baseColor = lerp(sandCol, seaColor, waterMask);

                    // 3. Crisp Animated Voronoi Water Caustics
                    if (shoreDist < surgeReach)
                    {
                        float t = _Time.y * 0.48;
                        float2 cUV1 = pos.xz * 0.28 + float2(sin(pos.z * 0.22) * 0.14, cos(pos.x * 0.22) * 0.14);
                        float2 cUV2 = pos.xz * 0.44 - float2(t * 0.08, t * 0.05);
                        float c1 = smoothstep(0.072, 0.008, Voronoi2(cUV1, t).y);
                        float c2 = smoothstep(0.065, 0.008, Voronoi2(cUV2, -t * 0.7).y);
                        float caustic = saturate(c1 * 0.80 + c2 * 0.30);

                        float causticFade = smoothstep(surgeReach, -0.45, shoreDist) * smoothstep(-14.0, -2.0, shoreDist);
                        float3 causticTint = SRGBToLinear(float3(0.72, 0.95, 0.94));
                        baseColor = lerp(baseColor, causticTint, caustic * causticFade * 0.10);
                    }

                    // 4. Surging & Receding Lacy Cellular Shoreline Foam Physics
                    if (shoreDist > -1.8 && shoreDist < 0.95)
                    {
                        float wobble = sin(pos.x * 0.85 + _Time.y * 0.9) * 0.05 + (ValueNoise(pos.xz * 2.4) - 0.5) * 0.09;
                        float dRel = shoreDist - surgeReach + wobble;

                        float leadLine = smoothstep(0.042, 0.009, abs(dRel));
                        float secondOffset = 0.46 + (1.0 - waveSurge) * 0.12;
                        float secondLine = smoothstep(0.038, 0.008, abs(dRel + secondOffset));

                        float swashZone = smoothstep(0.02, -0.04, dRel) * smoothstep(-secondOffset + 0.02, -secondOffset + 0.16, dRel);
                        float patchMask = smoothstep(0.42, 0.74, ValueNoise(pos.xz * 0.45 + float2(_Time.y * 0.15, 0.0)));
                        float2 laceVor = Voronoi2(pos.xz * 3.8 + float2(0.0, -waveSurge * 0.6), _Time.y * 1.2);
                        float cellWeb = smoothstep(0.09, 0.015, laceVor.y);
                        float surgeStrength = smoothstep(0.15, 0.92, waveSurge);
                        float lacySheet = swashZone * cellWeb * patchMask * surgeStrength * 0.82;

                        float detailNoise = ValueNoise(float2(pos.x * 12.5, pos.z * 12.5 - _Time.y * 0.5));
                        float droplets = smoothstep(0.15, 0.0, abs(dRel + secondOffset * 0.5)) * smoothstep(0.72, 0.84, detailNoise) * 0.65;

                        float foamMask = saturate(leadLine * 0.78 + secondLine * 0.61 + lacySheet * 0.72 + droplets * 0.72);
                        unlitFoam = max(unlitFoam, foamMask);
                    }

                    // 5. Concentric Foam Ripples around the Shore Rock at (4.3, Shore(4.3)-0.65)
                    if (shoreDist < 0.4)
                    {
                        float2 rockPos = float2(4.3, ShoreZ(4.3) - 0.65);
                        float rDist = length(pos.xz - rockPos);
                        if (rDist < 1.25 && rDist > 0.25)
                        {
                            float rockWave = frac(rDist * 1.8 - _Time.y * 0.45);
                            float rockRing = smoothstep(0.12, 0.02, abs(rockWave - 0.5)) * smoothstep(1.25, 0.45, rDist);
                            float rockCutout = smoothstep(0.38, 0.62, ValueNoise(pos.xz * 6.5 + _Time.y * 0.4));
                            unlitFoam = max(unlitFoam, rockRing * rockCutout * 0.85);
                        }

                        // 6. Interactive Concentric Water Ripples around the Fishing Bobber (_BobberData)
                        if (_BobberData.w > 0.5)
                        {
                            float bDist = length(pos.xz - _BobberData.xy);
                            float maxR = 1.6 * _BobberData.z;
                            if (bDist < maxR)
                            {
                                float ripplePhase = frac(bDist * 2.8 - _Time.y * (1.2 * _BobberData.z));
                                float ring = smoothstep(0.14, 0.02, abs(ripplePhase - 0.4)) * smoothstep(maxR, 0.08, bDist);
                                unlitFoam = max(unlitFoam, ring * 0.82);
                            }
                        }

                        // 7. Crisp Pinpoint Sun Glints / Sparkles on the Water
                        float2 glintUV1 = pos.xz * 13.5 + float2(_Time.y * 1.4, -_Time.y * 1.1);
                        float2 glintUV2 = pos.xz * 16.0 + float2(-_Time.y * 1.2, _Time.y * 1.5);
                        float glintNoise = ValueNoise(glintUV1) * ValueNoise(glintUV2);
                        float glintZone = smoothstep(1.5, -2.5, pos.x) * smoothstep(0.0, -0.6, shoreDist) * smoothstep(-3.8, -1.0, shoreDist);
                        float sunGlint = smoothstep(0.68, 0.76, glintNoise) * glintZone;
                        unlitFoam = max(unlitFoam, sunGlint * 0.90);
                    }
                }
                else
                {
                    baseColor = MeadowColor(pos.xz, vCol);
                }

                if (grassCoverage > 0.0 && !isMeadow)
                {
                    float3 meadow = MeadowColor(pos.xz, vCol);
                    // A restrained dark grass fringe helps paths read like worn clearings.
                    float verge = smoothstep(0.18, 0.48, grassCoverage) * (1.0 - smoothstep(0.65, 0.97, grassCoverage));
                    meadow *= 1.0 - verge * 0.09;
                    baseColor = lerp(baseColor, meadow, grassCoverage);
                }

                // Soft sunlit shading with clean turquoise-sky tinted shadows
                float lambert = saturate(dot(N, mainLight.direction) * 0.5 + 0.5);
                float3 shadowTint = shoreDist < 0.0 ? float3(0.46, 0.62, 0.64) : float3(0.54, 0.60, 0.56);
                float3 shadowFactor = lerp(shadowTint, float3(1.0, 1.0, 1.0), mainLight.shadowAttenuation);
                float3 ambient = SampleSH(N) * 0.45;
                float3 direct = mainLight.color * (0.36 + 0.64 * lambert) * shadowFactor;

                float3 finalRGB = baseColor * (ambient + direct);

                // Apply bright white shoreline foam, ripples & sun glints
                if (unlitFoam > 0.001)
                {
                    float3 foamWhite = SRGBToLinear(float3(0.97, 0.99, 0.98)) * (0.75 + 0.25 * mainLight.shadowAttenuation);
                    finalRGB = lerp(finalRGB, foamWhite, saturate(unlitFoam * 0.86));
                }

                return half4(finalRGB, 1.0);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}

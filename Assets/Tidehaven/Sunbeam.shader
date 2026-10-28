Shader "Tidehaven/Sunbeam"
{
    Properties
    {
        _Color ("Sunbeam Color", Color) = (1.0, 0.95, 0.78, 0.085)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+50" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "SunbeamForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Soft across-beam bell curve & longitudinal fade
                float edgeFade = sin(saturate(input.uv.x) * 3.14159);
                edgeFade = edgeFade * edgeFade;
                float lenFade = smoothstep(0.0, 0.25, input.uv.y) * smoothstep(1.0, 0.15, input.uv.y);
                float shimmer = 0.85 + 0.15 * sin(_Time.y * 0.7 + input.uv.x * 9.0 + input.color.r * 6.28);
                float alpha = _Color.a * edgeFade * lenFade * shimmer * input.color.a;
                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}

Shader "MeltFall/Dissolve"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.8, 0.75, 0.5, 1)
        _Cutoff ("Dissolve", Range(0, 1)) = 0
        _DissolveColor ("Edge Color", Color) = (1, 0.5, 0, 1)
        _EdgeWidth ("Edge Width", Range(0, 0.3)) = 0.08
        _NoiseScale ("Noise Scale", Float) = 5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _DissolveColor;
                float _Cutoff;
                float _EdgeWidth;
                float _NoiseScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv         : TEXCOORD1;
            };

            // Small 2D hash -> value noise. Deterministic, cheap, no textures.
            float Hash2(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                // Smoothstep interpolation weights.
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = Hash2(i + float2(0.0, 0.0));
                float b = Hash2(i + float2(1.0, 0.0));
                float c = Hash2(i + float2(0.0, 1.0));
                float d = Hash2(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs positions = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = positions.positionCS;
                OUT.positionWS = positions.positionWS;
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Procedural noise driven by UV so it works on any mesh; scaled by _NoiseScale.
                float noise = ValueNoise(IN.uv * _NoiseScale);

                // Carve away the dissolved region.
                float edge = noise - _Cutoff;
                clip(edge);

                half3 color = _BaseColor.rgb;

                // Glow the thin band just above the cutoff toward the edge color.
                if (_EdgeWidth > 0.0)
                {
                    float edgeT = 1.0 - saturate(edge / _EdgeWidth);
                    color = lerp(color, _DissolveColor.rgb, edgeT);
                }

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}

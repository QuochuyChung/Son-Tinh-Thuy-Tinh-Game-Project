// Stylized waterfall sheet: vertical noise streaks scrolling down the mesh (v = 0 at the top lip, 1 at the foot), soft edges,
// thin at the lip and foamy at the foot. Unlit and transparent, works with the URP fog. Used by MapDecor.Waterfall.
Shader "SonTinhThuyTinh/Waterfall"
{
    Properties
    {
        _Color ("Water Color", Color) = (0.55, 0.80, 0.92, 0.55)
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _Speed ("Flow Speed", Float) = 1.8
        _Streaks ("Streak Density", Float) = 9
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _FoamColor;
                float _Speed;
                float _Streaks;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fogCoord : TEXCOORD1;
            };

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float t = _Time.y * _Speed;

                // streaks: noise stretched along the flow, two layers moving at different speeds
                float2 p1 = float2(input.uv.x * _Streaks, input.uv.y * 2.5 - t);
                float2 p2 = float2(input.uv.x * _Streaks * 2.3 + 7.3, input.uv.y * 4.0 - t * 1.6);
                float n = ValueNoise(p1) * 0.6 + ValueNoise(p2) * 0.4;

                float edge = smoothstep(0.0, 0.2, input.uv.x) * smoothstep(1.0, 0.8, input.uv.x);   // ragged-looking soft sides
                edge *= lerp(0.55, 1.0, ValueNoise(float2(input.uv.y * 6.0 - t * 0.5, input.uv.x * 3.0)));
                float lip = smoothstep(0.0, 0.07, input.uv.y);                                        // thin at the top
                float foot = smoothstep(0.80, 1.0, input.uv.y);                                       // foam at the bottom

                half3 color = lerp(_Color.rgb, _FoamColor.rgb, saturate(n * 1.3 - 0.15 + foot * 0.9));
                half alpha = saturate((_Color.a + n * 0.55 + foot * 0.4) * edge * lip);

                half4 result = half4(color, alpha);
                result.rgb = MixFog(result.rgb, input.fogCoord);
                return result;
            }
            ENDHLSL
        }
    }
}

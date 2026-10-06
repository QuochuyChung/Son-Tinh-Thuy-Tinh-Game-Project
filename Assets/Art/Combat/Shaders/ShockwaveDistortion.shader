Shader "Custom/URP_ShockwaveDistortion"
{
    Properties
    {
        _DistortionStrength ("Distortion Strength", Range(0, 0.1)) = 0.04
        _RingWidth ("Ring Width", Range(0.01, 0.5)) = 0.12
        _Tint ("Tint Color", Color) = (0.5, 0.85, 1.0, 0.3)
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent+50" 
            "RenderPipeline"="UniversalPipeline" 
        }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "DistortionPass"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float4 color : COLOR;
            };

            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            CBUFFER_START(UnityPerMaterial)
                float _DistortionStrength;
                float _RingWidth;
                float4 _Tint;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.color = input.color;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 center = float2(0.5, 0.5);
                float2 toCenter = input.uv - center;
                float dist = length(toCenter);
                float2 dir = dist > 0.001 ? normalize(toCenter) : float2(0.0, 0.0);

                // Ring crest profile
                float ringDist = abs(dist - 0.4);
                float ringMask = saturate(1.0 - ringDist / max(0.01, _RingWidth));
                ringMask = smoothstep(0.0, 1.0, ringMask);

                float quadFade = saturate((0.5 - dist) * 12.0);
                float strength = ringMask * quadFade * _DistortionStrength * input.color.a;

                float2 screenUV = input.screenPos.xy / max(0.0001, input.screenPos.w);
                float2 distortedUV = screenUV + dir * strength;

                float4 sceneColor = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, distortedUV);
                float4 tinted = sceneColor + _Tint * (ringMask * input.color.a * 0.5);
                return float4(tinted.rgb, saturate(ringMask * quadFade * input.color.a * 1.5));
            }
            ENDHLSL
        }
    }
}

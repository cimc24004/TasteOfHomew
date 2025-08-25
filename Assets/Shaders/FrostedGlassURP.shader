Shader "Custom/FrostedGlassURP"
{
    Properties
    {
        _Tint("Tint (A = Opacity)", Color) = (1,1,1,0.30)
        _BlurRadius("Blur Radius (px)", Range(0,12)) = 5
        _Distortion("Distortion Amount", Range(0,0.08)) = 0.02

        [NoScaleOffset]_NoiseTex("Noise (optional)", 2D) = "gray" {}
        _NoiseTiling("Noise Tiling", Float) = 8
        _NoiseStrength("Noise Strength", Range(0,1)) = 0.4

        [Toggle] _DOUBLE_SIDED("Double Sided", Float) = 1
    }

    SubShader
    {
        Tags{ "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            Name "FrostedGlass"
            Tags{ "LightMode"="UniversalForward" }

            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile _ _DOUBLE_SIDED_ON

            // URP libs
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _BlurRadius;
                float _Distortion;
                float _NoiseTiling;
                float _NoiseStrength;
                float _DOUBLE_SIDED;
            CBUFFER_END

            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv0        : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                float2 uv0        : TEXCOORD2;
                float2 screenUV   : TEXCOORD3;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(posWS);

                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = normalize(GetCameraPositionWS() - posWS);

                OUT.uv0 = IN.uv0;
                OUT.screenUV = GetNormalizedScreenSpaceUV(OUT.positionCS); // 0..1
                return OUT;
            }

            // 12サンプルのPoissonディスク（ブラー核）
            static const float2 kPoisson[12] = {
                float2(-0.326, -0.406), float2(-0.840, -0.074),
                float2(-0.696,  0.457), float2(-0.203,  0.621),
                float2( 0.962, -0.195), float2( 0.473, -0.480),
                float2( 0.519,  0.767), float2( 0.185, -0.893),
                float2( 0.089,  0.366), float2(-0.521, -0.713),
                float2( 0.693, -0.055), float2( 0.253,  0.100)
            };

            // ブラー + ノイズ歪み + 法線に基づく軽い屈折（Depthは使わない）
            half4 frag (Varyings IN) : SV_Target
            {
                // 両面時は表向きに
                float3 n = normalize(IN.normalWS);
                if (_DOUBLE_SIDED > 0.5)
                    n = faceforward(n, -IN.viewDirWS, n);

                // 画面ピクセルスケール
                float2 texel = _ScreenParams.zw * _BlurRadius; // 1/width,1/height * px

                // ノイズでUV揺らし（なくてもOK）
                float2 nUV = IN.uv0 * _NoiseTiling;
                float2 noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, nUV).rg * 2.0 - 1.0;
                float2 noiseOffset = noise * (_NoiseStrength * 0.5);

                // 法線XYでスクリーン屈折方向（控えめ）
                float2 refractOffset = n.xy * _Distortion;

                float3 col = 0;
                // 中心も含めて12+1の擬似ガウシアン
                col += SampleSceneColor(IN.screenUV + refractOffset + noiseOffset).rgb * 0.24;

                [unroll]
                for (int i = 0; i < 12; i++)
                {
                    float2 o = kPoisson[i] * texel;
                    col += SampleSceneColor(IN.screenUV + refractOffset + noiseOffset + o).rgb * 0.063; // 12 * 0.063 ≒ 0.756
                }
                // 重み合計 ≒ 1.0 に揃う

                // 色味と不透明度
                float alpha = saturate(_Tint.a);
                float3 tinted = lerp(col, col * _Tint.rgb, 0.5); // ほんのり色ガラス

                return half4(tinted, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

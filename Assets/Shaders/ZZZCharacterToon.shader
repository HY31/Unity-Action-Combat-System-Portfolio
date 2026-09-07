Shader "Project/Character/ZZZ Toon"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Strength", Range(0, 2)) = 1
        [Toggle] _AlphaClip("Alpha Clip", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 0)
        _EmissionMap("Emission Map", 2D) = "white" {}
        _EmissionStrength("Emission Strength", Range(0, 5)) = 0

        [Header(Toon Lighting)]
        _ShadowColor("Shadow Color", Color) = (0.48, 0.55, 0.68, 1)
        _MidColor("Mid Color", Color) = (0.82, 0.86, 0.92, 1)
        _ShadowThreshold("Shadow Threshold", Range(0, 1)) = 0.36
        _HighlightThreshold("Highlight Threshold", Range(0, 1)) = 0.78
        _BandSoftness("Band Softness", Range(0.001, 0.2)) = 0.035
        _AmbientStrength("Ambient Strength", Range(0, 1)) = 0.32
        [Toggle] _IsFace("Face Lighting", Float) = 0

        [Header(Toon Specular)]
        _SpecularColor("Specular Color", Color) = (1, 0.96, 0.88, 1)
        _SpecularThreshold("Specular Threshold", Range(0, 1)) = 0.82
        _SpecularSoftness("Specular Softness", Range(0.001, 0.2)) = 0.025
        _SpecularStrength("Specular Strength", Range(0, 1)) = 0.16

        [Header(Rim Light)]
        _RimColor("Rim Color", Color) = (0.72, 0.9, 1, 1)
        _RimPower("Rim Power", Range(1, 12)) = 5
        _RimStrength("Rim Strength", Range(0, 1)) = 0.12

        [Header(Outline)]
        _OutlineColor("Outline Color", Color) = (0.018, 0.025, 0.04, 1)
        _OutlineWidth("Outline Width", Range(0, 3)) = 0.85
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ToonVertex
            #pragma fragment ToonFragment
            #pragma multi_compile _ FOG_LINEAR FOG_EXP FOG_EXP2
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _EmissionColor;
                half _EmissionStrength;
                half4 _ShadowColor;
                half4 _MidColor;
                half4 _SpecularColor;
                half4 _RimColor;
                half4 _OutlineColor;
                half _BumpScale;
                half _AlphaClip;
                half _Cutoff;
                half _ShadowThreshold;
                half _HighlightThreshold;
                half _BandSoftness;
                half _AmbientStrength;
                half _IsFace;
                half _SpecularThreshold;
                half _SpecularSoftness;
                half _SpecularStrength;
                half _RimPower;
                half _RimStrength;
                half _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
                half4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 tangentWS : TEXCOORD2;
                half3 bitangentWS : TEXCOORD3;
                float2 uv : TEXCOORD4;
                float4 shadowCoord : TEXCOORD5;
                half fogFactor : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ToonVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = normalInputs.tangentWS;
                output.bitangentWS = normalInputs.bitangentWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.shadowCoord = GetShadowCoord(positionInputs);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 ToonFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                if (_AlphaClip > 0.5h)
                    clip(baseSample.a - _Cutoff);

                half3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv),
                    _BumpScale);
                half3 normalWS = normalize(
                    normalTS.x * input.tangentWS +
                    normalTS.y * input.bitangentWS +
                    normalTS.z * input.normalWS);

                Light mainLight = GetMainLight(input.shadowCoord);
                half nDotL = saturate(dot(normalWS, mainLight.direction));
                half shadowAttenuation = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                half bandInput = nDotL * lerp(0.28h, 1.0h, shadowAttenuation);

                // 얼굴 전용 SDF가 없는 동안 코와 눈 주변에 생기는 거친 동적 그림자를 억제한다.
                bandInput = lerp(bandInput, max(bandInput, 0.72h), saturate(_IsFace));

                half softness = max(_BandSoftness, 0.001h);
                half midBand = smoothstep(
                    _ShadowThreshold - softness,
                    _ShadowThreshold + softness,
                    bandInput);
                half highlightBand = smoothstep(
                    _HighlightThreshold - softness,
                    _HighlightThreshold + softness,
                    nDotL);

                half3 toonRamp = lerp(_ShadowColor.rgb, _MidColor.rgb, midBand);
                toonRamp = lerp(toonRamp, half3(1.0h, 1.0h, 1.0h), highlightBand);

                half3 ambient = max(SampleSH(normalWS), half3(0.0h, 0.0h, 0.0h)) * _AmbientStrength;
                half3 lighting = toonRamp * mainLight.color + ambient;

                half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 halfDirection = SafeNormalize(mainLight.direction + viewDirectionWS);
                half nDotH = saturate(dot(normalWS, halfDirection));
                half specularBand = smoothstep(
                    _SpecularThreshold - _SpecularSoftness,
                    _SpecularThreshold + _SpecularSoftness,
                    nDotH);
                specularBand *= midBand * _SpecularStrength * (1.0h - saturate(_IsFace) * 0.7h);

                half rim = pow(saturate(1.0h - dot(normalWS, viewDirectionWS)), _RimPower);
                rim *= _RimStrength * smoothstep(0.08h, 0.65h, nDotL);

                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb
                    * _EmissionColor.rgb * _EmissionStrength;
                half3 color = baseSample.rgb * lighting;
                color += _SpecularColor.rgb * specularBand;
                color += _RimColor.rgb * rim;
                color += emission;
                color = MixFog(color, input.fogFactor);

                return half4(color, baseSample.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ToonOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _EmissionColor;
                half _EmissionStrength;
                half4 _ShadowColor;
                half4 _MidColor;
                half4 _SpecularColor;
                half4 _RimColor;
                half4 _OutlineColor;
                half _BumpScale;
                half _AlphaClip;
                half _Cutoff;
                half _ShadowThreshold;
                half _HighlightThreshold;
                half _BandSoftness;
                half _AmbientStrength;
                half _IsFace;
                half _SpecularThreshold;
                half _SpecularSoftness;
                half _SpecularStrength;
                half _RimPower;
                half _RimStrength;
                half _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings OutlineVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                half3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                positionWS += normalWS * (_OutlineWidth * 0.002);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 OutlineFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                if (_AlphaClip > 0.5h)
                    clip(alpha - _Cutoff);
                return _OutlineColor;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

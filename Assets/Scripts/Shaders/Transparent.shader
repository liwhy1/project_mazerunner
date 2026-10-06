Shader "Custom/TransparentLit"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Transparency("Transparency", Range(0.0, 1.0)) = 0.5

        [HideInInspector] _Surface("__surface", Float) = 1.0
        [HideInInspector] _Blend("__blend", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 5.0
        [HideInInspector] _DstBlend("__dst", Float) = 10.0
        [HideInInspector] _ZWrite("__zw", Float) = 0.0
        [HideInInspector] _Cull("__cull", Float) = 2.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM

            #pragma target 3.0

            #pragma vertex DepthVertex
            #pragma fragment DepthFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)

                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Transparency;

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
            };

            Varyings DepthVertex(Attributes input)
            {
                Varyings output;

                output.positionCS =
                    TransformObjectToHClip(input.positionOS.xyz);

                output.uv =
                    TRANSFORM_TEX(input.uv, _BaseMap);

                return output;
            }

            half4 DepthFragment(Varyings input) : SV_Target
            {
                half textureAlpha =
                    SAMPLE_TEXTURE2D(
                        _BaseMap,
                        sampler_BaseMap,
                        input.uv
                    ).a;

                half alpha =
                    textureAlpha *
                    _BaseColor.a *
                    _Transparency;

                // Completely invisible objects should not occupy
                // the depth buffer.
                clip(alpha - 0.001);

                return 0;
            }

            ENDHLSL
        }

        Pass
        {
            Name "ForwardLit"

            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Blend SrcAlpha OneMinusSrcAlpha

            ZWrite Off
            ZTest LEqual

            Cull Back

            HLSLPROGRAM

            #pragma target 3.0

            #pragma vertex LitVertex
            #pragma fragment LitFragment

            // Main light
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN

            // Additional lights
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX
            #pragma multi_compile _ _ADDITIONAL_LIGHTS

            // Additional light shadows
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS

            // Soft shadows
            #pragma multi_compile _ _SHADOWS_SOFT

            // Fog
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)

                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Transparency;

            CBUFFER_END


            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };


            struct Varyings
            {
                float4 positionCS : SV_POSITION;

                float3 positionWS : TEXCOORD0;

                float3 normalWS : TEXCOORD1;

                float2 uv : TEXCOORD2;

                float fogFactor : TEXCOORD3;
            };


            Varyings LitVertex(Attributes input)
            {
                Varyings output;

                output.positionCS =
                    TransformObjectToHClip(input.positionOS.xyz);

                output.positionWS =
                    TransformObjectToWorld(input.positionOS.xyz);

                output.normalWS =
                    TransformObjectToWorldNormal(input.normalOS);

                output.uv =
                    TRANSFORM_TEX(input.uv, _BaseMap);

                output.fogFactor =
                    ComputeFogFactor(output.positionCS.z);

                return output;
            }


            half4 LitFragment(Varyings input) : SV_Target
            {
                // ----------------------------------------------------
                // Texture
                // ----------------------------------------------------

                half4 textureColor =
                    SAMPLE_TEXTURE2D(
                        _BaseMap,
                        sampler_BaseMap,
                        input.uv
                    );

                half3 albedo =
                    textureColor.rgb *
                    _BaseColor.rgb;

                half alpha =
                    textureColor.a *
                    _BaseColor.a *
                    _Transparency;


                // ----------------------------------------------------
                // Surface
                // ----------------------------------------------------

                InputData inputData = (InputData)0;

                inputData.positionWS =
                    input.positionWS;

                inputData.normalWS =
                    normalize(input.normalWS);

                inputData.viewDirectionWS =
                    GetWorldSpaceNormalizeViewDir(
                        input.positionWS
                    );

                inputData.normalizedScreenSpaceUV =
                    GetNormalizedScreenSpaceUV(
                        input.positionCS
                    );


                // ----------------------------------------------------
                // Main light shadows
                // ----------------------------------------------------

                #if defined(_MAIN_LIGHT_SHADOWS) || \
                    defined(_MAIN_LIGHT_SHADOWS_CASCADE) || \
                    defined(_MAIN_LIGHT_SHADOWS_SCREEN)

                    inputData.shadowCoord =
                        TransformWorldToShadowCoord(
                            input.positionWS
                        );

                #else

                    inputData.shadowCoord =
                        float4(0, 0, 0, 0);

                #endif


                // ----------------------------------------------------
                // Fog
                // ----------------------------------------------------

                inputData.fogCoord =
                    input.fogFactor;


                // ----------------------------------------------------
                // Surface data
                // ----------------------------------------------------

                SurfaceData surfaceData = (SurfaceData)0;

                surfaceData.albedo =
                    albedo;

                surfaceData.metallic =
                    0.0;

                surfaceData.smoothness =
                    0.5;

                surfaceData.normalTS =
                    half3(0, 0, 1);

                surfaceData.emission =
                    half3(0, 0, 0);

                surfaceData.occlusion =
                    1.0;

                surfaceData.alpha =
                    alpha;


                // ----------------------------------------------------
                // URP lighting
                // ----------------------------------------------------

                half4 color =
                    UniversalFragmentPBR(
                        inputData,
                        surfaceData
                    );


                // ----------------------------------------------------
                // Fog
                // ----------------------------------------------------

                color.rgb =
                    MixFog(
                        color.rgb,
                        inputData.fogCoord
                    );


                return color;
            }

            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"

            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            Cull Back

            HLSLPROGRAM

            #pragma target 3.0

            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;


            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };


            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };


            float4 GetShadowPositionHClip(
                float3 positionWS,
                float3 normalWS
            )
            {
                float3 biasedPositionWS =
                    ApplyShadowBias(
                        positionWS,
                        normalWS,
                        _LightDirection
                    );

                float4 positionCS =
                    TransformWorldToHClip(
                        biasedPositionWS
                    );

                #if UNITY_REVERSED_Z

                    positionCS.z =
                        min(
                            positionCS.z,
                            UNITY_NEAR_CLIP_VALUE
                        );

                #else

                    positionCS.z =
                        max(
                            positionCS.z,
                            UNITY_NEAR_CLIP_VALUE
                        );

                #endif

                return positionCS;
            }


            Varyings ShadowVertex(Attributes input)
            {
                Varyings output;

                float3 positionWS =
                    TransformObjectToWorld(
                        input.positionOS.xyz
                    );

                float3 normalWS =
                    TransformObjectToWorldNormal(
                        input.normalOS
                    );

                output.positionCS =
                    GetShadowPositionHClip(
                        positionWS,
                        normalWS
                    );

                return output;
            }


            half4 ShadowFragment(Varyings input) : SV_Target
            {
                return 0;
            }

            ENDHLSL
        }
    }

    FallBack Off
}
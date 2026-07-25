Shader "Custom/UnityCameraDepthMap"
{
    Properties
    {
        _NearPlane ("Near Plane (White)", Float) = 0.1
        _FarPlane ("Far Plane (Black)", Float) = 10.0
        _WhiteValue ("White Value", Range(0, 1)) = 1.0
        _BlackValue ("Black Value", Range(0, 1)) = 0.0
        _Bias ("Bias (< 1 = more white, > 1 = more black)", Float) = 1.0
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100

        Pass
        {
            Name "UnityCameraDepthMap"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float _NearPlane;
                float _FarPlane;
                float _WhiteValue;
                float _BlackValue;
                float _Bias;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Sample depth texture (raw depth buffer value)
                float rawDepth = SampleSceneDepth(input.uv);

                // Convert to linear eye depth (distance from camera in world units)
                float linearDepth = LinearEyeDepth(rawDepth, _ZBufferParams);

                // Map depth to 0-1 range based on near/far planes
                // Near = 1 (white), Far = 0 (black)
                float normalizedDepth = saturate((linearDepth - _NearPlane) / (_FarPlane - _NearPlane));
                float depthValue = 1.0 - normalizedDepth; // Invert so near=white, far=black

                // Apply bias for non-linear adjustment
                // bias < 1: pushes values towards white (gamma < 1 brightens midtones)
                // bias > 1: pushes values towards black (gamma > 1 darkens midtones)
                float biasedDepth = pow(depthValue, _Bias);

                // Remap from [0,1] to [BlackValue, WhiteValue]
                float finalValue = lerp(_BlackValue, _WhiteValue, biasedDepth);

                return half4(finalValue, finalValue, finalValue, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

Shader "Custom/InsideOutCubemapXR_URP"
{
    Properties
    {
        _CubeMap("Cubemap", CUBE) = "" {} 
        _RotationSpeed("Rotation Speed (deg/sec)", Float) = 0.1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Background" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "InsideOutCubemapPass"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multicompile  _STEREO_INSTANCING_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURECUBE(_CubeMap);
            SAMPLER(sampler_CubeMap);

            CBUFFER_START(UnityPerMaterial)
                float _RotationSpeed;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 viewDirWS   : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 worldPos = TransformObjectToWorld(IN.positionOS);
                OUT.positionHCS = TransformWorldToHClip(worldPos);
                OUT.viewDirWS = worldPos - GetCameraPositionWS();
                return OUT;
            }

            // Funzione per ruotare un vettore attorno all'asse Y
            float3 RotateY(float3 dir, float angleRad)
            {
                float s = sin(angleRad);
                float c = cos(angleRad);
                float3x3 m = float3x3(
                    c, 0, s,
                    0, 1, 0,
                    -s, 0, c
                );
                return mul(m, dir);
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // Calcola l’angolo in radianti: _Time.y è in secondi
                float angleRad = radians(_RotationSpeed) * _Time.y;

                // Ruota la direzione di vista attorno all’asse Y
                float3 rotatedDir = RotateY(normalize(IN.viewDirWS), angleRad);

                return SAMPLE_TEXTURECUBE(_CubeMap, sampler_CubeMap, rotatedDir);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
Shader "BakedVolumetrics/MetaPassObjectShaderV2"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode("Cull Mode", Int) = 2
        _MainTex("Texture", 2D) = "white" {}
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
    }
    SubShader
    {
        Tags
        {
            //"RenderType" = "Opaque"
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
        }

        Cull[_CullMode]
        //ZTest Always
        //ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vertex_base
            #pragma fragment fragment_base

            #pragma multi_compile_local _ EMISSION_HDR_RGBM
            #pragma multi_compile_local _ EMISSION_HDR_RGBD
            #pragma multi_compile_local _ EMISSION_HDR_RGBE
            #pragma multi_compile_local _ EMISSION_HDR_LOG_LUV_32

            //||||||||||||||||||||||||||||| UNITY3D INCLUDES |||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||| UNITY3D INCLUDES |||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||| UNITY3D INCLUDES |||||||||||||||||||||||||||||

            #include "UnityCG.cginc"
            #include "MetaPassPacking.cginc"

            //||||||||||||||||||||||||||||| SHADER PARAMETERS |||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||| SHADER PARAMETERS |||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||| SHADER PARAMETERS |||||||||||||||||||||||||||||

            sampler2D _MainTex;

            float _Cutoff;

            struct meshData
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv1 : TEXCOORD1;
            };

            struct vertexToFragment
            {
                float4 vertexCameraClipPosition : SV_POSITION;
                float2 uv1 : TEXCOORD0;
                float3 normalWorld : TEXCOORD1;
            };

            vertexToFragment vertex_base(meshData data)
            {
                vertexToFragment vertex;

                vertex.vertexCameraClipPosition = UnityObjectToClipPos(data.vertex);

                //An attempt at geometry thickening during voxelization.
                //float4 vertexExtrusionValue = mul(unity_ObjectToWorld, float4(_VertexExtrusion, _VertexExtrusion, _VertexExtrusion, 0));
                //vertex.vertexCameraClipPosition = UnityObjectToClipPos(data.vertex + data.normal * length(vertexExtrusionValue));

                vertex.uv1 = data.uv1.xy;

                vertex.normalWorld = data.normal;

                return vertex;
            }

            float4 fragment_base(vertexToFragment vertex) : SV_Target
            {
                float4 packedColor = tex2D(_MainTex, vertex.uv1);
                float3 normalColor = vertex.normalWorld.xyz * 0.5 + 0.5;

                float4 outputAlbedoColor = float4(0, 0, 0, 0);
                float4 outputEmissiveColor = float4(0, 0, 0, 0);
                float4 outputNormalColor = float4(0, 0, 0, 0);

                UnpackMetaBuffer(packedColor, outputAlbedoColor, outputEmissiveColor, outputNormalColor);

                //clip(any(ALBEDO_A1) ? 1 : 0);

                //clip(mainColor.a - _Cutoff);

                normalColor = pow(normalColor, 1.0f / 2.2f);

                return PackMetaBuffer(outputAlbedoColor, outputEmissiveColor.rgb, normalColor);
            }
            ENDCG
        }
    }
}

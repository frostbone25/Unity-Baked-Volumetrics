Shader "Hidden/VoxelBufferNormal"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}

        //_VertexExtrusion("Vertex Extrusion", Float) = 0
    }
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
        }

        Cull Back
        //Cull Off
        //ZTest Always
        //ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vertex_base
            #pragma fragment fragment_base

            //||||||||||||||||||||||||||||| UNITY3D INCLUDES |||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||| UNITY3D INCLUDES |||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||| UNITY3D INCLUDES |||||||||||||||||||||||||||||

            #include "UnityCG.cginc"

            //||||||||||||||||||||||||||||| SHADER PARAMETERS |||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||| SHADER PARAMETERS |||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||| SHADER PARAMETERS |||||||||||||||||||||||||||||

            //An attempt at geometry thickening during voxelization.
            //float _VertexExtrusion;

            struct meshData
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct vertexToFragment
            {
                float4 vertexCameraClipPosition : SV_POSITION;
                float3 normalWorld : TEXCOORD0;
            };

            vertexToFragment vertex_base(meshData data)
            {
                vertexToFragment vertex;

                vertex.vertexCameraClipPosition = UnityObjectToClipPos(data.vertex);

                //An attempt at geometry thickening during voxelization.
                //float4 vertexExtrusionValue = mul(unity_ObjectToWorld, float4(_VertexExtrusion, _VertexExtrusion, _VertexExtrusion, 0));
                //vertex.vertexCameraClipPosition = UnityObjectToClipPos(data.vertex + data.normal * length(vertexExtrusionValue));

                vertex.normalWorld = data.normal;

                return vertex;
            }

            float4 fragment_base(vertexToFragment vertex) : SV_Target
            {
                return float4(vertex.normalWorld.xyz * 0.5 + 0.5, 1);
            }
            ENDCG
        }
    }
}

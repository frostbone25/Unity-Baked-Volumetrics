Shader "Hidden/VoxelBufferHidden"
{
    Properties
    {

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

            struct meshData
            {

            };

            struct vertexToFragment
            {

            };

            vertexToFragment vertex_base(meshData data)
            {
                vertexToFragment vertex;

                return vertex;
            }

            float4 fragment_base(vertexToFragment vertex) : SV_Target
            {
                return float4(0, 0, 0, 0);
            }
            ENDCG
        }
    }
}

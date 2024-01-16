Shader "Hidden/VoxelBufferEmissive"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        [HDR]_EmissionColor("Color", Color) = (1,1,1,1)
        _EmissionMap("Emission", 2D) = "black" {}

        //_VertexExtrusion("Vertex Extrusion", Float) = 0
    }
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
        }

        Cull Back
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

            sampler2D _EmissionMap;
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _EmissionColor;

            //An attempt at geometry thickening during voxelization.
            //float _VertexExtrusion;

            struct meshData
            {
                float4 vertex : POSITION;
                float2 uv0 : TEXCOORD0;
            };

            struct vertexToFragment
            {
                float4 vertexCameraClipPosition : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            vertexToFragment vertex_base(meshData data)
            {
                vertexToFragment vertex;

                vertex.vertexCameraClipPosition = UnityObjectToClipPos(data.vertex);

                //An attempt at geometry thickening during voxelization.
                //float4 vertexExtrusionValue = mul(unity_ObjectToWorld, float4(_VertexExtrusion, _VertexExtrusion, _VertexExtrusion, 0));
                //vertex.vertexCameraClipPosition = UnityObjectToClipPos(data.vertex + data.normal * length(vertexExtrusionValue));

                vertex.uv = TRANSFORM_TEX(data.uv0, _MainTex);

                return vertex;
            }

            float4 fragment_base(vertexToFragment vertex) : SV_Target
            {
                return tex2D(_EmissionMap, vertex.uv) * _EmissionColor;
            }
            ENDCG
        }
    }
}

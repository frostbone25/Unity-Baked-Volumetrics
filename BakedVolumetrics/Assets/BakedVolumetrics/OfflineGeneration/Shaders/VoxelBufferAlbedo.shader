Shader "Hidden/VoxelBufferAlbedo"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _MainTex("Texture", 2D) = "white" {}
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _Cutoff;

            //An attempt at geometry thickening during voxelization.
            //float _VertexExtrusion;

            struct meshData
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
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
                return tex2D(_MainTex, vertex.uv) * _Color;
            }
            ENDCG
        }
    }
}

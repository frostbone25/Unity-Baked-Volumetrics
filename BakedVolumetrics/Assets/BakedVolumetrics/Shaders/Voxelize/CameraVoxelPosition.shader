Shader "Hidden/CameraVoxelPosition"
{
    Properties
    {

    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Back
        ZTest Always
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 worldPosition : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPosition = mul(unity_ObjectToWorld, v.vertex);

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return float4(i.worldPosition.xyz, 1);
            }
            ENDCG
        }
    }
}

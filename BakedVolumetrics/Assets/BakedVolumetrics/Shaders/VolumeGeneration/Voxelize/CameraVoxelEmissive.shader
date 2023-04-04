Shader "Hidden/CameraVoxelEmissive"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        [HDR]_EmissionColor("Color", Color) = (1,1,1,1)
        _EmissionMap("Emission", 2D) = "black" {}
    }
        SubShader
        {
            Tags { "RenderType" = "Opaque" }
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
                    float2 uv : TEXCOORD0;
                };

                struct v2f
                {
                    float4 vertex : SV_POSITION;
                    float2 uv : TEXCOORD0;
                };

                sampler2D _EmissionMap;
                sampler2D _MainTex;
                float4 _MainTex_ST;
                float4 _EmissionColor;

                v2f vert(appdata v)
                {
                    v2f o;

                    o.vertex = UnityObjectToClipPos(v.vertex);
                    o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                    return o;
                }

                fixed4 frag(v2f i) : SV_Target
                {
                    return tex2D(_EmissionMap, i.uv) * _EmissionColor;
                }
                ENDCG
            }
        }
}

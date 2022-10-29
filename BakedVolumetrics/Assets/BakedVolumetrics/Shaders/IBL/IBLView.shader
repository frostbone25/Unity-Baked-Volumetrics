Shader "Hidden/IBLView"
{
    Properties
    {

    }
    SubShader
    {
        Tags
        {
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
            "IgnoreProjector" = "True"
            "DisableBatching" = "LODFading"
        }

        Cull Off
        ZTest Always
        ZWrite On

            Pass
            {
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma multi_compile_fwdbase
                #pragma multi_compile _ LIGHTMAP_ON

                #include "UnityCG.cginc"

                struct v2f
                {
                    float4 vertex : SV_POSITION;
                    float2 uvStaticLightmap : TEXCOORD1;
                };

                v2f vert(appdata_full v)
                {
                    v2f o;

                    o.vertex = UnityObjectToClipPos(v.vertex);
                    o.uvStaticLightmap.xy = v.texcoord1.xy * unity_LightmapST.xy + unity_LightmapST.zw;

                    return o;
                }

                fixed4 frag(v2f i) : SV_Target
                {
                    #if defined(LIGHTMAP_ON)
                        float2 lightmapUVs = i.uvStaticLightmap.xy;
                        float4 indirectLightmap = UNITY_SAMPLE_TEX2D(unity_Lightmap, lightmapUVs.xy);

                        #if defined(UNITY_LIGHTMAP_FULL_HDR)
                            indirectLightmap.rgb = DecodeLightmap(indirectLightmap);
                        #else
                            // decodeInstructions.x contains 2.0 when gamma color space is used or pow(2.0, 2.2) = 4.59 when linear color space is used on mobile platforms
                            indirectLightmap.rgb *= 4.59;
                        #endif

                        return float4(indirectLightmap.rgb, 1.0f);
                    #else
                        clip(0.0f);
                        return float4(0.0f, 0.0f, 0.0f, 0.0f);
                    #endif
                }
                ENDCG
            }
        }
}

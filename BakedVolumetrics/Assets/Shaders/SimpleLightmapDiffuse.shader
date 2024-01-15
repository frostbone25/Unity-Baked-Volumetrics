Shader "Unlit/SimpleLightmapDiffuse"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "LightMode" = "ForwardBase"
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vertex_base
            #pragma fragment fragment_base

            #pragma fragmentoption ARB_precision_hint_fastest

            #pragma multi_compile_fwdbase
            #pragma multi_compile _ LIGHTMAP_ON

            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct appdata
            {
                fixed4 vertex : POSITION;
                fixed2 uv0 : TEXCOORD0;
                fixed2 uv1 : TEXCOORD1;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct vertexToFragment
            {
                fixed4 vertex : SV_POSITION;
                fixed4 uv0uv1 : TEXCOORD0;

                UNITY_VERTEX_OUTPUT_STEREO
            };

            vertexToFragment vertex_base(appdata v)
            {
                vertexToFragment o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(vertexToFragment, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = UnityObjectToClipPos(v.vertex);

                o.uv0uv1.xy = v.uv0;

                #if defined(LIGHTMAP_ON)
                    o.uv0uv1.zw = v.uv1.xy * unity_LightmapST.xy + unity_LightmapST.zw;
                #endif

                return o;
            }

            fixed4 fragment_base(vertexToFragment i) : SV_Target
            {
                fixed4 albedo = tex2D(_MainTex, i.uv0uv1.xy);

                #if defined(LIGHTMAP_ON)
                    fixed4 lightmap = UNITY_SAMPLE_TEX2D(unity_Lightmap, i.uv0uv1.zw);
                    lightmap.rgb = DecodeLightmap(lightmap);
                    albedo.rgb *= lightmap.rgb;
                #endif

                return albedo;
            }
            ENDCG
        }

        Pass
        {
            Tags 
            { 
                "LightMode" = "ShadowCaster" 
            }

            CGPROGRAM
            #pragma vertex vertex_shadow_cast
            #pragma fragment fragment_shadow_caster

            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct vertexToFragmentShadow
            {
                V2F_SHADOW_CASTER;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            vertexToFragmentShadow vertex_shadow_cast(appdata_tan v)
            {
                vertexToFragmentShadow o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(vertexToFragmentShadow, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)

                return o;
            }

            fixed4 fragment_shadow_caster(vertexToFragmentShadow i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }

            ENDCG
        }

        UsePass "Standard/META"
    }
}

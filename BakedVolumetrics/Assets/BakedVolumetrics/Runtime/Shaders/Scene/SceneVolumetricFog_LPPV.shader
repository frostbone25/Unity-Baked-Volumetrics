//References
//UnityCG.cginc - https://github.com/TwoTailsGames/Unity-Built-in-Shaders/blob/master/CGIncludes/UnityCG.cginc
//UnityShaderVariables.cginc - https://github.com/TwoTailsGames/Unity-Built-in-Shaders/blob/master/CGIncludes/UnityShaderVariables.cginc

Shader "SceneVolumetricFog_LPPV"
{
    Properties
    {
        [Header(Raymarching)]
        _RaymarchStepSize("Raymarch Step Size", Float) = 25

        [Header(Rendering)]
        [Toggle(_HALF_RESOLUTION)] _HalfResolution("Half Resolution", Float) = 0
        [Toggle(_ANIMATED_NOISE)] _EnableAnimatedJitter("Animated Noise", Float) = 0
        [Toggle(_KILL_RAYS_EXITING_VOLUME)] _StopRaysExitingVolume("Kill Rays Exiting Volume", Float) = 1
        _JitterTexture("Jitter Texture", 2D) = "white" {}
        _RaymarchJitterStrength("Raymarch Jitter", Float) = 2
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent+2000" 
        }

        Cull Off
        ZWrite Off
        ZTest Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing  
            #pragma multi_compile SAMPLES_8 SAMPLES_16 SAMPLES_24 SAMPLES_32 SAMPLES_48 SAMPLES_64 SAMPLES_128
            #pragma multi_compile _ UNITY_LIGHT_PROBE_PROXY_VOLUME 

            #pragma shader_feature_local _ANIMATED_NOISE
            #pragma shader_feature_local _HALF_RESOLUTION
            #pragma shader_feature_local _KILL_RAYS_EXITING_VOLUME

            #include "UnityCG.cginc"
            #include "QuadIntrinsics.cginc"

            //NOTE: IF MIP QUAD OPTIMIZATION IS ENABLED
            //WE HAVE TO TARGET 5.0
            #if defined (_HALF_RESOLUTION)
                //#pragma target 5.0
                //#pragma require interpolators10
                //#pragma require interpolators15
                //#pragma require interpolators32
                //#pragma require mrt4
                //#pragma require mrt8
                #pragma require derivatives
                //#pragma require samplelod
                //#pragma require fragcoord
                //#pragma require integers
                //#pragma require 2darray
                #pragma require cubearray
                //#pragma require instancing
                //#pragma require geometry
                //#pragma require compute
                //#pragma require randomwrite
                //#pragma require tesshw
                //#pragma require tessellation
                //#pragma require msaatex
                //#pragma require sparsetex
                //#pragma require framebufferfetch
            #endif

            ///*
            #ifdef SAMPLES_8
                #define _RaymarchSteps 8
            #elif SAMPLES_16
                #define _RaymarchSteps 16
            #elif SAMPLES_24
                #define _RaymarchSteps 24
            #elif SAMPLES_32
                #define _RaymarchSteps 32
            #elif SAMPLES_48
                #define _RaymarchSteps 48
            #elif SAMPLES_64
                #define _RaymarchSteps 64
            #elif SAMPLES_128
                #define _RaymarchSteps 128
            #else
                #define _RaymarchSteps 32
            #endif
            //*/

            struct appdata
            {
                fixed4 vertex : POSITION;

                //Single Pass Instanced Support
                UNITY_VERTEX_INPUT_INSTANCE_ID 
            };

            struct vertexToFragment
            {
                fixed4 vertex : SV_POSITION;
                fixed4 screenPos : TEXCOORD0;
                fixed3 camRelativeWorldPos : TEXCOORD1;
                float3 worldDirection : TEXCOORD2;
                //Single Pass Instanced Support
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed _RaymarchStepSize;
            fixed _RaymarchJitterStrength;
            fixed4 _JitterTexture_TexelSize;
            fixed4 _CameraDepthTexture_TexelSize;
            sampler2D_half _JitterTexture;
            UNITY_DECLARE_SCREENSPACE_TEXTURE(_CameraDepthTexture);

            #if defined (_ANIMATED_NOISE)
                //animated noise courtesy of silent
                fixed r2sequence(fixed2 pixel)
                {
                    const fixed a1 = 0.75487766624669276;
                    const fixed a2 = 0.569840290998;

                    return frac(a1 * fixed(pixel.x) + a2 * fixed(pixel.y));
                }

                fixed2 r2_modified(fixed idx, fixed2 seed)
                {
                    return frac(seed + fixed(idx) * fixed2(0.245122333753, 0.430159709002));
                }

                fixed noise(fixed2 uv)
                {
                    //uv += r2_modified(_Time.y, uv);
                    uv += fixed2(_Time.y, _Time.y);
                    uv *= _ScreenParams.xy * _JitterTexture_TexelSize.xy;

                    return tex2Dlod(_JitterTexture, fixed4(uv, 0, 0));
                }
            #else
                fixed noise(fixed2 uv)
                {
                    #if defined (_HALF_RESOLUTION)
                        return tex2Dlod(_JitterTexture, fixed4(uv * _ScreenParams.xy * _JitterTexture_TexelSize.xy * 0.5, 0, 0));
                    #else 
                        return tex2Dlod(_JitterTexture, fixed4(uv * _ScreenParams.xy * _JitterTexture_TexelSize.xy, 0, 0));
                    #endif
                }
            #endif

            vertexToFragment vert(appdata v)
            {
                vertexToFragment o;

                //Single Pass Instanced Support
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(vertexToFragment, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.camRelativeWorldPos = mul(unity_ObjectToWorld, fixed4(v.vertex.xyz, 1.0)).xyz - _WorldSpaceCameraPos;
                
                // Screen Position technique from https://github.com/cnlohr/shadertrixx?tab=readme-ov-file#depth-textures--getting-worldspace-info
                // Subtract camera position from vertex position in world
                // to get a ray pointing from the camera to this vertex.
                o.worldDirection = mul(unity_ObjectToWorld, v.vertex).xyz - _WorldSpaceCameraPos;

                // Save the clip space position so we can use it later.
                // This also handles situations where the Y is flipped.
                float2 suv = o.vertex * float2( 0.5, 0.5*_ProjectionParams.x);
                                
                // Tricky, constants like the 0.5 and the second paramter
                // need to be premultiplied by o.vertex.w.
                o.screenPos = float4(TransformStereoScreenSpaceTex(suv + 0.5*o.vertex.w, o.vertex.w), 0, o.vertex.w);

                return o;
            }

            fixed4 frag(vertexToFragment i) : SV_Target
            {
                //Single Pass Instanced Support
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                #if defined (_HALF_RESOLUTION)
                    SETUP_QUAD_INTRINSICS(i.vertex)
                #endif

                //our final computed fog color
                fixed4 result = fixed4(0, 0, 0, 0); //rgb = fog color, a = transmittance

                #if defined (_HALF_RESOLUTION)
                    if (QuadGetLaneID() == 0)
                    {
                #endif

                // Compute projective scaling factor...
                float perspectiveDivide = 1.0f / i.vertex.w;

                // Calculate our UV within the screen (for reading depth buffer)
                float2 screenUV = i.screenPos.xy * perspectiveDivide;

                #if UNITY_UV_STARTS_AT_TOP
                    if (_CameraDepthTexture_TexelSize.y < 0)
                        screenUV.y = 1 - screenUV.y;
                #endif

                // Read depth, linearizing into worldspace units.
                float linearDepth = LinearEyeDepth(UNITY_SAMPLE_DEPTH(tex2D(_CameraDepthTexture, screenUV)));

                //Depth buffer is reversed on Oculus Quest
                #if UNITY_REVERSED_Z
                    linearDepth = 1.0 - linearDepth;
                #endif

                // Scale our view ray to unit depth.
                float3 direction = i.worldDirection * perspectiveDivide;
                float3 worldPos = direction * linearDepth + _WorldSpaceCameraPos;

                const fixed transformToLocal = unity_ProbeVolumeParams.y;
                const fixed texelSizeX = unity_ProbeVolumeParams.z;

                // UV offset by orientation
                fixed3 localViewDir = normalize(direction);

                //compute jitter
                fixed jitter = 1.0f + noise(screenUV + length(localViewDir)) * _RaymarchStepSize * _RaymarchJitterStrength;

                #if defined (_HALF_RESOLUTION)
                    jitter *= 2.0f;
                #endif

                //get our ray increment vector that we use so we can march into the scene. Jitter it also so we can mitigate banding/stepping artifacts
                fixed3 raymarch_rayIncrement = normalize(i.camRelativeWorldPos.xyz) / _RaymarchSteps;

                //get the length of the step
                fixed stepLength = length(raymarch_rayIncrement);

                //get our starting ray position from the camera
                fixed3 raymarch_currentPos = _WorldSpaceCameraPos + raymarch_rayIncrement * jitter;

                //start marching
                [unroll(_RaymarchSteps)]
                for (int i = 0; i < _RaymarchSteps; i++)
                {
                    //get the distances of the ray and the world position
                    fixed distanceRay = distance(_WorldSpaceCameraPos, raymarch_currentPos);
                    fixed distanceWorld = distance(_WorldSpaceCameraPos, worldPos);

                    //make sure we are within our little box
                    //if (scaledPos.x < 1.0f && scaledPos.x > 0.0f && scaledPos.y < 1.0f && scaledPos.y > 0.0f && scaledPos.z < 1.0f && scaledPos.z > 0.0f)
                        
                    //IMPORTANT: Check the current position distance of our ray compared to where we started.
                    //If our distance is less than that of the world then that means we aren't intersecting into any objects yet so keep accumulating.
                    if (distanceRay < distanceWorld)
                    {
                        //And also keep going if we haven't reached the fullest density just yet.
                        if (result.a < 1.0f)
                        {
                            //sample the fog color (rgb = color, a = density)

                            //The SH coefficients textures and probe occlusion are packed into 1 atlas.
                            //-------------------------
                            //| ShR | ShG | ShB | Occ |
                            //-------------------------

                            fixed3 position = (transformToLocal == 1.0f) ? mul(unity_ProbeVolumeWorldToObject, fixed4(raymarch_currentPos, 1.0)).xyz : raymarch_currentPos;
                            fixed3 texCoord = (position - unity_ProbeVolumeMin.xyz) * unity_ProbeVolumeSizeInv.xyz;
                            texCoord.x = texCoord.x * 0.25f;

                            // We need to compute proper X coordinate to sample.
                            // Clamp the coordinate otherwize we'll have leaking between RGB coefficients
                            fixed texCoordX = clamp(texCoord.x, 0.5f * texelSizeX, 0.25f - 0.5f * texelSizeX);

                            // sampler state comes from SHr (all SH textures share the same sampler)
                            texCoord.x = texCoordX;
                            fixed4 sphericalHarmonics_A_R = UNITY_SAMPLE_TEX3D_SAMPLER(unity_ProbeVolumeSH, unity_ProbeVolumeSH, texCoord);

                            texCoord.x = texCoordX + 0.25f;
                            fixed4 sphericalHarmonics_A_G = UNITY_SAMPLE_TEX3D_SAMPLER(unity_ProbeVolumeSH, unity_ProbeVolumeSH, texCoord);

                            texCoord.x = texCoordX + 0.5f;
                            fixed4 sphericalHarmonics_A_B = UNITY_SAMPLE_TEX3D_SAMPLER(unity_ProbeVolumeSH, unity_ProbeVolumeSH, texCoord);

                            // Linear + constant polynomial terms
                            fixed3 dotDirection = fixed3(0, 1, 0);
                            fixed3 sampledColor = fixed3(dot(sphericalHarmonics_A_R, dotDirection), dot(sphericalHarmonics_A_G, dotDirection), dot(sphericalHarmonics_A_B, dotDirection));
                            sampledColor = max(0.0, sampledColor);

                            //accumulate the samples
                            result += fixed4(sampledColor.rgb, 1.0f) * stepLength; //this is slightly cheaper                                    
                        }
                        else
                            break; //terminante the ray 
                    }
                    #if defined(_KILL_RAYS_EXITING_VOLUME)
                        else
                            break; //terminate the ray
                    #endif

                    //keep stepping forward into the scene
                    raymarch_currentPos += raymarch_rayIncrement * _RaymarchStepSize;
                }

                //clamp the alpha channel otherwise we get blending issues with bright spots
                result.a = clamp(result.a, 0.0f, 1.0f);

                #if defined (_HALF_RESOLUTION)
                    }
                    return QuadReadLaneAt(result, uint2(0, 0));
                #endif

                //return the final fog color
                return result;
            }
            ENDCG
        }
    }
}

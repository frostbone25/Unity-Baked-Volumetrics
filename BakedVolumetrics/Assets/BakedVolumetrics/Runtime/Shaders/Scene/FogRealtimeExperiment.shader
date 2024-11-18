Shader "BakedVolumetrics/RealtimeExperiment"
{
    Properties
    {
        [Header(Volume)]
        _SceneAlbedo("Scene Albedo", 3D) = "white" {}
        _VolumePos("Volume World Position", Vector) = (0, 0, 0, 0)
        _VolumeSize("Volume World Size", Vector) = (0, 0, 0, 0)
        _VolumeResolution("Volume Resolution", Vector) = (0, 0, 0, 0)

        [Header(Light)]
        [HDR] _LightColor("Light Color", Color) = (1, 1, 1, 0)
        _LightPos("Light World Position", Vector) = (0, 0, 0, 0)

        [Header(Raymarching)]
        _RaymarchStepSize("Raymarch Step Size", Float) = 25

        [Header(Rendering)]
        [Toggle(_HALF_RESOLUTION)] _HalfResolution("Half Resolution", Float) = 0
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
            //#pragma fragment frag
            #pragma fragment frag2

            #pragma multi_compile_instancing  

            #pragma shader_feature_local _KILL_RAYS_EXITING_VOLUME
            #pragma shader_feature_local _HALF_RESOLUTION

            #include "UnityCG.cginc"

            #include "QuadIntrinsics.cginc"

            #if defined (_HALF_RESOLUTION)
                #pragma require derivatives
                #pragma require cubearray
            #endif

            #define RAYMARCH_STEPS 8

            //This acts like a bias for the surface tracing functions, 1 gives best results.
            //Though this can cause issues with thin geometry (i.e. things that are only represented as a single voxel and no neighboring ones)
            //TODO: With voxelization, introduce an adjustable thickness modifier when generating them for the scene.
            #define SURFACE_DIRECT_OCCLUSION_SKIP_ITERATION 1

            //[FIX]: (Thanks Pema!) This is a solution to solve the problem with causing TDR/driver timeouts.
            //We force the occlusion checking loop to terminate at some point even if it manages to run forever somehow.
            #define MAX_LOOP_ITERATIONS 256

            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

                fixed _RaymarchStepSize;
                fixed _RaymarchJitterStrength;
                fixed4 _VolumePos;
                fixed4 _VolumeSize;
                fixed4 _JitterTexture_TexelSize;
                fixed4 _CameraDepthTexture_TexelSize;
                sampler2D_half _JitterTexture;
                sampler3D _SceneAlbedo;
                fixed4 _LightPos;
                float4 _LightColor;

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

                //Single Pass Instanced Support
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed noise(fixed2 uv)
            {
#if defined (_HALF_RESOLUTION)
                return tex2Dlod(_JitterTexture, fixed4(uv * _ScreenParams.xy * _JitterTexture_TexelSize.xy * 0.5, 0, 0));
#else 
                return tex2Dlod(_JitterTexture, fixed4(uv * _ScreenParams.xy * _JitterTexture_TexelSize.xy, 0, 0));
#endif
            }

                bool PositionInVolumeBounds(float3 worldPosition, float3 volumePosition, float3 volumeSize)
                {
                    if (worldPosition.x > volumePosition.x + volumeSize.x)
                        return false;

                    if (worldPosition.x < volumePosition.x - volumeSize.x)
                        return false;

                    if (worldPosition.y > volumePosition.y + volumeSize.x)
                        return false;

                    if (worldPosition.y < volumePosition.y - volumeSize.x)
                        return false;

                    return true;
                }

            vertexToFragment vert(appdata v)
            {
                vertexToFragment o;

                //Single Pass Instanced Support
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(vertexToFragment, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPos = UnityStereoTransformScreenSpaceTex(ComputeScreenPos(o.vertex));
                o.camRelativeWorldPos = mul(unity_ObjectToWorld, fixed4(v.vertex.xyz, 1.0)).xyz - _WorldSpaceCameraPos;

                return o;
            }

            float4 _VolumeResolution;

            fixed4 frag(vertexToFragment i) : SV_Target
            {
                //our final computed fog color
                fixed4 result = fixed4(0, 0, 0, 0); //rgb = fog color, a = transmittance

                //get our screen uv coords
                fixed2 screenUV = i.screenPos.xy / i.screenPos.w;

                //draw our scene depth texture and linearize it
                fixed linearDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos)));

                //calculate the world position view plane for the camera
                fixed3 cameraWorldPositionViewPlane = i.camRelativeWorldPos.xyz / dot(i.camRelativeWorldPos.xyz, unity_WorldToCamera._m20_m21_m22);

                //get the world position vector
                fixed3 worldPos = cameraWorldPositionViewPlane * linearDepth + _WorldSpaceCameraPos;

                // UV offset by orientation
                fixed3 localViewDir = normalize(cameraWorldPositionViewPlane);

                //compute jitter
                fixed jitter = 1.0f + noise(screenUV + length(localViewDir)) * _RaymarchStepSize * _RaymarchJitterStrength;

                //get our ray increment vector that we use so we can march into the scene. Jitter it also so we can mitigate banding/stepping artifacts
                fixed3 raymarch_rayIncrement = normalize(i.camRelativeWorldPos.xyz) / RAYMARCH_STEPS;

                //get the length of the step
                fixed stepLength = length(raymarch_rayIncrement);

                fixed3 halfVolumeSize = _VolumeSize * 0.5;

                //get our starting ray position from the camera
                fixed3 raymarch_currentPos = _WorldSpaceCameraPos + raymarch_rayIncrement * jitter;

                float3 singleVoxelSize = _VolumeSize / _VolumeResolution.xyz; //the size of a single voxel
                float minVoxelSize = min(singleVoxelSize.x, min(singleVoxelSize.y, singleVoxelSize.z)); //the minimum size of a single voxel.

                float3 worldVoxelPosition = worldPos;

                //get the light position, we are going to do some modifcations to it...
                float3 pointLightPosition = _LightPos.xyz;

                //compute the attenuation factor for the point light
                float distanceToPointLight = distance(worldVoxelPosition, pointLightPosition);
                float pointLightDistanceSample = 1.0f / (distanceToPointLight);

                //compute the current direction to the point light position
                float3 pointLightWorldDirection = normalize(worldVoxelPosition - pointLightPosition);

                //our ray marching parameters
                float3 pointLight_currentRayPosition = worldVoxelPosition;
                float3 pointLight_currentRayDirection = -pointLightWorldDirection;

                //get the primary light color (this already factors in the intensity of the light). 
                //this will also be modified with additional light shading later.
                float3 pointLightColorSample = _LightColor;

                //gets set by the while loop later on to determine if the light is occluded at the current voxel.
                bool pointLight_isOccluded = false;

                //reset so we can keep track of the amount of times we iterate through a loop.
                int iterationIndex = 0;

                while (distance(pointLight_currentRayPosition, pointLightPosition) > minVoxelSize && iterationIndex < MAX_LOOP_ITERATIONS)
                {
                    //if the ray goes out of bounds, stop the loop
                    if (PositionInVolumeBounds(pointLight_currentRayPosition.xyz, _VolumePos.xyz, _VolumeSize.xyz) == false)
                        break;

                    //keep stepping the ray in world space
                    pointLight_currentRayPosition += pointLight_currentRayDirection * singleVoxelSize;

                    //do a "shadow ray bias" so we can avoid a false occlusion bias hitting potentially the current pixel we originated from.
                    if (iterationIndex >= SURFACE_DIRECT_OCCLUSION_SKIP_ITERATION)
                    {
                        //normalize the ray from world space, to simple local 3D texture coordinates.
                        float3 pointLight_scaledRayPosition = ((pointLight_currentRayPosition + halfVolumeSize) - _VolumePos.xyz) / _VolumeSize.xyz;

                        //sample the scene albedo buffer's alpha channel only for occlusion checking.
                        float pointLight_sceneOcclusionSample = tex3Dlod(_SceneAlbedo, fixed4(pointLight_scaledRayPosition, 0)).a;

                        //if the alpha value is not zero (opaque) then we have hit a surface.
                        if (pointLight_sceneOcclusionSample > 0.0)
                        {
                            pointLight_isOccluded = true; //we are occluded, so we don't shade the surface
                            break; //stop the loop
                        }
                    }

                    //increment the amount of times this loop runs
                    iterationIndex++;
                }

                //if the current surface we are on is not occluded from the current light source, shade it.
                if (pointLight_isOccluded == false)
                    result.rgb += pointLightColorSample * pointLightDistanceSample;

                result.a = 1.0f;

                //return the final fog color
                return result;
            }

            fixed4 frag2(vertexToFragment i) : SV_Target
            {
                #if defined (_HALF_RESOLUTION)
                    SETUP_QUAD_INTRINSICS(i.vertex)
                #endif

                //our final computed fog color
                fixed4 result = fixed4(0, 0, 0, 0); //rgb = fog color, a = transmittance

#if defined (_HALF_RESOLUTION)
            if (QuadGetLaneID() == 0)
            {
#endif

                //get our screen uv coords
                fixed2 screenUV = i.screenPos.xy / i.screenPos.w;

                //draw our scene depth texture and linearize it
                fixed linearDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos)));

                //calculate the world position view plane for the camera
                fixed3 cameraWorldPositionViewPlane = i.camRelativeWorldPos.xyz / dot(i.camRelativeWorldPos.xyz, unity_WorldToCamera._m20_m21_m22);

                //get the world position vector
                fixed3 worldPos = cameraWorldPositionViewPlane * linearDepth + _WorldSpaceCameraPos;

                // UV offset by orientation
                fixed3 localViewDir = normalize(cameraWorldPositionViewPlane);

                //compute jitter
                fixed jitter = 1.0f + noise(screenUV + length(localViewDir)) * _RaymarchStepSize * _RaymarchJitterStrength;

#if defined (_HALF_RESOLUTION)
                jitter *= 2.0f;
#endif

                //get our ray increment vector that we use so we can march into the scene. Jitter it also so we can mitigate banding/stepping artifacts
                fixed3 raymarch_rayIncrement = normalize(i.camRelativeWorldPos.xyz) / RAYMARCH_STEPS;

                //get the length of the step
                fixed stepLength = length(raymarch_rayIncrement);

                fixed3 halfVolumeSize = _VolumeSize * 0.5;

                //get our starting ray position from the camera
                fixed3 raymarch_currentPos = _WorldSpaceCameraPos + raymarch_rayIncrement * jitter;

                float3 singleVoxelSize = _VolumeSize / _VolumeResolution.xyz; //the size of a single voxel
                float minVoxelSize = min(singleVoxelSize.x, min(singleVoxelSize.y, singleVoxelSize.z)); //the minimum size of a single voxel.

                float3 pointLightColorSample = _LightColor;

                //start marching
                for (int i = 0; i < RAYMARCH_STEPS; i++)
                {
                    //make sure we are within our little box
                    bool isInBox = all(abs(raymarch_currentPos - _VolumePos) < halfVolumeSize);

                    //IMPORTANT: Check the current position distance of our ray compared to where we started.
                    //If our distance is less than that of the world then that means we aren't intersecting into any objects yet so keep accumulating.
                    bool isRayPositionIntersectingScene = distance(_WorldSpaceCameraPos, raymarch_currentPos) < distance(_WorldSpaceCameraPos, worldPos);

                    if (!isRayPositionIntersectingScene || !isInBox)
                        break;

                    //get the light position, we are going to do some modifcations to it...
                    float3 pointLightPosition = _LightPos.xyz;

                    //compute the attenuation factor for the point light
                    float distanceToPointLight = distance(raymarch_currentPos, pointLightPosition);
                    float pointLightDistanceSample = 1.0f / (distanceToPointLight);

                    //compute the current direction to the point light position
                    float3 pointLightWorldDirection = normalize(raymarch_currentPos - pointLightPosition);

                    //our ray marching parameters
                    float3 pointLight_currentRayPosition = raymarch_currentPos;
                    float3 pointLight_currentRayDirection = -pointLightWorldDirection;

                    //gets set by the while loop later on to determine if the light is occluded at the current voxel.
                    bool pointLight_isOccluded = false;

                    //reset so we can keep track of the amount of times we iterate through a loop.
                    int iterationIndex = 0;

                    while (distance(pointLight_currentRayPosition, pointLightPosition) > minVoxelSize && iterationIndex < MAX_LOOP_ITERATIONS)
                    {
                        //if the ray goes out of bounds, stop the loop
                        if (PositionInVolumeBounds(pointLight_currentRayPosition.xyz, _VolumePos.xyz, _VolumeSize.xyz) == false)
                            break;

                        //keep stepping the ray in world space
                        pointLight_currentRayPosition += pointLight_currentRayDirection * singleVoxelSize;

                        //do a "shadow ray bias" so we can avoid a false occlusion bias hitting potentially the current pixel we originated from.
                        if (iterationIndex >= SURFACE_DIRECT_OCCLUSION_SKIP_ITERATION)
                        {
                            //normalize the ray from world space, to simple local 3D texture coordinates.
                            float3 pointLight_scaledRayPosition = ((pointLight_currentRayPosition + halfVolumeSize) - _VolumePos.xyz) / _VolumeSize.xyz;

                            //sample the scene albedo buffer's alpha channel only for occlusion checking.
                            float pointLight_sceneOcclusionSample = tex3Dlod(_SceneAlbedo, fixed4(pointLight_scaledRayPosition, 0)).a;

                            //if the alpha value is not zero (opaque) then we have hit a surface.
                            if (pointLight_sceneOcclusionSample > 0.0)
                            {
                                pointLight_isOccluded = true; //we are occluded, so we don't shade the surface
                                break; //stop the loop
                            }
                        }

                        //increment the amount of times this loop runs
                        iterationIndex++;
                    }

                    //if the current surface we are on is not occluded from the current light source, shade it.
                    if (pointLight_isOccluded == false)
                        result.rgb += pointLightColorSample * pointLightDistanceSample;

                    raymarch_currentPos += raymarch_rayIncrement * _RaymarchStepSize;
                }

                result.a = 1.0f;

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
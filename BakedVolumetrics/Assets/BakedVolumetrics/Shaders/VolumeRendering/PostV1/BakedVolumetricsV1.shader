Shader "Hidden/BakedVolumetricsV1"
{
	SubShader
	{
		//Cull Off ZWrite Off ZTest Always
		ZTest Off Cull Off ZWrite Off Blend Off
		Fog { Mode off }

		Pass //0 Main
		{
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#pragma fragmentoption ARB_precision_hint_fastest 
			#include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"

			//TODO: Make this adjustable
			#define _RaymarchSteps 32
			//#define AnimateNoise

			TEXTURE2D_SAMPLER2D(_MainTex, sampler_MainTex);
			TEXTURE2D_SAMPLER2D(_CameraDepthTexture, sampler_CameraDepthTexture);

			sampler2D _JitterTexture;
			sampler3D _VolumeTexture;

			float4x4 _ClipToView;
			float4x4 _ViewProjInv;

			float4 _MainTex_TexelSize;
			float4 _VolumePos;
			float4 _VolumeSize;

			float _VolumeDensity;
			float _RaymarchStepSize;
			float _RaymarchJitterStrength;

			struct NewAttributesDefault
			{
				float3 vertex : POSITION;
				float4 texcoord : TEXCOORD;
			};

			struct Varyings
			{
				float4 vertex : SV_POSITION;
				float2 texcoord : TEXCOORD0;
				float4 worldDirection : TEXCOORD1;
			};

			//sample a noise texture instead of calculating one (should save on resources)
			float noise(float2 p)
			{
				return tex2Dlod(_JitterTexture, float4(p, 0, 0)).r;
			}

			float GetDepth(float2 uv)
			{
				return SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv).r;
			}

			float4 GetWorldPositionFromDepth(float2 uv_depth)
			{
				float depth = GetDepth(uv_depth);

#if defined(SHADER_API_OPENGL)
				depth = depth * 2.0 - 1.0;
#endif

				float4 H = float4(uv_depth.x * 2.0 - 1.0, (uv_depth.y) * 2.0 - 1.0, depth, 1.0);

				float4 D = mul(_ViewProjInv, H);
				return D / D.w;
			}

			float BeerTerm(float density, float densityAtSample)
			{
				return exp(-density * densityAtSample);
			}

			Varyings Vert(NewAttributesDefault v)
			{
				Varyings o;
				o.vertex = float4(v.vertex.xy, 0.0, 1.0);
				o.texcoord = TransformTriangleVertexToUV(v.vertex.xy);

#if UNITY_UV_STARTS_AT_TOP
				o.texcoord = o.texcoord * float2(1.0, -1.0) + float2(0.0, 1.0);
#endif

				float4 H = float4(o.texcoord.x * 2.0 - 1.0, (o.texcoord.y) * 2.0 - 1.0, 0.0f, 1.0);
				float4 D = mul(_ViewProjInv, H);
				o.worldDirection = (D / D.w);
				o.worldDirection.xyz -= _WorldSpaceCameraPos;

				return o;
			}

			float4 Frag(Varyings i) : SV_Target
			{
				float2 uv = i.texcoord.xy;

#if UNITY_UV_STARTS_AT_TOP
				//uv.y = 1 - uv.y;
#endif

#if UNITY_SINGLE_PASS_STEREO
				// If Single-Pass Stereo mode is active, transform the
				// coordinates to get the correct output UV for the current eye.
				float4 scaleOffset = unity_StereoScaleOffset[unity_StereoEyeIndex];
				uv = (uv - scaleOffset.zw) / scaleOffset.xy;
#endif

				//draw our scene depth texture and linearize it
				float linearDepth = Linear01Depth(GetDepth(uv));

				float4 worldPos = GetWorldPositionFromDepth(uv);
				float3 cameraWorldPos = _WorldSpaceCameraPos;

				//scale our vectors to the volume
				float3 scaledWorldPos = ((worldPos - _VolumePos) + _VolumeSize * 0.5) / _VolumeSize;
				float3 scaledCameraPos = ((cameraWorldPos - _VolumePos) + _VolumeSize * 0.5) / _VolumeSize;

				//compute jitter
				#if defined (AnimateNoise)
					float jitter = 1.0f + noise(float2(uv.x + _Time.y, uv.y + _Time.y) * 10.0f) * _RaymarchStepSize * _RaymarchJitterStrength;
				#else
					float jitter = 1.0f + noise(uv.xy * 10.0f) * _RaymarchStepSize * _RaymarchJitterStrength;
				#endif

				//get our ray increment vector that we use so we can march into the scene. Jitter it also so we can mitigate banding/stepping artifacts
				float3 raymarch_rayIncrement = normalize(i.worldDirection) / _RaymarchSteps;

				//get the length of the step
				float stepLength = length(raymarch_rayIncrement);

				//get our starting ray position from the camera
				float3 raymarch_currentPos = _WorldSpaceCameraPos + raymarch_rayIncrement * jitter;

				//our final computed fog color
				float4 result = float4(0, 0, 0, 0); //rgb = fog color, a = transmittance

				//start marching
				for (int i = 0; i < _RaymarchSteps; i++)
				{
					//scale the current ray position to be within the volume
					float3 scaledPos = ((raymarch_currentPos - _VolumePos) + _VolumeSize * 0.5) / _VolumeSize;

					//get the distances of the ray and the world position
					float distanceRay = distance(scaledCameraPos, scaledPos);
					float distanceWorld = distance(scaledCameraPos, scaledWorldPos);

					//make sure we are within our little box
					if (scaledPos.x < 1.0f && scaledPos.x > 0.0f && scaledPos.y < 1.0f && scaledPos.y > 0.0f && scaledPos.z < 1.0f && scaledPos.z > 0.0f)
					{
						//IMPORTANT: Check the current position distance of our ray compared to where we started.
						//If our distance is less than that of the world then that means we aren't intersecting into any objects yet so keep accumulating.
						//And also keep going if we haven't reached the fullest density just yet.
						if (distanceRay < distanceWorld && result.a < 1.0f)
						{
							//sample the fog color
							float3 sampledColor = tex3Dlod(_VolumeTexture, float4(scaledPos, 0)).rgb;
							float density = _VolumeDensity; //cheapest
							//float density = exp(_VolumeDensity); //uses exponential falloff, looks a little nicer but may not be needed
							//float density = BeerTerm(_VolumeDensity, result.a);

							result += float4(sampledColor, density) * stepLength / density;
						}
						else
							break; //terminante the ray 
					}

					//keep stepping forward into the scene
					raymarch_currentPos += raymarch_rayIncrement * _RaymarchStepSize;
				}

				//clamp the alpha channel otherwise we get blending issues with bright spots
				result.a = clamp(result.a, 0.0f, 1.0f);

				return result;
            }

			ENDHLSL
        }

		Pass //1 Combine
		{
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#pragma fragmentoption ARB_precision_hint_fastest 
			#include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"

			TEXTURE2D_SAMPLER2D(_MainTex, sampler_MainTex);
			sampler2D _FogColor;

			float4 _MainTex_TexelSize;

			struct NewAttributesDefault
			{
				float3 vertex : POSITION;
				float4 texcoord : TEXCOORD;
			};

			struct Varyings
			{
				float4 vertex : SV_POSITION;
				float2 texcoord : TEXCOORD0;
			};

			Varyings Vert(NewAttributesDefault v)
			{
				Varyings o;
				o.vertex = float4(v.vertex.xy, 0.0, 1.0);
				o.texcoord = TransformTriangleVertexToUV(v.vertex.xy);

#if UNITY_UV_STARTS_AT_TOP
				o.texcoord = o.texcoord * float2(1.0, -1.0) + float2(0.0, 1.0);
#endif

				return o;
			}

			float4 Frag(Varyings i) : SV_Target
			{
				float2 uv = i.texcoord.xy;

#if UNITY_UV_STARTS_AT_TOP
				//uv.y = 1 - uv.y;
#endif

#if UNITY_SINGLE_PASS_STEREO
				// If Single-Pass Stereo mode is active, transform the
				// coordinates to get the correct output UV for the current eye.
				float4 scaleOffset = unity_StereoScaleOffset[unity_StereoEyeIndex];
				uv = (uv - scaleOffset.zw) / scaleOffset.xy;
#endif

				float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
				float4 fogColor = tex2Dlod(_FogColor, float4(uv, 0, 0));

				//fogColor.a = clamp(fogColor.a, 0.0f, 1.0f);

				color.rgb = lerp(color.rgb, fogColor.rgb, fogColor.a);

				return color;
			}

			ENDHLSL
		}
    }
}

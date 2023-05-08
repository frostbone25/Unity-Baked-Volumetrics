#if UNITY_EDITOR
using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace BakedVolumetrics
{
    public class SampleCPURaytrace : MonoBehaviour
    {
        //public
        //global settings
        public AttenuationType raytracedAttenuationType;

        public float ambientIntensity = 1.0f;
        public Color ambientColor = Color.black;

        public bool doSkylight = false;
        public float skylightIntensity = 1.0f;
        public Color skylightColor = Color.blue;

        public bool limitByRange = false;
        public bool indoorOnlySamples = false;

        public bool doOcclusion = true;
        public bool occlusionPreventLeaks = false;
        public float occlusionLeakFactor = 1.0f;

        public bool includeBakedLights = true;
        public bool includeMixedLights = true;
        public bool includeRealtimeLights = false;
        public bool includeDirectionalLights = true;
        public bool includePointLights = true;
        public bool includeSpotLights = true;

        //directional light settings
        public float directionalLightsMultiplier = 1.0f;
        public float occlusionDirectionalFade = 0.0f;

        //point light settings
        public float pointLightsMultiplier = 1.0f;
        public float occlusionPointFade = 0.0f;

        //spot light settings
        public float spotLightsMultiplier = 1.0f;
        public float occlusionSpotFade = 0.0f;
        public bool doSpotLightBleed;
        public float spotLightBleedAmount = 0.0f;

        //private
        [HideInInspector] public bool showUI;


        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| LIGHTS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| LIGHTS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| LIGHTS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||

        private Color SampleAmbientLight(Vector3 probePosition)
        {
            Color ambientSample = ambientColor * ambientIntensity;

            if(doSkylight)
            {
                if (Physics.Raycast(probePosition, Vector3.up, float.MaxValue) == false)
                    ambientSample += skylightColor * skylightIntensity;
            }

            return ambientSample;
        }

        private Color SampleDirectionalLight(Light light, Vector3 probePosition, bool occlusion_test)
        {
            Color lightSample = light.color * light.intensity * directionalLightsMultiplier;

            if (occlusion_test)
                return lightSample;
            else
                return lightSample * occlusionDirectionalFade;
        }

        private Color SamplePointLight(Light light, Vector3 probePosition, bool occlusion_test)
        {
            Vector3 lightPosition = light.transform.position;

            Color lightSample = light.color * light.intensity * pointLightsMultiplier;
            float currentDistance = Vector3.Distance(probePosition, lightPosition);

            //float attenuation = GetAttenuation(currentDistance * light.range);
            float attenuation = GetAttenuation(currentDistance);

            bool range_test = limitByRange ? currentDistance < light.range : true;

            if (range_test)
            {
                if (occlusion_test)
                    return lightSample * attenuation;
                else
                    return lightSample * attenuation * occlusionPointFade;
            }

            return Color.black;
        }

        private Color SampleSpotLight(Light light, Vector3 probePosition, bool occlusion_test)
        {
            Vector3 lightPosition = light.transform.position;
            Vector3 targetDirection = probePosition - lightPosition;

            Color lightSample = light.color * light.intensity * spotLightsMultiplier;
            float currentDistance = Vector3.Distance(probePosition, lightPosition);

            bool range_test = limitByRange ? currentDistance < light.range : true;

            if (range_test)
            {
                float angle = Vector3.Angle(targetDirection, light.transform.forward) * 2.0f;
                //float attenuation = GetAttenuation(currentDistance * light.range);
                float attenuation = GetAttenuation(currentDistance);

                if (angle < light.spotAngle)
                {
                    if (occlusion_test)
                        return lightSample * attenuation;
                    else
                        return lightSample * attenuation * occlusionSpotFade;
                }
                else
                {
                    if (doSpotLightBleed)
                    {
                        if (occlusion_test)
                            return lightSample * attenuation * spotLightBleedAmount;
                    }
                }
            }

            return Color.black;
        }

        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| MAIN ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| MAIN ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| MAIN ||||||||||||||||||||||||||||||||||||||||||||||||||||||||

        public Color SampleVolumetricColor(Vector3 probePosition, Vector3 voxelWorldSize)
        {
            Color colorResult = Color.black;

            bool test_leak = occlusionPreventLeaks ? (Physics.CheckBox(probePosition, voxelWorldSize * occlusionLeakFactor) == false) : true;

            if (!test_leak)
                return colorResult;

            colorResult += SampleAmbientLight(probePosition);

            Light[] sceneLights = FindObjectsOfType<Light>();

            for (int i = 0; i < sceneLights.Length; i++)
            {
                Light currentLight = sceneLights[i];

                LightmapBakeType currentLightBakeType = currentLight.bakingOutput.lightmapBakeType;

                bool mode_case1 = currentLightBakeType == LightmapBakeType.Realtime && includeRealtimeLights;
                bool mode_case2 = currentLightBakeType == LightmapBakeType.Mixed && includeMixedLights;
                bool mode_case3 = currentLightBakeType == LightmapBakeType.Baked && includeBakedLights;

                Vector3 lightPosition = currentLight.transform.position;
                Vector3 targetDirection = probePosition - lightPosition;
                float currentDistance = Vector3.Distance(probePosition, lightPosition);

                if (currentLight.enabled && (mode_case1 || mode_case2 || mode_case3))
                {
                    bool type_case1 = currentLight.type == LightType.Directional && includeDirectionalLights;
                    bool type_case2 = currentLight.type == LightType.Point && includePointLights;
                    bool type_case3 = currentLight.type == LightType.Spot && includeSpotLights;

                    if (type_case1) //directional lights
                    {
                        bool world_occlusion_test = doOcclusion ? Physics.Raycast(probePosition, -currentLight.transform.forward, float.MaxValue) == false : true;
                        colorResult += SampleDirectionalLight(currentLight, probePosition, world_occlusion_test);
                    }
                    else
                    {
                        bool local_occlusion_test = doOcclusion ? Physics.Raycast(lightPosition, targetDirection, currentDistance) == false : true;

                        if (type_case2) //point lights
                            colorResult += SamplePointLight(currentLight, probePosition, local_occlusion_test);
                        else if (type_case3) //spot lights
                            colorResult += SampleSpotLight(currentLight, probePosition, local_occlusion_test);
                    }
                }
            }

            return colorResult;
        }

        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| UTILITIES ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| UTILITIES ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| UTILITIES ||||||||||||||||||||||||||||||||||||||||||||||||||||||||

        private float GetAttenuation(float distance)
        {
            switch (raytracedAttenuationType)
            {
                case AttenuationType.Linear:
                    return (1.0f / distance) * Mathf.PI;
                case AttenuationType.InverseSquare:
                    return (1.0f / (distance * distance)) * Mathf.PI;
                default:
                    return (1.0f / distance) * Mathf.PI;
            }
        }
    }
}
#endif
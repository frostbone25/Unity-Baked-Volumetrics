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
using Unity.Jobs;
using Unity.Collections;

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
        public bool includeAreaLights = true;

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

        //area light settings
        public float areaLightsMultiplier = 1.0f;
        public float occlusionAreaFade = 0.0f;

        //private
        [HideInInspector] public bool showUI;

        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| MAIN ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| MAIN ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| MAIN ||||||||||||||||||||||||||||||||||||||||||||||||||||||||

        public Color SampleVolumetricColor(Vector3 probePosition, Vector3 voxelWorldSize)
        {
            Color colorResult = Color.black;

            bool leakTest = occlusionPreventLeaks ? (Physics.CheckBox(probePosition, voxelWorldSize * occlusionLeakFactor) == false) : true;

            if (!leakTest)
            {
                return colorResult;
            }

            colorResult += SampleFlatAmbientLight(ambientColor, ambientIntensity);

            if (doSkylight)
            {
                colorResult += SampleSkylight(probePosition, skylightColor, skylightIntensity);
            }

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
                    bool type_case4 = currentLight.type == LightType.Area && includeAreaLights;

                    if (type_case1) //directional lights
                    {
                        bool worldOcclusionTest = doOcclusion ? Physics.Raycast(probePosition, -currentLight.transform.forward, float.MaxValue) == false : true;
                        colorResult += SampleDirectionalLight(currentLight, worldOcclusionTest, directionalLightsMultiplier, occlusionDirectionalFade);
                    }
                    else
                    {
                        bool localOcclusionTest = doOcclusion ? Physics.Raycast(lightPosition, targetDirection, currentDistance) == false : true;

                        if (type_case2) //point lights
                        {
                            colorResult += SamplePointLight(currentLight, probePosition, raytracedAttenuationType, localOcclusionTest, limitByRange, pointLightsMultiplier, occlusionPointFade);
                        }    
                        else if (type_case3) //spot lights
                        {
                            colorResult += SampleSpotLight(currentLight, probePosition, raytracedAttenuationType, localOcclusionTest, limitByRange, doSpotLightBleed, spotLightsMultiplier, occlusionSpotFade, spotLightBleedAmount);
                        }
                        else if (type_case4) //area lights
                        {
                            colorResult += SampleAreaLight(currentLight, probePosition, raytracedAttenuationType, localOcclusionTest, limitByRange, areaLightsMultiplier, occlusionAreaFade);
                        }
                    }
                }
            }

            return colorResult;
        }

        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| LIGHTS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| LIGHTS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| LIGHTS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||

        public static Color SampleFlatAmbientLight(Color color, float intensity)
        {
            //obtain the color of the light
            Color ambientSample = color;

            //multiply the light sample by the light intensity
            ambientSample *= intensity;

            return ambientSample;
        }

        public static Color SampleSkylight(Vector3 probePosition, Color color, float intensity)
        {
            //obtain the color of the light
            Color skylightSample = color;

            //multiply the light sample by the light intensity
            skylightSample *= intensity;

            //shoot a ray upwards, if it hits something then we are in shadow from the skylight
            if (Physics.Raycast(probePosition, Vector3.up, float.MaxValue))
            {
                skylightSample = Color.black;
            }

            return skylightSample;
        }

        public static Color SampleDirectionalLight(Light light, bool occlusionTest, float intensityMultiplier, float occlusionFade)
        {
            //obtain the color of the light
            Color directionalLightSample = light.color;

            //multiply the color of the light by its intensity (and a user adjustable multiplier)
            directionalLightSample *= light.intensity * intensityMultiplier;

            if (occlusionTest)
            {
                //the light is not occluded at this current position.
                return directionalLightSample;
            }
            else
            {
                //the light is occluded at this current position, so return no color (or a slight hint of the color according to the fade factor)
                return directionalLightSample * occlusionFade;
            }
        }

        public static Color SamplePointLight(Light light, Vector3 probePosition, AttenuationType attenuationType, bool occlusionTest, bool limitByRange, float intensityMultiplier, float occlusionFade)
        {
            //obtain the color of the light
            Color pointLightSample = light.color;

            //multiply the color of the light by its intensity (and a user adjustable multiplier)
            pointLightSample *= light.intensity * intensityMultiplier;

            //calculate the distance to the light source for computing attenuation later
            float currentDistancetToSource = Vector3.Distance(probePosition, light.transform.position);

            //calculate the attenuation factor according the distance and type
            //float attenuation = GetAttenuation(attenuationType, currentDistance * light.range);
            float attenuation = GetAttenuation(attenuationType, currentDistancetToSource);

            //if enabled, limit the range of the light
            bool rangeTest = limitByRange ? currentDistancetToSource < light.range : true;

            //check if we pass the range test (i.e. we are within the range of the light)
            if (rangeTest)
            {
                if (occlusionTest)
                {
                    //the light is not occluded at this current position.
                    return pointLightSample * attenuation;
                }
                else
                {
                    //the light is occluded at this current position, so return no color (or a slight hint of the color according to the fade factor)
                    return pointLightSample * attenuation * occlusionFade;
                }
            }

            //if none of the other tests pass, then return nothing at all
            return Color.black;
        }

        public static Color SampleSpotLight(Light light, Vector3 probePosition, AttenuationType attenuationType, bool occlusionTest, bool limitByRange, bool lightBleeding, float intensityMultiplier, float occlusionFade, float bleedFade)
        {
            //obtain the color of the light
            Color spotLightSample = light.color;

            //multiply the color of the light by its intensity (and a user adjustable multiplier)
            spotLightSample *= light.intensity * intensityMultiplier;

            //calculate the direction to the light source
            Vector3 targetDirection = probePosition - light.transform.position;

            //calculate the distance to the light source for computing attenuation later
            float currentDistancetToSource = Vector3.Distance(probePosition, light.transform.position);

            //if enabled, limit the range of the light
            bool rangeTest = limitByRange ? currentDistancetToSource < light.range : true;

            //check if we pass the range test (i.e. we are within the range of the light)
            if (rangeTest)
            {
                //compute the angle to the light source
                float currentAngleToSource = Vector3.Angle(targetDirection.normalized, light.transform.forward) * 2.0f;

                //calculate the attenuation factor according the distance and type
                //float attenuation = GetAttenuation(attenuationType, currentDistance * light.range);
                float attenuation = GetAttenuation(attenuationType, currentDistancetToSource);

                //check if the current angle is less than the spot angle
                if (currentAngleToSource < light.spotAngle)
                {
                    //the light is not occluded at this current position.
                    if (occlusionTest)
                    {
                        //the light is not occluded at this current position.
                        return spotLightSample * attenuation;
                    }
                    else
                    {
                        //the light is occluded at this current position, so return no color (or a slight hint of the color according to the fade factor)
                        return spotLightSample * attenuation * occlusionFade;
                    }
                }
                else //we are outside of the spot light angle
                {
                    //do some light bleeding (if enabled)
                    if (lightBleeding)
                    {
                        //if the light is not occluded at the current position then return a slight hint of the color according to the fade factor
                        if (occlusionTest)
                        {
                            return spotLightSample * attenuation * bleedFade;
                        }
                    }
                }
            }

            //if none of the tests pass then return nothing
            return Color.black;
        }

        public static Color SampleAreaLight(Light light, Vector3 probePosition, AttenuationType attenuationType, bool occlusionTest, bool limitByRange, float intensityMultiplier, float occlusionFade)
        {
            //obtain the color of the light
            Color spotLightSample = light.color;

            //multiply the color of the light by its intensity (and a user adjustable multiplier)
            spotLightSample *= light.intensity * intensityMultiplier;

            //calculate the direction to the light source
            Vector3 targetDirection = probePosition - light.transform.position;

            //calculate the distance to the light source for computing attenuation later
            float currentDistancetToSource = Vector3.Distance(probePosition, light.transform.position);

            //if enabled, limit the range of the light
            bool rangeTest = limitByRange ? currentDistancetToSource < light.range : true;

            //check if we pass the range test (i.e. we are within the range of the light)
            if (rangeTest)
            {
                //compute the angle to the light source
                float currentAngleToSource = Vector3.Angle(targetDirection.normalized, light.transform.forward) * 2.0f;

                //calculate the attenuation factor according the distance and type
                //float attenuation = GetAttenuation(attenuationType, currentDistance * light.range);
                float attenuation = GetAttenuation(attenuationType, currentDistancetToSource);

                //check if the current angle is less than the spot angle
                if (currentAngleToSource < 180.0f)
                {
                    //the light is not occluded at this current position.
                    if (occlusionTest)
                    {
                        //the light is not occluded at this current position.
                        return spotLightSample * attenuation;
                    }
                    else
                    {
                        //the light is occluded at this current position, so return no color (or a slight hint of the color according to the fade factor)
                        return spotLightSample * attenuation * occlusionFade;
                    }
                }
            }

            //if none of the tests pass then return nothing
            return Color.black;
        }

        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| UTILITIES ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| UTILITIES ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| UTILITIES ||||||||||||||||||||||||||||||||||||||||||||||||||||||||

        public static float GetAttenuation(AttenuationType attenuationType, float distance)
        {
            switch (attenuationType)
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
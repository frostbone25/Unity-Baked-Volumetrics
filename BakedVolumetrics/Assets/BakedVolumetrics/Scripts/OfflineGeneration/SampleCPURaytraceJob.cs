#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using BakedVolumetrics;

public class SampleCPURaytraceJob : IJobParallelFor
{
    //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| PUBLIC ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
    //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| PUBLIC ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
    //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| PUBLIC ||||||||||||||||||||||||||||||||||||||||||||||||||||||||

    //global settings
    public AttenuationType raytracedAttenuationType;

    public float ambientIntensity;
    public Color ambientColor;

    public bool doSkylight;
    public float skylightIntensity;
    public Color skylightColor;

    public bool limitByRange;
    public bool indoorOnlySamples;

    public bool doOcclusion;
    public bool occlusionPreventLeaks;
    public float occlusionLeakFactor;

    public bool includeBakedLights;
    public bool includeMixedLights;
    public bool includeRealtimeLights;

    public bool includeDirectionalLights;
    public bool includePointLights;
    public bool includeSpotLights;
    public bool includeAreaLights;

    //directional light settings
    public float directionalLightsMultiplier;
    public float occlusionDirectionalFade;

    //point light settings
    public float pointLightsMultiplier;
    public float occlusionPointFade;

    //spot light settings
    public float spotLightsMultiplier;
    public float occlusionSpotFade;
    public bool doSpotLightBleed;
    public float spotLightBleedAmount;

    //area light settings
    public float areaLightsMultiplier;
    public float occlusionAreaFade;

    //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| JOBS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
    //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| JOBS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
    //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| JOBS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||

    public Vector3 probePosition;
    public Vector3 voxelWorldSize;
    public Light[] sceneLights;

    // By default containers are assumed to be read & write
    public NativeArray<Color> colorResults;

    // The code actually running on the job
    public void Execute(int i)
    {
        colorResults[i] = SampleVolumetricColor();
    }

    //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| JOBS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
    //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| JOBS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
    //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| JOBS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||

    public Color SampleVolumetricColor()
    {
        Color colorResult = Color.black;

        bool leakTest = occlusionPreventLeaks ? (Physics.CheckBox(probePosition, voxelWorldSize * occlusionLeakFactor) == false) : true;

        if (!leakTest)
        {
            return colorResult;
        }

        colorResult += SampleCPURaytrace.SampleFlatAmbientLight(ambientColor, ambientIntensity);

        if (doSkylight)
        {
            colorResult += SampleCPURaytrace.SampleSkylight(probePosition, skylightColor, skylightIntensity);
        }

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
                    colorResult += SampleCPURaytrace.SampleDirectionalLight(currentLight, worldOcclusionTest, directionalLightsMultiplier, occlusionDirectionalFade);
                }
                else
                {
                    bool localOcclusionTest = doOcclusion ? Physics.Raycast(lightPosition, targetDirection, currentDistance) == false : true;

                    if (type_case2) //point lights
                    {
                        colorResult += SampleCPURaytrace.SamplePointLight(currentLight, probePosition, raytracedAttenuationType, localOcclusionTest, limitByRange, pointLightsMultiplier, occlusionPointFade);
                    }
                    else if (type_case3) //spot lights
                    {
                        colorResult += SampleCPURaytrace.SampleSpotLight(currentLight, probePosition, raytracedAttenuationType, localOcclusionTest, limitByRange, doSpotLightBleed, spotLightsMultiplier, occlusionSpotFade, spotLightBleedAmount);
                    }
                    else if (type_case4) //area lights
                    {
                        colorResult += SampleCPURaytrace.SampleAreaLight(currentLight, probePosition, raytracedAttenuationType, localOcclusionTest, limitByRange, areaLightsMultiplier, occlusionAreaFade);
                    }
                }
            }
        }

        return colorResult;
    }
}
#endif
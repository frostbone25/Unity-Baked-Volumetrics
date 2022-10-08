#if UNITY_EDITOR
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.UIElements;
using UnityEngine.UIElements;


namespace BakedVolumetrics
{
    [CustomEditor(typeof(SampleCPURaytrace))]
    [CanEditMultipleObjects]
    public class SampleCPURaytraceEditor : Editor
    {
        SerializedProperty raytracedAttenuationType;
        SerializedProperty ambientIntensity;
        SerializedProperty ambientColor;
        SerializedProperty doSkylight;
        SerializedProperty limitByRange;
        SerializedProperty indoorOnlySamples;
        SerializedProperty skylightIntensity;
        SerializedProperty skylightColor;
        SerializedProperty directionalLightsMultiplier;
        SerializedProperty occlusionDirectionalFade;
        SerializedProperty pointLightsMultiplier;
        SerializedProperty occlusionPointFade;
        SerializedProperty spotLightsMultiplier;
        SerializedProperty occlusionSpotFade;
        SerializedProperty spotLightBleedAmount;
        SerializedProperty occlusionLeakFactor;
        SerializedProperty doOcclusion;
        SerializedProperty occlusionPreventLeaks;
        SerializedProperty doSpotLightBleed;
        SerializedProperty includeBakedLights;
        SerializedProperty includeMixedLights;
        SerializedProperty includeRealtimeLights;
        SerializedProperty includeDirectionalLights;
        SerializedProperty includePointLights;
        SerializedProperty includeSpotLights;
        SerializedProperty showUI;

        void OnEnable()
        {
            raytracedAttenuationType = serializedObject.FindProperty("raytracedAttenuationType");
            ambientIntensity = serializedObject.FindProperty("ambientIntensity");
            ambientColor = serializedObject.FindProperty("ambientColor");
            doSkylight = serializedObject.FindProperty("doSkylight");
            limitByRange = serializedObject.FindProperty("limitByRange");
            indoorOnlySamples = serializedObject.FindProperty("indoorOnlySamples");
            skylightIntensity = serializedObject.FindProperty("skylightIntensity");
            skylightColor = serializedObject.FindProperty("skylightColor");
            directionalLightsMultiplier = serializedObject.FindProperty("directionalLightsMultiplier");
            occlusionDirectionalFade = serializedObject.FindProperty("occlusionDirectionalFade");
            pointLightsMultiplier = serializedObject.FindProperty("pointLightsMultiplier");
            occlusionPointFade = serializedObject.FindProperty("occlusionPointFade");
            spotLightsMultiplier = serializedObject.FindProperty("spotLightsMultiplier");
            occlusionSpotFade = serializedObject.FindProperty("occlusionSpotFade");
            spotLightBleedAmount = serializedObject.FindProperty("spotLightBleedAmount");
            occlusionLeakFactor = serializedObject.FindProperty("occlusionLeakFactor");
            doOcclusion = serializedObject.FindProperty("doOcclusion");
            occlusionPreventLeaks = serializedObject.FindProperty("occlusionPreventLeaks");
            doSpotLightBleed = serializedObject.FindProperty("doSpotLightBleed");
            includeBakedLights = serializedObject.FindProperty("includeBakedLights");
            includeMixedLights = serializedObject.FindProperty("includeMixedLights");
            includeRealtimeLights = serializedObject.FindProperty("includeRealtimeLights");
            includeDirectionalLights = serializedObject.FindProperty("includeDirectionalLights");
            includePointLights = serializedObject.FindProperty("includePointLights");
            includeSpotLights = serializedObject.FindProperty("includeSpotLights");
            showUI = serializedObject.FindProperty("showUI");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (showUI.boolValue == false)
                return;

            SampleCPURaytrace sampleRaytrace = serializedObject.targetObject as SampleCPURaytrace;

            EditorGUILayout.LabelField("Raytraced Volume Lighting", EditorStyles.whiteLargeLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Light Types", EditorStyles.boldLabel);
            sampleRaytrace.includeDirectionalLights = EditorGUILayout.ToggleLeft("Include Directional Lights", sampleRaytrace.includeDirectionalLights);
            sampleRaytrace.includePointLights = EditorGUILayout.ToggleLeft("Include Point Lights", sampleRaytrace.includePointLights);
            sampleRaytrace.includeSpotLights = EditorGUILayout.ToggleLeft("Include Spot Lights", sampleRaytrace.includeSpotLights);
            EditorGUILayout.EndVertical();
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Light Modes", EditorStyles.boldLabel);
            sampleRaytrace.includeBakedLights = EditorGUILayout.ToggleLeft("Include Baked Lights", sampleRaytrace.includeBakedLights);
            sampleRaytrace.includeMixedLights = EditorGUILayout.ToggleLeft("Include Mixed Lights", sampleRaytrace.includeMixedLights);
            sampleRaytrace.includeRealtimeLights = EditorGUILayout.ToggleLeft("Include Realtime Lights", sampleRaytrace.includeRealtimeLights);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Raytracing Settings", EditorStyles.whiteLargeLabel);
            EditorGUILayout.LabelField("Global", EditorStyles.boldLabel);

            if (sampleRaytrace.includePointLights || sampleRaytrace.includeSpotLights)
                sampleRaytrace.raytracedAttenuationType = (AttenuationType)EditorGUILayout.EnumPopup("Attenuation Type", sampleRaytrace.raytracedAttenuationType);

            EditorGUILayout.BeginHorizontal();
            sampleRaytrace.ambientColor = EditorGUILayout.ColorField("Ambient Color", sampleRaytrace.ambientColor);
            sampleRaytrace.ambientIntensity = EditorGUILayout.FloatField("Ambient Intensity", sampleRaytrace.ambientIntensity);
            EditorGUILayout.EndHorizontal();

            sampleRaytrace.doOcclusion = EditorGUILayout.ToggleLeft("Enable Occlusion", sampleRaytrace.doOcclusion);
            sampleRaytrace.doSkylight = EditorGUILayout.ToggleLeft("Enable Skylight", sampleRaytrace.doSkylight);
            sampleRaytrace.limitByRange = EditorGUILayout.ToggleLeft("Limit Local Lights By Range", sampleRaytrace.limitByRange);
            sampleRaytrace.indoorOnlySamples = EditorGUILayout.ToggleLeft("Use Indoor Only Samples", sampleRaytrace.indoorOnlySamples);

            EditorGUILayout.BeginHorizontal();
            sampleRaytrace.occlusionPreventLeaks = EditorGUILayout.ToggleLeft("Prevent Light Leaks", sampleRaytrace.occlusionPreventLeaks);
            if (sampleRaytrace.occlusionPreventLeaks) sampleRaytrace.occlusionLeakFactor = EditorGUILayout.FloatField("Light Leak Factor", sampleRaytrace.occlusionLeakFactor);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            if (sampleRaytrace.doSkylight)
            {
                EditorGUILayout.LabelField("Skylight Settings", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                sampleRaytrace.skylightColor = EditorGUILayout.ColorField("Skylight Color", sampleRaytrace.skylightColor);
                sampleRaytrace.skylightIntensity = EditorGUILayout.FloatField("Skylight Intensity", sampleRaytrace.skylightIntensity);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(10);
            }

            if (sampleRaytrace.includeDirectionalLights)
            {
                EditorGUILayout.LabelField("Directional Lights", EditorStyles.boldLabel);

                sampleRaytrace.directionalLightsMultiplier = EditorGUILayout.FloatField("Directional Lights Multiplier", sampleRaytrace.directionalLightsMultiplier);
                if (sampleRaytrace.doOcclusion) sampleRaytrace.occlusionDirectionalFade = EditorGUILayout.Slider("Occlusion Fade", sampleRaytrace.occlusionDirectionalFade, 0.0f, 1.0f);

                EditorGUILayout.Space(10);
            }

            if (sampleRaytrace.includePointLights)
            {
                EditorGUILayout.LabelField("Point Lights", EditorStyles.boldLabel);

                sampleRaytrace.pointLightsMultiplier = EditorGUILayout.FloatField("Point Lights Multiplier", sampleRaytrace.pointLightsMultiplier);
                if (sampleRaytrace.doOcclusion) sampleRaytrace.occlusionPointFade = EditorGUILayout.Slider("Occlusion Fade", sampleRaytrace.occlusionPointFade, 0.0f, 1.0f);

                EditorGUILayout.Space(10);
            }

            if (sampleRaytrace.includeSpotLights)
            {
                EditorGUILayout.LabelField("Spot Lights", EditorStyles.boldLabel);

                sampleRaytrace.doSpotLightBleed = EditorGUILayout.ToggleLeft("Spot Light Bleed", sampleRaytrace.doSpotLightBleed);
                sampleRaytrace.spotLightsMultiplier = EditorGUILayout.FloatField("Spot Lights Multiplier", sampleRaytrace.spotLightsMultiplier);
                if (sampleRaytrace.doOcclusion) sampleRaytrace.occlusionSpotFade = EditorGUILayout.Slider("Occlusion Fade", sampleRaytrace.occlusionSpotFade, 0.0f, 1.0f);
                if (sampleRaytrace.doSpotLightBleed) sampleRaytrace.spotLightBleedAmount = EditorGUILayout.Slider("Bleed Amount", sampleRaytrace.spotLightBleedAmount, 0.0f, 1.0f);

                EditorGUILayout.Space(10);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace BakedVolumetrics
{
    [CustomEditor(typeof(SampleVoxelTracer))]
    public class SampleVoxelTracerEditor : Editor
    {
        SerializedProperty showUI;

        //|||||||||||||||||||||||||||||||||||||||||| VoxelTracer VARIABLES ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| VoxelTracer VARIABLES ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| VoxelTracer VARIABLES ||||||||||||||||||||||||||||||||||||||||||

        //Environment Options
        SerializedProperty enableEnvironmentLighting;
        SerializedProperty environmentResolution;
        SerializedProperty customEnvironmentMap;

        //Bake Options
        SerializedProperty volumetricTracing;
        SerializedProperty directSurfaceSamples;
        SerializedProperty directVolumetricSamples;
        SerializedProperty environmentSurfaceSamples;
        SerializedProperty environmentVolumetricSamples;
        SerializedProperty bounceSurfaceSamples;
        SerializedProperty bounceVolumetricSamples;
        SerializedProperty bounces;
        SerializedProperty normalOrientedHemisphereSampling;

        //Artistic Controls
        SerializedProperty albedoBoost;
        SerializedProperty indirectIntensity;
        SerializedProperty environmentIntensity;
        SerializedProperty emissiveIntensity;

        //Misc
        SerializedProperty enableGPU_Readback_Limit;
        SerializedProperty GPU_Readback_Limit;

        //Post Bake Options
        SerializedProperty volumetricDirectGaussianSamples;
        SerializedProperty volumetricBounceGaussianSamples;
        SerializedProperty volumetricEnvironmentGaussianSamples;

        //|||||||||||||||||||||||||||||||||||||||||| EDITOR VARIABLES ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| EDITOR VARIABLES ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| EDITOR VARIABLES ||||||||||||||||||||||||||||||||||||||||||

        private static int guiSpace = 10;

        private bool useCustomEnvironmentMap = false;
        private bool uncappedEditorValues = false;

        void OnEnable()
        {
            showUI = serializedObject.FindProperty("showUI");

            //Environment Options
            enableEnvironmentLighting = serializedObject.FindProperty("enableEnvironmentLighting");
            environmentResolution = serializedObject.FindProperty("environmentResolution");
            customEnvironmentMap = serializedObject.FindProperty("customEnvironmentMap");

            //Bake Options
            volumetricTracing = serializedObject.FindProperty("volumetricTracing");
            directSurfaceSamples = serializedObject.FindProperty("directSurfaceSamples");
            directVolumetricSamples = serializedObject.FindProperty("directVolumetricSamples");
            environmentSurfaceSamples = serializedObject.FindProperty("environmentSurfaceSamples");
            environmentVolumetricSamples = serializedObject.FindProperty("environmentVolumetricSamples");
            bounceSurfaceSamples = serializedObject.FindProperty("bounceSurfaceSamples");
            bounceVolumetricSamples = serializedObject.FindProperty("bounceVolumetricSamples");
            bounces = serializedObject.FindProperty("bounces");
            normalOrientedHemisphereSampling = serializedObject.FindProperty("normalOrientedHemisphereSampling");

            //Artistic Controls
            albedoBoost = serializedObject.FindProperty("albedoBoost");
            indirectIntensity = serializedObject.FindProperty("indirectIntensity");
            environmentIntensity = serializedObject.FindProperty("environmentIntensity");
            emissiveIntensity = serializedObject.FindProperty("emissiveIntensity");

            //Misc
            enableGPU_Readback_Limit = serializedObject.FindProperty("enableGPU_Readback_Limit");
            GPU_Readback_Limit = serializedObject.FindProperty("GPU_Readback_Limit");

            //Post Bake Options
            volumetricDirectGaussianSamples = serializedObject.FindProperty("volumetricDirectGaussianSamples");
            volumetricBounceGaussianSamples = serializedObject.FindProperty("volumetricBounceGaussianSamples");
            volumetricEnvironmentGaussianSamples = serializedObject.FindProperty("volumetricEnvironmentGaussianSamples");
        }

        private void IntProperty(SerializedProperty serializedProperty, string label, int minimumValue)
        {
            if (uncappedEditorValues)
            {
                serializedProperty.intValue = EditorGUILayout.IntField(label, serializedProperty.intValue);
                serializedProperty.intValue = Mathf.Max(minimumValue, serializedProperty.intValue); //clamp to never reach below the defined minimumValue
            }
            else
                EditorGUILayout.PropertyField(serializedProperty);
        }

        private void FloatProperty(SerializedProperty serializedProperty, string label, float minimumValue)
        {
            if (uncappedEditorValues)
            {
                serializedProperty.floatValue = EditorGUILayout.FloatField(label, serializedProperty.floatValue);
                serializedProperty.floatValue = Mathf.Max(minimumValue, serializedProperty.floatValue); //clamp to never reach below the defined minimumValue
            }
            else
                EditorGUILayout.PropertyField(serializedProperty);
        }

        public override void OnInspectorGUI()
        {
            GUIStyle errorStyle = new GUIStyle(EditorStyles.label);
            errorStyle.normal.textColor = Color.red;

            SampleVoxelTracer scriptObject = serializedObject.targetObject as SampleVoxelTracer;

            serializedObject.Update();

            if (showUI.boolValue == false)
                return;

            //|||||||||||||||||||||||||||||||||||||||||| VOXEL TRACING OPTIONS ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| VOXEL TRACING OPTIONS ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| VOXEL TRACING OPTIONS ||||||||||||||||||||||||||||||||||||||||||

            EditorGUILayout.LabelField("Voxel Tracing Options", EditorStyles.whiteLargeLabel);

            EditorGUILayout.PropertyField(normalOrientedHemisphereSampling);
            EditorGUILayout.PropertyField(volumetricTracing);
            EditorGUILayout.PropertyField(enableEnvironmentLighting);
            EditorGUILayout.Space(guiSpace);

            //|||||||||||||||||||||||||||||||||||||||||| DIRECT LIGHTING ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| DIRECT LIGHTING ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| DIRECT LIGHTING ||||||||||||||||||||||||||||||||||||||||||

            EditorGUILayout.LabelField("Direct Lighting", EditorStyles.whiteLargeLabel);

            FloatProperty(albedoBoost, "Albedo Boost", 0.0f);

            IntProperty(directSurfaceSamples, "Direct Surface Samples", 1);

            if(volumetricTracing.boolValue)
                IntProperty(directVolumetricSamples, "Direct Volumetric Samples", 1);

            EditorGUILayout.Space(guiSpace);

            //|||||||||||||||||||||||||||||||||||||||||| BOUNCE LIGHTING ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| BOUNCE LIGHTING ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| BOUNCE LIGHTING ||||||||||||||||||||||||||||||||||||||||||

            EditorGUILayout.LabelField("Bounce Lighting", EditorStyles.whiteLargeLabel);

            FloatProperty(indirectIntensity, "Indirect Intensity", 0.0f);

            IntProperty(bounceSurfaceSamples, "Bounce Surface Samples", 1);

            if (volumetricTracing.boolValue)
                IntProperty(bounceVolumetricSamples, "Bounce Volumetric Samples", 1);

            IntProperty(bounces, "Bounces", 1);

            EditorGUILayout.Space(guiSpace);

            //|||||||||||||||||||||||||||||||||||||||||| EMISSIVE LIGHTING ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| EMISSIVE LIGHTING ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| EMISSIVE LIGHTING ||||||||||||||||||||||||||||||||||||||||||

            EditorGUILayout.LabelField("Emissive Lighting", EditorStyles.whiteLargeLabel);

            FloatProperty(emissiveIntensity, "Emissive Intensity", 0.0f);

            EditorGUILayout.Space(guiSpace);

            //|||||||||||||||||||||||||||||||||||||||||| ENVIRONMENT LIGHTING ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| ENVIRONMENT LIGHTING ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| ENVIRONMENT LIGHTING ||||||||||||||||||||||||||||||||||||||||||

            if (enableEnvironmentLighting.boolValue)
            {
                EditorGUILayout.LabelField("Environment Lighting", EditorStyles.whiteLargeLabel);

                useCustomEnvironmentMap = EditorGUILayout.Toggle("Use Custom Environment Map", useCustomEnvironmentMap);

                if (!useCustomEnvironmentMap)
                    IntProperty(environmentResolution, "Environment Resolution", 32);

                if(useCustomEnvironmentMap)
                    EditorGUILayout.PropertyField(customEnvironmentMap);

                FloatProperty(environmentIntensity, "Environment Intensity", 0.0f);

                IntProperty(environmentSurfaceSamples, "Environment Surface Samples", 1);

                if (volumetricTracing.boolValue)
                    IntProperty(environmentVolumetricSamples, "Environment Volumetric Samples", 1);

                EditorGUILayout.Space(guiSpace);
            }

            //|||||||||||||||||||||||||||||||||||||||||| MISC ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| MISC ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| MISC ||||||||||||||||||||||||||||||||||||||||||

            EditorGUILayout.LabelField("Miscellaneous", EditorStyles.whiteLargeLabel);

            uncappedEditorValues = EditorGUILayout.Toggle("Uncap Editor Values", uncappedEditorValues);
            EditorGUILayout.Space(guiSpace);

            EditorGUILayout.LabelField("This will intentionally stall the CPU to wait until the GPU is ready after X amount of compute shader dispatches. It prevents the GPU from being overburdened and potentially crashing with the amount of work.", EditorStyles.helpBox);

            EditorGUILayout.PropertyField(enableGPU_Readback_Limit);

            if (enableGPU_Readback_Limit.boolValue)
            {
                EditorGUILayout.PropertyField(GPU_Readback_Limit);
            }

            EditorGUILayout.Space(guiSpace);

            //|||||||||||||||||||||||||||||||||||||||||| POST VOLUMETRIC BAKE OPTIONS ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| POST VOLUMETRIC BAKE OPTIONS ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| POST VOLUMETRIC BAKE OPTIONS ||||||||||||||||||||||||||||||||||||||||||

            if (volumetricTracing.boolValue)
            {
                EditorGUILayout.LabelField("Post Volumetric Bake Options", EditorStyles.whiteLargeLabel);

                IntProperty(volumetricDirectGaussianSamples, "Volumetric Direct Gaussian Samples", 0);
                IntProperty(volumetricBounceGaussianSamples, "Volumetric Bounce Gaussian Samples", 0);

                if(enableEnvironmentLighting.boolValue)
                    IntProperty(volumetricEnvironmentGaussianSamples, "Volumetric Environment Gaussian Samples", 0);

                EditorGUILayout.Space(guiSpace);
            }

            /*
            //|||||||||||||||||||||||||||||||||||||||||| FUNCTIONS ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| FUNCTIONS ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| FUNCTIONS ||||||||||||||||||||||||||||||||||||||||||

            EditorGUILayout.LabelField("Functions", EditorStyles.whiteLargeLabel);
            EditorGUILayout.Space(guiSpace);

            EditorGUILayout.LabelField("Setup", EditorStyles.boldLabel);

            if (GUILayout.Button("Generate Scene Buffers"))
                scriptObject.GenerateVolumes();

            if(enableEnvironmentLighting.boolValue)
            {
                if (GUILayout.Button("Capture Environment Map"))
                    scriptObject.CaptureEnvironment();
            }    

            EditorGUILayout.Space(guiSpace);

            EditorGUILayout.LabelField("Direct Lighting", EditorStyles.boldLabel);

            if (GUILayout.Button("Trace Direct Surface Lighting"))
                scriptObject.TraceDirectSurfaceLighting();

            if (volumetricTracing.boolValue)
            {
                if (GUILayout.Button("Trace Direct Volume Lighting"))
                    scriptObject.TraceDirectVolumeLighting();
            }

            if (enableEnvironmentLighting.boolValue)
            {
                if (GUILayout.Button("Trace Environment Surface Lighting"))
                    scriptObject.TraceEnvironmentSurfaceLighting();

                if (volumetricTracing.boolValue)
                {
                    if (GUILayout.Button("Trace Environment Volume Lighting"))
                        scriptObject.TraceEnvironmentVolumeLighting();
                }
            }

            EditorGUILayout.Space(guiSpace);

            EditorGUILayout.LabelField("Bounce Lighting", EditorStyles.boldLabel);

            if (GUILayout.Button("Trace Bounce Surface Lighting"))
                scriptObject.TraceBounceSurfaceLighting();

            if (volumetricTracing.boolValue)
            {
                if (GUILayout.Button("Trace Bounce Volume Lighting"))
                    scriptObject.TraceBounceVolumeLighting();
            }

            EditorGUILayout.Space(guiSpace);

            EditorGUILayout.LabelField("Final", EditorStyles.boldLabel);

            if (GUILayout.Button("Combine Surface Direct and Bounce Light"))
                scriptObject.CombineSurfaceLighting();

            if (volumetricTracing.boolValue)
            {
                if (GUILayout.Button("Combine Volume Direct and Bounce Light"))
                    scriptObject.CombineVolumeLighting();
            }
            */

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
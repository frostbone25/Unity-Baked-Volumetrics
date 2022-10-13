#if UNITY_EDITOR
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace BakedVolumetrics
{
    [CustomEditor(typeof(VolumeGenerator))]
    [CanEditMultipleObjects]
    public class VolumeGeneratorEditor : Editor
    {
        SerializedProperty lightingSource;
        SerializedProperty volumeName;
        SerializedProperty volumeSize;
        SerializedProperty volumeBitDepth;
        SerializedProperty voxelCalculation;
        SerializedProperty customVolumeResolution;
        SerializedProperty voxelDensitySize;
        SerializedProperty previewBounds;
        SerializedProperty previewVoxels;
        SerializedProperty combineColorType;
        SerializedProperty additiveLightprobeIntensity;
        SerializedProperty additiveRaytracedIntensity;
        SerializedProperty lerpFactor;
        SerializedProperty raymarchSamples;
        SerializedProperty previewDensityHeight;
        SerializedProperty densityType;
        SerializedProperty densityHeight;
        SerializedProperty densityHeightFallof;
        SerializedProperty densityConstant;
        SerializedProperty densityTop;
        SerializedProperty densityBottom;

        VolumeGenerator scriptObject;

        void OnEnable()
        {
            volumeName = serializedObject.FindProperty("volumeName");
            volumeSize = serializedObject.FindProperty("volumeSize");
            lightingSource = serializedObject.FindProperty("lightingSource");
            volumeBitDepth = serializedObject.FindProperty("volumeBitDepth");
            voxelCalculation = serializedObject.FindProperty("voxelCalculation");
            customVolumeResolution = serializedObject.FindProperty("customVolumeResolution");
            voxelDensitySize = serializedObject.FindProperty("voxelDensitySize");
            previewBounds = serializedObject.FindProperty("previewBounds");
            previewVoxels = serializedObject.FindProperty("previewVoxels");
            raymarchSamples = serializedObject.FindProperty("raymarchSamples");
            combineColorType = serializedObject.FindProperty("combineColorType");
            additiveLightprobeIntensity = serializedObject.FindProperty("additiveLightprobeIntensity");
            additiveRaytracedIntensity = serializedObject.FindProperty("additiveRaytracedIntensity");
            lerpFactor = serializedObject.FindProperty("lerpFactor");
            previewDensityHeight = serializedObject.FindProperty("previewDensityHeight");
            densityType = serializedObject.FindProperty("densityType");
            densityHeight = serializedObject.FindProperty("densityHeight");
            densityHeightFallof = serializedObject.FindProperty("densityHeightFallof");
            densityConstant = serializedObject.FindProperty("densityConstant");
            densityTop = serializedObject.FindProperty("densityTop");
            densityBottom = serializedObject.FindProperty("densityBottom");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            scriptObject = serializedObject.targetObject as VolumeGenerator;
            scriptObject.Setup();
            scriptObject.UpdateMaterialKeywords();

            LightingSource lightingSourceValue = (LightingSource)lightingSource.intValue;
            scriptObject.sampleCPURaytrace.showUI = lightingSourceValue == LightingSource.CPU_Raytrace;
            scriptObject.sampleLightprobe.showUI = lightingSourceValue == LightingSource.LightProbes;
            //scriptObject.sampleRaytrace.showUI = lightingSourceValue == LightingSource.CPU_Raytracer || lightingSourceValue == LightingSource.Combined;
            //scriptObject.sampleLightprobe.showUI = lightingSourceValue == LightingSource.Lightprobes || lightingSourceValue == LightingSource.Combined;

            //||||||||||||||||||||||||||||||||| VOLUME PROPERTIES |||||||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||||||| VOLUME PROPERTIES |||||||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||||||| VOLUME PROPERTIES |||||||||||||||||||||||||||||||||
            EditorGUILayout.LabelField("Volume Properties", EditorStyles.whiteLargeLabel);
            EditorGUILayout.PropertyField(volumeName);
            EditorGUILayout.PropertyField(volumeSize);
            EditorGUILayout.Space(10);

            //||||||||||||||||||||||||||||||||| VOLUME QUALITY |||||||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||||||| VOLUME QUALITY |||||||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||||||| VOLUME QUALITY |||||||||||||||||||||||||||||||||
            EditorGUILayout.LabelField("Volume Quality", EditorStyles.whiteLargeLabel);
            EditorGUILayout.PropertyField(volumeBitDepth);
            EditorGUILayout.PropertyField(voxelCalculation);
            VoxelCalculation voxelCalculationValue = (VoxelCalculation)voxelCalculation.intValue;

            if (voxelCalculationValue == VoxelCalculation.Custom)
                EditorGUILayout.PropertyField(customVolumeResolution);
            else if (voxelCalculationValue == VoxelCalculation.Automatic)
                EditorGUILayout.PropertyField(voxelDensitySize);

            voxelDensitySize.floatValue = Mathf.Max(0.0f, voxelDensitySize.floatValue);

            EditorGUILayout.LabelField(string.Format("[{0}x{1}x{2}] {3} voxels.", scriptObject.GetVoxelResolution().x, scriptObject.GetVoxelResolution().y, scriptObject.GetVoxelResolution().z, scriptObject.GetTotalVoxelCount()), EditorStyles.helpBox);
            EditorGUILayout.Space(10);

            //||||||||||||||||||||||||||||||||| VOLUME RENDERING |||||||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||||||| VOLUME RENDERING |||||||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||||||| VOLUME RENDERING |||||||||||||||||||||||||||||||||
            EditorGUILayout.LabelField("Volume Rendering", EditorStyles.whiteLargeLabel);
            EditorGUILayout.PropertyField(raymarchSamples);
            EditorGUILayout.PropertyField(lightingSource);

            /*
            if(lightingSourceValue == LightingSource.Combined)
            {
                EditorGUILayout.PropertyField(combineColorType);
                CombineColorType combineColorTypeValue = (CombineColorType)combineColorType.intValue;

                EditorGUILayout.LabelField("Combine Lighting Sources Options", EditorStyles.whiteLargeLabel);

                if (combineColorTypeValue == CombineColorType.Additive)
                {
                    EditorGUILayout.PropertyField(additiveLightprobeIntensity);
                    EditorGUILayout.PropertyField(additiveRaytracedIntensity);
                }
                else if (combineColorTypeValue == CombineColorType.Lerp)
                {
                    lerpFactor.floatValue = EditorGUILayout.Slider("Lerp Factor", lerpFactor.floatValue, 0.0f, 1.0f);
                }
            }
            */

            //||||||||||||||||||||||||||||||||| VOLUME DENSITY |||||||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||||||| VOLUME DENSITY |||||||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||||||| VOLUME DENSITY |||||||||||||||||||||||||||||||||
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Volume Density", EditorStyles.whiteLargeLabel);
            EditorGUILayout.PropertyField(densityType);

            DensityType densityTypeValue = (DensityType)densityType.intValue;

            if (densityTypeValue == DensityType.Constant)
            {
                EditorGUILayout.PropertyField(densityConstant);
            }
            else if (densityTypeValue == DensityType.HeightBased || densityTypeValue == DensityType.HeightBasedLuminance)
            {
                EditorGUILayout.PropertyField(densityTop);
                EditorGUILayout.PropertyField(densityBottom);

                EditorGUILayout.PropertyField(densityHeight);
                EditorGUILayout.PropertyField(densityHeightFallof);
            }

            //||||||||||||||||||||||||||||||||| GIZMOS |||||||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||||||| GIZMOS |||||||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||||||| GIZMOS |||||||||||||||||||||||||||||||||
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Gizmos", EditorStyles.whiteLargeLabel);
            EditorGUILayout.PropertyField(previewBounds);

            if((DensityType)densityType.intValue == DensityType.HeightBased) 
                EditorGUILayout.PropertyField(previewDensityHeight);

            EditorGUILayout.PropertyField(previewVoxels);
            EditorGUILayout.Space(10);

            //||||||||||||||||||||||||||||||||| FUNCTIONS |||||||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||||||| FUNCTIONS |||||||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||||||| FUNCTIONS |||||||||||||||||||||||||||||||||
            if (GUILayout.Button("Update Material"))
            {
                scriptObject.UpdateMaterial();
            }

            if (GUILayout.Button("Generate Volume"))
            {
                scriptObject.GenerateVolume();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
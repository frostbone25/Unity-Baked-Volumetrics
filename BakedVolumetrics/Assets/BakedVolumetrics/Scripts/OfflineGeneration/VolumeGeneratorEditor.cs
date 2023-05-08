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
        SerializedProperty densityInvertLuminance;

        VolumeGenerator scriptObject;

        void OnEnable()
        {
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
            densityInvertLuminance = serializedObject.FindProperty("densityInvertLuminance");
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
            scriptObject.sampleIBL.showUI = lightingSourceValue == LightingSource.IBL;
            //scriptObject.sampleRaytrace.showUI = lightingSourceValue == LightingSource.CPU_Raytracer || lightingSourceValue == LightingSource.Combined;
            //scriptObject.sampleLightprobe.showUI = lightingSourceValue == LightingSource.Lightprobes || lightingSourceValue == LightingSource.Combined;

            //||||||||||||||||||||||||||||||||| VOLUME PROPERTIES |||||||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||||||| VOLUME PROPERTIES |||||||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||||||| VOLUME PROPERTIES |||||||||||||||||||||||||||||||||
            EditorGUILayout.LabelField("Volume Properties", EditorStyles.whiteLargeLabel);
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

            if(lightingSourceValue == LightingSource.LightProbes)
            {
                EditorGUILayout.LabelField("NOTE: The resolution of the final bake is dependent on how dense your light probe groups are. If your probe groups are sparse populated but you're generating at a high resolution volume then you won't get any sharper results and will just be wasting memory/vram/disk space. If you want sharper results consider using a different lighting source.", EditorStyles.helpBox);
            }

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

            if (densityTypeValue == DensityType.Luminance)
            {
                EditorGUILayout.PropertyField(densityInvertLuminance);
            }
            else if(densityTypeValue == DensityType.HeightBasedLuminance)
            {
                EditorGUILayout.PropertyField(densityInvertLuminance);
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
            EditorGUILayout.LabelField("Generation", EditorStyles.whiteLargeLabel);


            if (GUILayout.Button("Generate Volume"))
            {
                scriptObject.GenerateVolume(0);
            }

            if (GUILayout.Button("Generate Volume with Post Effects"))
            {
                scriptObject.GenerateVolume(1);
            }

            if (GUILayout.Button("Apply Post Effects Only"))
            {
                scriptObject.GenerateVolume(2);
            }

            //||||||||||||||||||||||||||||||||| FUNCTIONS |||||||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||||||| FUNCTIONS |||||||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||||||| FUNCTIONS |||||||||||||||||||||||||||||||||
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Utility", EditorStyles.whiteLargeLabel);

            if (GUILayout.Button("Update Material"))
            {
                scriptObject.UpdateMaterial();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
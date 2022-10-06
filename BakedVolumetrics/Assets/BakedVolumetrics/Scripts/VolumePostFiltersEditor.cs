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
    [CustomEditor(typeof(VolumePostFilters))]
    [CanEditMultipleObjects]
    public class VolumePostFiltersEditor : Editor
    {
        SerializedProperty postAdjustments;
        SerializedProperty brightness;
        SerializedProperty contrast;
        SerializedProperty saturation;
        SerializedProperty vibrance;
        SerializedProperty hueShift;
        SerializedProperty gamma;
        SerializedProperty colorFilterAmount;
        SerializedProperty colorFilter;
        SerializedProperty colorMultiply;
        SerializedProperty postBlur;
        SerializedProperty gaussianSamples;

        void OnEnable()
        {
            postAdjustments = serializedObject.FindProperty("postAdjustments");
            brightness = serializedObject.FindProperty("brightness");
            contrast = serializedObject.FindProperty("contrast");
            saturation = serializedObject.FindProperty("saturation");
            vibrance = serializedObject.FindProperty("vibrance");
            hueShift = serializedObject.FindProperty("hueShift");
            gamma = serializedObject.FindProperty("gamma");
            colorFilterAmount = serializedObject.FindProperty("colorFilterAmount");
            colorFilter = serializedObject.FindProperty("colorFilter");
            colorMultiply = serializedObject.FindProperty("colorMultiply");
            postBlur = serializedObject.FindProperty("postBlur");
            gaussianSamples = serializedObject.FindProperty("gaussianSamples");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            VolumePostFilters volumePostFilters = serializedObject.targetObject as VolumePostFilters;

            EditorGUILayout.LabelField("Post Volume Filters", EditorStyles.whiteLargeLabel);

            EditorGUILayout.LabelField("Adjustments", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(postAdjustments);

            if (postAdjustments.boolValue)
            {
                EditorGUILayout.PropertyField(brightness);
                EditorGUILayout.PropertyField(contrast);
                EditorGUILayout.PropertyField(saturation);
                EditorGUILayout.PropertyField(vibrance);
                EditorGUILayout.PropertyField(hueShift);
                EditorGUILayout.PropertyField(gamma);
                EditorGUILayout.PropertyField(colorFilterAmount);
                EditorGUILayout.PropertyField(colorFilter);
                EditorGUILayout.PropertyField(colorMultiply);
            }
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Blur", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(postBlur);

            if(postBlur.boolValue)
                EditorGUILayout.PropertyField(gaussianSamples);

            EditorGUILayout.Space(10);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
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
    [CustomEditor(typeof(SampleLightprobe))]
    [CanEditMultipleObjects]
    public class SampleLightprobeEditor : Editor
    {
        SerializedProperty showUI;

        SerializedProperty occlusionLeakFactor;
        SerializedProperty occlusionPreventLeaks;
        SerializedProperty indoorOnlySamples;

        SerializedProperty gaussianBlurSamples;
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

        void OnEnable()
        {
            showUI = serializedObject.FindProperty("showUI");

            occlusionLeakFactor = serializedObject.FindProperty("occlusionLeakFactor");
            occlusionPreventLeaks = serializedObject.FindProperty("occlusionPreventLeaks");
            indoorOnlySamples = serializedObject.FindProperty("indoorOnlySamples");

            gaussianBlurSamples = serializedObject.FindProperty("gaussianBlurSamples");
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
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (showUI.boolValue == false)
                return;

            EditorGUILayout.LabelField("Light Probe Baking Options", EditorStyles.whiteLargeLabel);

            EditorGUILayout.PropertyField(indoorOnlySamples);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(occlusionPreventLeaks);

            if (occlusionPreventLeaks.boolValue) 
                EditorGUILayout.PropertyField(occlusionLeakFactor);

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Post Bake Options", EditorStyles.whiteLargeLabel);

            EditorGUILayout.PropertyField(gaussianBlurSamples);
            EditorGUILayout.PropertyField(postAdjustments);

            if(postAdjustments.boolValue)
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

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
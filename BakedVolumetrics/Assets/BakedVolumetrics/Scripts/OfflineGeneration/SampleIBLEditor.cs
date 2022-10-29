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
    [CustomEditor(typeof(SampleIBL))]
    [CanEditMultipleObjects]
    public class SampleIBLEditor : Editor
    {
        SerializedProperty sampleResolution;
        SerializedProperty occlusionLeakFactor;
        SerializedProperty occlusionPreventLeaks;
        SerializedProperty indoorOnlySamples;
        SerializedProperty showUI;

        void OnEnable()
        {
            sampleResolution = serializedObject.FindProperty("sampleResolution");
            occlusionLeakFactor = serializedObject.FindProperty("occlusionLeakFactor");
            occlusionPreventLeaks = serializedObject.FindProperty("occlusionPreventLeaks");
            indoorOnlySamples = serializedObject.FindProperty("indoorOnlySamples");
            showUI = serializedObject.FindProperty("showUI");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (showUI.boolValue == false)
                return;

            EditorGUILayout.LabelField("IBL Volume Lighting", EditorStyles.whiteLargeLabel);

            EditorGUILayout.PropertyField(sampleResolution);

            EditorGUILayout.PropertyField(indoorOnlySamples);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(occlusionPreventLeaks);
            if (occlusionPreventLeaks.boolValue) EditorGUILayout.PropertyField(occlusionLeakFactor);
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
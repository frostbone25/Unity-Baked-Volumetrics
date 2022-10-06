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
        SerializedProperty occlusionLeakFactor;
        SerializedProperty occlusionPreventLeaks;
        SerializedProperty indoorOnlySamples;
        SerializedProperty showUI;

        void OnEnable()
        {
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

            EditorGUILayout.LabelField("Lightprobe Volume Lighting", EditorStyles.whiteLargeLabel);

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
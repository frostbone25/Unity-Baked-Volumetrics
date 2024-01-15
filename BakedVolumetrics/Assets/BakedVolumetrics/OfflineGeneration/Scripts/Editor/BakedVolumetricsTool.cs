#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace BakedVolumetrics
{
    public class BakedVolumetricsTool : EditorWindow
    {
        private static string sceneCollidersParentName = "TEMP_SceneStaticColliders";
        private GameObject sceneCollidersParent;

        //GUI related
        private static int guiSectionSpacePixels = 10;
        private Vector2Int windowSize = new Vector2Int(500, 500);

        //add a menu item at the top of the unity editor toolbar
        [MenuItem("Baked Volumetrics/Setup")]
        public static void ShowWindow()
        {
            //get the window and open it
            GetWindow(typeof(BakedVolumetricsTool));
        }

        /// <summary>
        /// GUI display function for the window
        /// </summary>
        void OnGUI()
        {
            maxSize = windowSize;
            minSize = windowSize;

            //window title
            GUILayout.Label("Baked Volumetrics", EditorStyles.whiteLargeLabel);
            GUILayout.Space(guiSectionSpacePixels);

            GUILayout.Label("Main", EditorStyles.label);

            if (GUILayout.Button("Create Volume"))
                CreateVolume();

            GUILayout.Space(guiSectionSpacePixels);
        }

        public void CreateVolume()
        {
            GameObject newVolumeGameObject = new GameObject("NewBakedVolume");
            VolumeGenerator newVolumeScript = newVolumeGameObject.AddComponent<VolumeGenerator>();
            newVolumeScript.Setup();
            newVolumeScript.SetupSceneObjectVolume();
        }
    }
}

#endif
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
        private static string sceneCollidersParentName = "TEMP_SceneColliders";
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

            GUILayout.Label("Utilities", EditorStyles.label);
            GUILayout.Label("Note: When generating a volume using a raytraced/combined source, if your scene doesn't have any you need to spawn colliders so it can trace against them to calculate the volumetric lighting.", EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Spawn Colliders"))
                SpawnSceneColliders();

            if (GUILayout.Button("Destroy Colliders"))
                DestroySceneColliders();
            EditorGUILayout.EndHorizontal();
        }

        public void CreateVolume()
        {
            GameObject newVolumeGameObject = new GameObject("NewBakedVolume");
            VolumeGenerator newVolumeScript = newVolumeGameObject.AddComponent<VolumeGenerator>();
            newVolumeScript.Setup();
            newVolumeScript.SetupSceneObjectVolume();
        }

        public void SpawnSceneColliders()
        {
            sceneCollidersParent = new GameObject(sceneCollidersParentName);

            MeshFilter[] meshes = FindObjectsOfType<MeshFilter>();

            for(int i = 0; i < meshes.Length; i++)
            {
                GameObject meshGameObject = meshes[i].gameObject;

                StaticEditorFlags staticEditorFlags = GameObjectUtility.GetStaticEditorFlags(meshGameObject);

                if(staticEditorFlags.HasFlag(StaticEditorFlags.ContributeGI))
                {
                    GameObject sceneColliderChild = new GameObject("collider");
                    sceneColliderChild.transform.SetParent(sceneCollidersParent.transform);

                    sceneColliderChild.transform.position = meshGameObject.transform.position;
                    sceneColliderChild.transform.rotation = meshGameObject.transform.rotation;
                    sceneColliderChild.transform.localScale = meshGameObject.transform.localScale;
                    
                    MeshCollider meshCollider = sceneColliderChild.AddComponent<MeshCollider>();
                    meshCollider.sharedMesh = meshes[i].sharedMesh;
                }
            }
        }

        public void DestroySceneColliders()
        {
            if (sceneCollidersParent != null)
                DestroyImmediate(sceneCollidersParent);
            else
            {
                sceneCollidersParent = GameObject.Find(sceneCollidersParentName);

                if(sceneCollidersParent != null)
                    DestroyImmediate(sceneCollidersParent);
            }
        }
    }
}

#endif
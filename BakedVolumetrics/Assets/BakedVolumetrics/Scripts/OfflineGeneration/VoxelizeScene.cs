#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices.ComTypes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace BakedVolumetrics
{
    /*
     * SELF NOTE 1: The Anti-Aliasing when used for the generation of the voxels does help maintain geometry at low resolutions, however there is a quirk because it also samples the background color.
     * This was noticable when rendering position/normals voxel buffers where when on some geo looked darker than usual because the BG color was black.
     * Not to sure how to solve this and just might deal with it?
     *
     * SELF NOTE 2: Generating normals/position buffers might not actually be needed? More so the normals? We might be able to compute those by hand in the compute shader.
    */

    public class VoxelizeScene : MonoBehaviour
    {
        [Header("Voxelizer Main")]
        public string voxelName = "Voxel";
        public Vector3 voxelSize = new Vector3(10.0f, 10.0f, 10.0f);
        public float voxelDensitySize = 1.0f;

        [Header("Baking Options")]
        public bool enableAnitAliasing = false;
        public bool blendVoxelResult = false;

        [Header("Bakes")]
        public bool bakeAlbedo = true;
        public bool bakeEmissive = true;
        public bool bakeNormal = false;
        public bool bakePosition = false;

        [Header("Gizmos")]
        public bool previewBounds = true;
        public bool previewVoxels = false;

        private RenderTexture voxelCameraSlice;
        private GameObject voxelCameraGameObject;
        private Camera voxelCamera;

        private Vector3Int voxelResolution;
        private ComputeShader slicer;
        private ComputeShader voxelize;
        private Shader cameraVoxelAlbedoShader;
        private Shader cameraVoxelEmissiveShader;
        private Shader cameraVoxelNormalShader;
        private Shader cameraVoxelPositionShader;

        private static TextureFormat textureformat = TextureFormat.RGBAHalf;
        private static RenderTextureFormat rendertextureformat = RenderTextureFormat.ARGBHalf;

        private void GetResources()
        {
            if (slicer == null) slicer = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/BakedVolumetrics/ComputeShaders/Slicer.compute");
            if (voxelize == null) voxelize = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/BakedVolumetrics/ComputeShaders/VoxelTracing.compute");

            if (cameraVoxelAlbedoShader == null) cameraVoxelAlbedoShader = Shader.Find("Hidden/CameraVoxelAlbedo");
            if (cameraVoxelEmissiveShader == null) cameraVoxelEmissiveShader = Shader.Find("Hidden/CameraVoxelEmissive");
            if (cameraVoxelNormalShader == null) cameraVoxelNormalShader = Shader.Find("Hidden/CameraVoxelNormal");
            if (cameraVoxelPositionShader == null) cameraVoxelPositionShader = Shader.Find("Hidden/CameraVoxelPosition");
        }

        private void SetupVoxelCamera()
        {
            if (voxelCameraGameObject == null)
                voxelCameraGameObject = new GameObject("VoxelizeSceneCamera");

            if(voxelCamera == null)
                voxelCamera = voxelCameraGameObject.AddComponent<Camera>();

            voxelCamera.enabled = false;
            voxelCamera.forceIntoRenderTexture = true;
            voxelCamera.useOcclusionCulling = false;
            voxelCamera.orthographic = true;
            voxelCamera.nearClipPlane = 0.0f;
            voxelCamera.farClipPlane = voxelDensitySize;
            voxelCamera.clearFlags = CameraClearFlags.Color;
            voxelCamera.backgroundColor = new Color(0, 0, 0, 0);
            voxelCamera.depthTextureMode = DepthTextureMode.None;
            voxelCamera.renderingPath = RenderingPath.Forward;
        }

        private void CleanupVoxelCamera()
        {
            if (voxelCameraGameObject != null)
                DestroyImmediate(voxelCameraGameObject);

            if (voxelCamera != null)
                DestroyImmediate(voxelCamera);

            voxelCameraGameObject = null;
            voxelCamera = null;
        }

        private void CalculateResolution()
        {
            voxelResolution = new Vector3Int((int)(voxelSize.x / voxelDensitySize), (int)(voxelSize.y / voxelDensitySize), (int)(voxelSize.z / voxelDensitySize));
        }

        public void SetupVolume()
        {
            //check if there is a data folder, if not then create one
            if (AssetDatabase.IsValidFolder("Assets/BakedVolumetrics/Data") == false)
                AssetDatabase.CreateFolder("Assets/BakedVolumetrics", "Data");

            //check for a folder of the same scene name
            UnityEngine.SceneManagement.Scene activeScene = EditorSceneManager.GetActiveScene();
            string sceneName = activeScene.name;
            string sceneVolumetricsFolder = "Assets/BakedVolumetrics/Data/" + sceneName;

            if (activeScene.IsValid() == false || string.IsNullOrEmpty(activeScene.path))
            {
                string message = "Scene is not valid! Be sure to save the scene before you setup volumetrics for it!";
                EditorUtility.DisplayDialog("Error", message, "OK");
                Debug.LogError(message);
                return;
            }

            //check if there is a folder sharing the scene name, if there isn't then create one
            if (AssetDatabase.IsValidFolder(sceneVolumetricsFolder) == false)
                AssetDatabase.CreateFolder("Assets/BakedVolumetrics/Data", sceneName);
        }

        private Texture2D ConvertFromRenderTexture2D(RenderTexture rt, TextureFormat texFormat)
        {
            Texture2D output = new Texture2D(rt.width, rt.height, texFormat, false);
            output.alphaIsTransparency = true;

            RenderTexture.active = rt;

            output.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            output.Apply();

            return output;
        }

        [ContextMenu("Generate")]
        public void GenerateVolumes()
        {
            if (bakeAlbedo) GenerateVolume(cameraVoxelAlbedoShader, string.Format("{0}_albedo", voxelName), rendertextureformat, textureformat);
            if (bakeEmissive) GenerateVolume(cameraVoxelEmissiveShader, string.Format("{0}_emissive", voxelName), rendertextureformat, textureformat);
            if (bakeNormal) GenerateVolume(cameraVoxelNormalShader, string.Format("{0}_normal", voxelName), rendertextureformat, textureformat);
            if (bakePosition) GenerateVolume(cameraVoxelPositionShader, string.Format("{0}_position", voxelName), rendertextureformat, textureformat);
        }

        public void GenerateVolume(Shader replacementShader, string filename, RenderTextureFormat rtFormat, TextureFormat texFormat)
        {
            GetResources();
            CalculateResolution();
            SetupVolume();
            SetupVoxelCamera();

            float xOffset = voxelSize.x / voxelResolution.x;
            float yOffset = voxelSize.y / voxelResolution.y;
            float zOffset = voxelSize.z / voxelResolution.z;

            string renderTypeKey = "";
            int rtDepth = 16;

            voxelCamera.SetReplacementShader(replacementShader, renderTypeKey);
            voxelCamera.allowMSAA = enableAnitAliasing;

            //||||||||||||||||||||||||||||||||| X |||||||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||||||| X |||||||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||||||| X |||||||||||||||||||||||||||||||||
            Texture2D[] slices_x_neg = new Texture2D[voxelResolution.x];
            Texture2D[] slices_x_pos = new Texture2D[voxelResolution.x];

            voxelCameraSlice = new RenderTexture(voxelResolution.z, voxelResolution.y, rtDepth, rtFormat);
            voxelCameraSlice.antiAliasing = enableAnitAliasing ? 8 : 0;
            voxelCameraSlice.filterMode = FilterMode.Point;
            voxelCamera.targetTexture = voxelCameraSlice;
            voxelCamera.orthographicSize = voxelSize.y * 0.5f;

            //--------------------- X POSITIVE ---------------------
            voxelCameraGameObject.transform.eulerAngles = new Vector3(0, 90.0f, 0);

            for (int i = 0; i < voxelResolution.x; i++)
            {
                voxelCameraGameObject.transform.position = transform.position - new Vector3(voxelSize.x / 2.0f, 0, 0) + new Vector3(xOffset * i, 0, 0);
                voxelCamera.Render();
                slices_x_neg[i] = ConvertFromRenderTexture2D(voxelCameraSlice, texFormat);
            }

            //--------------------- X NEGATIVE ---------------------
            voxelCameraGameObject.transform.eulerAngles = new Vector3(0, -90.0f, 0);

            for (int i = 0; i < voxelResolution.x; i++)
            {
                voxelCameraGameObject.transform.position = transform.position + new Vector3(voxelSize.x / 2.0f, 0, 0) - new Vector3(xOffset * i, 0, 0);
                voxelCamera.Render();
                slices_x_pos[i] = ConvertFromRenderTexture2D(voxelCameraSlice, texFormat);
            }

            //||||||||||||||||||||||||||||||||| Y |||||||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||||||| Y |||||||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||||||| Y |||||||||||||||||||||||||||||||||
            Texture2D[] slices_y_pos = new Texture2D[voxelResolution.y];
            Texture2D[] slices_y_neg = new Texture2D[voxelResolution.y];

            voxelCameraSlice = new RenderTexture(voxelResolution.x, voxelResolution.z, rtDepth, rtFormat);
            voxelCameraSlice.antiAliasing = enableAnitAliasing ? 8 : 0;
            voxelCameraSlice.filterMode = FilterMode.Point;
            voxelCamera.targetTexture = voxelCameraSlice;
            voxelCamera.orthographicSize = voxelSize.z * 0.5f;

            //--------------------- Y POSITIVE ---------------------
            voxelCameraGameObject.transform.eulerAngles = new Vector3(-90.0f, 0, 0);

            for (int i = 0; i < voxelResolution.y; i++)
            {
                voxelCameraGameObject.transform.position = transform.position - new Vector3(0, voxelSize.y / 2.0f, 0) + new Vector3(0, yOffset * i, 0);
                voxelCamera.Render();
                slices_y_pos[i] = ConvertFromRenderTexture2D(voxelCameraSlice, texFormat);
            }

            //--------------------- Y NEGATIVE ---------------------
            voxelCameraGameObject.transform.eulerAngles = new Vector3(90.0f, 0, 0);

            for (int i = 0; i < voxelResolution.y; i++)
            {
                voxelCameraGameObject.transform.position = transform.position + new Vector3(0, voxelSize.y / 2.0f, 0) - new Vector3(0, yOffset * i, 0);
                voxelCamera.Render();
                slices_y_neg[i] = ConvertFromRenderTexture2D(voxelCameraSlice, texFormat);
            }

            //||||||||||||||||||||||||||||||||| Z |||||||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||||||| Z |||||||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||||||| Z |||||||||||||||||||||||||||||||||
            Texture2D[] slices_z_pos = new Texture2D[voxelResolution.z];
            Texture2D[] slices_z_neg = new Texture2D[voxelResolution.z];

            voxelCameraSlice = new RenderTexture(voxelResolution.x, voxelResolution.y, rtDepth, rtFormat);
            voxelCameraSlice.antiAliasing = enableAnitAliasing ? 8 : 0;
            voxelCameraSlice.filterMode = FilterMode.Point;
            voxelCamera.targetTexture = voxelCameraSlice;
            voxelCamera.orthographicSize = voxelSize.y * 0.5f;

            //--------------------- Z POSITIVE ---------------------
            voxelCameraGameObject.transform.eulerAngles = new Vector3(0, 0, 0);

            for (int i = 0; i < voxelResolution.z; i++)
            {
                voxelCameraGameObject.transform.position = transform.position - new Vector3(0, 0, voxelSize.z / 2.0f) + new Vector3(0, 0, zOffset * i);
                voxelCamera.Render();
                slices_z_pos[i] = ConvertFromRenderTexture2D(voxelCameraSlice, texFormat);
            }

            //--------------------- Z NEGATIVE ---------------------
            voxelCameraGameObject.transform.eulerAngles = new Vector3(0, 180.0f, 0);

            for (int i = 0; i < voxelResolution.z; i++)
            {
                voxelCameraGameObject.transform.position = transform.position + new Vector3(0, 0, voxelSize.z / 2.0f) - new Vector3(0, 0, zOffset * i);
                voxelCamera.Render();
                slices_z_neg[i] = ConvertFromRenderTexture2D(voxelCameraSlice, texFormat);
            }

            //--------------------- COMBINE RESULTS ---------------------
            Texture3D result = new Texture3D(voxelResolution.x, voxelResolution.y, voxelResolution.z, DefaultFormat.HDR, TextureCreationFlags.None);
            result.filterMode = FilterMode.Point;

            for (int x = 0; x < result.width; x++)
            {
                for(int y = 0; y < result.height; y++)
                {
                    for(int z = 0; z < result.depth; z++)
                    {
                        Color colorResult = new Color(0, 0, 0, 0);

                        Color color_x_pos = slices_x_pos[(result.width - 1) - x].GetPixel(z, y);
                        Color color_x_neg = slices_x_neg[x].GetPixel((result.depth - 1) - z, y);

                        Color color_y_pos = slices_y_pos[y].GetPixel(x, (result.depth - 1) - z);
                        Color color_y_neg = slices_y_neg[(result.height - 1) - y].GetPixel(x, z);

                        Color color_z_pos = slices_z_pos[z].GetPixel(x, y);
                        Color color_z_neg = slices_z_neg[(result.depth - 1) - z].GetPixel((result.width - 1) - x, y);

                        if(blendVoxelResult)
                        {
                            int alphaIndex = 0;

                            if (color_x_pos.a > 0.0f)
                            {
                                colorResult += color_x_pos;
                                alphaIndex++;
                            }

                            if (color_x_neg.a > 0.0f)
                            {
                                colorResult += color_x_neg;
                                alphaIndex++;
                            }

                            if (color_y_pos.a > 0.0f)
                            {
                                colorResult += color_y_pos;
                                alphaIndex++;
                            }

                            if (color_y_neg.a > 0.0f)
                            {
                                colorResult += color_y_neg;
                                alphaIndex++;
                            }

                            if (color_z_pos.a > 0.0f)
                            {
                                colorResult += color_z_pos;
                                alphaIndex++;
                            }

                            if (color_z_neg.a > 0.0f)
                            {
                                colorResult += color_z_neg;
                                alphaIndex++;
                            }

                            if (alphaIndex > 0)
                                colorResult = new Color(colorResult.r / alphaIndex, colorResult.g / alphaIndex, colorResult.b / alphaIndex, colorResult.a);
                        }
                        else
                        {
                            if (color_x_pos.a > 0.0f)
                                colorResult += color_x_pos;
                            else if (color_x_neg.a > 0.0f)
                                colorResult += color_x_neg;
                            else if (color_y_pos.a > 0.0f)
                                colorResult += color_y_pos;
                            else if (color_y_neg.a > 0.0f)
                                colorResult += color_y_neg;
                            else if (color_z_pos.a > 0.0f)
                                colorResult += color_z_pos;
                            else if (color_z_neg.a > 0.0f)
                                colorResult += color_z_neg;
                        }

                        result.SetPixel(x, y, z, colorResult);
                    }
                }
            }

            //--------------------- FINAL ---------------------
            SaveVolumeTexture(filename, result);
            CleanupVoxelCamera();
            voxelCameraSlice.Release();
        }

        private void SaveVolumeTexture(string fileName, Texture3D tex3D)
        {
            //build the paths
            UnityEngine.SceneManagement.Scene activeScene = EditorSceneManager.GetActiveScene();
            string sceneName = activeScene.name;
            string sceneVolumetricsFolder = "Assets/BakedVolumetrics/Data/" + sceneName;
            string volumeAssetName = fileName + ".asset";
            string volumeAssetPath = sceneVolumetricsFolder + "/" + volumeAssetName;

            //AssetDatabase.DeleteAsset(volumeAssetPath);
            AssetDatabase.CreateAsset(tex3D, volumeAssetPath);
        }

        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| GIZMOS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| GIZMOS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| GIZMOS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||

        private void OnDrawGizmos()
        {
            CalculateResolution();

            Gizmos.color = Color.white;

            if (previewBounds)
                Gizmos.DrawWireCube(transform.position, voxelSize);

            if (previewVoxels)
            {
                //3d loop for our volume
                for (int x = -voxelResolution.x / 2; x <= voxelResolution.x / 2; x++)
                {
                    //get the x offset
                    float x_offset = voxelSize.x / voxelResolution.x;

                    for (int y = -voxelResolution.y / 2; y <= voxelResolution.y / 2; y++)
                    {
                        //get the y offset
                        float y_offset = voxelSize.y / voxelResolution.y;

                        for (int z = -voxelResolution.z / 2; z <= voxelResolution.z / 2; z++)
                        {
                            //get the z offset
                            float z_offset = voxelSize.z / voxelResolution.z;

                            Vector3 probePosition = new Vector3(transform.position.x + (x * x_offset), transform.position.y + (y * y_offset), transform.position.z + (z * z_offset));
                            Vector3 voxelWorldSize = new Vector3(x_offset, y_offset, z_offset);

                            Gizmos.DrawWireCube(probePosition, voxelWorldSize);
                        }
                    }
                }
            }
        }
    }
}

#endif
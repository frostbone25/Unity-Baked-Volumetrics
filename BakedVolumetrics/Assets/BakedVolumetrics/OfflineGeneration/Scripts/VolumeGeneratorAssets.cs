#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BakedVolumetricsOffline
{
    public class VolumeGeneratorAssets
    {
        public static string localAssetFolder =                          "Assets/BakedVolumetrics";
        public static string localAssetDataFolder =                      "Assets/BakedVolumetrics/Data";
        public static string localAssetDataSharedFolder =                "Assets/BakedVolumetrics/Data/Shared";
        public static string localAssetShadersFolder =                   "Assets/BakedVolumetrics/OfflineGeneration/Shaders";

        private static string voxelDirectSurfaceLightAssetPath =          "Assets/BakedVolumetrics/OfflineGeneration/Shaders/VoxelDirectSurfaceLight.compute";
        private static string voxelDirectVolumetricLightAssetPath =       "Assets/BakedVolumetrics/OfflineGeneration/Shaders/VoxelDirectVolumetricLight.compute";
        private static string voxelBounceSurfaceLightAssetPath =          "Assets/BakedVolumetrics/OfflineGeneration/Shaders/VoxelBounceSurfaceLight.compute";
        private static string voxelBounceVolumetricLightBufferAssetPath = "Assets/BakedVolumetrics/OfflineGeneration/Shaders/VoxelBounceVolumetricLight.compute";
        private static string voxelEnvironmentSurfaceLightAssetPath =     "Assets/BakedVolumetrics/OfflineGeneration/Shaders/VoxelEnvironmentSurfaceLight.compute";
        private static string voxelEnvironmentVolumetricLightAssetPath =  "Assets/BakedVolumetrics/OfflineGeneration/Shaders/VoxelEnvironmentVolumetricLight.compute";
        private static string combineBuffersAssetPath =                   "Assets/BakedVolumetrics/OfflineGeneration/Shaders/CombineBuffers.compute";
        private static string gaussianBlurAssetPath =                     "Assets/BakedVolumetrics/OfflineGeneration/Shaders/GaussianBlur3D.compute";
        private static string voxelizeSceneAssetPath =                    "Assets/BakedVolumetrics/OfflineGeneration/Shaders/VoxelizeScene.compute";
        private static string densityAssetPath =                          "Assets/BakedVolumetrics/OfflineGeneration/Shaders/Density.compute";
        private static string dilateAssetPath =                           "Assets/BakedVolumetrics/OfflineGeneration/Shaders/Dilation.compute";
        private static string dataPackingAssetPath =                      "Assets/BakedVolumetrics/OfflineGeneration/Shaders/DataPacking.compute";
        private static string adjustmentsAssetPath =                      "Assets/BakedVolumetrics/OfflineGeneration/Shaders/Adjustments.compute";
        private static string hdrpackingAssetPath =                       "Assets/BakedVolumetrics/OfflineGeneration/Shaders/HDRPacking.compute";

        private UnityEngine.SceneManagement.Scene activeScene => EditorSceneManager.GetActiveScene();
        public string localAssetSceneDataFolder => string.Format("{0}/{1}", localAssetDataFolder, activeScene.name);

        public ComputeShader voxelDirectSurfaceLight;
        public ComputeShader voxelDirectVolumetricLight;
        public ComputeShader voxelBounceSurfaceLight;
        public ComputeShader voxelBounceVolumetricLight;
        public ComputeShader voxelEnvironmentSurfaceLight;
        public ComputeShader voxelEnvironmentVolumetricLight;
        public ComputeShader combineBuffers;
        public ComputeShader gaussianBlur;
        public ComputeShader voxelizeScene;
        public ComputeShader density;
        public ComputeShader dilate;
        public ComputeShader dataPacking;
        public ComputeShader adjustments;
        public ComputeShader hdrpacking;

        public Shader metaPassObjectShaderV1;
        public Shader metaPassObjectShaderV2;
        public Shader sceneVolumetricFog;
        public Shader sceneVolumetricFogLPPV;

        private Material fogMaterial;
        private Material fogMaterialLPPV;

        public void GetResources()
        {
            voxelDirectSurfaceLight = AssetDatabase.LoadAssetAtPath<ComputeShader>(voxelDirectSurfaceLightAssetPath);
            voxelDirectVolumetricLight = AssetDatabase.LoadAssetAtPath<ComputeShader>(voxelDirectVolumetricLightAssetPath);
            voxelBounceSurfaceLight = AssetDatabase.LoadAssetAtPath<ComputeShader>(voxelBounceSurfaceLightAssetPath);
            voxelBounceVolumetricLight = AssetDatabase.LoadAssetAtPath<ComputeShader>(voxelBounceVolumetricLightBufferAssetPath);
            voxelEnvironmentSurfaceLight = AssetDatabase.LoadAssetAtPath<ComputeShader>(voxelEnvironmentSurfaceLightAssetPath);
            voxelEnvironmentVolumetricLight = AssetDatabase.LoadAssetAtPath<ComputeShader>(voxelEnvironmentVolumetricLightAssetPath);
            combineBuffers = AssetDatabase.LoadAssetAtPath<ComputeShader>(combineBuffersAssetPath);
            gaussianBlur = AssetDatabase.LoadAssetAtPath<ComputeShader>(gaussianBlurAssetPath);
            voxelizeScene = AssetDatabase.LoadAssetAtPath<ComputeShader>(voxelizeSceneAssetPath);
            density = AssetDatabase.LoadAssetAtPath<ComputeShader>(densityAssetPath);
            dilate = AssetDatabase.LoadAssetAtPath<ComputeShader>(dilateAssetPath);
            dataPacking = AssetDatabase.LoadAssetAtPath<ComputeShader>(dataPackingAssetPath);
            adjustments = AssetDatabase.LoadAssetAtPath<ComputeShader>(adjustmentsAssetPath);
            hdrpacking = AssetDatabase.LoadAssetAtPath<ComputeShader>(hdrpackingAssetPath);

            metaPassObjectShaderV1 = Shader.Find("BakedVolumetrics/MetaPassObjectShaderV1");
            metaPassObjectShaderV2 = Shader.Find("BakedVolumetrics/MetaPassObjectShaderV2");
            sceneVolumetricFog = Shader.Find("BakedVolumetrics/SceneVolumetricFog");
            sceneVolumetricFogLPPV = Shader.Find("BakedVolumetrics/SceneVolumetricFog_LPPV");
        }

        public bool HasResources()
        {
            if (voxelDirectSurfaceLight == null)
            {
                Debug.LogError(string.Format("{0} does not exist!", voxelDirectSurfaceLightAssetPath));
                return false;
            }
            else if (voxelDirectVolumetricLight == null)
            {
                Debug.LogError(string.Format("{0} does not exist!", voxelDirectVolumetricLightAssetPath));
                return false;
            }
            else if (voxelBounceSurfaceLight == null)
            {
                Debug.LogError(string.Format("{0} does not exist!", voxelBounceSurfaceLightAssetPath));
                return false;
            }
            else if (voxelBounceVolumetricLight == null)
            {
                Debug.LogError(string.Format("{0} does not exist!", voxelBounceVolumetricLightBufferAssetPath));
                return false;
            }
            else if (voxelEnvironmentSurfaceLight == null)
            {
                Debug.LogError(string.Format("{0} does not exist!", voxelEnvironmentSurfaceLightAssetPath));
                return false;
            }
            else if (voxelEnvironmentVolumetricLight == null)
            {
                Debug.LogError(string.Format("{0} does not exist!", voxelEnvironmentVolumetricLightAssetPath));
                return false;
            }
            else if (combineBuffers == null)
            {
                Debug.LogError(string.Format("{0} does not exist!", combineBuffersAssetPath));
                return false;
            }
            else if (gaussianBlur == null)
            {
                Debug.LogError(string.Format("{0} does not exist!", gaussianBlurAssetPath));
                return false;
            }
            else if (voxelizeScene == null)
            {
                Debug.LogError(string.Format("{0} does not exist!", voxelizeSceneAssetPath));
                return false;
            }
            else if (density == null)
            {
                Debug.LogError(string.Format("{0} does not exist!", densityAssetPath));
                return false;
            }
            else if (dilate == null)
            {
                Debug.LogError(string.Format("{0} does not exist!", dilateAssetPath));
                return false;
            }
            else if (dataPacking == null)
            {
                Debug.LogError(string.Format("{0} does not exist!", dataPackingAssetPath));
                return false;
            }
            else if (adjustments == null)
            {
                Debug.LogError(string.Format("{0} does not exist!", adjustmentsAssetPath));
                return false;
            }
            else if (hdrpacking == null)
            {
                Debug.LogError(string.Format("{0} does not exist!", hdrpackingAssetPath));
                return false;
            }
            else if (sceneVolumetricFog == null)
            {
                Debug.LogError("'SceneVolumetricFog' does not exist!");
                return false;
            }
            else if (sceneVolumetricFogLPPV == null)
            {
                Debug.LogError("'SceneVolumetricFog_LPPV' does not exist!");
                return false;
            }

            return true;
        }

        public bool PrepareAssetFolders()
        {
            //check if there is a data folder, if not then create one
            if (AssetDatabase.IsValidFolder(localAssetDataFolder) == false)
                AssetDatabase.CreateFolder(localAssetFolder, "Data");

            if (activeScene.IsValid() == false || string.IsNullOrEmpty(activeScene.path))
            {
                string message = "Scene is not valid! Be sure to save the scene before you setup volumetrics for it!";
                EditorUtility.DisplayDialog("Error", message, "OK");
                Debug.LogError(message);
                return false;
            }

            //check if there is a folder sharing the scene name, if there isn't then create one
            if (AssetDatabase.IsValidFolder(localAssetSceneDataFolder) == false)
                AssetDatabase.CreateFolder(localAssetDataFolder, activeScene.name);

            return true;
        }

        public Material GetVolumeMaterial(string volumeName)
        {
            bool prepareAssetFoldersResult = PrepareAssetFolders();

            if (prepareAssetFoldersResult == false)
                return null;

            string assetPath = string.Format("{0}/{1}.mat", localAssetSceneDataFolder, volumeName);

            //try loading one at the path
            fogMaterial = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

            //if there is no material, create one
            if (fogMaterial == null)
            {
                fogMaterial = new Material(sceneVolumetricFog);
                AssetDatabase.CreateAsset(fogMaterial, assetPath);
            }

            //setup noise
            fogMaterial.SetTexture("_JitterTexture", NoiseLibrary.GetBlueNoise());

            return fogMaterial;
        }

        public Material GetVolume_LPPV_Material(string volumeName)
        {
            bool prepareAssetFoldersResult = PrepareAssetFolders();

            if (prepareAssetFoldersResult == false)
                return null;

            string assetPath = string.Format("{0}/{1}_LPPV.mat", localAssetSceneDataFolder, volumeName);

            //try loading one at the path
            fogMaterialLPPV = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

            //if there is no material, create one
            if (fogMaterialLPPV == null)
            {
                fogMaterialLPPV = new Material(sceneVolumetricFogLPPV);
                AssetDatabase.CreateAsset(fogMaterialLPPV, assetPath);
            }

            //setup noise
            fogMaterialLPPV.SetTexture("_JitterTexture", NoiseLibrary.GetBlueNoise());

            return fogMaterialLPPV;
        }
    }
}
#endif
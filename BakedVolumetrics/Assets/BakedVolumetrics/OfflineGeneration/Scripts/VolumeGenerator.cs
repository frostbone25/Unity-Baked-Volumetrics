#if UNITY_EDITOR
using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;

namespace BakedVolumetrics
{
    public class VolumeGenerator : MonoBehaviour
    {
        //|||||||||||||||||||||||||||||||||||||||||| PUBLIC VARIABLES ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| PUBLIC VARIABLES ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| PUBLIC VARIABLES ||||||||||||||||||||||||||||||||||||||||||

        public string volumeName => gameObject.name;

        public LightingSource lightingSource = LightingSource.LightProbes;
        public RaymarchSamples raymarchSamples = RaymarchSamples._32;
        public VolumeBitDepth volumeBitDepth = VolumeBitDepth.RGBA16;
        public VoxelCalculation voxelCalculation = VoxelCalculation.Automatic;
        public Vector3Int customVolumeResolution = new Vector3Int(16, 16, 16);
        public float voxelDensitySize = 1.0f;

        public Vector3 volumeSize = new Vector3(10.0f, 10.0f, 10.0f);

        public bool previewBounds = true;
        public bool previewDensityHeight = true;
        public bool previewVoxels = false;

        public DensityType densityType = DensityType.Constant;
        public float densityConstant = 1.0f;
        public float densityTop = 0.0f;
        public float densityBottom = 1.0f;
        public float densityHeight = 0.0f;
        public float densityHeightFallof = 1.0f;
        public bool densityInvertLuminance = false;

        public SampleLightprobe sampleLightprobe;
        public SampleVoxelTracer sampleVoxelTracer;

        public float volumeLightProbeGroupDensityMultiplier = 1;

        //|||||||||||||||||||||||||||||||||||||||||| PRIVATE VARIABLES ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| PRIVATE VARIABLES ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| PRIVATE VARIABLES ||||||||||||||||||||||||||||||||||||||||||

        private UnityEngine.SceneManagement.Scene activeScene => EditorSceneManager.GetActiveScene();

        private string localAssetFolder = "Assets/BakedVolumetrics";
        private string localAssetDataFolder => localAssetFolder + "/Data";
        private string localAssetSceneDataFolder => localAssetDataFolder + "/" + activeScene.name;
        private string fogMaterialAssetPath => localAssetSceneDataFolder + "/" + string.Format("{0}.mat", volumeName);
        private string fogMaterialLPPVAssetPath => localAssetSceneDataFolder + "/" + string.Format("{0}_LPPV.mat", volumeName);

        private GameObject fogSceneObject;
        private MeshRenderer fogMeshRenderer;
        private Material fogMaterial;
        private Material fogMaterialLPPV;
        private Vector3Int volumeResolution;
        private LightProbeGroup volumeLightProbeGroup;
        private LightProbeProxyVolume lightProbeProxyVolume;

        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| GETTERS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| GETTERS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| GETTERS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||

        public int GetTotalVoxelCount() => volumeResolution.x * volumeResolution.y * volumeResolution.z;

        public Vector3Int GetVoxelResolution() => volumeResolution;

        public long GetVolumeSpaceUsage()
        {
            Texture3D volumeAsset = GetVolumeTexture();

            if (volumeAsset == null)
                return 0;
            else
                return Profiler.GetRuntimeMemorySizeLong(volumeAsset);
        }

        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| SETUP ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| SETUP ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| SETUP ||||||||||||||||||||||||||||||||||||||||||||||||||||||||

        public void Setup()
        {
            sampleLightprobe = gameObject.GetComponent<SampleLightprobe>();
            sampleVoxelTracer = gameObject.GetComponent<SampleVoxelTracer>();

            if (sampleLightprobe == null)
                sampleLightprobe = gameObject.AddComponent<SampleLightprobe>();

            if (sampleVoxelTracer == null)
                sampleVoxelTracer = gameObject.AddComponent<SampleVoxelTracer>();

            CalculateResolution();
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

        public Material GetVolumeMaterial()
        {
            bool prepareAssetFoldersResult = PrepareAssetFolders();

            if (prepareAssetFoldersResult == false)
                return null;

            //try loading one at the path
            fogMaterial = AssetDatabase.LoadAssetAtPath<Material>(fogMaterialAssetPath);

            //if there is no material, create one
            if (fogMaterial == null)
            {
                fogMaterial = new Material(Shader.Find("SceneVolumetricFog"));
                AssetDatabase.CreateAsset(fogMaterial, fogMaterialAssetPath);
            }

            //setup noise
            fogMaterial.SetTexture("_JitterTexture", NoiseLibrary.GetBlueNoise());

            return fogMaterial;
        }

        public Material GetVolume_LPPV_Material()
        {
            bool prepareAssetFoldersResult = PrepareAssetFolders();

            if (prepareAssetFoldersResult == false)
                return null;

            //try loading one at the path
            fogMaterialLPPV = AssetDatabase.LoadAssetAtPath<Material>(fogMaterialLPPVAssetPath);

            //if there is no material, create one
            if (fogMaterialLPPV == null)
            {
                fogMaterialLPPV = new Material(Shader.Find("SceneVolumetricFog_LPPV"));
                AssetDatabase.CreateAsset(fogMaterialLPPV, fogMaterialLPPVAssetPath);
            }

            //setup noise
            fogMaterialLPPV.SetTexture("_JitterTexture", NoiseLibrary.GetBlueNoise());

            return fogMaterialLPPV;
        }

        public Texture3D GetVolumeTexture()
        {
            if(lightingSource == LightingSource.LightProbes)
            {
                return sampleLightprobe.GetGeneratedVolume();
            }
            else if (lightingSource == LightingSource.VoxelTracer)
            {
                return sampleVoxelTracer.GetGeneratedVolume();
            }
            else
            {
                return null;
            }
        }

        public void SetupSceneObjectVolume()
        {
            fogMaterial = GetVolumeMaterial();
            fogMaterialLPPV = GetVolume_LPPV_Material();

            //if there is no scene object, try finding one
            if (fogSceneObject == null)
            {
                if (transform.childCount > 0)
                    fogSceneObject = transform.GetChild(0).gameObject;
            }

            //if you cant find one, then create a new one
            if (fogSceneObject == null)
            {
                fogSceneObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                fogSceneObject.name = "BakedVolumetricsMesh";
                fogSceneObject.transform.SetParent(transform);
                fogSceneObject.transform.localPosition = Vector3.zero;
                fogSceneObject.transform.localScale = Vector3.one * volumeSize.magnitude * 1.5f;

                //destroy the sphere collider that is generated automatically
                if (fogSceneObject.GetComponent<SphereCollider>() != null)
                    DestroyImmediate(fogSceneObject.GetComponent<SphereCollider>());

                fogMeshRenderer = fogSceneObject.GetComponent<MeshRenderer>();
                fogMeshRenderer.sharedMaterial = fogMaterial;
                fogMeshRenderer.receiveShadows = false;
                fogMeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                fogMeshRenderer.lightProbeUsage = LightProbeUsage.Off;
                fogMeshRenderer.allowOcclusionWhenDynamic = false;
            }
        }

        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| MAIN ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| MAIN ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| MAIN ||||||||||||||||||||||||||||||||||||||||||||||||||||||||

        public void GenerateVolume()
        {
            if (lightingSource == LightingSource.LightProbeProxyVolume)
                return;

            if(lightingSource == LightingSource.LightProbes)
                sampleLightprobe.GenerateVolume(volumeResolution, volumeSize);

            UpdateMaterial();
        }

        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| MATERIAL ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| MATERIAL ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| MATERIAL ||||||||||||||||||||||||||||||||||||||||||||||||||||||||

        public void UpdateMaterial()
        {
            if(lightingSource == LightingSource.LightProbeProxyVolume)
            {
                fogMaterialLPPV = GetVolumeMaterial();

                if(fogMaterialLPPV == null)
                {
                    return;
                }

                UpdateMaterialKeywords();

                fogMaterialLPPV.SetTexture("_JitterTexture", NoiseLibrary.GetBlueNoise());

                fogSceneObject.transform.localScale = Vector3.one * volumeSize.magnitude * 1.5f;
            }   
            else
            {
                //if there is no scene object, try finding one
                if (fogSceneObject == null)
                {
                    if (transform.childCount > 0)
                        fogSceneObject = transform.GetChild(0).gameObject;
                }

                if (fogMeshRenderer == null)
                {
                    fogMeshRenderer = fogSceneObject.GetComponent<MeshRenderer>();
                }

                fogMaterial = GetVolumeMaterial();

                if (fogMaterial == null)
                {
                    return;
                }

                fogMeshRenderer.lightProbeProxyVolumeOverride = null;
                fogMeshRenderer.lightProbeUsage = LightProbeUsage.Off;
                fogMeshRenderer.material = fogMaterial;

                fogMaterial.SetVector("_VolumePos", new Vector4(transform.position.x, transform.position.y, transform.position.z, 0.0f));
                fogMaterial.SetVector("_VolumeSize", new Vector4(volumeSize.x, volumeSize.y, volumeSize.z, 0.0f));
                UpdateMaterialKeywords();

                fogMaterial.SetTexture("_VolumeTexture", GetVolumeTexture());
                fogMaterial.SetTexture("_JitterTexture", NoiseLibrary.GetBlueNoise());

                fogSceneObject.transform.localScale = Vector3.one * volumeSize.magnitude * 1.5f;
            }
        }

        public void UpdateMaterialKeywords()
        {
            Material material = lightingSource == LightingSource.LightProbeProxyVolume ? fogMaterialLPPV : fogMaterial;

            if (material == null)
                return;

            switch (raymarchSamples)
            {
                case RaymarchSamples._8:
                    material.EnableKeyword("SAMPLES_8");
                    material.DisableKeyword("SAMPLES_16");
                    material.DisableKeyword("SAMPLES_24");
                    material.DisableKeyword("SAMPLES_32");
                    material.DisableKeyword("SAMPLES_48");
                    material.DisableKeyword("SAMPLES_64");
                    material.DisableKeyword("SAMPLES_128");
                    break;
                case RaymarchSamples._16:
                    material.DisableKeyword("SAMPLES_8");
                    material.EnableKeyword("SAMPLES_16");
                    material.DisableKeyword("SAMPLES_24");
                    material.DisableKeyword("SAMPLES_32");
                    material.DisableKeyword("SAMPLES_48");
                    material.DisableKeyword("SAMPLES_64");
                    material.DisableKeyword("SAMPLES_128");
                    break;
                case RaymarchSamples._24:
                    material.DisableKeyword("SAMPLES_8");
                    material.DisableKeyword("SAMPLES_16");
                    material.EnableKeyword("SAMPLES_24");
                    material.DisableKeyword("SAMPLES_32");
                    material.DisableKeyword("SAMPLES_48");
                    material.DisableKeyword("SAMPLES_64");
                    material.DisableKeyword("SAMPLES_128");
                    break;
                case RaymarchSamples._32:
                    material.DisableKeyword("SAMPLES_8");
                    material.DisableKeyword("SAMPLES_16");
                    material.DisableKeyword("SAMPLES_24");
                    material.EnableKeyword("SAMPLES_32");
                    material.DisableKeyword("SAMPLES_48");
                    material.DisableKeyword("SAMPLES_64");
                    material.DisableKeyword("SAMPLES_128");
                    break;
                case RaymarchSamples._48:
                    material.DisableKeyword("SAMPLES_8");
                    material.DisableKeyword("SAMPLES_16");
                    material.DisableKeyword("SAMPLES_24");
                    material.DisableKeyword("SAMPLES_32");
                    material.EnableKeyword("SAMPLES_48");
                    material.DisableKeyword("SAMPLES_64");
                    material.DisableKeyword("SAMPLES_128");
                    break;
                case RaymarchSamples._64:
                    material.DisableKeyword("SAMPLES_8");
                    material.DisableKeyword("SAMPLES_16");
                    material.DisableKeyword("SAMPLES_24");
                    material.DisableKeyword("SAMPLES_32");
                    material.DisableKeyword("SAMPLES_48");
                    material.EnableKeyword("SAMPLES_64");
                    material.DisableKeyword("SAMPLES_128");
                    break;
                case RaymarchSamples._128:
                    material.DisableKeyword("SAMPLES_8");
                    material.DisableKeyword("SAMPLES_16");
                    material.DisableKeyword("SAMPLES_24");
                    material.DisableKeyword("SAMPLES_32");
                    material.DisableKeyword("SAMPLES_48");
                    material.DisableKeyword("SAMPLES_64");
                    material.EnableKeyword("SAMPLES_128");
                    break;
            }
        }

        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| UTILITIES ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| UTILITIES ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| UTILITIES ||||||||||||||||||||||||||||||||||||||||||||||||||||||||

        private void CalculateResolution()
        {
            if (voxelCalculation == VoxelCalculation.Custom)
                volumeResolution = customVolumeResolution;
            else if (voxelCalculation == VoxelCalculation.Automatic)
                volumeResolution = new Vector3Int((int)(volumeSize.x / voxelDensitySize), (int)(volumeSize.y / voxelDensitySize), (int)(volumeSize.z / voxelDensitySize));
        }

        public TextureFormat GetTextureFormat()
        {
            switch (volumeBitDepth)
            {
                case VolumeBitDepth.RGB8:
                    return TextureFormat.RGB24;
                case VolumeBitDepth.RGBA8:
                    return TextureFormat.RGBA32;
                case VolumeBitDepth.RGBA16:
                    return TextureFormat.RGBAHalf;
                case VolumeBitDepth.RGBA32:
                    return TextureFormat.RGBAFloat;
                default:
                    return TextureFormat.RGBAHalf;
            }
        }

        public RenderTextureFormat GetRenderTextureFormat()
        {
            switch (volumeBitDepth)
            {
                case VolumeBitDepth.RGB8:
                    return RenderTextureFormat.ARGB32;
                case VolumeBitDepth.RGBA8:
                    return RenderTextureFormat.ARGB32;
                case VolumeBitDepth.RGBA16:
                    return RenderTextureFormat.ARGBHalf;
                case VolumeBitDepth.RGBA32:
                    return RenderTextureFormat.ARGBFloat;
                default:
                    return RenderTextureFormat.ARGBHalf;
            }
        }

        public void GenerateLPPV()
        {
            lightProbeProxyVolume = transform.GetComponentInChildren<LightProbeProxyVolume>();

            if (lightProbeProxyVolume == null)
            {
                GameObject newGameObject = new GameObject("Light Probe Proxy Volume");

                newGameObject.transform.SetParent(transform);
                newGameObject.transform.localPosition = Vector3.zero;

                lightProbeProxyVolume = newGameObject.AddComponent<LightProbeProxyVolume>();
            }

            //if there is no scene object, try finding one
            if (fogSceneObject == null)
            {
                if (transform.childCount > 0)
                    fogSceneObject = transform.GetChild(0).gameObject;
            }

            if (fogMeshRenderer == null)
            {
                fogMeshRenderer = fogSceneObject.GetComponent<MeshRenderer>();
            }

            fogMaterialLPPV = GetVolume_LPPV_Material();

            fogMeshRenderer.lightProbeProxyVolumeOverride = lightProbeProxyVolume.gameObject;
            fogMeshRenderer.lightProbeUsage = LightProbeUsage.UseProxyVolume;
            fogMeshRenderer.material = fogMaterialLPPV;

            lightProbeProxyVolume.transform.localScale = volumeSize;
            lightProbeProxyVolume.boundingBoxMode = LightProbeProxyVolume.BoundingBoxMode.AutomaticLocal;
            lightProbeProxyVolume.probePositionMode = LightProbeProxyVolume.ProbePositionMode.CellCorner;
            lightProbeProxyVolume.refreshMode = LightProbeProxyVolume.RefreshMode.EveryFrame;
            lightProbeProxyVolume.qualityMode = LightProbeProxyVolume.QualityMode.Normal;
            lightProbeProxyVolume.dataFormat = LightProbeProxyVolume.DataFormat.Float;
        }

        public void GenerateLightProbeGroup()
        {
            volumeLightProbeGroup = transform.GetComponentInChildren<LightProbeGroup>();

            if (volumeLightProbeGroup == null)
            {
                GameObject newGameObject = new GameObject("Volume Light Probe Group");

                newGameObject.transform.SetParent(transform);
                newGameObject.transform.localPosition = Vector3.zero;

                volumeLightProbeGroup = newGameObject.AddComponent<LightProbeGroup>();
            }

            List<Vector3> probePositions = new List<Vector3>();

            Vector3Int modifiedVolumeResolution = volumeResolution;
            modifiedVolumeResolution.x = (int)((float)modifiedVolumeResolution.x * volumeLightProbeGroupDensityMultiplier);
            modifiedVolumeResolution.y = (int)((float)modifiedVolumeResolution.y * volumeLightProbeGroupDensityMultiplier);
            modifiedVolumeResolution.z = (int)((float)modifiedVolumeResolution.z * volumeLightProbeGroupDensityMultiplier);

            //3d loop for our volume
            for (int x = -modifiedVolumeResolution.x / 2; x <= modifiedVolumeResolution.x / 2; x++)
            {
                //get the x offset
                float x_offset = volumeSize.x / modifiedVolumeResolution.x;

                for (int y = -modifiedVolumeResolution.y / 2; y <= modifiedVolumeResolution.y / 2; y++)
                {
                    //get the y offset
                    float y_offset = volumeSize.y / modifiedVolumeResolution.y;

                    for (int z = -modifiedVolumeResolution.z / 2; z <= modifiedVolumeResolution.z / 2; z++)
                    {
                        //get the z offset
                        float z_offset = volumeSize.z / modifiedVolumeResolution.z;

                        //Vector3 probePosition = new Vector3(transform.position.x + (x * x_offset), transform.position.y + (y * y_offset), transform.position.z + (z * z_offset));
                        Vector3 probePosition = new Vector3(x * x_offset, y * y_offset, z * z_offset);

                        probePositions.Add(probePosition);
                    }
                }
            }

            volumeLightProbeGroup.probePositions = probePositions.ToArray();
        }

        public bool CheckForLightProbes()
        {
            LightProbeGroup[] existingGroups = FindObjectsOfType<LightProbeGroup>();

            return existingGroups == null || existingGroups.Length < 1;
        }

        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| GIZMOS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| GIZMOS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| GIZMOS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||

        private void OnDrawGizmos()
        {
            CalculateResolution();

            //bounds
            if(!previewVoxels && previewBounds)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(transform.position, volumeSize);
            }

            //height preview
            if(previewDensityHeight && (densityType == DensityType.HeightBased || densityType == DensityType.HeightBasedLuminance))
            {
                Gizmos.color = Color.cyan;

                Vector3 heightPosition = new Vector3(transform.position.x, densityHeight + (densityHeightFallof * 0.5f), transform.position.z);
                Vector3 heightFallof = new Vector3(volumeSize.x, densityHeightFallof, volumeSize.z);

                Gizmos.DrawWireCube(heightPosition, heightFallof);
            }

            //voxel preview (note to self: this is really damn slow)
            if(previewVoxels)
            {
                Gizmos.color = Color.white;

                //3d loop for our volume
                for (int x = -volumeResolution.x / 2; x <= volumeResolution.x / 2; x++)
                {
                    //get the x offset
                    float x_offset = volumeSize.x / volumeResolution.x;

                    for (int y = -volumeResolution.y / 2; y <= volumeResolution.y / 2; y++)
                    {
                        //get the y offset
                        float y_offset = volumeSize.y / volumeResolution.y;

                        for (int z = -volumeResolution.z / 2; z <= volumeResolution.z / 2; z++)
                        {
                            //get the z offset
                            float z_offset = volumeSize.z / volumeResolution.z;

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
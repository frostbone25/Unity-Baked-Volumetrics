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

namespace BakedVolumetrics
{
    public class VolumeGenerator : MonoBehaviour
    {
        //public
        public string volumeName = "Volume";

        public LightingSource lightingSource = LightingSource.LightProbes;
        public CombineColorType combineColorType = CombineColorType.Additive;
        public float additiveLightprobeIntensity = 1.0f;
        public float additiveRaytracedIntensity = 1.0f;
        public float lerpFactor = 0.5f;

        public RaymarchSamples raymarchSamples = RaymarchSamples._32;
        public VolumeBitDepth volumeBitDepth = VolumeBitDepth.RGBA16;
        public VoxelCalculation voxelCalculation = VoxelCalculation.Automatic;
        public Vector3Int customVolumeResolution = new Vector3Int(16, 16, 16);
        public float voxelDensitySize = 1.0f;

        public Vector3 volumeSize = new Vector3(10.0f, 10.0f, 10.0f);

        public bool previewBounds = true;
        public bool previewDensityHeight = true;
        public bool previewVoxels = false;

        public int jitterResolution = 64;

        public DensityType densityType = DensityType.Constant;
        public float densityConstant = 1.0f;
        public float densityTop = 0.0f;
        public float densityBottom = 1.0f;
        public float densityHeight = 0.0f;
        public float densityHeightFallof = 1.0f;

        public SampleLightprobe sampleLightprobe;
        public SampleVoxelRaytrace sampleVoxelRaytrace;
        public SampleCPURaytrace sampleCPURaytrace;
        public VolumePostFilters volumePostFilters;

        //private
        private GameObject fogSceneObject;
        private MeshRenderer fogMeshRenderer;
        private Material fogMaterial;
        private Vector3Int volumeResolution;
        private Texture3D volumeTexture;

        private static string sceneStaticCollidersName = "TEMP_SceneStaticColliders";
        private GameObject sceneStaticColliders;

        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| GETTERS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| GETTERS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| GETTERS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||

        public int GetTotalVoxelCount() => volumeResolution.x * volumeResolution.y * volumeResolution.z;

        public Vector3Int GetVoxelResolution() => volumeResolution;

        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| SETUP ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| SETUP ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| SETUP ||||||||||||||||||||||||||||||||||||||||||||||||||||||||

        /// <summary>
        /// Spawns mesh colliders for all objects in the scene marked with the 'ContributeGI' static flag.
        /// <para>This is used in general for features (if they are enabled) like preventing light leaks, doing occlusion, or getting indoor only samples.</para>
        /// </summary>
        public void SetupSceneColliders()
        {
            sceneStaticColliders = new GameObject(sceneStaticCollidersName);

            MeshFilter[] meshes = FindObjectsOfType<MeshFilter>();

            for (int i = 0; i < meshes.Length; i++)
            {
                GameObject meshGameObject = meshes[i].gameObject;

                StaticEditorFlags staticEditorFlags = GameObjectUtility.GetStaticEditorFlags(meshGameObject);

                if (staticEditorFlags.HasFlag(StaticEditorFlags.ContributeGI))
                {
                    GameObject sceneColliderChild = new GameObject("collider");
                    sceneColliderChild.transform.SetParent(sceneStaticColliders.transform);

                    sceneColliderChild.transform.position = meshGameObject.transform.position;
                    sceneColliderChild.transform.rotation = meshGameObject.transform.rotation;
                    sceneColliderChild.transform.localScale = meshGameObject.transform.localScale;

                    MeshCollider meshCollider = sceneColliderChild.AddComponent<MeshCollider>();
                    meshCollider.sharedMesh = meshes[i].sharedMesh;
                }
            }
        }

        public void RemoveSceneColliders()
        {
            if (sceneStaticColliders != null)
                DestroyImmediate(sceneStaticColliders);
            else
            {
                sceneStaticColliders = GameObject.Find(sceneStaticCollidersName);

                if (sceneStaticColliders != null)
                    DestroyImmediate(sceneStaticColliders);
            }
        }

        public void Setup()
        {
            sampleCPURaytrace = gameObject.GetComponent<SampleCPURaytrace>();
            sampleLightprobe = gameObject.GetComponent<SampleLightprobe>();
            volumePostFilters = gameObject.GetComponent<VolumePostFilters>();
            sampleVoxelRaytrace = gameObject.GetComponent<SampleVoxelRaytrace>();

            if (sampleLightprobe == null)
                sampleLightprobe = gameObject.AddComponent<SampleLightprobe>();

            if (sampleVoxelRaytrace == null)
                sampleVoxelRaytrace = gameObject.AddComponent<SampleVoxelRaytrace>();

            if (sampleCPURaytrace == null)
                sampleCPURaytrace = gameObject.AddComponent<SampleCPURaytrace>();

            if (volumePostFilters == null)
                volumePostFilters = gameObject.AddComponent<VolumePostFilters>();

            CalculateResolution();
        }

        public void SetupSceneObjectVolume()
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

            //construct volume material path
            string volumeMaterialName = volumeName + ".mat";
            string volumeMaterialPath = sceneVolumetricsFolder + "/" + volumeMaterialName;

            //try loading one at the path
            fogMaterial = AssetDatabase.LoadAssetAtPath<Material>(volumeMaterialPath);

            //if there is no material, create one
            if (fogMaterial == null)
            {
                fogMaterial = new Material(Shader.Find("SceneVolumetricFog"));
                AssetDatabase.CreateAsset(fogMaterial, volumeMaterialPath);
            }

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

        private static Vector3 GetLuminance()
        {
            if (PlayerSettings.colorSpace == ColorSpace.Gamma)
                return new Vector3(0.22f, 0.707f, 0.071f);
            else if (PlayerSettings.colorSpace == ColorSpace.Linear)
                return new Vector3(0.0396819152f, 0.45802179f, 0.00609653955f);
            else
                return Vector3.one;
        }

        private void SampleVolumetricColor(int x, int y, int z, float x_offset, float y_offset, float z_offset)
        {
            Vector3 probePosition = new Vector3(transform.position.x + (x * x_offset), transform.position.y + (y * y_offset), transform.position.z + (z * z_offset));
            Vector3 voxelWorldSize = new Vector3(x_offset, y_offset, z_offset);

            //|||||||||||||||||||| COLOR (RGB) ||||||||||||||||||||||||
            //|||||||||||||||||||| COLOR (RGB) ||||||||||||||||||||||||
            //|||||||||||||||||||| COLOR (RGB) ||||||||||||||||||||||||
            Color colorResult = Color.black;

            if (lightingSource == LightingSource.CPU_Raytrace)
                colorResult = sampleCPURaytrace.SampleVolumetricColor(probePosition, voxelWorldSize);
            else if (lightingSource == LightingSource.LightProbes)
                colorResult = sampleLightprobe.SampleVolumetricColor(probePosition, voxelWorldSize);
            /*
            if (lightingSource == LightingSource.Combined)
            {
                Color raytraceColor = sampleRaytrace.SampleVolumetricColor(probePosition, voxelWorldSize);
                Color lightprobeColor = sampleLightprobe.SampleVolumetricColor(probePosition, voxelWorldSize);

                if (combineColorType == CombineColorType.Lerp)
                    colorResult = Color.Lerp(lightprobeColor, raytraceColor, lerpFactor);
                else if (combineColorType == CombineColorType.Additive)
                    colorResult = (raytraceColor * additiveRaytracedIntensity) + (lightprobeColor * additiveLightprobeIntensity);
            }
            */

            //|||||||||||||||||||| DENSITY (A) ||||||||||||||||||||||||
            //|||||||||||||||||||| DENSITY (A) ||||||||||||||||||||||||
            //|||||||||||||||||||| DENSITY (A) ||||||||||||||||||||||||
            float alphaResult = 1.0f;

            if (densityType == DensityType.Constant)
            {
                alphaResult = densityConstant;
            }
            else if(densityType == DensityType.Luminance)
            {
                Vector3 luminance = GetLuminance();
                Vector3 colorAsVector = new Vector3(colorResult.r, colorResult.g, colorResult.b);

                alphaResult = Vector3.Dot(colorAsVector, luminance);
            }
            else if(densityType == DensityType.HeightBased)
            {
                float lerpFactor = Mathf.Clamp((probePosition.y - densityHeight) / densityHeightFallof, 0.0f, 1.0f);
                alphaResult = Mathf.Lerp(densityBottom, densityTop, lerpFactor);
            }
            else if (densityType == DensityType.HeightBasedLuminance)
            {
                Vector3 luminance = GetLuminance();
                Vector3 colorAsVector = new Vector3(colorResult.r, colorResult.g, colorResult.b);

                float lumaResult = Vector3.Dot(colorAsVector, luminance);
                float lerpFactor = Mathf.Clamp((probePosition.y - densityHeight) / densityHeightFallof, 0.0f, 1.0f);
                alphaResult = Mathf.Lerp(lumaResult * densityBottom, lumaResult * densityTop, lerpFactor);
            }

            colorResult = new Color(colorResult.r, colorResult.g, colorResult.b, alphaResult);

            //|||||||||||||||||||| FINAL ||||||||||||||||||||||||
            //|||||||||||||||||||| FINAL ||||||||||||||||||||||||
            //|||||||||||||||||||| FINAL ||||||||||||||||||||||||
            Vector3Int volumeTexturePosition = new Vector3Int(x + (volumeResolution.x / 2), y + (volumeResolution.y / 2), z + (volumeResolution.z / 2));
            volumeTexture.SetPixel(volumeTexturePosition.x, volumeTexturePosition.y, volumeTexturePosition.z, colorResult);
        }

        public void GenerateVolume()
        {
            UpdateProgressBar("Setting up and creating 3d texture...", 0.25f);

            if (volumeTexture != null)
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(volumeTexture));

            volumeTexture = null;

            volumeTexture = new Texture3D(volumeResolution.x, volumeResolution.y, volumeResolution.z, GetTextureFormat(), false);
            volumeTexture.wrapMode = TextureWrapMode.Clamp;
            volumeTexture.name = name;

            Setup();
            SetupSceneColliders();
            CalculateResolution();

            UpdateProgressBar("Sampling colors into 3d texture...", 0.5f);

            for (int x = -volumeResolution.x / 2; x <= volumeResolution.x / 2; x++)
            {
                float x_offset = volumeSize.x / volumeResolution.x;

                for (int y = -volumeResolution.y / 2; y <= volumeResolution.y / 2; y++)
                {
                    float y_offset = volumeSize.y / volumeResolution.y;

                    for (int z = -volumeResolution.z / 2; z <= volumeResolution.z / 2; z++)
                    {
                        float z_offset = volumeSize.z / volumeResolution.z;

                        SampleVolumetricColor(x, y, z, x_offset, y_offset, z_offset);
                    }
                }
            }

            volumeTexture.Apply();

            SaveVolumeTexture(volumeTexture);
            RemoveSceneColliders();
            UpdateMaterial();
        }

        private void SaveVolumeTexture(Texture3D tex3D)
        {
            UpdateProgressBar("Saving generated 3d texture...", 0.75f);
            SetupSceneObjectVolume();

            //build the paths
            UnityEngine.SceneManagement.Scene activeScene = EditorSceneManager.GetActiveScene();
            string sceneName = activeScene.name;
            string sceneVolumetricsFolder = "Assets/BakedVolumetrics/Data/" + sceneName;
            string volumeAssetName = volumeName + ".asset";
            string volumeAssetPath = sceneVolumetricsFolder + "/" + volumeAssetName;

            AssetDatabase.DeleteAsset(volumeAssetPath);
            AssetDatabase.CreateAsset(tex3D, volumeAssetPath);

            UpdateProgressBar("Applying post effects to 3d texture...", 0.9f);
            volumePostFilters.ApplyPostEffects(volumeAssetPath, GetTextureFormat(), GetRenderTextureFormat());

            CloseProgressBar();
        }

        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| MATERIAL ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| MATERIAL ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| MATERIAL ||||||||||||||||||||||||||||||||||||||||||||||||||||||||

        public void UpdateMaterial()
        {
            if (fogMaterial == null)
                return;

            fogMaterial.SetVector("_VolumePos", new Vector4(transform.position.x, transform.position.y, transform.position.z, 0.0f));
            fogMaterial.SetVector("_VolumeSize", new Vector4(volumeSize.x, volumeSize.y, volumeSize.z, 0.0f));
            UpdateMaterialKeywords();

            //build the paths
            UnityEngine.SceneManagement.Scene activeScene = EditorSceneManager.GetActiveScene();
            string sceneName = activeScene.name;
            string sceneVolumetricsFolder = "Assets/BakedVolumetrics/Data/" + sceneName;
            string volumeAssetName = volumeName + ".asset";
            string volumeAssetPath = sceneVolumetricsFolder + "/" + volumeAssetName;
            fogMaterial.SetTexture("_VolumeTexture", AssetDatabase.LoadAssetAtPath<Texture3D>(volumeAssetPath));

            fogMaterial.SetTexture("_JitterTexture", GetJitter());

            fogSceneObject.transform.localScale = Vector3.one * volumeSize.magnitude * 1.5f;
        }

        public void UpdateMaterialKeywords()
        {
            if(fogMaterial == null)
            {
                UnityEngine.SceneManagement.Scene activeScene = EditorSceneManager.GetActiveScene();
                string sceneName = activeScene.name;
                string sceneVolumetricsFolder = "Assets/BakedVolumetrics/Data/" + sceneName;
                string volumeMaterialName = volumeName + ".mat";
                string volumeMaterialPath = sceneVolumetricsFolder + "/" + volumeMaterialName;

                //try loading one at the path
                fogMaterial = AssetDatabase.LoadAssetAtPath<Material>(volumeMaterialPath);
            }

            switch (raymarchSamples)
            {
                case RaymarchSamples._8:
                    fogMaterial.EnableKeyword("SAMPLES_8");
                    fogMaterial.DisableKeyword("SAMPLES_16");
                    fogMaterial.DisableKeyword("SAMPLES_24");
                    fogMaterial.DisableKeyword("SAMPLES_32");
                    fogMaterial.DisableKeyword("SAMPLES_48");
                    fogMaterial.DisableKeyword("SAMPLES_64");
                    fogMaterial.DisableKeyword("SAMPLES_128");
                    break;
                case RaymarchSamples._16:
                    fogMaterial.DisableKeyword("SAMPLES_8");
                    fogMaterial.EnableKeyword("SAMPLES_16");
                    fogMaterial.DisableKeyword("SAMPLES_24");
                    fogMaterial.DisableKeyword("SAMPLES_32");
                    fogMaterial.DisableKeyword("SAMPLES_48");
                    fogMaterial.DisableKeyword("SAMPLES_64");
                    fogMaterial.DisableKeyword("SAMPLES_128");
                    break;
                case RaymarchSamples._24:
                    fogMaterial.DisableKeyword("SAMPLES_8");
                    fogMaterial.DisableKeyword("SAMPLES_16");
                    fogMaterial.EnableKeyword("SAMPLES_24");
                    fogMaterial.DisableKeyword("SAMPLES_32");
                    fogMaterial.DisableKeyword("SAMPLES_48");
                    fogMaterial.DisableKeyword("SAMPLES_64");
                    fogMaterial.DisableKeyword("SAMPLES_128");
                    break;
                case RaymarchSamples._32:
                    fogMaterial.DisableKeyword("SAMPLES_8");
                    fogMaterial.DisableKeyword("SAMPLES_16");
                    fogMaterial.DisableKeyword("SAMPLES_24");
                    fogMaterial.EnableKeyword("SAMPLES_32");
                    fogMaterial.DisableKeyword("SAMPLES_48");
                    fogMaterial.DisableKeyword("SAMPLES_64");
                    fogMaterial.DisableKeyword("SAMPLES_128");
                    break;
                case RaymarchSamples._48:
                    fogMaterial.DisableKeyword("SAMPLES_8");
                    fogMaterial.DisableKeyword("SAMPLES_16");
                    fogMaterial.DisableKeyword("SAMPLES_24");
                    fogMaterial.DisableKeyword("SAMPLES_32");
                    fogMaterial.EnableKeyword("SAMPLES_48");
                    fogMaterial.DisableKeyword("SAMPLES_64");
                    fogMaterial.DisableKeyword("SAMPLES_128");
                    break;
                case RaymarchSamples._64:
                    fogMaterial.DisableKeyword("SAMPLES_8");
                    fogMaterial.DisableKeyword("SAMPLES_16");
                    fogMaterial.DisableKeyword("SAMPLES_24");
                    fogMaterial.DisableKeyword("SAMPLES_32");
                    fogMaterial.DisableKeyword("SAMPLES_48");
                    fogMaterial.EnableKeyword("SAMPLES_64");
                    fogMaterial.DisableKeyword("SAMPLES_128");
                    break;
                case RaymarchSamples._128:
                    fogMaterial.DisableKeyword("SAMPLES_8");
                    fogMaterial.DisableKeyword("SAMPLES_16");
                    fogMaterial.DisableKeyword("SAMPLES_24");
                    fogMaterial.DisableKeyword("SAMPLES_32");
                    fogMaterial.DisableKeyword("SAMPLES_48");
                    fogMaterial.DisableKeyword("SAMPLES_64");
                    fogMaterial.EnableKeyword("SAMPLES_128");
                    break;
            }
        }

        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| UTILITIES ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| UTILITIES ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| UTILITIES ||||||||||||||||||||||||||||||||||||||||||||||||||||||||

        public void UpdateProgressBar(string description, float progress)
        {
            EditorUtility.DisplayProgressBar("Volume Generator", description, progress);
        }

        public void CloseProgressBar()
        {
            EditorUtility.ClearProgressBar();
        }

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

        private Texture2D GetJitter()
        {
            if (AssetDatabase.IsValidFolder("Assets/BakedVolumetrics/Data") == false)
                AssetDatabase.CreateFolder("Assets/BakedVolumetrics", "Data");

            string sharedVolumetricsFolder = "Assets/BakedVolumetrics/Data/Shared";

            if (AssetDatabase.IsValidFolder(sharedVolumetricsFolder) == false)
                AssetDatabase.CreateFolder("Assets/BakedVolumetrics/Data", "Shared");

            string jitterAssetName = "Jitter.asset";
            string jitterAssetPath = sharedVolumetricsFolder + "/" + jitterAssetName;

            Texture2D jitter = AssetDatabase.LoadAssetAtPath<Texture2D>(jitterAssetPath);

            if (jitter == null)
            {
                jitter = new Texture2D(jitterResolution, jitterResolution, TextureFormat.RGBA4444, false);
                jitter.filterMode = FilterMode.Bilinear;
                jitter.anisoLevel = 0;

                for (int x = 0; x < jitterResolution; x++)
                {
                    for (int y = 0; y < jitterResolution; y++)
                    {
                        Color randomColor = new Color(UnityEngine.Random.Range(0.0f, 1.0f), 0.0f, 0.0f, 1.0f);
                        jitter.SetPixel(x, y, randomColor);
                    }
                }

                AssetDatabase.CreateAsset(jitter, jitterAssetPath);
            }

            return jitter;
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
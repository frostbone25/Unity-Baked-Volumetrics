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
using System.Diagnostics.Contracts;

namespace BakedVolumetricsOffline
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
        public bool seperateDensityTexture = false;
        public bool useDensityTextureForLPPV = false;

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
        public float densityLuminanceMultiplier = 1.0f;
        public bool densityInvertLuminance = false;

        public bool postAdjustments;
        public float brightness = 1.0f;
        public float contrast = 0.0f;
        public float saturation = 0.0f;
        public float vibrance = 0.0f;
        public float hueShift = 0.0f;
        public float gamma = 1.0f;
        public float colorFilterAmount;
        public Color colorFilter = Color.white;
        public Color colorMultiply = Color.white;

        //[SerializeField]
        //[SerializeReference]
        public SampleLightprobe sampleLightprobe;

        //[SerializeField]
        //[SerializeReference]
        public SampleVoxelTracer sampleVoxelTracer;

        public float volumeLightProbeGroupDensityMultiplier = 1;
        [HideInInspector] public Bounds voxelBounds => new Bounds(transform.position, volumeSize);

        //|||||||||||||||||||||||||||||||||||||||||| PRIVATE VARIABLES ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| PRIVATE VARIABLES ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| PRIVATE VARIABLES ||||||||||||||||||||||||||||||||||||||||||

        private VolumeGeneratorAssets volumeGeneratorAssets;
        private RenderTextureConverter renderTextureConverter;

        private string finalVolumeAssetPath => string.Format("{0}/{1}.asset", volumeGeneratorAssets.localAssetSceneDataFolder, volumeName);
        private string finalVolumeDensityAssetPath => string.Format("{0}/{1}_Density.asset", volumeGeneratorAssets.localAssetSceneDataFolder, volumeName);
        private string fogMaterialAssetPath => string.Format("{0}/{1}.mat", volumeGeneratorAssets.localAssetSceneDataFolder, volumeName);
        private string fogMaterialLPPVAssetPath => string.Format("{0}/{1}_LPPV.mat", volumeGeneratorAssets.localAssetSceneDataFolder, volumeName);

        private uint THREAD_GROUP_SIZE_X = 0;
        private uint THREAD_GROUP_SIZE_Y = 0;
        private uint THREAD_GROUP_SIZE_Z = 0;

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
            Texture3D volumeAsset = GetFinalVolume();

            if (volumeAsset == null)
                return 0;
            else
                return Profiler.GetRuntimeMemorySizeLong(volumeAsset);
        }

        public Texture3D GetGeneratedVolume()
        {
            if (lightingSource == LightingSource.LightProbes)
                return sampleLightprobe.GetFinalGeneratedVolume();
            else if (lightingSource == LightingSource.VoxelTracer)
                return sampleVoxelTracer.GetFinalGeneratedVolume();
            else
                return null;
        }

        public Texture3D GetFinalVolume() => AssetDatabase.LoadAssetAtPath<Texture3D>(finalVolumeAssetPath);

        public Texture3D GetDensityVolume() => AssetDatabase.LoadAssetAtPath<Texture3D>(finalVolumeDensityAssetPath);

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

        public Texture3D GetEmptyVolumeTexture() => new Texture3D(GetVoxelResolution().x, GetVoxelResolution().y, GetVoxelResolution().z, TextureFormat.RGBA32, false);

        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| SETUP ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| SETUP ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| SETUP ||||||||||||||||||||||||||||||||||||||||||||||||||||||||

        public void Setup()
        {
            if (volumeGeneratorAssets == null)
                volumeGeneratorAssets = new VolumeGeneratorAssets();

            if (sampleLightprobe == null)
                sampleLightprobe = new SampleLightprobe(this, volumeGeneratorAssets);

            if (sampleVoxelTracer == null)
                sampleVoxelTracer = new SampleVoxelTracer(this, volumeGeneratorAssets);

            if (renderTextureConverter == null)
                renderTextureConverter = new RenderTextureConverter();

            CalculateResolution();
        }

        public void SetupSceneObjectVolume()
        {
            fogMaterial = volumeGeneratorAssets.GetVolumeMaterial(volumeName);
            fogMaterialLPPV = volumeGeneratorAssets.GetVolume_LPPV_Material(volumeName);

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
            volumeGeneratorAssets.PrepareAssetFolders();
            volumeGeneratorAssets.GetResources();

            if(lightingSource != LightingSource.LightProbeProxyVolume)
            {
                if (lightingSource == LightingSource.LightProbes)
                    sampleLightprobe.GenerateVolume();
                else if (lightingSource == LightingSource.VoxelTracer)
                    sampleVoxelTracer.GenerateVolume();

                Texture3D generatedVolume = GetGeneratedVolume();

                if (postAdjustments)
                    ApplyAdjustments(generatedVolume);

                ApplyDensity(postAdjustments ? GetFinalVolume() : generatedVolume, seperateDensityTexture);
            }
            else
            {
                lightProbeProxyVolume = transform.GetComponentInChildren<LightProbeProxyVolume>();

                if (lightProbeProxyVolume == null)
                {
                    GameObject newGameObject = new GameObject("Light Probe Proxy Volume");

                    newGameObject.transform.SetParent(transform);
                    newGameObject.transform.localPosition = Vector3.zero;

                    lightProbeProxyVolume = newGameObject.AddComponent<LightProbeProxyVolume>();
                }

                volumeGeneratorAssets.GetResources();

                lightProbeProxyVolume.transform.localScale = volumeSize;
                lightProbeProxyVolume.boundingBoxMode = LightProbeProxyVolume.BoundingBoxMode.AutomaticLocal;
                lightProbeProxyVolume.probePositionMode = LightProbeProxyVolume.ProbePositionMode.CellCorner;
                lightProbeProxyVolume.refreshMode = LightProbeProxyVolume.RefreshMode.EveryFrame;
                lightProbeProxyVolume.qualityMode = LightProbeProxyVolume.QualityMode.Normal;
                lightProbeProxyVolume.dataFormat = LightProbeProxyVolume.DataFormat.Float;
                lightProbeProxyVolume.probeDensity = voxelDensitySize;

                ApplyDensity(GetEmptyVolumeTexture(), useDensityTextureForLPPV);
            }

            UpdateMaterial();
        }

        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| MATERIAL ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| MATERIAL ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| MATERIAL ||||||||||||||||||||||||||||||||||||||||||||||||||||||||

        public void UpdateMaterial()
        {
            //if there is no scene object, try finding one
            if (fogSceneObject == null)
            {
                if (transform.childCount > 0)
                    fogSceneObject = transform.GetChild(0).gameObject;
            }

            if (fogMeshRenderer == null)
                fogMeshRenderer = fogSceneObject.GetComponent<MeshRenderer>();

            if (lightingSource == LightingSource.LightProbeProxyVolume)
            {
                fogMaterialLPPV = volumeGeneratorAssets.GetVolume_LPPV_Material(volumeName);

                if(fogMaterialLPPV == null)
                    return;

                fogMeshRenderer.lightProbeProxyVolumeOverride = lightProbeProxyVolume.gameObject;
                fogMeshRenderer.lightProbeUsage = LightProbeUsage.UseProxyVolume;
                fogMeshRenderer.sharedMaterial = fogMaterialLPPV;

                VolumeGeneratorUtility.SetMaterialKeyword(fogMaterialLPPV, "_SAMPLES__8", raymarchSamples == RaymarchSamples._8);
                VolumeGeneratorUtility.SetMaterialKeyword(fogMaterialLPPV, "_SAMPLES__16", raymarchSamples == RaymarchSamples._16);
                VolumeGeneratorUtility.SetMaterialKeyword(fogMaterialLPPV, "_SAMPLES__24", raymarchSamples == RaymarchSamples._24);
                VolumeGeneratorUtility.SetMaterialKeyword(fogMaterialLPPV, "_SAMPLES__32", raymarchSamples == RaymarchSamples._32);
                VolumeGeneratorUtility.SetMaterialKeyword(fogMaterialLPPV, "_SAMPLES__48", raymarchSamples == RaymarchSamples._48);
                VolumeGeneratorUtility.SetMaterialKeyword(fogMaterialLPPV, "_SAMPLES__64", raymarchSamples == RaymarchSamples._64);
                VolumeGeneratorUtility.SetMaterialKeyword(fogMaterialLPPV, "_SAMPLES__128", raymarchSamples == RaymarchSamples._128);
                VolumeGeneratorUtility.SetMaterialKeyword(fogMaterialLPPV, "_USE_DENSITY_TEXTURE", useDensityTextureForLPPV);

                fogMaterialLPPV.SetInt("_Samples", (int)raymarchSamples);
                fogMaterialLPPV.SetInt("_UseDensityTexture", useDensityTextureForLPPV ? 1 : 0);
                fogMaterialLPPV.SetVector("_VolumePos", new Vector4(transform.position.x, transform.position.y, transform.position.z, 0.0f));
                fogMaterialLPPV.SetVector("_VolumeSize", new Vector4(volumeSize.x, volumeSize.y, volumeSize.z, 0.0f));
                fogMaterialLPPV.SetTexture("_JitterTexture", NoiseLibrary.GetBlueNoise());
                fogMaterialLPPV.SetTexture("_DensityVolumeTexture", useDensityTextureForLPPV ? GetDensityVolume() : null);
            }   
            else
            {
                fogMaterial = volumeGeneratorAssets.GetVolumeMaterial(volumeName);

                if (fogMaterial == null)
                    return;

                fogMeshRenderer.lightProbeProxyVolumeOverride = null;
                fogMeshRenderer.lightProbeUsage = LightProbeUsage.Off;
                fogMeshRenderer.sharedMaterial = fogMaterial;

                VolumeGeneratorUtility.SetMaterialKeyword(fogMaterial, "_SAMPLES__8", raymarchSamples == RaymarchSamples._8);
                VolumeGeneratorUtility.SetMaterialKeyword(fogMaterial, "_SAMPLES__16", raymarchSamples == RaymarchSamples._16);
                VolumeGeneratorUtility.SetMaterialKeyword(fogMaterial, "_SAMPLES__24", raymarchSamples == RaymarchSamples._24);
                VolumeGeneratorUtility.SetMaterialKeyword(fogMaterial, "_SAMPLES__32", raymarchSamples == RaymarchSamples._32);
                VolumeGeneratorUtility.SetMaterialKeyword(fogMaterial, "_SAMPLES__48", raymarchSamples == RaymarchSamples._48);
                VolumeGeneratorUtility.SetMaterialKeyword(fogMaterial, "_SAMPLES__64", raymarchSamples == RaymarchSamples._64);
                VolumeGeneratorUtility.SetMaterialKeyword(fogMaterial, "_SAMPLES__128", raymarchSamples == RaymarchSamples._128);
                VolumeGeneratorUtility.SetMaterialKeyword(fogMaterial, "_USE_DENSITY_TEXTURE", seperateDensityTexture);

                fogMaterial.SetInt("_Samples", (int)raymarchSamples);
                fogMaterial.SetInt("_UseDensityTexture", seperateDensityTexture ? 1 : 0);
                fogMaterial.SetVector("_VolumePos", new Vector4(transform.position.x, transform.position.y, transform.position.z, 0.0f));
                fogMaterial.SetVector("_VolumeSize", new Vector4(volumeSize.x, volumeSize.y, volumeSize.z, 0.0f));
                fogMaterial.SetTexture("_VolumeTexture", GetFinalVolume());
                fogMaterial.SetTexture("_JitterTexture", NoiseLibrary.GetBlueNoise());
                fogMaterial.SetTexture("_DensityVolumeTexture", seperateDensityTexture ? GetDensityVolume() : null);
            }

            fogSceneObject.transform.localScale = Vector3.one * volumeSize.magnitude * 1.5f;
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

        public void ApplyAdjustments(Texture3D volumeRead)
        {
            volumeGeneratorAssets.GetResources();

            //double timeBeforeBake = Time.realtimeSinceStartupAsDouble;
            double timeBeforeBake = Time.realtimeSinceStartup;

            VolumeGeneratorUtility.UpdateProgressBar("Preparing to generate Light Probe volume...", 0.5f);

            //|||||||||||||||||||||||||||||||||||||||||| COMPUTE SHADER SETUP ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| COMPUTE SHADER SETUP ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| COMPUTE SHADER SETUP ||||||||||||||||||||||||||||||||||||||||||

            //consruct our render texture that we will write into
            RenderTexture volumeWrite = new RenderTexture(volumeResolution.x, volumeResolution.y, 0, GetRenderTextureFormat());
            volumeWrite.dimension = TextureDimension.Tex3D;
            volumeWrite.filterMode = FilterMode.Bilinear;
            volumeWrite.wrapMode = TextureWrapMode.Clamp;
            volumeWrite.volumeDepth = volumeResolution.z;
            volumeWrite.enableRandomWrite = true;
            volumeWrite.Create();

            //|||||||||||||||||||||||||||||||||||||||||| APPLYING POST ADJUSTMENTS ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| APPLYING POST ADJUSTMENTS ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| APPLYING POST ADJUSTMENTS ||||||||||||||||||||||||||||||||||||||||||

            VolumeGeneratorUtility.UpdateProgressBar("Applying Post Adjustments...", 0.5f);

            //fetch our main adjustments function kernel in the compute shader
            int ComputeShader_Adjustments = volumeGeneratorAssets.adjustments.FindKernel("Adjustments");

            //make sure the compute shader knows the following parameters.
            volumeGeneratorAssets.adjustments.SetFloat("Brightness", brightness);
            volumeGeneratorAssets.adjustments.SetFloat("Contrast", contrast);
            volumeGeneratorAssets.adjustments.SetFloat("Saturation", saturation);
            volumeGeneratorAssets.adjustments.SetFloat("Vibrance", vibrance);
            volumeGeneratorAssets.adjustments.SetFloat("HueShift", hueShift);
            volumeGeneratorAssets.adjustments.SetFloat("Gamma", gamma);
            volumeGeneratorAssets.adjustments.SetFloat("ColorFilterStrength", colorFilterAmount);
            volumeGeneratorAssets.adjustments.SetVector("ColorFilter", colorFilter);
            volumeGeneratorAssets.adjustments.SetVector("ColorMultiply", colorMultiply);
            volumeGeneratorAssets.adjustments.SetVector("VolumeResolution", new Vector4(volumeResolution.x, volumeResolution.y, volumeResolution.z, 0));

            //feed our compute shader the appropriate textures.
            volumeGeneratorAssets.adjustments.SetTexture(ComputeShader_Adjustments, "VolumetricBase", volumeRead);
            volumeGeneratorAssets.adjustments.SetTexture(ComputeShader_Adjustments, "VolumetricWrite", volumeWrite);

            //let the GPU perform color adjustments to the 3D volume.
            volumeGeneratorAssets.adjustments.GetKernelThreadGroupSizes(ComputeShader_Adjustments, out THREAD_GROUP_SIZE_X, out THREAD_GROUP_SIZE_Y, out THREAD_GROUP_SIZE_Z);
            volumeGeneratorAssets.adjustments.Dispatch(ComputeShader_Adjustments, Mathf.CeilToInt(volumeResolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(volumeResolution.y / THREAD_GROUP_SIZE_Y), Mathf.CeilToInt(volumeResolution.z / THREAD_GROUP_SIZE_Z));

            //|||||||||||||||||||||||||||||||||||||||||| SAVING RESULTS ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| SAVING RESULTS ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| SAVING RESULTS ||||||||||||||||||||||||||||||||||||||||||

            VolumeGeneratorUtility.UpdateProgressBar("Saving to disk...", 0.5f);

            //save it!
            renderTextureConverter.SaveRenderTexture3DAsTexture3D(volumeWrite, finalVolumeAssetPath);

            //we are done with this, so clean up.
            volumeWrite.Release();

            //double timeAfterBake = Time.realtimeSinceStartupAsDouble - timeBeforeBake;
            double timeAfterBake = Time.realtimeSinceStartup - timeBeforeBake;
            Debug.Log(string.Format("'{0}' took {1} seconds to bake.", volumeName, timeAfterBake));

            VolumeGeneratorUtility.CloseProgressBar();
        }

        public void ApplyDensity(Texture3D volumeRead, bool generateSeperateTexture)
        {
            volumeGeneratorAssets.GetResources();

            //double timeBeforeBake = Time.realtimeSinceStartupAsDouble;
            double timeBeforeBake = Time.realtimeSinceStartup;

            VolumeGeneratorUtility.UpdateProgressBar("Preparing to generate Light Probe volume...", 0.5f);

            //|||||||||||||||||||||||||||||||||||||||||| COMPUTE SHADER SETUP ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| COMPUTE SHADER SETUP ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| COMPUTE SHADER SETUP ||||||||||||||||||||||||||||||||||||||||||

            //consruct our render texture that we will write into
            RenderTexture volumeWrite = new RenderTexture(volumeResolution.x, volumeResolution.y, 0, seperateDensityTexture ? RenderTextureFormat.R8 : GetRenderTextureFormat());
            volumeWrite.filterMode = FilterMode.Bilinear;
            volumeWrite.wrapMode = TextureWrapMode.Clamp;
            volumeWrite.dimension = TextureDimension.Tex3D;
            volumeWrite.volumeDepth = volumeResolution.z;
            volumeWrite.enableRandomWrite = true;
            volumeWrite.Create();

            //|||||||||||||||||||||||||||||||||||||||||| APPLYING DENSITY ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| APPLYING DENSITY ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| APPLYING DENSITY ||||||||||||||||||||||||||||||||||||||||||

            VolumeGeneratorUtility.UpdateProgressBar("Applying Density...", 0.5f);

            //fetch our main adjustments function kernel in the compute shader
            int ComputeShader_ApplyVolumeDensity = volumeGeneratorAssets.density.FindKernel("ComputeShader_ApplyVolumeDensity");

            volumeGeneratorAssets.density.SetVector("VolumeResolution", new Vector4(volumeResolution.x, volumeResolution.y, volumeResolution.z, 0));
            volumeGeneratorAssets.density.SetVector("VolumePosition", transform.position);
            volumeGeneratorAssets.density.SetVector("VolumeSize", volumeSize);

            //make sure the compute shader knows the following parameters.
            volumeGeneratorAssets.density.SetFloat("DensityConstant", densityConstant);
            volumeGeneratorAssets.density.SetFloat("DensityTop", densityTop);
            volumeGeneratorAssets.density.SetFloat("DensityBottom", densityBottom);
            volumeGeneratorAssets.density.SetFloat("DensityHeight", densityHeight);
            volumeGeneratorAssets.density.SetFloat("DensityHeightFallof", densityHeightFallof);
            volumeGeneratorAssets.density.SetFloat("DensityLuminanceMultiplier", densityLuminanceMultiplier);
            volumeGeneratorAssets.density.SetBool("DensityInvertLuminance", densityInvertLuminance);
            //volumeGeneratorAssets.density.SetVector("unity_ColorSpaceLuminance", Shader.GetGlobalVector("unity_ColorSpaceLuminance"));
            volumeGeneratorAssets.density.SetVector("unity_ColorSpaceLuminance", new Vector4(0.2125f, 0.7154f, 0.0721f, 0.0f));

            VolumeGeneratorUtility.SetComputeKeyword(volumeGeneratorAssets.density, "SEPERATE_DENSITY_TEXTURE", generateSeperateTexture);
            VolumeGeneratorUtility.SetComputeKeyword(volumeGeneratorAssets.density, "DENSITY_CONSTANT", densityType == DensityType.Constant);
            VolumeGeneratorUtility.SetComputeKeyword(volumeGeneratorAssets.density, "DENSITY_LUMINANCE", densityType == DensityType.Luminance);
            VolumeGeneratorUtility.SetComputeKeyword(volumeGeneratorAssets.density, "DENSITY_HEIGHTBASED", densityType == DensityType.HeightBased);
            VolumeGeneratorUtility.SetComputeKeyword(volumeGeneratorAssets.density, "DENSITY_HEIGHTBASEDLUMINANCE", densityType == DensityType.HeightBasedLuminance);

            //feed our compute shader the appropriate textures.
            volumeGeneratorAssets.density.SetTexture(ComputeShader_ApplyVolumeDensity, "Read", volumeRead);
            volumeGeneratorAssets.density.SetTexture(ComputeShader_ApplyVolumeDensity, "Write", volumeWrite);

            //let the GPU perform color adjustments to the 3D volume.
            volumeGeneratorAssets.density.GetKernelThreadGroupSizes(ComputeShader_ApplyVolumeDensity, out THREAD_GROUP_SIZE_X, out THREAD_GROUP_SIZE_Y, out THREAD_GROUP_SIZE_Z);
            volumeGeneratorAssets.density.Dispatch(ComputeShader_ApplyVolumeDensity, Mathf.CeilToInt(volumeResolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(volumeResolution.y / THREAD_GROUP_SIZE_Y), Mathf.CeilToInt(volumeResolution.z / THREAD_GROUP_SIZE_Z));

            //|||||||||||||||||||||||||||||||||||||||||| SAVING RESULTS ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| SAVING RESULTS ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| SAVING RESULTS ||||||||||||||||||||||||||||||||||||||||||

            VolumeGeneratorUtility.UpdateProgressBar("Saving to disk...", 0.5f);

            //save it!
            renderTextureConverter.SaveRenderTexture3DAsTexture3D(volumeWrite, generateSeperateTexture ? finalVolumeDensityAssetPath : finalVolumeAssetPath);

            //we are done with this, so clean up.
            volumeWrite.Release();

            //double timeAfterBake = Time.realtimeSinceStartupAsDouble - timeBeforeBake;
            double timeAfterBake = Time.realtimeSinceStartup - timeBeforeBake;
            Debug.Log(string.Format("'{0}' took {1} seconds to bake.", volumeName, timeAfterBake));

            VolumeGeneratorUtility.CloseProgressBar();
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
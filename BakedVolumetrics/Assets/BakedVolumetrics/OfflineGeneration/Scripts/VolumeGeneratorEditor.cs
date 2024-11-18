#if UNITY_EDITOR
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using System.Diagnostics.Contracts;
using BakedVolumetrics;
using UnityEditorInternal;

namespace BakedVolumetricsOffline
{
    [CustomEditor(typeof(VolumeGenerator))]
    [CanEditMultipleObjects]
    public class VolumeGeneratorEditor : Editor
    {
        private VolumeGenerator volumeGenerator;
        private SampleLightprobe sampleLightprobe;
        private SampleVoxelTracer sampleVoxelTracer;

        private static int guiSpace = 10;

        private GUIStyle errorStyle;
        private GUIStyle bgLightGrey;

        private bool useCustomEnvironmentMap = false;
        private bool uncappedEditorValues = false;

        //|||||||||||||||||||||||||||||||||||||||||| VOLUME PROPERTIES UI ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| VOLUME PROPERTIES UI ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| VOLUME PROPERTIES UI ||||||||||||||||||||||||||||||||||||||||||

        private void VolumePropertiesUI()
        {
            EditorGUILayout.LabelField("Volume Properties", EditorStyles.boldLabel);
            volumeGenerator.volumeSize = EditorGUILayout.Vector3Field("Volume Size", volumeGenerator.volumeSize);
            EditorGUILayout.Space(guiSpace);
        }

        //|||||||||||||||||||||||||||||||||||||||||| VOLUME QUALITY UI ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| VOLUME QUALITY UI ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| VOLUME QUALITY UI ||||||||||||||||||||||||||||||||||||||||||

        private void VolumeQualityUI()
        {
            EditorGUILayout.LabelField("Volume Quality", EditorStyles.boldLabel);

            if(volumeGenerator.lightingSource != LightingSource.LightProbeProxyVolume)
            {
                volumeGenerator.volumeBitDepth = (VolumeBitDepth)EditorGUILayout.EnumPopup("Volume Bit Depth", volumeGenerator.volumeBitDepth);
                volumeGenerator.seperateDensityTexture = EditorGUILayout.Toggle("Seperate Density Texture", volumeGenerator.seperateDensityTexture);
            }
            else
                volumeGenerator.useDensityTextureForLPPV = EditorGUILayout.Toggle("Use Density Texture For LPPV", volumeGenerator.useDensityTextureForLPPV);

            volumeGenerator.voxelCalculation = (VoxelCalculation)EditorGUILayout.EnumPopup("Resolution Calculation", volumeGenerator.voxelCalculation);

            if (volumeGenerator.voxelCalculation == VoxelCalculation.Custom)
                volumeGenerator.customVolumeResolution = EditorGUILayout.Vector3IntField("Custom Volume Resolution", volumeGenerator.customVolumeResolution);
            else if (volumeGenerator.voxelCalculation == VoxelCalculation.Automatic)
                volumeGenerator.voxelDensitySize = EditorGUILayout.FloatField("Voxel Density Size", volumeGenerator.voxelDensitySize);

            volumeGenerator.voxelDensitySize = Mathf.Max(0.0f, volumeGenerator.voxelDensitySize);
            volumeGenerator.customVolumeResolution = Vector3Int.Min(Vector3Int.zero, volumeGenerator.customVolumeResolution);

            EditorGUILayout.LabelField(string.Format("Resolution: {0}x{1}x{2} [{3}] voxels.", volumeGenerator.GetVoxelResolution().x, volumeGenerator.GetVoxelResolution().y, volumeGenerator.GetVoxelResolution().z, volumeGenerator.GetTotalVoxelCount()), EditorStyles.helpBox);
            EditorGUILayout.LabelField(string.Format("Disk/Memory Size: {0} MB [{1} KB] [{2} BYTES]", Mathf.RoundToInt((float)(volumeGenerator.GetVolumeSpaceUsage() * 0.0001)) * 0.01, Mathf.RoundToInt((float)(volumeGenerator.GetVolumeSpaceUsage() * 0.001)), volumeGenerator.GetVolumeSpaceUsage()), EditorStyles.helpBox);
            EditorGUILayout.Space(guiSpace);
        }

        //|||||||||||||||||||||||||||||||||||||||||| VOLUME RENDERING UI ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| VOLUME RENDERING UI ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| VOLUME RENDERING UI ||||||||||||||||||||||||||||||||||||||||||

        private void VolumeRenderingUI()
        {
            EditorGUILayout.LabelField("Volume Rendering", EditorStyles.boldLabel);
            volumeGenerator.raymarchSamples = (RaymarchSamples)EditorGUILayout.EnumPopup("Raymarch Samples", volumeGenerator.raymarchSamples);
            volumeGenerator.lightingSource = (LightingSource)EditorGUILayout.EnumPopup("Lighting Source", volumeGenerator.lightingSource);

            if (volumeGenerator.lightingSource == LightingSource.LightProbes)
            {
                EditorGUILayout.LabelField("NOTE: The resolution of the light probe bake is heavily dependent on how dense your current light probe groups are. If your probe groups are sparse populated but you're generating at a high resolution volume then you won't get any sharper results and will just be wasting memory/vram/disk space. If you want sharper results consider using a different lighting source.", EditorStyles.helpBox);

                if (volumeGenerator.CheckForLightProbes())
                    EditorGUILayout.LabelField("There is no active light probe group in the scene! Either build one for your scene, or we can generate one based on the bounds of this volume. Make sure you generate lighting for the scene so that they are being used.", errorStyle);

                volumeGenerator.volumeLightProbeGroupDensityMultiplier = EditorGUILayout.FloatField("Light Probe Group Density Multiplier", volumeGenerator.volumeLightProbeGroupDensityMultiplier);

                if (GUILayout.Button("Generate Light Probe Group"))
                    volumeGenerator.GenerateLightProbeGroup();
            }
            else if (volumeGenerator.lightingSource == LightingSource.LightProbeProxyVolume)
            {
                EditorGUILayout.LabelField("NOTE: The resolution of the Light Probe Proxy Volume (LPPV) is dependent on two factors. #1: The density of your light probe groups. #2: The resolution of the LPPV. The advantage is that this is dynamic, however results can be rather coarse looking due to the runtime nature.", EditorStyles.helpBox);

                if (volumeGenerator.CheckForLightProbes())
                    EditorGUILayout.LabelField("There is no active light probe group in the scene! Either build one for your scene, or we can generate one based on the bounds of this volume. Make sure you generate lighting for the scene so that they are being used.", errorStyle);

                volumeGenerator.volumeLightProbeGroupDensityMultiplier = EditorGUILayout.FloatField("Light Probe Group Density Multiplier", volumeGenerator.volumeLightProbeGroupDensityMultiplier);

                if (GUILayout.Button("Generate Light Probe Group"))
                    volumeGenerator.GenerateLightProbeGroup();
            }

            EditorGUILayout.Space(guiSpace);
        }

        //|||||||||||||||||||||||||||||||||||||||||| VOLUME DENSITY UI ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| VOLUME DENSITY UI ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| VOLUME DENSITY UI ||||||||||||||||||||||||||||||||||||||||||

        private void VolumeDensityUI()
        {
            EditorGUILayout.LabelField("Volume Density", EditorStyles.boldLabel);

            bool dontShowVolumeDensity = volumeGenerator.lightingSource == LightingSource.LightProbeProxyVolume || volumeGenerator.volumeBitDepth == VolumeBitDepth.RGB8;

            if (volumeGenerator.lightingSource == LightingSource.LightProbeProxyVolume)
            {
                //EditorGUILayout.LabelField("Light Probe Proxy Volumes (LPPVs) are in use, which generates a volume at runtime. Density can't be applied.", EditorStyles.helpBox);

                if(volumeGenerator.useDensityTextureForLPPV)
                {
                    volumeGenerator.densityType = (DensityType)EditorGUILayout.EnumPopup("Density Type", volumeGenerator.densityType);

                    if (volumeGenerator.densityType == DensityType.Constant)
                        volumeGenerator.densityConstant = EditorGUILayout.FloatField("Density Constant", volumeGenerator.densityConstant);
                    else if (volumeGenerator.densityType == DensityType.Luminance)
                        EditorGUILayout.LabelField("Luminance Density can't be generated when Light Probe Proxy Volumes (LPPVs) are in use.", EditorStyles.helpBox);
                    else if (volumeGenerator.densityType == DensityType.HeightBased)
                    {
                        volumeGenerator.densityTop = EditorGUILayout.FloatField("Density Top", volumeGenerator.densityTop);
                        volumeGenerator.densityBottom = EditorGUILayout.FloatField("Density Bottom", volumeGenerator.densityBottom);
                        volumeGenerator.densityHeight = EditorGUILayout.FloatField("Density Height", volumeGenerator.densityHeight);
                        volumeGenerator.densityHeightFallof = EditorGUILayout.FloatField("Density Height Falloff", volumeGenerator.densityHeightFallof);
                    }
                    else if (volumeGenerator.densityType == DensityType.HeightBasedLuminance)
                        EditorGUILayout.LabelField("Height Based Luminance Density can't be generated when Light Probe Proxy Volumes (LPPVs) are in use.", EditorStyles.helpBox);
                }
                else
                {
                    EditorGUILayout.LabelField("Density needs to be generated seperately when Light Probe Proxy Volumes (LPPVs) are in use. Enable 'Use Density Texture For LPPV' if you want to have varied density.", EditorStyles.helpBox);
                }
            }

            if (volumeGenerator.volumeBitDepth == VolumeBitDepth.RGB8)
            {
                EditorGUILayout.LabelField("Bit Depth is set to RGB8, which has no alpha channel. This means that density will have to be constant since we don't have an alpha channel to set a unique value for every voxel.", EditorStyles.helpBox);

                volumeGenerator.densityType = 0; //0 = Constant
            }

            if (dontShowVolumeDensity == false)
            {
                volumeGenerator.densityType = (DensityType)EditorGUILayout.EnumPopup("Density Type", volumeGenerator.densityType);

                if (volumeGenerator.densityType == DensityType.Constant)
                {
                    volumeGenerator.densityConstant = EditorGUILayout.FloatField("Density Constant", volumeGenerator.densityConstant);
                }
                else if (volumeGenerator.densityType == DensityType.Luminance)
                {
                    volumeGenerator.densityInvertLuminance = EditorGUILayout.Toggle("Density Invert Luminance", volumeGenerator.densityInvertLuminance);
                    volumeGenerator.densityLuminanceMultiplier = EditorGUILayout.FloatField("Density Luminance Multiplier", volumeGenerator.densityLuminanceMultiplier);
                }
                else if (volumeGenerator.densityType == DensityType.HeightBased)
                {
                    volumeGenerator.densityTop = EditorGUILayout.FloatField("Density Top", volumeGenerator.densityTop);
                    volumeGenerator.densityBottom = EditorGUILayout.FloatField("Density Bottom", volumeGenerator.densityBottom);
                    volumeGenerator.densityHeight = EditorGUILayout.FloatField("Density Height", volumeGenerator.densityHeight);
                    volumeGenerator.densityHeightFallof = EditorGUILayout.FloatField("Density Height Falloff", volumeGenerator.densityHeightFallof);
                }
                else if (volumeGenerator.densityType == DensityType.HeightBasedLuminance)
                {
                    volumeGenerator.densityInvertLuminance = EditorGUILayout.Toggle("Density Invert Luminance", volumeGenerator.densityInvertLuminance);
                    volumeGenerator.densityLuminanceMultiplier = EditorGUILayout.FloatField("Density Luminance Multiplier", volumeGenerator.densityLuminanceMultiplier);
                    volumeGenerator.densityTop = EditorGUILayout.FloatField("Density Top", volumeGenerator.densityTop);
                    volumeGenerator.densityBottom = EditorGUILayout.FloatField("Density Bottom", volumeGenerator.densityBottom);
                    volumeGenerator.densityHeight = EditorGUILayout.FloatField("Density Height", volumeGenerator.densityHeight);
                    volumeGenerator.densityHeightFallof = EditorGUILayout.FloatField("Density Height Falloff", volumeGenerator.densityHeightFallof);
                }
            }

            volumeGenerator.densityConstant = Mathf.Max(0, volumeGenerator.densityConstant);
            volumeGenerator.densityTop = Mathf.Max(0, volumeGenerator.densityTop);
            volumeGenerator.densityBottom = Mathf.Max(0, volumeGenerator.densityBottom);
            volumeGenerator.densityHeight = Mathf.Max(0, volumeGenerator.densityHeight);
            volumeGenerator.densityHeightFallof = Mathf.Max(0, volumeGenerator.densityHeightFallof);

            EditorGUILayout.Space(guiSpace);
        }

        //|||||||||||||||||||||||||||||||||||||||||| SAMPLE LIGHTPROBE UI ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| SAMPLE LIGHTPROBE UI ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| SAMPLE LIGHTPROBE UI ||||||||||||||||||||||||||||||||||||||||||

        private void SampleLightprobeUI()
        {
            if (sampleLightprobe == null)
                return;

            GUILayout.BeginVertical(bgLightGrey);
            EditorGUILayout.LabelField("Voxel Rendering: Sample Light Probes", EditorStyles.whiteLargeLabel);
            GUILayout.EndVertical();

            EditorGUILayout.Space(guiSpace);

            EditorGUILayout.LabelField("Light Probe Baking Options", EditorStyles.boldLabel);

            sampleLightprobe.indoorOnlySamples = EditorGUILayout.Toggle("Indoor Only Samples", sampleLightprobe.indoorOnlySamples);

            EditorGUILayout.BeginHorizontal();
            sampleLightprobe.occlusionPreventLeaks = EditorGUILayout.Toggle("Occlusion Prevent Leaks", sampleLightprobe.occlusionPreventLeaks);

            if (sampleLightprobe.occlusionPreventLeaks)
                sampleLightprobe.occlusionLeakFactor = EditorGUILayout.FloatField("Occlusion Leak Factor", sampleLightprobe.occlusionLeakFactor);

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(guiSpace);
            EditorGUILayout.LabelField("Post Bake Options", EditorStyles.boldLabel);

            sampleLightprobe.gaussianBlurSamples = EditorGUILayout.IntSlider("Gaussian Blur Samples", sampleLightprobe.gaussianBlurSamples, 0, 128);

            EditorGUILayout.Space(guiSpace);
        }

        //|||||||||||||||||||||||||||||||||||||||||| SAMPLE VOXEL TRACER UI ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| SAMPLE VOXEL TRACER UI ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| SAMPLE VOXEL TRACER UI ||||||||||||||||||||||||||||||||||||||||||
        
        private void SampleVoxelTracer()
        {
            if (sampleVoxelTracer == null)
                return;

            GUILayout.BeginVertical(bgLightGrey);
            EditorGUILayout.LabelField("Voxel Tracer: Scene Voxelization", EditorStyles.whiteLargeLabel);
            GUILayout.EndVertical();

            EditorGUILayout.Space(guiSpace);

            EditorGUILayout.LabelField("Voxel Main", EditorStyles.boldLabel);
            sampleVoxelTracer.sceneVoxelizerType = (SceneVoxelizerType)EditorGUILayout.EnumPopup("Scene Voxelizer Type", sampleVoxelTracer.sceneVoxelizerType);
            EditorGUILayout.Space(guiSpace);

            EditorGUILayout.LabelField("Voxel Meta Pass Properties", EditorStyles.boldLabel);
            sampleVoxelTracer.texelDensityPerUnit = EditorGUILayout.FloatField("Texel Density Per Unit", sampleVoxelTracer.texelDensityPerUnit);
            sampleVoxelTracer.minimumBufferResolution = EditorGUILayout.IntField("Minimum Buffer Resolution", sampleVoxelTracer.minimumBufferResolution);
            sampleVoxelTracer.performDilation = EditorGUILayout.Toggle("Perform Dilation", sampleVoxelTracer.performDilation);

            if (sampleVoxelTracer.performDilation)
                sampleVoxelTracer.dilationPixelSize = EditorGUILayout.IntField("Dilation Pixel Size", sampleVoxelTracer.dilationPixelSize);

            if (sampleVoxelTracer.sceneVoxelizerType == SceneVoxelizerType.MetaExtraction3Pass)
            {
                sampleVoxelTracer.useBilinearFiltering = EditorGUILayout.Toggle("Use Bilinear Filtering", sampleVoxelTracer.useBilinearFiltering);
                sampleVoxelTracer.emissionHalfPrecision = EditorGUILayout.Toggle("Emission Half Precision", sampleVoxelTracer.emissionHalfPrecision);
            }
            else if (sampleVoxelTracer.sceneVoxelizerType == SceneVoxelizerType.MetaExtraction1Pass)
            {
                sampleVoxelTracer.emissionHDREncoding = (HDREncoding)EditorGUILayout.EnumPopup("Emission HDR Encoding", sampleVoxelTracer.emissionHDREncoding);

                switch (sampleVoxelTracer.emissionHDREncoding)
                {
                    case HDREncoding.RGBM:
                    case HDREncoding.RGBD:
                        sampleVoxelTracer.emissionHDREncodingRange = EditorGUILayout.FloatField("Emission HDR Encoding Range", sampleVoxelTracer.emissionHDREncodingRange);
                        break;
                    default:
                        break;
                }
            }

            sampleVoxelTracer.texelDensityPerUnit = Mathf.Max(0.01f, sampleVoxelTracer.texelDensityPerUnit);
            sampleVoxelTracer.minimumBufferResolution = Mathf.Max(4, sampleVoxelTracer.minimumBufferResolution);
            sampleVoxelTracer.dilationPixelSize = Mathf.Max(1, sampleVoxelTracer.dilationPixelSize);
            sampleVoxelTracer.emissionHDREncodingRange = Mathf.Max(1, sampleVoxelTracer.emissionHDREncodingRange);

            EditorGUILayout.Space(guiSpace);

            EditorGUILayout.LabelField("Voxel Rendering", EditorStyles.boldLabel);
            sampleVoxelTracer.blendAlbedoVoxelSlices = EditorGUILayout.Toggle("Blend Albedo Voxel Slices", sampleVoxelTracer.blendAlbedoVoxelSlices);
            sampleVoxelTracer.blendEmissiveVoxelSlices = EditorGUILayout.Toggle("Blend Emissive Voxel Slices", sampleVoxelTracer.blendEmissiveVoxelSlices);
            sampleVoxelTracer.blendNormalVoxelSlices = EditorGUILayout.Toggle("Blend Normal Voxel Slices", sampleVoxelTracer.blendNormalVoxelSlices);
            sampleVoxelTracer.doubleSidedGeometry = EditorGUILayout.Toggle("Double Sided Geometry", sampleVoxelTracer.doubleSidedGeometry);
            EditorGUILayout.Space(guiSpace);

            EditorGUILayout.LabelField("Voxel Optimizations", EditorStyles.boldLabel);
            sampleVoxelTracer.onlyUseGIContributors = EditorGUILayout.Toggle("Only Use GI Contributors", sampleVoxelTracer.onlyUseGIContributors);
            sampleVoxelTracer.onlyUseShadowCasters = EditorGUILayout.Toggle("Only Use Shadow Casters", sampleVoxelTracer.onlyUseShadowCasters);
            sampleVoxelTracer.onlyUseMeshesWithinBounds = EditorGUILayout.Toggle("Only Use Meshes Within Bounds", sampleVoxelTracer.onlyUseMeshesWithinBounds);
            sampleVoxelTracer.useBoundingBoxCullingForRendering = EditorGUILayout.Toggle("Use Bounding Box Culling For Rendering", sampleVoxelTracer.useBoundingBoxCullingForRendering);
            sampleVoxelTracer.objectLayerMask = EditorGUILayout.MaskField("Object Layer Mask", InternalEditorUtility.LayerMaskToConcatenatedLayersMask(sampleVoxelTracer.objectLayerMask), InternalEditorUtility.layers);
            EditorGUILayout.Space(guiSpace);

            //|||||||||||||||||||||||||||||||||||||||||| VOXEL TRACING OPTIONS ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| VOXEL TRACING OPTIONS ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| VOXEL TRACING OPTIONS ||||||||||||||||||||||||||||||||||||||||||

            GUILayout.BeginVertical(bgLightGrey);
            EditorGUILayout.LabelField("Voxel Tracer: Voxel Tracing Options", EditorStyles.whiteLargeLabel);
            GUILayout.EndVertical();

            EditorGUILayout.Space(guiSpace);

            sampleVoxelTracer.normalOrientedHemisphereSampling = EditorGUILayout.Toggle("Normal Oriented Hemisphere Sampling", sampleVoxelTracer.normalOrientedHemisphereSampling);
            sampleVoxelTracer.enableEnvironmentLighting = EditorGUILayout.Toggle("Enable Environment Lighting", sampleVoxelTracer.enableEnvironmentLighting);
            sampleVoxelTracer.lightLayerMask = EditorGUILayout.MaskField("Light Layer Mask", InternalEditorUtility.LayerMaskToConcatenatedLayersMask(sampleVoxelTracer.lightLayerMask), InternalEditorUtility.layers);
            sampleVoxelTracer.lightAttenuationType = (LightAttenuationType)EditorGUILayout.EnumPopup("Light Attenuation Type", sampleVoxelTracer.lightAttenuationType);
            sampleVoxelTracer.halfPrecisionLighting = EditorGUILayout.Toggle("Half Precision Lighting", sampleVoxelTracer.halfPrecisionLighting);

            uncappedEditorValues = EditorGUILayout.Toggle("Uncap Editor Values", uncappedEditorValues);
            EditorGUILayout.Space(guiSpace);

            EditorGUILayout.LabelField("GPU Readback", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("This will intentionally stall the CPU to wait until the GPU is ready after X amount of compute shader dispatches. It prevents the GPU from being overburdened and potentially crashing with the amount of work.", EditorStyles.helpBox);
            
            sampleVoxelTracer.enableGPU_Readback_Limit = EditorGUILayout.Toggle("Enable GPU Readback Limit", sampleVoxelTracer.enableGPU_Readback_Limit);

            if (sampleVoxelTracer.enableGPU_Readback_Limit)
            {
                if (uncappedEditorValues)
                    sampleVoxelTracer.GPU_Readback_Limit = EditorGUILayout.IntField("GPU Readback Limit", sampleVoxelTracer.GPU_Readback_Limit);
                else
                    sampleVoxelTracer.GPU_Readback_Limit = EditorGUILayout.IntSlider("GPU Readback Limit", sampleVoxelTracer.GPU_Readback_Limit, 1, 32);
            }

            sampleVoxelTracer.GPU_Readback_Limit = Mathf.Max(1, sampleVoxelTracer.GPU_Readback_Limit);

            EditorGUILayout.Space(guiSpace);

            //|||||||||||||||||||||||||||||||||||||||||| DIRECT LIGHTING ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| DIRECT LIGHTING ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| DIRECT LIGHTING ||||||||||||||||||||||||||||||||||||||||||

            GUILayout.BeginVertical(bgLightGrey);
            EditorGUILayout.LabelField("Voxel Tracer: Lighting", EditorStyles.whiteLargeLabel);
            GUILayout.EndVertical();

            EditorGUILayout.Space(guiSpace);

            EditorGUILayout.LabelField("Direct Lighting", EditorStyles.boldLabel);

            if(uncappedEditorValues)
                sampleVoxelTracer.albedoBoost = EditorGUILayout.FloatField("Albedo Boost", sampleVoxelTracer.albedoBoost);
            else
                sampleVoxelTracer.albedoBoost = EditorGUILayout.Slider("Albedo Boost", sampleVoxelTracer.albedoBoost, 1, 10);

            if (uncappedEditorValues)
                sampleVoxelTracer.directSurfaceSamples = EditorGUILayout.IntField("Direct Surface Samples", sampleVoxelTracer.directSurfaceSamples);
            else
                sampleVoxelTracer.directSurfaceSamples = EditorGUILayout.IntSlider("Direct Surface Samples", sampleVoxelTracer.directSurfaceSamples, 1, 8192);

            if (uncappedEditorValues)
                sampleVoxelTracer.directVolumetricSamples = EditorGUILayout.IntField("Direct Volumetric Samples", sampleVoxelTracer.directVolumetricSamples);
            else
                sampleVoxelTracer.directVolumetricSamples = EditorGUILayout.IntSlider("Direct Volumetric Samples", sampleVoxelTracer.directVolumetricSamples, 1, 8192);

            sampleVoxelTracer.albedoBoost = Mathf.Max(0, sampleVoxelTracer.albedoBoost);
            sampleVoxelTracer.directSurfaceSamples = Mathf.Max(1, sampleVoxelTracer.directSurfaceSamples);
            sampleVoxelTracer.directVolumetricSamples = Mathf.Max(1, sampleVoxelTracer.directVolumetricSamples);

            EditorGUILayout.Space(guiSpace);

            //|||||||||||||||||||||||||||||||||||||||||| EMISSIVE LIGHTING ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| EMISSIVE LIGHTING ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| EMISSIVE LIGHTING ||||||||||||||||||||||||||||||||||||||||||

            EditorGUILayout.LabelField("Emissive Lighting", EditorStyles.boldLabel);

            if (uncappedEditorValues)
                sampleVoxelTracer.emissiveIntensity = EditorGUILayout.FloatField("Emissive Intensity", sampleVoxelTracer.emissiveIntensity);
            else
                sampleVoxelTracer.emissiveIntensity = EditorGUILayout.Slider("Emissive Intensity", sampleVoxelTracer.emissiveIntensity, 0, 8);

            sampleVoxelTracer.emissiveIntensity = Mathf.Max(0, sampleVoxelTracer.emissiveIntensity);

            EditorGUILayout.Space(guiSpace);

            //|||||||||||||||||||||||||||||||||||||||||| ENVIRONMENT LIGHTING ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| ENVIRONMENT LIGHTING ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| ENVIRONMENT LIGHTING ||||||||||||||||||||||||||||||||||||||||||

            if (sampleVoxelTracer.enableEnvironmentLighting)
            {
                EditorGUILayout.LabelField("Environment Lighting", EditorStyles.boldLabel);

                useCustomEnvironmentMap = EditorGUILayout.Toggle("Use Custom Environment Map", useCustomEnvironmentMap);

                if (!useCustomEnvironmentMap)
                {
                    if(uncappedEditorValues)
                        sampleVoxelTracer.environmentResolution = EditorGUILayout.IntField("Environment Resolution", sampleVoxelTracer.environmentResolution);
                    else
                        sampleVoxelTracer.environmentResolution = (int)(EnvironmentMapResolution)EditorGUILayout.EnumPopup("Environment Resolution", (EnvironmentMapResolution)sampleVoxelTracer.environmentResolution);
                }

                if (useCustomEnvironmentMap)
                    sampleVoxelTracer.customEnvironmentMap = (Cubemap)EditorGUILayout.ObjectField("Custom Environment Map", sampleVoxelTracer.customEnvironmentMap, typeof(Cubemap), false);

                if (uncappedEditorValues)
                    sampleVoxelTracer.environmentIntensity = EditorGUILayout.FloatField("Environment Intensity", sampleVoxelTracer.environmentIntensity);
                else
                    sampleVoxelTracer.environmentIntensity = EditorGUILayout.Slider("Environment Intensity", sampleVoxelTracer.environmentIntensity, 0, 8);

                if (uncappedEditorValues)
                    sampleVoxelTracer.environmentSurfaceSamples = EditorGUILayout.IntField("Environment Surface Samples", sampleVoxelTracer.environmentSurfaceSamples);
                else
                    sampleVoxelTracer.environmentSurfaceSamples = EditorGUILayout.IntSlider("Environment Surface Samples", sampleVoxelTracer.environmentSurfaceSamples, 1, 8192);

                if (uncappedEditorValues)
                    sampleVoxelTracer.environmentVolumetricSamples = EditorGUILayout.IntField("Environment Volumetric Samples", sampleVoxelTracer.environmentVolumetricSamples);
                else
                    sampleVoxelTracer.environmentVolumetricSamples = EditorGUILayout.IntSlider("Environment Volumetric Samples", sampleVoxelTracer.environmentVolumetricSamples, 1, 8192);

                sampleVoxelTracer.environmentResolution = Mathf.Max(32, sampleVoxelTracer.environmentResolution);
                sampleVoxelTracer.environmentIntensity = Mathf.Max(0, sampleVoxelTracer.environmentIntensity);
                sampleVoxelTracer.environmentSurfaceSamples = Mathf.Max(1, sampleVoxelTracer.environmentSurfaceSamples);
                sampleVoxelTracer.environmentVolumetricSamples = Mathf.Max(1, sampleVoxelTracer.environmentVolumetricSamples);

                EditorGUILayout.Space(guiSpace);
            }

            //|||||||||||||||||||||||||||||||||||||||||| BOUNCE LIGHTING ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| BOUNCE LIGHTING ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| BOUNCE LIGHTING ||||||||||||||||||||||||||||||||||||||||||

            EditorGUILayout.LabelField("Bounce Lighting", EditorStyles.boldLabel);

            if (uncappedEditorValues)
                sampleVoxelTracer.indirectIntensity = EditorGUILayout.FloatField("Indirect Intensity", sampleVoxelTracer.indirectIntensity);
            else
                sampleVoxelTracer.indirectIntensity = EditorGUILayout.Slider("Indirect Intensity", sampleVoxelTracer.indirectIntensity, 0, 5);

            if (uncappedEditorValues)
                sampleVoxelTracer.bounceSurfaceSamples = EditorGUILayout.IntField("Bounce Surface Samples", sampleVoxelTracer.bounceSurfaceSamples);
            else
                sampleVoxelTracer.bounceSurfaceSamples = EditorGUILayout.IntSlider("Bounce Surface Samples", sampleVoxelTracer.bounceSurfaceSamples, 1, 8192);

            if (uncappedEditorValues)
                sampleVoxelTracer.bounceVolumetricSamples = EditorGUILayout.IntField("Bounce Volumetric Samples", sampleVoxelTracer.bounceVolumetricSamples);
            else
                sampleVoxelTracer.bounceVolumetricSamples = EditorGUILayout.IntSlider("Bounce Volumetric Samples", sampleVoxelTracer.bounceVolumetricSamples, 1, 8192);

            if (uncappedEditorValues)
                sampleVoxelTracer.bounces = EditorGUILayout.IntField("Bounces", sampleVoxelTracer.bounces);
            else
                sampleVoxelTracer.bounces = EditorGUILayout.IntSlider("Bounces", sampleVoxelTracer.bounces, 1, 8);

            sampleVoxelTracer.indirectIntensity = Mathf.Max(0, sampleVoxelTracer.indirectIntensity);
            sampleVoxelTracer.bounceSurfaceSamples = Mathf.Max(1, sampleVoxelTracer.bounceSurfaceSamples);
            sampleVoxelTracer.bounceVolumetricSamples = Mathf.Max(1, sampleVoxelTracer.bounceVolumetricSamples);
            sampleVoxelTracer.bounces = Mathf.Max(1, sampleVoxelTracer.bounces);

            EditorGUILayout.Space(guiSpace);

            //|||||||||||||||||||||||||||||||||||||||||| POST VOLUMETRIC BAKE OPTIONS ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| POST VOLUMETRIC BAKE OPTIONS ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| POST VOLUMETRIC BAKE OPTIONS ||||||||||||||||||||||||||||||||||||||||||

            GUILayout.BeginVertical(bgLightGrey);
            EditorGUILayout.LabelField("Voxel Tracer: Volumetric Filtering", EditorStyles.whiteLargeLabel);
            GUILayout.EndVertical();

            EditorGUILayout.Space(guiSpace);

            if (uncappedEditorValues)
                sampleVoxelTracer.volumetricDirectGaussianSamples = EditorGUILayout.IntField("Volumetric Direct Gaussian Samples", sampleVoxelTracer.volumetricDirectGaussianSamples);
            else
                sampleVoxelTracer.volumetricDirectGaussianSamples = EditorGUILayout.IntSlider("Volumetric Direct Gaussian Samples", sampleVoxelTracer.volumetricDirectGaussianSamples, 0, 64);

            if (uncappedEditorValues)
                sampleVoxelTracer.volumetricBounceGaussianSamples = EditorGUILayout.IntField("Volumetric Bounce Gaussian Samples", sampleVoxelTracer.volumetricBounceGaussianSamples);
            else
                sampleVoxelTracer.volumetricBounceGaussianSamples = EditorGUILayout.IntSlider("Volumetric Bounce Gaussian Samples", sampleVoxelTracer.volumetricBounceGaussianSamples, 0, 64);

            if (volumeGenerator.sampleVoxelTracer.enableEnvironmentLighting)
            {
                if (uncappedEditorValues)
                    sampleVoxelTracer.volumetricEnvironmentGaussianSamples = EditorGUILayout.IntField("Volumetric Environment Gaussian Samples", sampleVoxelTracer.volumetricEnvironmentGaussianSamples);
                else
                    sampleVoxelTracer.volumetricEnvironmentGaussianSamples = EditorGUILayout.IntSlider("Volumetric Environment Gaussian Samples", sampleVoxelTracer.volumetricEnvironmentGaussianSamples, 0, 64);
            }

            sampleVoxelTracer.volumetricDirectGaussianSamples = Mathf.Max(0, sampleVoxelTracer.volumetricDirectGaussianSamples);
            sampleVoxelTracer.volumetricBounceGaussianSamples = Mathf.Max(0, sampleVoxelTracer.volumetricBounceGaussianSamples);
            sampleVoxelTracer.volumetricEnvironmentGaussianSamples = Mathf.Max(0, sampleVoxelTracer.volumetricEnvironmentGaussianSamples);

            EditorGUILayout.Space(guiSpace);
        }

        private void ApplyPostUI()
        {
            if (volumeGenerator.lightingSource == LightingSource.LightProbeProxyVolume)
                return;

            GUILayout.BeginVertical(bgLightGrey);
            EditorGUILayout.LabelField("Post Volume Adjustments", EditorStyles.whiteLargeLabel);
            GUILayout.EndVertical();

            EditorGUILayout.Space(guiSpace);

            volumeGenerator.postAdjustments = EditorGUILayout.Toggle("Post Adjustments", volumeGenerator.postAdjustments);

            if (volumeGenerator.postAdjustments)
            {
                volumeGenerator.brightness = EditorGUILayout.FloatField("Brightness", volumeGenerator.brightness);
                volumeGenerator.contrast = EditorGUILayout.FloatField("Contrast", volumeGenerator.contrast);
                volumeGenerator.saturation = EditorGUILayout.FloatField("Saturation", volumeGenerator.saturation);
                volumeGenerator.vibrance = EditorGUILayout.FloatField("Vibrance", volumeGenerator.vibrance);
                volumeGenerator.hueShift = EditorGUILayout.FloatField("Hue Shift", volumeGenerator.hueShift);
                volumeGenerator.gamma = EditorGUILayout.FloatField("Gamma", volumeGenerator.gamma);
                volumeGenerator.colorFilterAmount = EditorGUILayout.FloatField("Color Filter Amount", volumeGenerator.colorFilterAmount);
                volumeGenerator.colorFilter = EditorGUILayout.ColorField("Color Filter", volumeGenerator.colorFilter);
                volumeGenerator.colorMultiply = EditorGUILayout.ColorField("Color Multiply", volumeGenerator.colorMultiply);
            }

            EditorGUILayout.Space(guiSpace);
        }

        //|||||||||||||||||||||||||||||||||||||||||| GIZMOS UI ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| GIZMOS UI ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| GIZMOS UI ||||||||||||||||||||||||||||||||||||||||||

        private void GizmosUI()
        {
            GUILayout.BeginVertical(bgLightGrey);
            EditorGUILayout.LabelField("Gizmos", EditorStyles.whiteLargeLabel);
            GUILayout.EndVertical();

            EditorGUILayout.Space(guiSpace);

            volumeGenerator.previewBounds = EditorGUILayout.Toggle("Preview Bounds", volumeGenerator.previewBounds);

            if (volumeGenerator.densityType == DensityType.HeightBased)
                volumeGenerator.previewDensityHeight = EditorGUILayout.Toggle("Preview Density Height", volumeGenerator.previewDensityHeight);

            volumeGenerator.previewVoxels = EditorGUILayout.Toggle("Preview Voxels", volumeGenerator.previewVoxels);

            EditorGUILayout.Space(guiSpace);
        }

        public override void OnInspectorGUI()
        {
            errorStyle = new GUIStyle(EditorStyles.helpBox);
            errorStyle.normal.background = new Texture2D(2, 2);
            
            for(int x = 0; x < errorStyle.normal.background.width; x++)
            {
                for (int y = 0; y < errorStyle.normal.background.height; y++)
                {
                    errorStyle.normal.background.SetPixel(x, y, Color.red);
                }
            }

            errorStyle.normal.textColor = Color.red;

            if (bgLightGrey == null)
            {
                bgLightGrey = new GUIStyle(EditorStyles.label);
                bgLightGrey.normal.background = Texture2D.linearGrayTexture;
            }

            serializedObject.Update();

            volumeGenerator = serializedObject.targetObject as VolumeGenerator;
            sampleLightprobe = volumeGenerator.sampleLightprobe;
            sampleVoxelTracer = volumeGenerator.sampleVoxelTracer;

            volumeGenerator.Setup();
            volumeGenerator.UpdateMaterial();

            GUILayout.BeginVertical(bgLightGrey);
            EditorGUILayout.LabelField("Main Properties", EditorStyles.whiteLargeLabel);
            GUILayout.EndVertical();

            EditorGUILayout.Space(guiSpace);

            VolumePropertiesUI();
            VolumeQualityUI();
            VolumeDensityUI();
            VolumeRenderingUI();

            //||||||||||||||||||||||||||||||||| FUNCTIONS |||||||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||||||| FUNCTIONS |||||||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||||||| FUNCTIONS |||||||||||||||||||||||||||||||||

            switch (volumeGenerator.lightingSource)
            {
                case LightingSource.LightProbes:
                    SampleLightprobeUI();
                    break;
                case LightingSource.VoxelTracer:
                    SampleVoxelTracer();
                    break;
            }

            ApplyPostUI();

            //|||||||||||||||||||||||||||||||||||||||||| FUNCTIONS ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| FUNCTIONS ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| FUNCTIONS ||||||||||||||||||||||||||||||||||||||||||

            GUILayout.BeginVertical(bgLightGrey);
            EditorGUILayout.LabelField("Functions", EditorStyles.whiteLargeLabel);
            GUILayout.EndVertical();

            EditorGUILayout.Space(guiSpace);

            if (GUILayout.Button("Generate Volume"))
                volumeGenerator.GenerateVolume();

            if (GUILayout.Button("Update Material"))
                volumeGenerator.UpdateMaterial();

            EditorGUILayout.Space(guiSpace);

            GizmosUI();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
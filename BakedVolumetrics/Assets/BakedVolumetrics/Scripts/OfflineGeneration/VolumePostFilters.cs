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
    public class VolumePostFilters : MonoBehaviour
    {
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

        public bool postBlur;
        public int gaussianSamples = 4;

        private ComputeShader adjustments;
        private ComputeShader gaussian3D;
        private ComputeShader slicer;

        private RenderTextureFormat renderTextureFormat;
        private TextureFormat textureFormat;

        private void GetResources()
        {
            if (adjustments == null) adjustments = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/BakedVolumetrics/ComputeShaders/Adjustments3D.compute");
            if (gaussian3D == null) gaussian3D = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/BakedVolumetrics/ComputeShaders/GaussianBlur3D.compute");
            if (slicer == null) slicer = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/BakedVolumetrics/ComputeShaders/Slicer.compute");
        }

        public void ApplyPostEffects(string sourceVolumeAssetPath, string destinationVolumeAssetPath, TextureFormat textureFormat, RenderTextureFormat renderTextureFormat)
        {
            this.renderTextureFormat = renderTextureFormat;
            this.textureFormat = textureFormat;

            GetResources();

            Texture3D sourceVolume = AssetDatabase.LoadAssetAtPath<Texture3D>(sourceVolumeAssetPath);
            AssetDatabase.DeleteAsset(destinationVolumeAssetPath);

            if (postAdjustments)
                VolumeAdjustments(sourceVolume, destinationVolumeAssetPath);

            if (postBlur)
                VolumeBlur(sourceVolume, destinationVolumeAssetPath);
        }

        public void VolumeAdjustments(Texture3D sourceVolume, string destinationVolumeAssetPath)
        {
            int compute_adjustments = adjustments.FindKernel("Adjustments");

            adjustments.SetFloat("Brightness", brightness);
            adjustments.SetFloat("Contrast", contrast);
            adjustments.SetFloat("Saturation", saturation);
            adjustments.SetFloat("Vibrance", vibrance);
            adjustments.SetFloat("HueShift", hueShift);
            adjustments.SetFloat("Gamma", gamma);
            adjustments.SetFloat("ColorFilterStrength", colorFilterAmount);

            adjustments.SetVector("ColorFilter", colorFilter);
            adjustments.SetVector("ColorMultiply", colorMultiply);
            adjustments.SetVector("VolumeResolution", new Vector4(sourceVolume.width, sourceVolume.height, sourceVolume.depth, 0));

            //---------------------------------------- Adjustments ----------------------------------------
            RenderTexture volumeWrite = new RenderTexture(sourceVolume.width, sourceVolume.height, 0, renderTextureFormat);
            volumeWrite.dimension = TextureDimension.Tex3D;
            volumeWrite.volumeDepth = sourceVolume.depth;
            volumeWrite.enableRandomWrite = true;
            volumeWrite.Create();

            adjustments.SetTexture(compute_adjustments, "VolumetricBase", sourceVolume);
            adjustments.SetTexture(compute_adjustments, "VolumetricWrite", volumeWrite);
            adjustments.Dispatch(compute_adjustments, sourceVolume.width, sourceVolume.height, sourceVolume.depth);

            //---------------------------------------- FINAL ----------------------------------------
            RenderTextureConverter renderTextureConverter = new RenderTextureConverter(slicer, renderTextureFormat, textureFormat);
            renderTextureConverter.Save3D(volumeWrite, destinationVolumeAssetPath, new RenderTextureConverter.TextureObjectSettings() { anisoLevel = 0, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat });

            volumeWrite.Release();
        }

        public void VolumeBlur(Texture3D sourceVolume, string destinationVolumeAssetPath)
        {
            int compute_blur = gaussian3D.FindKernel("CSMain");

            gaussian3D.SetInt("BlurSamples", gaussianSamples);
            gaussian3D.SetVector("VolumeResolution", new Vector4(sourceVolume.width, sourceVolume.height, sourceVolume.depth, 0));

            //---------------------------------------- BLUR X ----------------------------------------
            RenderTexture volumeWriteX = new RenderTexture(sourceVolume.width, sourceVolume.height, 0, renderTextureFormat);
            volumeWriteX.dimension = TextureDimension.Tex3D;
            volumeWriteX.volumeDepth = sourceVolume.depth;
            volumeWriteX.enableRandomWrite = true;
            volumeWriteX.Create();

            gaussian3D.SetTexture(compute_blur, "VolumetricBase", sourceVolume);
            gaussian3D.SetTexture(compute_blur, "VolumetricWrite", volumeWriteX);
            gaussian3D.SetVector("BlurDirection", new Vector4(1, 0, 0, 0));
            gaussian3D.Dispatch(compute_blur, sourceVolume.width, sourceVolume.height, sourceVolume.depth);

            //---------------------------------------- BLUR Y ----------------------------------------
            RenderTexture volumeWriteY = new RenderTexture(sourceVolume.width, sourceVolume.height, 0, renderTextureFormat);
            volumeWriteY.dimension = TextureDimension.Tex3D;
            volumeWriteY.volumeDepth = sourceVolume.depth;
            volumeWriteY.enableRandomWrite = true;
            volumeWriteY.Create();

            gaussian3D.SetTexture(compute_blur, "VolumetricBase", volumeWriteX);
            gaussian3D.SetTexture(compute_blur, "VolumetricWrite", volumeWriteY);
            gaussian3D.SetVector("BlurDirection", new Vector4(0, 1, 0, 0));
            gaussian3D.Dispatch(compute_blur, sourceVolume.width, sourceVolume.height, sourceVolume.depth);

            //---------------------------------------- BLUR Z ----------------------------------------
            RenderTexture volumeWriteZ = new RenderTexture(sourceVolume.width, sourceVolume.height, 0, renderTextureFormat);
            volumeWriteZ.dimension = TextureDimension.Tex3D;
            volumeWriteZ.volumeDepth = sourceVolume.depth;
            volumeWriteZ.enableRandomWrite = true;
            volumeWriteZ.Create();

            gaussian3D.SetTexture(compute_blur, "VolumetricBase", volumeWriteY);
            gaussian3D.SetTexture(compute_blur, "VolumetricWrite", volumeWriteZ);
            gaussian3D.SetVector("BlurDirection", new Vector4(0, 0, 1, 0));
            gaussian3D.Dispatch(compute_blur, sourceVolume.width, sourceVolume.height, sourceVolume.depth);

            //---------------------------------------- FINAL ----------------------------------------
            RenderTextureConverter renderTextureConverter = new RenderTextureConverter(slicer, renderTextureFormat, textureFormat);
            renderTextureConverter.Save3D(volumeWriteZ, destinationVolumeAssetPath, new RenderTextureConverter.TextureObjectSettings() { anisoLevel = 0, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat });

            volumeWriteX.Release();
            volumeWriteY.Release();
            volumeWriteZ.Release();
        }
    }
}

#endif
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

namespace BakedVolumetrics
{
    public sealed class BakedVolumetricsV4Renderer : PostProcessEffectRenderer<BakedVolumetricsV4>
    {
        public override void Render(PostProcessRenderContext context)
        {
            /*
            var sheet = context.propertySheets.Get(Shader.Find("Hidden/BakedVolumetricsV3"));

            Matrix4x4 clipToView = GL.GetGPUProjectionMatrix(context.camera.projectionMatrix, true).inverse;
            sheet.properties.SetMatrix("_ClipToView", clipToView);

            Matrix4x4 viewMat = context.camera.worldToCameraMatrix;
            Matrix4x4 projMat = GL.GetGPUProjectionMatrix(context.camera.projectionMatrix, false);
            Matrix4x4 viewProjMat = (projMat * viewMat);
            sheet.properties.SetMatrix("_ViewProjInv", viewProjMat.inverse);

            //main properties
            sheet.properties.SetFloat("_VolumeDensity", settings.density.value);
            sheet.properties.SetFloat("_RaymarchStepSize", settings.stepSize.value);
            sheet.properties.SetFloat("_RaymarchJitterStrength", settings.jitterStrength.value);
            sheet.properties.SetVector("_VolumeResolution", settings.volumeResolution.value);
            sheet.properties.SetVector("_VolumePos", settings.volumePosition.value);
            sheet.properties.SetVector("_VolumeSize", settings.volumeSize.value);

            var volumeTexture = settings.volume.value == null ? RuntimeUtilities.whiteTexture3D : settings.volume.value;
            sheet.properties.SetTexture("_VolumeTexture", volumeTexture);
            var jitterTexture = settings.jitter.value == null ? RuntimeUtilities.blackTexture : settings.jitter.value;
            sheet.properties.SetTexture("_JitterTexture", jitterTexture);

            //fog color
            var fogColor = RenderTexture.GetTemporary(context.width / 2, context.height / 2, 0, RenderTextureFormat.ARGB32);
            fogColor.filterMode = FilterMode.Bilinear;
            context.command.BlitFullscreenTriangle(context.source, fogColor, sheet, 0);

            //low res depth
            var lowResDepth = RenderTexture.GetTemporary(context.width / 2, context.height / 2, 0, RenderTextureFormat.RHalf);
            lowResDepth.filterMode = FilterMode.Bilinear;
            context.command.BlitFullscreenTriangle(context.source, lowResDepth, sheet, 1);

            sheet.properties.SetTexture("_FogColor", fogColor);
            sheet.properties.SetTexture("_LowResDepth", lowResDepth);
            sheet.properties.SetFloat("_DownsampleFactor", 2);
            context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 2); //main

            RenderTexture.ReleaseTemporary(lowResDepth);
            RenderTexture.ReleaseTemporary(fogColor);
            */
        }
    }
}
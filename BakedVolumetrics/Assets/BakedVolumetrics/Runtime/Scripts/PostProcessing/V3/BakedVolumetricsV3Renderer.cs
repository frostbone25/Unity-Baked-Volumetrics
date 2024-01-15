using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

namespace BakedVolumetrics
{
    public sealed class BakedVolumetricsV3Renderer : PostProcessEffectRenderer<BakedVolumetricsV3>
    {
        public override void Render(PostProcessRenderContext context)
        {
            //self note: render is called every frame... why the hell are we needing to find this shader every frame?
            var sheet = context.propertySheets.Get(Shader.Find("Hidden/BakedVolumetricsV3"));

            Matrix4x4 clipToView = GL.GetGPUProjectionMatrix(context.camera.projectionMatrix, true).inverse;
            Matrix4x4 projMat = GL.GetGPUProjectionMatrix(context.camera.projectionMatrix, false);
            Matrix4x4 viewMat = context.camera.worldToCameraMatrix;
            Matrix4x4 viewProjMat = (projMat * viewMat);
            sheet.properties.SetMatrix(ShaderIDs.ClipToView, clipToView);
            sheet.properties.SetMatrix(ShaderIDs.ViewProjInv, viewProjMat.inverse);

            sheet.properties.SetFloat(ShaderIDs.VolumeDensity, settings.density.value);
            sheet.properties.SetFloat(ShaderIDs.RaymarchStepSize, settings.stepSize.value);
            sheet.properties.SetFloat(ShaderIDs.RaymarchJitterStrength, settings.jitterStrength.value);

            var jitterTexture = settings.jitter.value == null ? RuntimeUtilities.blackTexture : settings.jitter.value;
            sheet.properties.SetTexture(ShaderIDs.JitterTexture, jitterTexture);

            if (settings.volumes.value == null)
                return;

            RenderTexture previousFogColor = null;
            RenderTexture combinedFogColor = null;

            for (int i = 0; i < settings.volumes.value.Length; i++)
            {
                BakedVolumetricsV3SingleVolume singleVolumeSettings = settings.volumes.value[i];

                //main properties
                sheet.properties.SetVector(ShaderIDs.VolumeResolution, new Vector4(singleVolumeSettings.volumeResolution.x, singleVolumeSettings.volumeResolution.y, singleVolumeSettings.volumeResolution.z, 0));
                sheet.properties.SetVector(ShaderIDs.VolumePos, singleVolumeSettings.volumePosition);
                sheet.properties.SetVector(ShaderIDs.VolumeSize, singleVolumeSettings.volumeSize);

                var volumeTexture = singleVolumeSettings.volume == null ? RuntimeUtilities.whiteTexture3D : singleVolumeSettings.volume;
                sheet.properties.SetTexture(ShaderIDs.VolumeTexture, volumeTexture);

                //fog color
                previousFogColor = RenderTexture.GetTemporary(context.width, context.height, 0, RenderTextureFormat.ARGB32);

                context.command.BlitFullscreenTriangle(context.source, previousFogColor, sheet, 0); //combine

                if (i > 0)
                {
                    combinedFogColor = RenderTexture.GetTemporary(context.width, context.height, 0, RenderTextureFormat.ARGB32);

                    sheet.properties.SetTexture("_PrevFogColor", previousFogColor);

                    //context.command.BlitFullscreenTriangle(previousFogColor, combinedFogColor, sheet, 1); //combine
                    //context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 1); //combine
                    context.command.BlitFullscreenTriangle(previousFogColor, context.destination, sheet, 1); //combine
                }
            }

            //sheet.properties.SetTexture(ShaderIDs.FogColor, nextFogColor);
            //context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 2); //combine

            RenderTexture.ReleaseTemporary(previousFogColor);
            RenderTexture.ReleaseTemporary(combinedFogColor);
        }
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

namespace BakedVolumetrics
{
	public sealed class BakedVolumetricsV2Renderer : PostProcessEffectRenderer<BakedVolumetricsV2>
	{
		public override void Render(PostProcessRenderContext context)
		{
            //self note: render is called every frame... why the hell are we needing to find this shader every frame?
            var sheet = context.propertySheets.Get(Shader.Find("Hidden/BakedVolumetricsV2"));

			Matrix4x4 clipToView = GL.GetGPUProjectionMatrix(context.camera.projectionMatrix, true).inverse;
			sheet.properties.SetMatrix(ShaderIDs.ClipToView, clipToView);

			Matrix4x4 viewMat = context.camera.worldToCameraMatrix;
			Matrix4x4 projMat = GL.GetGPUProjectionMatrix(context.camera.projectionMatrix, false);
			Matrix4x4 viewProjMat = (projMat * viewMat);
			sheet.properties.SetMatrix(ShaderIDs.ViewProjInv, viewProjMat.inverse);

			//main properties
			sheet.properties.SetFloat(ShaderIDs.VolumeDensity, settings.density.value);
			sheet.properties.SetFloat(ShaderIDs.RaymarchStepSize, settings.stepSize.value);
			sheet.properties.SetFloat(ShaderIDs.RaymarchJitterStrength, settings.jitterStrength.value);
			sheet.properties.SetVector(ShaderIDs.VolumeResolution, settings.volumeResolution.value);
			sheet.properties.SetVector(ShaderIDs.VolumePos, settings.volumePosition.value);
			sheet.properties.SetVector(ShaderIDs.VolumeSize, settings.volumeSize.value);

			var volumeTexture = settings.volume.value == null ? RuntimeUtilities.whiteTexture3D : settings.volume.value;
			sheet.properties.SetTexture(ShaderIDs.VolumeTexture, volumeTexture);
			var jitterTexture = settings.jitter.value == null ? RuntimeUtilities.blackTexture : settings.jitter.value;
			sheet.properties.SetTexture(ShaderIDs.JitterTexture, jitterTexture);

			//fog color
			var fogColor = RenderTexture.GetTemporary(context.width / 2, context.height / 2, 0, RenderTextureFormat.ARGB32);
			fogColor.filterMode = FilterMode.Bilinear;
			context.command.BlitFullscreenTriangle(context.source, fogColor, sheet, 0);

			//low res depth
			var lowResDepth = RenderTexture.GetTemporary(context.width / 2, context.height / 2, 0, RenderTextureFormat.RHalf);
			lowResDepth.filterMode = FilterMode.Bilinear;
			context.command.BlitFullscreenTriangle(context.source, lowResDepth, sheet, 1);

			sheet.properties.SetTexture(ShaderIDs.FogColor, fogColor);
			sheet.properties.SetTexture(ShaderIDs.LowResDepth, lowResDepth);
			sheet.properties.SetFloat(ShaderIDs.DownsampleFactor, 2);
			context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 2); //main

			RenderTexture.ReleaseTemporary(lowResDepth);
			RenderTexture.ReleaseTemporary(fogColor);
		}
	}
}
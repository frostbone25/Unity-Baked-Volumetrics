using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

namespace VRProject1_PostProcessing
{
	[Serializable]
	[PostProcess(typeof(BakedVolumetricsV1Renderer), PostProcessEvent.BeforeStack, "Custom/BakedVolumetricsV1")]
	public sealed class BakedVolumetricsV1 : PostProcessEffectSettings
	{
		public TextureParameter volume = new TextureParameter();
		public TextureParameter jitter = new TextureParameter();
		public FloatParameter density = new FloatParameter() { value = 0.0f };
		public FloatParameter stepSize = new FloatParameter() { value = 25.0f };
		public FloatParameter jitterStrength = new FloatParameter() { value = 2.0f };

		public Vector3Parameter volumeResolution = new Vector3Parameter();
		public Vector3Parameter volumePosition = new Vector3Parameter();
		public Vector3Parameter volumeSize = new Vector3Parameter();
	}

	public sealed class BakedVolumetricsV1Renderer : PostProcessEffectRenderer<BakedVolumetricsV1>
	{
		public override void Render(PostProcessRenderContext context)
		{
			var sheet = context.propertySheets.Get(Shader.Find("Hidden/BakedVolumetricsV1"));

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

			int downsample = 2;

			//fog color
			var fogColor = RenderTexture.GetTemporary(context.width / downsample, context.height / downsample, 0, context.sourceFormat);
			fogColor.filterMode = FilterMode.Bilinear;
			context.command.BlitFullscreenTriangle(context.source, fogColor, sheet, 0);

			//low res depth
			var lowResDepth = RenderTexture.GetTemporary(context.width / downsample, context.height / downsample, 0, context.sourceFormat);
			lowResDepth.filterMode = FilterMode.Bilinear;
			context.command.BlitFullscreenTriangle(context.source, lowResDepth, sheet, 1);

			sheet.properties.SetTexture("_FogColor", fogColor);
			sheet.properties.SetTexture("_LowResDepth", lowResDepth);
			sheet.properties.SetFloat("_DownsampleFactor", downsample);
			context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 2); //main

			RenderTexture.ReleaseTemporary(lowResDepth);
			RenderTexture.ReleaseTemporary(fogColor);
		}
	}
}